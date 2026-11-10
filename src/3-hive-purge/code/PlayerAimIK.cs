using HybridShooter.Weapon;
using RootMotion.FinalIK;
using UnityEngine;

namespace HybridShooter.Player
{
    /// <summary>
    /// 조준 IK 연결 (Final IK AimIK). 매 프레임 조준점과 가중치를 AimIK 에 넘긴다 (Player 루트에 붙인다). 사격 반동은 PlayerGunRecoil.
    ///  - 조준점: 사격과 같은 광선(GunSlot.AimRay: 카메라 정면, 3인칭은 플레이어 옆부터)이 처음 닿는 곳 (없으면 maxAimDistance 앞).
    ///    자기 콜라이더는 무시 → 총구 방향과 실제 탄착점이 같은 점을 향한다
    ///  - 조준 기준: 지금 든 총의 총구(Gun.Muzzle). 총구의 정면이 조준점을 향하도록 척추를 돌린다 (총이 없으면 오른손 아래 AimTransform)
    ///  - 가중치: 드라이버의 조준 정도(Aim) × 상체 자세 레이어 가중치 × 팔 동작 없음(ActionBlend)
    ///  - 1인칭: 총은 PlayerFirstPersonIK 가 눈 기준 자리로 옮기므로, AimIK 는 가슴 정면을 조준점으로 돌린다 (척추가 시선을 따라가 팔이 덜 뻗게).
    ///    가중치는 조준 여부와 상관없이 firstPersonSpineWeight, 아래를 볼수록 firstPersonSpineWeightDown 쪽으로 (허리를 덜 숙여 목이 카메라 앞으로 안 나오게),
    ///    위를 볼수록 firstPersonSpineWeightUp 쪽으로
    ///    → 들고 다닐 때 0, 조준·사격할 때 1, 재장전·꺼내기·구르기·사망 중 0
    /// AimIK(실행 순서 9997)는 이 스크립트(100)와 PlayerController 의 카메라 갱신(0) 다음에 풀린다
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class PlayerAimIK : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerAnimationDriver driver;
        [Tooltip("비워 두면 같은 오브젝트에서 찾는다")]
        [SerializeField] private GunSlot gunSlot;
        [Tooltip("몸(Body)의 AimIK")]
        [SerializeField] private AimIK aimIK;
        [Tooltip("조준점을 찾을 때 광선이 닿는 레이어")]
        [SerializeField] private LayerMask aimMask = ~0;
        [SerializeField] private float maxAimDistance = 100f;
        [Tooltip("조준점이 이보다 가까우면(벽에 붙음) 이 거리로 민다. 너무 가까우면 척추가 크게 꺾인다")]
        [SerializeField] private float minAimDistance = 2f;
        [Tooltip("1인칭에서 척추가 시선을 따라가는 정도 (1 = 가슴 정면이 조준점을 정확히 향함)")]
        [SerializeField, Range(0f, 1f)] private float firstPersonSpineWeight = 0.7f;
        [Tooltip("1인칭에서 firstPersonDownPitch 만큼 아래를 볼 때의 척추 가중치 (그 사이는 보간). 낮을수록 허리를 덜 숙여 목이 카메라 앞으로 덜 나온다. firstPersonSpineWeight 와 같으면 예전과 같다")]
        [SerializeField, Range(0f, 1f)] private float firstPersonSpineWeightDown = 0.3f;
        [SerializeField] private float firstPersonDownPitch = 55f;
        [Tooltip("1인칭에서 firstPersonUpPitch 만큼 위를 볼 때의 척추 가중치 (그 사이는 보간). 낮을수록 가슴이 덜 젖혀져 어깨가 눈 앞 총 자리에서 덜 멀어진다. firstPersonSpineWeight 와 같으면 예전과 같다")]
        [SerializeField, Range(0f, 1f)] private float firstPersonSpineWeightUp = 0.3f;
        [SerializeField] private float firstPersonUpPitch = 70f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Transform defaultAimTransform;
        private Vector3 defaultAxis;
        private Gun aimGun;
        private Transform chestAim;            // 1인칭 조준 기준: 가슴 정면 (매 프레임 몸 정면으로 다시 맞춘다)
        private Transform body;
        private bool aimByChest;

