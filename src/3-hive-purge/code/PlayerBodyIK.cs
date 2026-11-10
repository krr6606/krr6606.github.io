using HybridShooter.Weapon;
using RootMotion.FinalIK;
using UnityEngine;

namespace HybridShooter.Player
{
    /// <summary>
    /// 왼손 IK · 발 IK (Final IK). Player 루트에 붙인다.
    ///
    /// 왼손 (LimbIK, Final IK 데모 SecondHandOnGun 방식)
    ///  - 애니메이션에서는 두 손이 총을 함께 잡고 있다. AimIK 가 척추를 돌려 오른손(총)이 움직이면 왼손이 총에서 떨어진다
    ///  - 그래서 AimIK 전에 "오른손 기준 왼손 위치"를 기록해 두고, AimIK 가 끝난 직후(OnPostUpdate) 왼팔 LimbIK 로 그 자리에 되돌린다
    ///  - 총별 손 위치(WeaponGripTable, 프로필에 연결)가 있으면 총을 꺼낼 때 총 위치를 맞추고 "LeftHandGrip"·"Sight"·"RightHandGrip" 자식을 만든다.
    ///    (총은 오른손 뼈 아래 WeaponSocket 에 붙어 있어 어느 자세에서든 손을 따라간다)
    ///    왼손은 LeftHandGrip 위치·회전을 우선 쓴다 (데이터가 없으면 기존 LeftHandGrip 자식, 그것도 없으면 애니메이션 자리)
    ///  - 가중치 = 상체 자세 레이어 × 팔 동작 없음 (재장전·꺼내기 중에는 애니메이션 그대로)
    ///  - 아이템 사용(PlayerItemIK): 왼손 목표를 아이템 자리로 잠깐 옮긴다 (SetLeftHandOverride, 1인칭도 같은 값을 쓴다)
    /// 발 (GrounderFBBIK): 경사·계단에서 발을 땅에 맞춘다. 공중·구르기·사망 중에는 끈다. 설정은 프로필 footIK (시작할 때 입힘)
    ///
    /// 1인칭에서는 LimbIK 대신 PlayerFirstPersonIK 가 FBBIK 왼손 효과기로 LeftHandGrip 에 붙인다 (LimbIK 가중치 × (1 - 1인칭 정도))
    ///
    /// 실행 순서: 이 스크립트(9996, 기록) → AimIK(9997) → [왼손 LimbIK] → PlayerFirstPersonIK(9998) → FullBodyBipedIK(9999, 반동·발)
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(9996)]
    public class PlayerBodyIK : MonoBehaviour
    {
        public const string LeftHandGripName = "LeftHandGrip";
        public const string SightName = "Sight";
        public const string RightHandGripName = "RightHandGrip";

        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerAnimationDriver driver;
        [SerializeField] private GunSlot gunSlot;
        [Tooltip("몸(Body)의 AimIK. 이 IK 가 풀린 직후 왼손을 맞춘다")]
        [SerializeField] private AimIK aimIK;
        [Tooltip("왼팔 LimbIK (컴포넌트는 꺼 두고 이 스크립트가 직접 푼다)")]
        [SerializeField] private LimbIK leftArmIK;
        [Tooltip("발 IK")]
        [SerializeField] private GrounderFBBIK grounder;
        [Tooltip("왼손 IK 를 켜고 끄는 시간(초)")]
        [SerializeField] private float leftHandBlendTime = 0.15f;
        [Tooltip("발 IK 를 켜고 끄는 시간(초)")]
        [SerializeField] private float footBlendTime = 0.2f;

        private Transform leftHand;
        private Transform rightHand;
        private Vector3 leftPosInRight;
        private Quaternion leftRotInRight = Quaternion.identity;
        private float leftHandWeight;
        private Gun gripGun;
        private Transform grip;
        private Transform sight;
        private Transform rightGrip;
        private Vector3 overridePosition;
        private Quaternion overrideRotation = Quaternion.identity;
        private bool overrideUsesRotation;
        private float overrideWeight;

        public float LeftHandWeight => leftHandWeight;
        public float FootWeight => grounder != null ? grounder.weight : 0f;
        public GrounderFBBIK Grounder => grounder;
        /// <summary>지금 손 위치를 맞춘 총</summary>
        public Gun GripGun => gripGun;
        public Transform LeftHandGrip => grip;
        public Transform Sight => sight;
        /// <summary>손잡이 지점 (오른손 바닥이 올 자리, 데이터에 있을 때만)</summary>
        public Transform RightHandGrip => rightGrip;
        public Transform LeftHandBone => leftHand;
        public Transform RightHandBone => rightHand;
        public WeaponGripTable GripTable => driver != null && driver.Profile != null ? driver.Profile.gripTable : null;

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (driver == null) driver = GetComponent<PlayerAnimationDriver>();
            if (gunSlot == null) gunSlot = GetComponent<GunSlot>();
            if (aimIK == null) aimIK = GetComponentInChildren<AimIK>();
            if (leftArmIK == null) leftArmIK = GetComponentInChildren<LimbIK>();
            if (grounder == null) grounder = GetComponentInChildren<GrounderFBBIK>();

            Animator animator = driver != null ? driver.Animator : GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            // 왼팔 IK 는 AimIK 가 끝난 직후에 직접 푼다 (컴포넌트가 스스로 LateUpdate 에서 풀지 않게)
            if (leftArmIK != null) leftArmIK.enabled = false;
            ClearEditorExecutionFlags();
            ApplyFootSettings();
        }

        /// <summary>프로필의 발 IK 설정을 Grounder 에 입힌다</summary>
        private void ApplyFootSettings()
        {
            if (grounder == null) return;
            // 발 IK 레이캐스트도 이동 판정과 같은 땅을 보게 한다 (씬마다 터레인 레이어가 달라도 어긋나지 않게)
            if (player != null) grounder.solver.layers = player.GroundMask;

            PlayerAnimationProfile profile = driver != null ? driver.Profile : null;
            if (profile == null || profile.footIK == null) return;
            PlayerAnimationProfile.FootIKSettings s = profile.footIK;
            grounder.solver.maxStep = s.maxStep;
            grounder.solver.footRadius = s.footRadius;
            grounder.solver.footSpeed = s.footSpeed;
            grounder.solver.pelvisSpeed = s.pelvisSpeed;
            grounder.solver.lowerPelvisWeight = s.lowerPelvisWeight;
            grounder.solver.liftPelvisWeight = s.liftPelvisWeight;
            grounder.solver.footRotationWeight = s.footRotationWeight;
            grounder.solver.maxFootRotationAngle = s.maxFootRotationAngle;
        }

        /// <summary>
        /// Final IK 의 EditorIK(편집 모드 미리보기)를 썼던 솔버는 executedInEditor 가 켜진 채 저장되고,
        /// 그 상태로는 Play 에서 초기화를 조용히 건너뛰어 IK 가 전혀 풀리지 않는다 (경고도 없음). 시작할 때 꺼 준다
        /// </summary>
        private void ClearEditorExecutionFlags()
        {
            foreach (IK ik in GetComponentsInChildren<IK>(true))
            {
                IKSolver solver = ik.GetIKSolver();
                if (solver == null || solver.executedInEditor == false) continue;
                solver.executedInEditor = false;
                Debug.LogWarning($"{ik.GetType().Name}({ik.name}): 편집 모드 실행 표시(executedInEditor)가 남아 있어 껐습니다. 씬의 값도 꺼 두세요", ik);
            }
        }

        private void OnEnable()
        {
            if (aimIK != null) aimIK.solver.OnPostUpdate += AfterAimIK;
        }

        private void OnDisable()
        {
            if (aimIK != null) aimIK.solver.OnPostUpdate -= AfterAimIK;
        }

        private void LateUpdate()
        {
            if (driver == null || player == null) return;
            float dt = Time.deltaTime;

            // 애니메이션이 정한 "오른손 기준 왼손" 을 AimIK 전에 기록
            if (leftHand != null && rightHand != null)
            {
                leftPosInRight = rightHand.InverseTransformPoint(leftHand.position);
                leftRotInRight = Quaternion.Inverse(rightHand.rotation) * leftHand.rotation;
            }

            float target = driver.UpperPoseWeight * driver.ActionBlend;
            leftHandWeight = Mathf.MoveTowards(leftHandWeight, target, leftHandBlendTime > 0f ? dt / leftHandBlendTime : 1f);
            UpdateGrip();
            // AimIK 가 없으면 여기서 바로 맞춘다
            if (aimIK == null || aimIK.enabled == false) SolveLeftHand();

            if (grounder != null)
            {
                bool feet = player.IsOnGround && player.IsRolling == false && player.IsAlive && player.IsExternallyControlled == false;
                grounder.weight = Mathf.MoveTowards(grounder.weight, feet ? 1f : 0f, footBlendTime > 0f ? dt / footBlendTime : 1f);
            }
        }

        private void AfterAimIK() => SolveLeftHand();

        /// <summary>왼손 목표를 아이템 자리로 옮긴다 (매 프레임 PlayerBodyIK 보다 먼저 부른다. weight 0 = 총 손잡이 그대로)</summary>
        public void SetLeftHandOverride(Vector3 position, Quaternion rotation, bool useRotation, float weight)
        {
            overridePosition = position;
            overrideRotation = rotation;
            overrideUsesRotation = useRotation;
            overrideWeight = Mathf.Clamp01(weight);
        }

        /// <summary>왼손 목표(손잡이 등)에 아이템 자리를 섞는다. 회전을 쓰지 않는 자리면 손 회전은 그대로</summary>
        public void ApplyLeftHandOverride(ref Vector3 position, ref Quaternion rotation)
        {
            if (overrideWeight <= 0f) return;
            position = Vector3.Lerp(position, overridePosition, overrideWeight);
            if (overrideUsesRotation) rotation = Quaternion.Slerp(rotation, overrideRotation, overrideWeight);
        }

        /// <summary>왼손을 총 손잡이(있으면) 또는 기록해 둔 오른손 기준 자리로</summary>
        private void SolveLeftHand()
        {
            if (leftArmIK == null || leftHand == null || rightHand == null || leftHandWeight <= 0f) return;

            Vector3 position = grip != null ? grip.position : rightHand.TransformPoint(leftPosInRight);
            Quaternion rotation = grip != null ? grip.rotation : rightHand.rotation * leftRotInRight;
            ApplyLeftHandOverride(ref position, ref rotation);
            leftArmIK.solver.IKPosition = position;
            leftArmIK.solver.IKRotation = rotation;
            float weight = leftHandWeight * (1f - player.FirstPersonBlend);
            leftArmIK.solver.IKPositionWeight = weight;
            leftArmIK.solver.IKRotationWeight = weight;
            leftArmIK.solver.Update();
        }

        /// <summary>총이 바뀌면 손 위치 데이터를 입힌다</summary>
        private void UpdateGrip()
        {
            Gun gun = gunSlot != null ? gunSlot.CurrentGun : null;
            if (gun == gripGun) return;
            gripGun = gun;
            ApplyGrip();
        }

        /// <summary>
        /// 지금 든 총에 손 위치 데이터를 입힌다: 총 위치(소켓 기준), LeftHandGrip·Sight·RightHandGrip 자식 (없으면 만든다).
        /// 데이터가 없는 총은 이미 있는 자식을 그대로 쓴다
        /// </summary>
        private void ApplyGrip()
        {
            grip = null;
            sight = null;
            rightGrip = null;
            if (gripGun == null) return;
            Transform gun = gripGun.transform;
            WeaponGripTable table = GripTable;
            WeaponGripTable.Entry entry = table != null ? table.Find(gripGun) : null;
            if (entry == null)
            {
                grip = gun.Find(LeftHandGripName);
                sight = gun.Find(SightName);
                return;
            }
            gun.SetLocalPositionAndRotation(entry.gunPosition, entry.gunRotation);
            grip = EnsureChild(gun, LeftHandGripName);
            grip.SetLocalPositionAndRotation(entry.leftHandPosition, entry.leftHandRotation);
            sight = EnsureChild(gun, SightName);
            sight.SetLocalPositionAndRotation(entry.sightPosition, Quaternion.identity);
            if (entry.gripPoint != Vector3.zero)
            {
                rightGrip = EnsureChild(gun, RightHandGripName);
                rightGrip.SetLocalPositionAndRotation(entry.gripPoint, Quaternion.identity);
            }
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null) return child;
            child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
