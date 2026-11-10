using System;
using System.Collections.Generic;
using HybridShooter.Weapon;
using UnityEngine;

namespace HybridShooter.Player
{
    /// <summary>
    /// 총별 손 위치 데이터 (총 종류 = Gun 클래스 이름으로 찾는다). PlayerAnimationProfile.gripTable 에 연결한다.
    /// 에셋이라 Play 모드에서 바꾼 값이 Play 를 끝내도 남는다.
    ///  - 총 위치·회전 (WeaponSocket 기준, 소켓은 오른손 뼈 아래) = 오른손이 쥐는 자리
    ///  - 손잡이 지점 (총 기준) = 오른손 바닥이 올 자리
    ///  - 왼손 손목 자리 (총 기준) → PlayerBodyIK 가 왼팔 LimbIK 로 붙인다
    ///  - 조준기 (총 기준) → 1인칭 조준 때 눈이 이 뒤에 온다
    ///  - 1인칭 평소 조준기 자리 · 조준 거리 → PlayerFirstPersonIK (비워 두면 그 컴포넌트의 기본값)
    ///  - 사격 반동 (총 기준) → PlayerGunRecoil (customRecoil 이 꺼져 있으면 총 종류별 기본값)
    ///  - 조준 방식 (조준기 정렬 / 스코프 화면 / 어깨 견착) → PlayerFirstPersonIK · PlayerAimView (Auto = 총 종류별 기본)
    /// </summary>
    [CreateAssetMenu(menuName = "HybridShooter/Weapon Grip Table", fileName = "WeaponGripTable")]
    public class WeaponGripTable : ScriptableObject
    {
        /// <summary>조준 방식</summary>
        public enum AimMode
        {
            [Tooltip("총 종류별 기본 (저격총 = 스코프, 로켓·그레네이드 런처 = 어깨 견착, 나머지 = 조준기 정렬)")]
            Auto,
            [Tooltip("1인칭 조준 때 조준기를 화면 중앙 눈앞에 (가늠자·가늠쇠로 본다)")]
            Sight,
            [Tooltip("조준하면 1·3인칭 모두 스코프 화면 (눈 카메라 + 확대 + 오버레이, 총·몸은 숨김)")]
            Scope,
            [Tooltip("1인칭 조준 때 총은 어깨 위 옆자리, 화면 중앙에 조준선 UI")]
            Shoulder,
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("총 종류 (Gun 클래스 이름: Rifle, Pistol ...)")]
            public string gunType;
            [Tooltip("총 위치 (WeaponSocket 기준)")]
            public Vector3 gunPosition;
            public Quaternion gunRotation = Quaternion.identity;
            [Tooltip("손잡이 지점 (총 기준). 오른손 바닥이 이 자리에 오도록 총을 붙인다. (0,0,0) = 아직 없음")]
            public Vector3 gripPoint;
            [Tooltip("왼손 손목 자리 (총 기준)")]
            public Vector3 leftHandPosition;
            public Quaternion leftHandRotation = Quaternion.identity;
            [Tooltip("조준기 자리 (총 기준, 방향 = 총 정면)")]
            public Vector3 sightPosition;
            [Tooltip("1인칭 평소: 조준기가 올 자리 (1인칭 눈 기준 m, x 오른쪽 y 위 z 앞). (0,0,0) = PlayerFirstPersonIK 기본값")]
            public Vector3 fpHipSight;
            [Tooltip("1인칭 조준: 눈과 조준기 사이 거리(m). 0 = PlayerFirstPersonIK 기본값")]
            public float adsDistance;
            [Tooltip("조준 방식. Auto = 총 종류별 기본 (DefaultAimMode)")]
            public AimMode aimMode;
            [Tooltip("어깨 견착 조준: 조준기가 올 자리 (1인칭 눈 기준 m, x 오른쪽 y 위 z 앞). (0,0,0) = PlayerFirstPersonIK 기본값")]
            public Vector3 fpShoulderSight;
            [Tooltip("반동을 아래 값으로 (꺼져 있으면 총 종류별 기본값 DefaultRecoil)")]
            public bool customRecoil;
            public RecoilSettings recoil = new RecoilSettings();
        }

