using System.Collections.Generic;
using HybridShooter.Weapon;
using RootMotion.FinalIK;
using UnityEngine;

namespace HybridShooter.Player
{
    /// <summary>
    /// 애니메이션 떨림 측정 (디버그 전용, AnimJitterScenarioRunner 가 붙인다).
    /// 한 프레임에 같은 뼈(골반·가슴·머리·양손·총구)의 위치를 여러 단계에서 기록해 어느 단계에서 튀었는지 가른다
    ///  - Anim     : 애니메이터 직후, IK 전 (AnimJitterProbeHook, 실행 순서 9990)
    ///  - PreFBBIK : AimIK·왼손 LimbIK 직후, FBBIK·Grounder 전 (FBBIK OnPreUpdate)
    ///  - Final    : 모든 IK 뒤 화면에 그려지는 자세 (이 스크립트, 10001)
    ///  - Camera   : Final 을 지금 카메라 기준으로 (카메라·루트 흔들림까지 포함)
    /// Anim·PreFBBIK·Final 은 몸(Body) 기준 좌표라 이동·회전은 빠지고 자세 변화만 남는다.
    /// 가속도 = 위치를 두 번 미분한 크기(m/s²). 튐 판정·요약은 AnimJitterReport
    /// 같은 프레임의 이벤트(물리 박자, 클립 루프, 상태 전환, Sprint 전환, 조준점 점프, 왼팔 다 펴짐, 골반 높이 점프)도 같이 남긴다
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10001)]
    public class AnimJitterProbe : MonoBehaviour
    {
        public const int StageAnim = 0, StagePreFullBody = 1, StageFinal = 2, StageCamera = 3;
        public const int StageCount = 4;
        public static readonly string[] StageNames = { "Anim", "PreFBBIK", "Final", "Camera" };

        public const int BoneHips = 0, BoneChest = 1, BoneHead = 2, BoneLeftHand = 3, BoneRightHand = 4, BoneMuzzle = 5;
        public const int BoneCount = 6;
        public static readonly string[] BoneNames = { "Hips", "Chest", "Head", "LeftHand", "RightHand", "Muzzle" };
        /// <summary>회전 가속도를 재는 뼈 (가슴·머리)</summary>
        public const int AngBoneCount = 2;
        public static readonly string[] AngBoneNames = { "Chest", "Head" };

        public const int EventPhysicsUneven = 0, EventBaseLoop = 1, EventUpperLoop = 2, EventBaseTransition = 3, EventSprintToggle = 4,
            EventGroundedToggle = 5, EventAimPointJump = 6, EventLeftArmStretch = 7, EventPelvisJump = 8;
        public const int EventCount = 9;
        public static readonly string[] EventNames =
        {
            "PhysicsUneven", "BaseLoop", "UpperLoop", "BaseTransition", "SprintToggle",
            "GroundedToggle", "AimPointJump", "LeftArmStretch", "PelvisJump",
        };

        [Tooltip("조준점 거리가 한 프레임에 이만큼(m) 넘게 바뀌면 AimPointJump")]
        [SerializeField] private float aimJumpDistance = 0.5f;
        [Tooltip("왼손 목표까지 거리가 팔 길이의 이 비율을 넘으면 LeftArmStretch")]
        [SerializeField] private float leftArmStretchRatio = 0.98f;
        [Tooltip("Grounder 골반 높이가 한 프레임에 이만큼(m) 넘게 바뀌면 PelvisJump")]
        [SerializeField] private float pelvisJumpDistance = 0.01f;

        /// <summary>한 프레임 기록</summary>
        public class Sample
        {
            public float time, dt;
            public int fixedSteps;
            /// <summary>[stage * BoneCount + bone] 위치 가속도 크기(m/s²)</summary>
            public readonly float[] acc = new float[StageCount * BoneCount];
            /// <summary>[stage * AngBoneCount + bone] 회전 가속도 크기(도/s²)</summary>
            public readonly float[] ang = new float[StageCount * AngBoneCount];
            public float rootY, rootAcc;
            public float moveX, moveY, speedScale, speed;
            public bool sprint, grounded, inTransition;
            public int baseState;
            public float baseTime, upperTime;
            public float aimWeight, aimDistance, leftWeight, leftReach, pelvisOffset;
            public int events;

            public float Acc(int stage, int bone) => acc[stage * BoneCount + bone];
            public float Ang(int stage, int bone) => ang[stage * AngBoneCount + bone];
            public bool Has(int evt) => (events & (1 << evt)) != 0;
        }

        private PlayerController player;
        private PlayerAnimationDriver driver;
        private PlayerBodyIK bodyIK;
        private PlayerAimIK playerAimIK;
        private GunSlot gunSlot;
        private AimIK aimIK;
        private FullBodyBipedIK fbbik;
        private GrounderFBBIK grounder;
        private Animator animator;
        private Transform body;
        private Transform leftUpperArm;
        private float leftArmLength;
        private int upperLayer = -1;
        private AnimJitterProbeHook hook;
        private bool ready;

        private readonly Transform[] bones = new Transform[BoneCount];
        private readonly Vector3[,] pos = new Vector3[StageCount, BoneCount];
        private readonly Quaternion[,] rot = new Quaternion[StageCount, AngBoneCount];
        private readonly int[] capturedFrame = new int[StageCount];
        private readonly Vector3[,] prevPos = new Vector3[StageCount, BoneCount];
        private readonly Vector3[,] prevVel = new Vector3[StageCount, BoneCount];
        private readonly Quaternion[,] prevRot = new Quaternion[StageCount, AngBoneCount];
        private readonly Vector3[,] prevAngVel = new Vector3[StageCount, AngBoneCount];
        private int history;          // 이어서 쌓인 프레임 수 (2 이상이어야 가속도가 의미 있다)
        private int fixedSteps;
        private float prevRootY, prevRootVel;

        // 이벤트 판정용 지난 프레임 값
        private int prevFixedSteps = -1;
        private int prevBaseHash, prevUpperHash;
        private float prevBaseTime, prevUpperTime;
        private bool prevInTransition, prevSprint, prevGrounded;
        private float prevAimDistance, prevPelvis;

        public bool Recording { get; private set; }
        public List<Sample> Samples { get; } = new List<Sample>(512);
        public bool Ready => ready;

        private void Start()
        {
            player = GetComponent<PlayerController>();
            driver = GetComponent<PlayerAnimationDriver>();
            bodyIK = GetComponent<PlayerBodyIK>();
            playerAimIK = GetComponent<PlayerAimIK>();
            gunSlot = GetComponent<GunSlot>();
            aimIK = GetComponentInChildren<AimIK>(true);
            fbbik = GetComponentInChildren<FullBodyBipedIK>(true);
            grounder = GetComponentInChildren<GrounderFBBIK>(true);
            animator = driver != null ? driver.Animator : GetComponentInChildren<Animator>();
            if (player == null || animator == null || animator.isHuman == false)
            {
                Debug.LogWarning($"{name}: AnimJitterProbe 준비 안 됨 (PlayerController·휴머노이드 Animator 필요)", this);
                enabled = false;
                return;
            }

            body = animator.transform;
            bones[BoneHips] = animator.GetBoneTransform(HumanBodyBones.Hips);
            bones[BoneChest] = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (bones[BoneChest] == null) bones[BoneChest] = animator.GetBoneTransform(HumanBodyBones.Chest);
            bones[BoneHead] = animator.GetBoneTransform(HumanBodyBones.Head);
            bones[BoneLeftHand] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            bones[BoneRightHand] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (leftUpperArm != null && leftLowerArm != null && bones[BoneLeftHand] != null)
                leftArmLength = Vector3.Distance(leftUpperArm.position, leftLowerArm.position) + Vector3.Distance(leftLowerArm.position, bones[BoneLeftHand].position);
            upperLayer = animator.GetLayerIndex(PlayerAnimNames.Layers.UpperPose);

            hook = gameObject.AddComponent<AnimJitterProbeHook>();
            hook.Probe = this;
            if (fbbik != null) fbbik.solver.OnPreUpdate += OnBeforeFullBody;
            ready = true;
        }

        private void OnDestroy()
        {
            if (fbbik != null) fbbik.solver.OnPreUpdate -= OnBeforeFullBody;
            if (hook != null) Destroy(hook);
        }

        public void BeginRecording()
        {
            Samples.Clear();
            history = 0; // 기록 시작 직전 순간이동·설정 변경이 가속도에 섞이지 않게 처음부터 다시 쌓는다
            Recording = true;
        }

        public void EndRecording() => Recording = false;

        private void FixedUpdate() => fixedSteps++;

        private void OnBeforeFullBody() => CaptureStage(StagePreFullBody);

        /// <summary>그 단계의 뼈 위치·회전을 기록 (한 프레임에 한 번)</summary>
        public void CaptureStage(int stage)
        {
            if (ready == false || capturedFrame[stage] == Time.frameCount) return;
            capturedFrame[stage] = Time.frameCount;

            Transform frame = body;
            if (stage == StageCamera && player.CurrentCameraTransform != null) frame = player.CurrentCameraTransform;
            Quaternion inverse = Quaternion.Inverse(frame.rotation);
            bones[BoneMuzzle] = CurrentMuzzle();
            for (int b = 0; b < BoneCount; b++)
                pos[stage, b] = bones[b] != null ? frame.InverseTransformPoint(bones[b].position) : Vector3.zero;
            rot[stage, 0] = bones[BoneChest] != null ? inverse * bones[BoneChest].rotation : Quaternion.identity;
            rot[stage, 1] = bones[BoneHead] != null ? inverse * bones[BoneHead].rotation : Quaternion.identity;
        }

        private Transform CurrentMuzzle()
        {
            Gun gun = bodyIK != null && bodyIK.GripGun != null ? bodyIK.GripGun : gunSlot != null ? gunSlot.CurrentGun : null;
            return gun != null ? gun.Muzzle : null;
        }

        private void LateUpdate()
        {
            if (ready == false) return;
            int frame = Time.frameCount;
            // 훅이 못 돌았으면(꺼짐) 지금 자세로, FBBIK 가 안 돌았으면 IK 전 자세를 그대로 쓴다
            if (capturedFrame[StageAnim] != frame) CaptureStage(StageAnim);
            if (capturedFrame[StagePreFullBody] != frame) CopyStage(StageAnim, StagePreFullBody);
            CaptureStage(StageFinal);
            CaptureStage(StageCamera);

            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            Sample s = Recording && history >= 2 ? new Sample() : null;

            for (int st = 0; st < StageCount; st++)
            {
                for (int b = 0; b < BoneCount; b++)
                {
                    Vector3 vel = (pos[st, b] - prevPos[st, b]) / dt;
                    if (s != null) s.acc[st * BoneCount + b] = (vel - prevVel[st, b]).magnitude / dt;
                    if (history >= 1) prevVel[st, b] = vel;
                    prevPos[st, b] = pos[st, b];
                }
                for (int b = 0; b < AngBoneCount; b++)
                {
                    (rot[st, b] * Quaternion.Inverse(prevRot[st, b])).ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f) angle -= 360f;
                    Vector3 angVel = float.IsInfinity(axis.x) || float.IsNaN(axis.x) ? Vector3.zero : axis * (angle / dt);
                    if (s != null) s.ang[st * AngBoneCount + b] = (angVel - prevAngVel[st, b]).magnitude / dt;
                    if (history >= 1) prevAngVel[st, b] = angVel;
                    prevRot[st, b] = rot[st, b];
                }
            }

            float rootY = body.position.y;
            float rootVel = (rootY - prevRootY) / dt;
            float rootAcc = Mathf.Abs(rootVel - prevRootVel) / dt;
            if (history >= 1) prevRootVel = rootVel;
            prevRootY = rootY;

            int events = ReadEvents(out AnimatorStateInfo baseInfo, out float upperTime, out float aimWeight, out float aimDistance,
                out float leftWeight, out float leftReach, out float pelvis);

            if (s != null)
            {
                s.time = Time.time;
                s.dt = dt;
                s.fixedSteps = fixedSteps;
                s.rootY = rootY;
                s.rootAcc = rootAcc;
                s.moveX = animator.GetFloat(PlayerAnimNames.Hash.MoveX);
                s.moveY = animator.GetFloat(PlayerAnimNames.Hash.MoveY);
                s.speedScale = animator.GetFloat(PlayerAnimNames.Hash.MoveSpeedScale);
                s.speed = player.HorizontalSpeed;
                s.sprint = animator.GetBool(PlayerAnimNames.Hash.Sprint);
                s.grounded = player.IsGrounded;
                s.inTransition = animator.IsInTransition(0);
                s.baseState = baseInfo.shortNameHash;
                s.baseTime = baseInfo.normalizedTime;
                s.upperTime = upperTime;
                s.aimWeight = aimWeight;
                s.aimDistance = aimDistance;
                s.leftWeight = leftWeight;
                s.leftReach = leftReach;
                s.pelvisOffset = pelvis;
                s.events = events;
                Samples.Add(s);
            }

            history++;
            fixedSteps = 0;
        }

        private void CopyStage(int from, int to)
        {
            for (int b = 0; b < BoneCount; b++) pos[to, b] = pos[from, b];
            for (int b = 0; b < AngBoneCount; b++) rot[to, b] = rot[from, b];
            capturedFrame[to] = Time.frameCount;
        }

        /// <summary>이번 프레임 이벤트 비트 (EventNames 순서). 지난 프레임 값과 비교한다</summary>
        private int ReadEvents(out AnimatorStateInfo baseInfo, out float upperTime, out float aimWeight, out float aimDistance,
            out float leftWeight, out float leftReach, out float pelvis)
        {
            int events = 0;
            bool hasPrev = history >= 1;

            if (hasPrev && prevFixedSteps >= 0 && fixedSteps != prevFixedSteps) events |= 1 << EventPhysicsUneven;
            prevFixedSteps = fixedSteps;

            baseInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool inTransition = animator.IsInTransition(0);
            if (hasPrev && baseInfo.shortNameHash == prevBaseHash && Mathf.Floor(baseInfo.normalizedTime) > Mathf.Floor(prevBaseTime)) events |= 1 << EventBaseLoop;
            if (hasPrev && (baseInfo.shortNameHash != prevBaseHash || (inTransition && prevInTransition == false))) events |= 1 << EventBaseTransition;
            prevBaseHash = baseInfo.shortNameHash;
            prevBaseTime = baseInfo.normalizedTime;
            prevInTransition = inTransition;

            upperTime = 0f;
            if (upperLayer >= 0)
            {
                AnimatorStateInfo upper = animator.GetCurrentAnimatorStateInfo(upperLayer);
                upperTime = upper.normalizedTime;
                if (hasPrev && upper.shortNameHash == prevUpperHash && Mathf.Floor(upperTime) > Mathf.Floor(prevUpperTime)) events |= 1 << EventUpperLoop;
                prevUpperHash = upper.shortNameHash;
                prevUpperTime = upperTime;
            }

            bool sprint = animator.GetBool(PlayerAnimNames.Hash.Sprint);
            if (hasPrev && sprint != prevSprint) events |= 1 << EventSprintToggle;
            prevSprint = sprint;

            bool grounded = player.IsGrounded;
            if (hasPrev && grounded != prevGrounded) events |= 1 << EventGroundedToggle;
            prevGrounded = grounded;

            aimWeight = aimIK != null && aimIK.enabled ? aimIK.solver.IKPositionWeight : 0f;
            aimDistance = playerAimIK != null && player.CurrentCameraTransform != null
                ? Vector3.Distance(player.CurrentCameraTransform.position, playerAimIK.AimPoint) : 0f;
            if (hasPrev && aimWeight > 0.01f && Mathf.Abs(aimDistance - prevAimDistance) > aimJumpDistance) events |= 1 << EventAimPointJump;
            prevAimDistance = aimDistance;

            leftWeight = bodyIK != null && bodyIK.DebugDisableLeftHand == false ? bodyIK.LeftHandWeight * (1f - player.FirstPersonBlend) : 0f;
            leftReach = bodyIK != null && leftUpperArm != null && leftArmLength > 0f
                ? Vector3.Distance(leftUpperArm.position, bodyIK.LeftHandTarget) / leftArmLength : 0f;
            if (leftWeight > 0.01f && leftReach > leftArmStretchRatio) events |= 1 << EventLeftArmStretch;

            pelvis = grounder != null && grounder.enabled && grounder.solver.pelvis != null ? grounder.solver.pelvis.heightOffset : 0f;
            if (hasPrev && Mathf.Abs(pelvis - prevPelvis) > pelvisJumpDistance) events |= 1 << EventPelvisJump;
            prevPelvis = pelvis;

            return events;
        }
    }
}
