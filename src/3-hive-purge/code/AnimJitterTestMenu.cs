using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HybridShooter.Player.EditorTools
{
    /// <summary>
    /// 애니메이션 떨림 자동 측정 메뉴 (Tools/HybridShooter/Animation Jitter Test).
    ///  - Run Quick / Run Full: 클립 이음새 검사(clip_loop.md)를 먼저 쓰고, PlayerTest 씬을 Play 해서 Player 에 AnimJitterScenarioRunner 를 붙인다.
    ///    끝나면 Play 를 멈춘다 (이미 Play 중이었으면 지금 씬의 Player 로 재고 Play 는 그대로 둔다)
    ///  - 결과: <프로젝트>/Logs/AnimJitter/<시각>/summary.md · clip_loop.md · 실행별 CSV
    /// </summary>
    [InitializeOnLoad]
    internal static class AnimJitterTestMenu
    {
        private const string ScenePath = "Assets/Scenes/MS/Scenes/PlayerTest.unity";
        private const string FolderKey = "HybridShooter.AnimJitter.Folder";
        private const string QuickKey = "HybridShooter.AnimJitter.Quick";
        private const string AutoExitKey = "HybridShooter.AnimJitter.AutoExit";

        private static string ResultsRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "AnimJitter"));

        static AnimJitterTestMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static string NewResultFolder()
        {
            string folder = Path.Combine(ResultsRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        [MenuItem("Tools/HybridShooter/Animation Jitter Test/Run Quick (평지 Baseline, 약 1분)", false, 100)]
        private static void RunQuick() => Run(true);

        [MenuItem("Tools/HybridShooter/Animation Jitter Test/Run Full (IK 비교·지형·144fps·1인칭, 약 5분)", false, 101)]
        private static void RunFull() => Run(false);

        [MenuItem("Tools/HybridShooter/Animation Jitter Test/Open Results Folder", false, 120)]
        private static void OpenResults()
        {
            Directory.CreateDirectory(ResultsRoot);
            EditorUtility.RevealInFinder(ResultsRoot);
        }

        private static void Run(bool quick)
        {
            if (EditorApplication.isPlaying)
            {
                string folder = NewResultFolder();
                AnimClipLoopCheck.Write(folder);
                Attach(folder, quick, autoExit: false);
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            string newFolder = NewResultFolder();
            AnimClipLoopCheck.Write(newFolder);
            SessionState.SetString(FolderKey, newFolder);
            SessionState.SetBool(QuickKey, quick);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            string folder = SessionState.GetString(FolderKey, "");
            if (string.IsNullOrEmpty(folder)) return;
            SessionState.EraseString(FolderKey);
            Attach(folder, SessionState.GetBool(QuickKey, true), autoExit: true);
        }

        private static void Attach(string folder, bool quick, bool autoExit)
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player == null)
            {
                Debug.LogError("[AnimJitter] 씬에 PlayerController 가 없습니다");
                if (autoExit) EditorApplication.ExitPlaymode();
                return;
            }
            if (player.GetComponent<AnimJitterScenarioRunner>() != null)
            {
                Debug.LogWarning("[AnimJitter] 이미 측정 중입니다");
                return;
            }

            SessionState.SetBool(AutoExitKey, autoExit);
            AnimJitterScenarioRunner.Completed -= OnCompleted;
            AnimJitterScenarioRunner.Completed += OnCompleted;
            var runner = player.gameObject.AddComponent<AnimJitterScenarioRunner>();
            runner.quick = quick;
            runner.outputFolder = folder;
        }

        private static void OnCompleted(string folder)
        {
            AnimJitterScenarioRunner.Completed -= OnCompleted;
            Debug.Log($"[AnimJitter] 결과: {Path.Combine(folder, "summary.md")}");
            if (SessionState.GetBool(AutoExitKey, false)) EditorApplication.ExitPlaymode();
        }
    }
}