        /// <summary>사격 반동 (총 기준, PlayerGunRecoil). 한 발 크기 × 프로필 recoilMagnitude</summary>
        [Serializable]
        public class RecoilSettings
        {
            [Tooltip("총구가 위로 튀는 각도(도). 손잡이를 축으로 돈다")]
            public float pitch = 3f;
            [Tooltip("총이 뒤(개머리판 쪽)로 밀리는 거리(m)")]
            public float back = 0.03f;
            [Tooltip("좌우로 흔들리는 최대 각도(도, 무작위)")]
            public float yaw = 1f;
            [Tooltip("옆으로 기우는 최대 각도(도, 무작위)")]
            public float roll = 2f;
            [Tooltip("한 발 쐈을 때 가장 크게 튀기까지 시간(초). 제자리로 돌아오는 데는 약 5배")]
            public float kickTime = 0.05f;
            [Tooltip("연사할 때 쌓이는 한계 (한 발 크기의 배수)")]
            public float maxStack = 2.5f;

            public RecoilSettings() { }
            public RecoilSettings(float pitch, float back, float yaw, float roll, float kickTime, float maxStack)
            {
                this.pitch = pitch; this.back = back; this.yaw = yaw; this.roll = roll; this.kickTime = kickTime; this.maxStack = maxStack;
            }
        }

        private static readonly Dictionary<string, RecoilSettings> DefaultRecoils = new Dictionary<string, RecoilSettings>
        {
            { "Pistol", new RecoilSettings(6f, 0.03f, 1.5f, 3f, 0.05f, 2f) },
            { "SMG", new RecoilSettings(2f, 0.02f, 1f, 1.5f, 0.04f, 3f) },
            { "Rifle", new RecoilSettings(3f, 0.03f, 1f, 2f, 0.05f, 2.5f) },
            { "AssaultRifle", new RecoilSettings(3f, 0.03f, 1f, 2f, 0.05f, 2.5f) },
            { "ShotGun", new RecoilSettings(8f, 0.06f, 2f, 3f, 0.07f, 1.5f) },
            { "SniperRifle", new RecoilSettings(7f, 0.06f, 1f, 2f, 0.08f, 1.5f) },
            { "RocketLauncher", new RecoilSettings(4f, 0.08f, 1f, 1f, 0.1f, 1.2f) },
            { "GrenadeLauncher", new RecoilSettings(6f, 0.05f, 1f, 2f, 0.08f, 1.5f) },
        };
        private static readonly RecoilSettings FallbackRecoil = new RecoilSettings();

        /// <summary>총 종류별 기본 조준 방식</summary>
        public static AimMode DefaultAimMode(string gunType) => gunType switch
        {
            "SniperRifle" => AimMode.Scope,
            "RocketLauncher" => AimMode.Shoulder,
            "GrenadeLauncher" => AimMode.Shoulder,
            _ => AimMode.Sight,
        };

        /// <summary>이 총의 조준 방식 (표에 Auto 가 아니면 그 값, 아니면 종류별 기본)</summary>
        public AimMode AimModeFor(Gun gun)
        {
            if (gun == null) return AimMode.Sight;
            Entry entry = Find(gun);
            return entry != null && entry.aimMode != AimMode.Auto ? entry.aimMode : DefaultAimMode(Key(gun));
        }

        /// <summary>총 종류별 기본 반동 (표에 값이 없을 때)</summary>
        public static RecoilSettings DefaultRecoil(string gunType) =>
            gunType != null && DefaultRecoils.TryGetValue(gunType, out RecoilSettings settings) ? settings : FallbackRecoil;

        /// <summary>이 총의 반동: 표에 customRecoil 이 켜져 있으면 그 값, 아니면 종류별 기본값</summary>
        public RecoilSettings RecoilFor(Gun gun)
        {
            if (gun == null) return FallbackRecoil;
            Entry entry = Find(gun);
            return entry != null && entry.customRecoil && entry.recoil != null ? entry.recoil : DefaultRecoil(Key(gun));
        }

        public List<Entry> entries = new List<Entry>();

        public static string Key(Gun gun) => gun.GetType().Name;

        public Entry Find(Gun gun)
        {
            if (gun == null) return null;
            string key = Key(gun);
            foreach (Entry entry in entries)
                if (entry.gunType == key) return entry;
            return null;
        }
    }
}
