using HybridShooter.Weapon;
using RootMotion.FinalIK;
using UnityEngine;

namespace HybridShooter.Player
{
    /// <summary>
    /// 1인칭 무기 자리 (Final IK FullBodyBipedIK 손 효과기). Player 루트에 붙인다.
    /// 1인칭 카메라는 눈 높이에 고정이라, 총이 애니메이션(걷기·숨쉬기)대로 흔들리면 화면 속 총이 출렁이고 조준기가 중앙에 오지 않는다.
    /// 그래서 1인칭에서는 총이 있어야 할 자리를 눈 기준으로 정하고, 두 손을 FBBIK 로 그 자리에 끌어온다 (손이 안 닿으면 상체가 따라온다)
    ///  - 평소: 조준기가 눈 기준 hipSight 자리 (화면 오른쪽 아래)
    ///  - 조준: 조준기가 눈 바로 앞 화면 정중앙 (adsDistance 앞). 그 사이는 드라이버의 실제 조준 정도(AdsWeight)로 보간 (조준 없이 쏠 때는 평소 자리 그대로)
    ///    어깨 견착 총(로켓·그레네이드 런처)은 조준기가 어깨 위 옆자리(shoulderSight), 저격총은 조준이 끝나면 스코프 화면(PlayerAimView)이 덮는다
    ///  - 총 방향 = 눈 정면. 벽이 총 길이보다 가까우면 총을 몸 쪽으로 당기고 총구를 내린다
    ///  - 도달 당김: 오른손 목표가 지금(FBBIK 전) 어깨에서 팔 길이 × maxReachRatio 보다 멀면 총을 시선 축을 따라 그만큼 몸 쪽으로 당긴다.
    ///    눈은 몸 뼈와 따로 고정이라 위를 볼수록 총 자리가 팔 길이 밖으로 나가고, 그러면 FBBIK 가 어깨·목까지 앞으로 끌어낸다.
    ///    시선 축을 따라 당기므로 조준 중 조준기는 화면 중앙에 남는다
    ///  - 사격 반동(PlayerGunRecoil)은 이 총 자리에 더한다 (조준 중에는 줄여서)
    ///  - 오른손 = 총과 손의 지금 관계 그대로 (총은 오른손 아래 WeaponSocket 에 붙어 있다), 왼손 = 총의 LeftHandGrip
    ///  - 팔꿈치: 손 효과기 회전을 쓰면 FBBIK 는 팔꿈치 방향을 손 회전에서 정한다 → 아래를 보면 손과 함께 돌아 팔이 안쪽으로 꺾인다.
    ///    그래서 어깨에서 몸 기준(좌우 yaw 만, 위아래 시선은 따르지 않음) elbowDirection 쪽에 bend goal 을 두고 그 방향으로 접는다
    ///    (가중치 = elbowWeight × 손 IK 가중치 → 3인칭·반동·재장전·던지기에는 영향 없음)
    ///  - 총별 값은 WeaponGripTable(fpHipSight, adsDistance), 없으면 여기 기본값
    ///  - 가중치 = 1인칭 정도(시점 전환 중 부드럽게) × 상체 자세 레이어 × 팔 동작 없음 (재장전·꺼내기는 애니메이션 그대로)
    ///  - 몸 내리기: 점프 동작은 골반을 위로 올리는데(클립 Root Y 가 포즈에 들어감) 눈은 루트 기준 고정이라 목·어깨가 카메라를 가린다.
    ///    그래서 손 목표를 정하기 전에(FBBIK 전) 목이 눈보다 minNeckBelowEye 만큼 아래 오도록 골반째 몸 전체를 내린다 (× 1인칭 정도, 3인칭은 그대로).
    ///    총이 없어도 한다
    /// 실행 순서: AimIK(9997, 척추가 시선을 따라감) → 이 스크립트(9998, 손 목표) → FullBodyBipedIK(9999)
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(9998)]
    public class PlayerFirstPersonIK : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerAnimationDriver driver;
        [SerializeField] private PlayerBodyIK bodyIK;
        [Tooltip("몸(Body)의 FullBodyBipedIK. 비워 두면 자식에서 찾는다")]
        [SerializeField] private FullBodyBipedIK fbbik;
        [Tooltip("사격 반동 (총 기준). 1인칭에서는 눈 기준 총 자리에 반동을 더한다")]
        [SerializeField] private PlayerGunRecoil recoil;

        [Header("기본값 (총별 값이 없을 때)")]
        [Tooltip("평소: 조준기가 올 자리 (1인칭 눈 기준 m, x 오른쪽 y 위 z 앞)")]
        [SerializeField] private Vector3 hipSight = new Vector3(0.13f, -0.14f, 0.32f);
        [Tooltip("조준: 눈과 조준기 사이 거리(m)")]
        [SerializeField] private float adsDistance = 0.15f;
        [Tooltip("어깨 견착 조준: 조준기가 올 자리 (1인칭 눈 기준 m). 총이 화면 오른쪽 아래로 비켜 있고 중앙은 조준선 UI")]
        [SerializeField] private Vector3 shoulderSight = new Vector3(0.17f, -0.12f, 0.3f);

        [Header("팔꿈치 방향")]
        [Tooltip("오른 팔꿈치가 향할 방향 (몸 기준, x 바깥 y 위 z 앞. 왼팔은 x 를 뒤집는다). 위아래 시선은 따르지 않는다")]
        [SerializeField] private Vector3 elbowDirection = new Vector3(0.5f, -1f, -0.3f);
        [Tooltip("팔꿈치 방향을 덮어쓰는 정도 (× 손 IK 가중치). 1 = 손 회전에서 온 방향을 완전히 무시, 0 = 끔")]
        [SerializeField, Range(0f, 1f)] private float elbowWeight = 1f;

        [Header("벽 가까이")]
        [Tooltip("총이 벽에 닿는지 검사할 레이어")]
        [SerializeField] private LayerMask wallMask = ~0;
        [SerializeField] private float wallProbeRadius = 0.05f;
        [Tooltip("총을 몸 쪽으로 당기는 최대 거리(m)")]
        [SerializeField] private float maxWallPull = 0.35f;
        [Tooltip("최대로 당겼을 때 총구를 내리는 각도(도)")]
        [SerializeField] private float wallPitch = 40f;
        [SerializeField] private float wallSmoothTime = 0.08f;

        [Header("팔 도달")]
        [Tooltip("오른손 목표가 FBBIK 전 어깨에서 팔 길이 × 이 값보다 멀면 총을 시선 축을 따라 몸 쪽으로 당긴다. 0 = 끔")]
        [SerializeField, Range(0f, 1f)] private float maxReachRatio = 0.95f;

        [Header("몸 내리기 (점프 등)")]
        [Tooltip("목 뼈가 1인칭 눈보다 최소 이만큼(m) 아래 있게 한다. 모자라면 FBBIK 전에 몸 전체를 그만큼 내린다 (× 1인칭 정도). 0 = 끔.\n" +
                 "서 있을 때 눈-목 높이 차보다 작게 둔다")]
        [SerializeField] private float minNeckBelowEye = 0.1f;

        private const float MinReachSightDistance = 0.05f; // 도달 당김으로 당겨도 조준기는 눈에서 이만큼 앞에
        private readonly RaycastHit[] hits = new RaycastHit[8];
        private float wallPull;
        private float wallPullVelocity;
        private Transform rightElbowGoal;
        private Transform leftElbowGoal;
        private Transform hips;
        private Transform neck;

        /// <summary>지금 1인칭 손 IK 가중치</summary>
        public float Weight { get; private set; }

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (driver == null) driver = GetComponent<PlayerAnimationDriver>();
            if (bodyIK == null) bodyIK = GetComponent<PlayerBodyIK>();
            if (fbbik == null) fbbik = GetComponentInChildren<FullBodyBipedIK>();
            if (recoil == null) recoil = GetComponent<PlayerGunRecoil>();
            Animator animator = driver != null && driver.Animator != null ? driver.Animator : GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                if (neck == null) neck = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            // bend goal 은 씬에 두지 않고 실행할 때 만든다 (씬·프리팹에 남지 않게)
            rightElbowGoal = new GameObject("FP_RightElbowGoal").transform;
            rightElbowGoal.SetParent(transform, false);
            leftElbowGoal = new GameObject("FP_LeftElbowGoal").transform;
            leftElbowGoal.SetParent(transform, false);
        }

        private void OnEnable()
        {
            if (fbbik == null) return;
            fbbik.solver.rightArmChain.bendConstraint.bendGoal = rightElbowGoal;
            fbbik.solver.leftArmChain.bendConstraint.bendGoal = leftElbowGoal;
        }

        private void OnDisable()
        {
            if (fbbik == null) return;
            SetElbowWeights(0f, 0f);
            if (fbbik.solver.rightArmChain.bendConstraint.bendGoal == rightElbowGoal) fbbik.solver.rightArmChain.bendConstraint.bendGoal = null;
            if (fbbik.solver.leftArmChain.bendConstraint.bendGoal == leftElbowGoal) fbbik.solver.leftArmChain.bendConstraint.bendGoal = null;
        }

        private void LateUpdate()
        {
            if (fbbik == null || player == null || driver == null || bodyIK == null) return;
            LowerBodyBelowEye(); // 도달 당김이 내린 뒤의 어깨를 보도록 먼저
            IKEffector right = fbbik.solver.rightHandEffector;
            IKEffector left = fbbik.solver.leftHandEffector;

            Gun gun = bodyIK.GripGun;
            Transform hand = bodyIK.RightHandBone;
            Weight = player.FirstPersonBlend * driver.UpperPoseWeight * driver.ActionBlend;
            if (Weight <= 0f || gun == null || hand == null || bodyIK.Sight == null)
            {
                SetWeights(right, 0f);
                SetWeights(left, 0f);
                SetElbowWeights(0f, 0f);
                wallPull = 0f;
                return;
            }

            // 총 기준(크기 무시, m) 지점들. 모두 총에 붙어 있어 관계는 매 프레임 같다
            Transform g = gun.transform;
            Quaternion toGun = Quaternion.Inverse(g.rotation);
            Vector3 sightInGun = toGun * (bodyIK.Sight.position - g.position);
            Vector3 muzzleInGun = toGun * (gun.Muzzle.position - g.position);

            // 조준기가 올 자리 (눈 기준): 평소 ↔ 조준
            WeaponGripTable table = bodyIK.GripTable;
            WeaponGripTable.Entry entry = table != null ? table.Find(gun) : null;
            Vector3 hip = entry != null && entry.fpHipSight != Vector3.zero ? entry.fpHipSight : hipSight;
            float ads = entry != null && entry.adsDistance > 0f ? entry.adsDistance : adsDistance;
            Vector3 aimSight = new Vector3(0f, 0f, ads);
            if (table != null && table.AimModeFor(gun) == WeaponGripTable.AimMode.Shoulder)
                aimSight = entry != null && entry.fpShoulderSight != Vector3.zero ? entry.fpShoulderSight : shoulderSight;
            Vector3 sightLocal = Vector3.Lerp(hip, aimSight, driver.AdsWeight);

            Pose eye = player.FirstPersonEyePose;
            float reach = sightLocal.z + (muzzleInGun.z - sightInGun.z) + wallProbeRadius;
            float targetPull = Mathf.Min(maxWallPull, Mathf.Max(0f, reach - WallDistance(eye, reach)));
            wallPull = Mathf.SmoothDamp(wallPull, targetPull, ref wallPullVelocity, wallSmoothTime);
            sightLocal.z -= wallPull;

            Quaternion gunRotation = eye.rotation * Quaternion.Euler(maxWallPull > 0f ? wallPitch * wallPull / maxWallPull : 0f, 0f, 0f);
            sightLocal.z -= ReachPullFor(eye, sightLocal, gunRotation, sightInGun, toGun * (hand.position - g.position)); // 도달 당김
            Vector3 gunPosition = eye.position + eye.rotation * sightLocal - gunRotation * sightInGun;
            if (recoil != null) recoil.ApplyFirstPerson(ref gunPosition, ref gunRotation, gun, driver.AdsWeight);

            // 총이 그 자리에 오려면 손이 어디 있어야 하는지 (총과 손의 관계 그대로)
            right.position = gunPosition + gunRotation * (toGun * (hand.position - g.position));
            right.rotation = gunRotation * (toGun * hand.rotation);
            SetWeights(right, Weight);

            Transform grip = bodyIK.LeftHandGrip;
            float leftWeight = grip != null ? Weight * bodyIK.LeftHandWeight : 0f;
            if (grip != null)
            {
                Vector3 leftPosition = gunPosition + gunRotation * (toGun * (grip.position - g.position));
                Quaternion leftRotation = gunRotation * (toGun * grip.rotation);
                bodyIK.ApplyLeftHandOverride(ref leftPosition, ref leftRotation); // 아이템 사용 중이면 그 자리로
                left.position = leftPosition;
                left.rotation = leftRotation;
            }
            SetWeights(left, leftWeight);

            UpdateElbowGoals();
            SetElbowWeights(elbowWeight * Weight, elbowWeight * leftWeight);
        }

        /// <summary>
        /// 몸 내리기: 목이 눈보다 minNeckBelowEye 만큼 아래 오도록 골반(모든 몸 뼈의 부모)을 내린다.
        /// FBBIK 는 이 자세를 읽어서 풀고, 다음 프레임 애니메이터가 골반을 다시 쓰므로 쌓이지 않는다
        /// </summary>
        private void LowerBodyBelowEye()
        {
            if (hips == null || neck == null || minNeckBelowEye <= 0f) return;
            float neckBelowEye = player.FirstPersonEyePose.position.y - neck.position.y;
            float drop = Mathf.Max(0f, minNeckBelowEye - neckBelowEye) * player.FirstPersonBlend;
            if (drop > 0f) hips.position += Vector3.down * drop;
        }

        /// <summary>
        /// 도달 당김: 오른손 목표가 지금(FBBIK 전) 오른 어깨에서 팔 길이 × maxReachRatio 안에 들어오도록 조준기 자리를 시선 정면 축을 따라 당길 거리(m).
        /// 시선 축을 따라 당기므로 조준 중 조준기는 화면 중앙에 남는다. 축만으로 못 들어오면 가장 가까워지는 만큼 (조준기는 눈에서 MinReachSightDistance 앞까지)
        /// </summary>
        private float ReachPullFor(Pose eye, Vector3 sightLocal, Quaternion gunRotation, Vector3 sightInGun, Vector3 handInGun)
        {
            if (maxReachRatio <= 0f) return 0f;
            RootMotion.BipedReferences refs = fbbik.references;
            if (refs.rightUpperArm == null || refs.rightForearm == null || refs.rightHand == null) return 0f;
            float radius = maxReachRatio *(Vector3.Distance(refs.rightUpperArm.position, refs.rightForearm.position)
                                                     + Vector3.Distance(refs.rightForearm.position, refs.rightHand.position));
            Vector3 handTarget = eye.position + eye.rotation * sightLocal + gunRotation * (handInGun - sightInGun);
            Vector3 d = handTarget - refs.rightUpperArm.position;
            if (d.sqrMagnitude <= radius * radius) return 0f;
            // |d - forward·p| = radius 의 작은 해
            float df = Vector3.Dot(d, eye.rotation * Vector3.forward);
            float disc = df * df - d.sqrMagnitude + radius * radius;
            float pull = disc >= 0f ? df - Mathf.Sqrt(disc) : df;
            return Mathf.Clamp(pull, 0f, Mathf.Max(0f, sightLocal.z - MinReachSightDistance));
        }

        /// <summary>팔꿈치 bend goal: 어깨에서 몸 기준(시선 yaw 만) elbowDirection 쪽. 왼팔은 좌우 반전</summary>
        private void UpdateElbowGoals()
        {
            RootMotion.BipedReferences refs = fbbik.references;
            if (refs.rightUpperArm == null || refs.leftUpperArm == null) return;
            Quaternion bodyYaw = Quaternion.Euler(0f, player.LookYaw, 0f);
            Vector3 mirrored = new Vector3(-elbowDirection.x, elbowDirection.y, elbowDirection.z);
            rightElbowGoal.position = refs.rightUpperArm.position + bodyYaw * elbowDirection.normalized * 0.4f;
            leftElbowGoal.position = refs.leftUpperArm.position + bodyYaw * mirrored.normalized * 0.4f;
        }

        private void SetElbowWeights(float right, float left)
        {
            fbbik.solver.rightArmChain.bendConstraint.weight = right;
            fbbik.solver.leftArmChain.bendConstraint.weight = left;
        }

        /// <summary>씬 뷰: 어깨 → 팔꿈치 bend goal (가중치가 있을 때만)</summary>
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying == false || fbbik == null || rightElbowGoal == null) return;
            RootMotion.BipedReferences refs = fbbik.references;
            Gizmos.color = Color.cyan;
            if (refs.rightUpperArm != null && fbbik.solver.rightArmChain.bendConstraint.weight > 0f)
            {
                Gizmos.DrawLine(refs.rightUpperArm.position, rightElbowGoal.position);
                Gizmos.DrawWireSphere(rightElbowGoal.position, 0.03f);
            }
            if (refs.leftUpperArm != null && fbbik.solver.leftArmChain.bendConstraint.weight > 0f)
            {
                Gizmos.DrawLine(refs.leftUpperArm.position, leftElbowGoal.position);
                Gizmos.DrawWireSphere(leftElbowGoal.position, 0.03f);
            }
        }

        /// <summary>눈 정면으로 벽까지 거리 (reach 안에 없으면 reach)</summary>
        private float WallDistance(Pose eye, float reach)
        {
            Vector3 forward = eye.rotation * Vector3.forward;
            int count = Physics.SphereCastNonAlloc(eye.position, wallProbeRadius, forward, hits, reach, wallMask, QueryTriggerInteraction.Ignore);
            float distance = reach;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(player.transform)) continue;
                if (hits[i].distance < distance) distance = hits[i].distance; // 0 = 눈이 이미 벽에 붙어 있음
            }
            return distance;
        }

        private static void SetWeights(IKEffector effector, float weight)
        {
            effector.positionWeight = weight;
            effector.rotationWeight = weight;
        }
    }
}