        /// <summary>마지막으로 계산한 조준점</summary>
        public Vector3 AimPoint { get; private set; }

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (driver == null) driver = GetComponent<PlayerAnimationDriver>();
            if (aimIK == null) aimIK = GetComponentInChildren<AimIK>();
            if (gunSlot == null) gunSlot = GetComponent<GunSlot>();
            if (aimIK != null)
            {
                defaultAimTransform = aimIK.solver.transform;
                defaultAxis = aimIK.solver.axis;
            }
            CreateChestAim();
        }

        /// <summary>가슴 뼈 아래 빈 오브젝트 (1인칭 AimIK 기준). 방향은 매 프레임 애니메이션 뒤에 몸 정면으로 맞춘다</summary>
        private void CreateChestAim()
        {
            Animator animator = driver != null && driver.Animator != null ? driver.Animator : GetComponentInChildren<Animator>();
            if (animator == null || animator.isHuman == false) return;
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) return;
            body = animator.transform;
            chestAim = new GameObject("FP_ChestAim").transform;
            chestAim.SetParent(chest, false);
        }

        private void LateUpdate()
        {
            if (aimIK == null || player == null || driver == null) return;
            UpdateAimTransform();
            // 시작 시 자세가 아니라 이번 프레임 애니메이션 자세 기준 몸 정면 → AimIK 는 시선과의 차이(주로 위아래)만큼만 척추를 돌린다
            if (chestAim != null) chestAim.rotation = Quaternion.LookRotation(body.forward, body.up);

            float aim = Mathf.Lerp(driver.AimWeight, FirstPersonSpineWeight(), player.FirstPersonBlend);
            float weight = aim * driver.UpperPoseWeight * driver.ActionBlend;
            if (player.CurrentCameraTransform == null || weight <= 0f)
            {
                aimIK.solver.IKPositionWeight = 0f;
                return;
            }

            Ray ray = gunSlot != null ? gunSlot.AimRay : new Ray(player.CurrentCameraTransform.position, player.CurrentCameraTransform.forward);
            AimPoint = FindAimPoint(ray.origin, ray.direction);
            aimIK.solver.IKPosition = AimPoint;
            aimIK.solver.IKPositionWeight = weight;
        }

        /// <summary>1인칭 척추 가중치: 정면 = firstPersonSpineWeight, 아래로 갈수록 firstPersonSpineWeightDown, 위로 갈수록 firstPersonSpineWeightUp 쪽으로</summary>
        private float FirstPersonSpineWeight()
        {
            float pitch = player.LookPitch;
            if (pitch < 0f)
                return Mathf.Lerp(firstPersonSpineWeight, firstPersonSpineWeightUp, Mathf.InverseLerp(0f, firstPersonUpPitch, -pitch));
            return Mathf.Lerp(firstPersonSpineWeight, firstPersonSpineWeightDown, Mathf.InverseLerp(0f, firstPersonDownPitch, pitch));
        }

        /// <summary>AimIK 기준: 1인칭이면 가슴 정면, 아니면 지금 든 총의 총구 (총구 축 = 총의 정면)</summary>
        private void UpdateAimTransform()
        {
            Gun gun = gunSlot != null ? gunSlot.CurrentGun : null;
            bool byChest = chestAim != null && player.FirstPersonBlend >= 0.5f;
            if (gun == aimGun && byChest == aimByChest) return;
            aimGun = gun;
            aimByChest = byChest;
            Transform muzzle = gun != null ? gun.Muzzle : null;
            if (byChest)
            {
                aimIK.solver.transform = chestAim;
                aimIK.solver.axis = Vector3.forward;
            }
            else if (muzzle != null)
            {
                aimIK.solver.transform = muzzle;
                aimIK.solver.axis = muzzle.InverseTransformDirection(gun.transform.forward);
            }
            else if (defaultAimTransform != null)
            {
                aimIK.solver.transform = defaultAimTransform;
                aimIK.solver.axis = defaultAxis;
            }
        }

        private Vector3 FindAimPoint(Vector3 origin, Vector3 direction)
        {
            float distance = maxAimDistance;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(player.transform)) continue;
                if (hits[i].distance < distance) distance = hits[i].distance;
            }
            return origin + direction * Mathf.Max(distance, minAimDistance);
        }
    }
}
