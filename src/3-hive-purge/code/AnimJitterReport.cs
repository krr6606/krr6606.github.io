using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HybridShooter.Player
{
    /// <summary>
    /// AnimJitterProbe 기록 → 튐 판정·원인 단계·이벤트 상관 → CSV(프레임별)·summary.md (디버그 전용).
    /// 튐 = 상체 뼈(가슴·머리·양손·총구) 가속도가 그 실행의 중앙값 × popRatio 와 popFloor 중 큰 값을 넘은 프레임
    /// 원인 단계 = 그 프레임(±1)에 이미 튄 가장 이른 단계: Anim(애니메이션) → PreFBBIK(AimIK·왼손) → Final(FBBIK·Grounder)
    /// </summary>
    public static class AnimJitterReport
    {
        public const int OriginAnim = 0, OriginAimHand = 1, OriginFullBody = 2;
        public static readonly string[] OriginNames = { "Anim(클립·블렌드·전환)", "AimIK·왼손", "FBBIK·Grounder" };
        /// <summary>프로브 이벤트 + 루트 높이 튐(분석에서 판정)</summary>
        public static readonly int EventCount = AnimJitterProbe.EventCount + 1;
        public static readonly int EventRootYSpike = AnimJitterProbe.EventCount;
        private static readonly int[] KeyBones =
            { AnimJitterProbe.BoneChest, AnimJitterProbe.BoneHead, AnimJitterProbe.BoneLeftHand, AnimJitterProbe.BoneRightHand, AnimJitterProbe.BoneMuzzle };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string EventName(int e) => e < AnimJitterProbe.EventCount ? AnimJitterProbe.EventNames[e] : "RootYSpike";

        /// <summary>실행 하나의 결과 (프레임 기록은 버리고 수치만 남긴다)</summary>
        public class RunResult
        {
            public string pass, surface, view, scenario, config;
            public int targetFps;
            public int frames;
            public float duration, avgFps, avgSpeed;
            public int pops;                                   // Final 단계 튐 프레임 수
            public readonly int[] origin = new int[3];         // 튐 원인 단계별
            public int cameraOnlyPops;                         // 화면(카메라 기준)에서만 튐 → 카메라·루트
            public readonly float[] rms = new float[AnimJitterProbe.StageCount];      // 상체 뼈 가속도 RMS (m/s²)
            public readonly float[] chestAngRms = new float[AnimJitterProbe.StageCount];
            public float maxRatio;                             // 가장 큰 튐 / 중앙값
            public string maxWhere = "-";
            public readonly int[] eventFrames = new int[EventCount];   // 이벤트(±1프레임)가 있는 프레임 수
            public readonly int[] popWithEvent = new int[EventCount];  // 그중 Final 튐인 프레임 수
            public int sprintToggles, baseLoops, upperLoops, rootSpikes;
            public float maxLeftReach, maxAimJump, maxPelvisStep;

            public float PopsPer10s => duration > 0f ? pops * 10f / duration : 0f;
            public bool IsBaseline => config == "Baseline";
        }

        public static RunResult Analyze(List<AnimJitterProbe.Sample> samples, float popRatio, float popFloor)
        {
            var r = new RunResult();
            int n = samples.Count;
            r.frames = n;
            if (n == 0) return r;
            r.duration = samples.Sum(s => s.dt);
            r.avgFps = r.duration > 0f ? n / r.duration : 0f;
            r.avgSpeed = samples.Average(s => s.speed);

            // 단계별 튐
            var pop = new bool[AnimJitterProbe.StageCount][];
            for (int st = 0; st < AnimJitterProbe.StageCount; st++)
            {
                pop[st] = new bool[n];
                double sum = 0;
                foreach (int b in KeyBones)
                {
                    float median = Median(samples.Select(s => s.Acc(st, b)));
                    float threshold = Mathf.Max(median * popRatio, popFloor);
                    for (int i = 0; i < n; i++)
                    {
                        float a = samples[i].Acc(st, b);
                        sum += a * a;
                        if (a <= threshold) continue;
                        pop[st][i] = true;
                        float ratio = a / Mathf.Max(median, 1e-3f);
                        if (st == AnimJitterProbe.StageFinal && ratio > r.maxRatio)
                        {
                            r.maxRatio = ratio;
                            r.maxWhere = $"{AnimJitterProbe.BoneNames[b]} @{i}";
                        }
                    }
                }
                r.rms[st] = (float)Math.Sqrt(sum / (n * KeyBones.Length));
                r.chestAngRms[st] = (float)Math.Sqrt(samples.Average(s => (double)s.Ang(st, 0) * s.Ang(st, 0)));
            }

            bool[] final = pop[AnimJitterProbe.StageFinal];
            for (int i = 0; i < n; i++)
            {
                if (final[i])
                {
                    r.pops++;
                    if (Near(pop[AnimJitterProbe.StageAnim], i)) r.origin[OriginAnim]++;
                    else if (Near(pop[AnimJitterProbe.StagePreFullBody], i)) r.origin[OriginAimHand]++;
                    else r.origin[OriginFullBody]++;
                }
                else if (pop[AnimJitterProbe.StageCamera][i] && Near(final, i) == false) r.cameraOnlyPops++;
            }

            // 루트 높이 튐 (물리 위치 흔들림)
            float rootMedian = Median(samples.Select(s => s.rootAcc));
            float rootThreshold = Mathf.Max(rootMedian * popRatio, 2f);
            var rootSpike = new bool[n];
            for (int i = 0; i < n; i++) rootSpike[i] = samples[i].rootAcc > rootThreshold;

            // 이벤트 상관 (±1 프레임)
            for (int e = 0; e < EventCount; e++)
            {
                for (int i = 0; i < n; i++)
                {
                    bool near = false;
                    for (int j = Mathf.Max(0, i - 1); j <= Mathf.Min(n - 1, i + 1) && near == false; j++)
                        near = e == EventRootYSpike ? rootSpike[j] : samples[j].Has(e);
                    if (near == false) continue;
                    r.eventFrames[e]++;
                    if (final[i]) r.popWithEvent[e]++;
                }
            }

            for (int i = 0; i < n; i++)
            {
                AnimJitterProbe.Sample s = samples[i];
                if (s.Has(AnimJitterProbe.EventSprintToggle)) r.sprintToggles++;
                if (s.Has(AnimJitterProbe.EventBaseLoop)) r.baseLoops++;
                if (s.Has(AnimJitterProbe.EventUpperLoop)) r.upperLoops++;
                if (rootSpike[i]) r.rootSpikes++;
                if (s.leftWeight > 0.01f) r.maxLeftReach = Mathf.Max(r.maxLeftReach, s.leftReach);
                if (i > 0)
                {
                    if (s.aimWeight > 0.01f) r.maxAimJump = Mathf.Max(r.maxAimJump, Mathf.Abs(s.aimDistance - samples[i - 1].aimDistance));
                    r.maxPelvisStep = Mathf.Max(r.maxPelvisStep, Mathf.Abs(s.pelvisOffset - samples[i - 1].pelvisOffset));
                }
            }
            return r;
        }

        private static bool Near(bool[] flags, int i) =>
            flags[i] || (i > 0 && flags[i - 1]) || (i < flags.Length - 1 && flags[i + 1]);

        private static float Median(IEnumerable<float> values)
        {
            List<float> list = values.ToList();
            if (list.Count == 0) return 0f;
            list.Sort();
            return list[list.Count / 2];
        }

        #region CSV
        public static void WriteCsv(string path, List<AnimJitterProbe.Sample> samples)
        {
            var sb = new StringBuilder(samples.Count * 400);
            sb.Append("frame,time,dt,fixedSteps,speed,moveX,moveY,speedScale,sprint,grounded,baseState,baseTime,upperTime,inTransition,aimW,aimDist,leftW,leftReach,pelvis,rootY,rootAcc");
            for (int st = 0; st < AnimJitterProbe.StageCount; st++)
                for (int b = 0; b < AnimJitterProbe.BoneCount; b++)
                    sb.Append(',').Append($"acc_{AnimJitterProbe.StageNames[st]}_{AnimJitterProbe.BoneNames[b]}");
            for (int st = 0; st < AnimJitterProbe.StageCount; st++)
                for (int b = 0; b < AnimJitterProbe.AngBoneCount; b++)
                    sb.Append(',').Append($"ang_{AnimJitterProbe.StageNames[st]}_{AnimJitterProbe.AngBoneNames[b]}");
            sb.Append(",events\n");

            for (int i = 0; i < samples.Count; i++)
            {
                AnimJitterProbe.Sample s = samples[i];
                sb.Append(i).Append(',');
                F(sb, s.time); F(sb, s.dt * 1000f); sb.Append(s.fixedSteps).Append(',');
                F(sb, s.speed); F(sb, s.moveX); F(sb, s.moveY); F(sb, s.speedScale);
                sb.Append(s.sprint ? 1 : 0).Append(',').Append(s.grounded ? 1 : 0).Append(',').Append(s.baseState).Append(',');
                F(sb, s.baseTime); F(sb, s.upperTime); sb.Append(s.inTransition ? 1 : 0).Append(',');
                F(sb, s.aimWeight); F(sb, s.aimDistance); F(sb, s.leftWeight); F(sb, s.leftReach); F(sb, s.pelvisOffset);
                F(sb, s.rootY); F(sb, s.rootAcc);
                foreach (float a in s.acc) F(sb, a);
                foreach (float a in s.ang) F(sb, a);
                bool first = true;
                for (int e = 0; e < AnimJitterProbe.EventCount; e++)
                {
                    if (s.Has(e) == false) continue;
                    if (first == false) sb.Append('|');
                    sb.Append(AnimJitterProbe.EventNames[e]);
                    first = false;
                }
                sb.Append('\n');
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static void F(StringBuilder sb, float v) => sb.Append(v.ToString("0.####", Inv)).Append(',');
        #endregion

        #region summary.md
        public static string Summary(List<RunResult> runs, string header, float popRatio, float popFloor)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 애니메이션 떨림 측정 결과");
            sb.AppendLine();
            sb.AppendLine(header);
            sb.AppendLine($"- 튐 판정: 상체 뼈(가슴·머리·양손·총구) 가속도 > max(그 실행 중앙값 × {popRatio:0.#}, {popFloor:0.#} m/s²)");
            sb.AppendLine("- 원인 단계: 그 프레임(±1)에 이미 튄 가장 이른 단계. Anim = IK 전 애니메이션, AimIK·왼손 = FBBIK 전, FBBIK·Grounder = 마지막");
            sb.AppendLine("- 카메라만 = 몸 자세는 매끄러운데 카메라 기준으로만 튐 (카메라·루트 움직임)");
            sb.AppendLine("- RMS = 상체 뼈 가속도 제곱평균 (m/s²). Anim → Final 이 크게 늘면 IK 가 흔들림을 더한다");
            sb.AppendLine();

            sb.AppendLine("## 1. 자동 판정");
            sb.AppendLine();
            foreach (string line in Verdict(runs)) sb.AppendLine($"- {line}");
            sb.AppendLine();

            sb.AppendLine("## 2. 기준 설정(Baseline) 시나리오별");
            sb.AppendLine();
            sb.AppendLine("| 묶음 | 바닥 | 시점 | FPS 목표/실제 | 시나리오 | 속도 m/s | 튐/10초 | Anim | AimIK·왼손 | FBBIK·Grounder | 카메라만 | RMS Anim→PreFBBIK→Final | 가슴 회전 RMS Anim→Final (°/s²) | Sprint 전환 | 루프 Base/Upper | 루트Y 튐 | 왼팔 최대 | 최대 튐 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (RunResult r in runs.Where(x => x.IsBaseline))
            {
                sb.AppendLine($"| {r.pass} | {r.surface} | {r.view} | {r.targetFps}/{r.avgFps:0} | {r.scenario} | {r.avgSpeed:0.00} | **{r.PopsPer10s:0.0}** | " +
                              $"{r.origin[OriginAnim]} | {r.origin[OriginAimHand]} | {r.origin[OriginFullBody]} | {r.cameraOnlyPops} | " +
                              $"{r.rms[0]:0.0} → {r.rms[1]:0.0} → {r.rms[2]:0.0} | {r.chestAngRms[0]:0} → {r.chestAngRms[2]:0} | {r.sprintToggles} | {r.baseLoops}/{r.upperLoops} | {r.rootSpikes} | " +
                              $"{(r.maxLeftReach > 0f ? r.maxLeftReach.ToString("0.00") : "-")} | ×{r.maxRatio:0.0} {r.maxWhere} |");
            }
            sb.AppendLine();

            List<RunResult> compare = runs.Where(x => x.pass == "IKCompare").ToList();
            List<string> configs = compare.Select(x => x.config).Distinct().ToList();
            if (configs.Count > 1)
            {
                sb.AppendLine("## 3. IK·레이어 끄기 비교 (평지, 3인칭, 60fps) — 튐/10초 (괄호: Baseline 대비)");
                sb.AppendLine();
                sb.AppendLine("끈 것을 껐을 때 크게 줄어들면 그것이 원인 (NoUpperPose 는 Upper Pose 레이어만 끈다. 조준·왼손 IK 는 그대로. 이동 중에는 Baseline 도 레이어가 upperPoseMoving 까지 낮아져 있다)");
                sb.AppendLine();
                sb.AppendLine("| 시나리오 | " + string.Join(" | ", configs) + " |");
                sb.AppendLine("|---|" + string.Concat(configs.Select(_ => "---|")));
                foreach (string scenario in compare.Select(x => x.scenario).Distinct())
                {
                    RunResult baseline = compare.FirstOrDefault(x => x.scenario == scenario && x.IsBaseline);
                    sb.Append($"| {scenario} |");
                    foreach (string config in configs)
                    {
                        RunResult r = compare.FirstOrDefault(x => x.scenario == scenario && x.config == config);
                        if (r == null) { sb.Append(" - |"); continue; }
                        string delta = baseline != null && r != baseline && baseline.PopsPer10s > 0f
                            ? $" ({(r.PopsPer10s - baseline.PopsPer10s) / baseline.PopsPer10s * 100f:+0;-0}%)" : "";
                        sb.Append($" {r.PopsPer10s:0.0}{delta} |");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
                sb.AppendLine("| 설정 | 전체 튐/10초 | RMS Final 평균 |");
                sb.AppendLine("|---|---|---|");
                foreach (string config in configs)
                {
                    List<RunResult> list = compare.Where(x => x.config == config).ToList();
                    sb.AppendLine($"| {config} | {Rate(list):0.0} | {list.Average(x => x.rms[2]):0.0} |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## 4. 튐과 같이 일어난 이벤트 (Baseline 전체 합산, ±1프레임)");
            sb.AppendLine();
            sb.AppendLine("배수(lift) = 튄 프레임 중 이벤트 비율 ÷ 전체 프레임 중 이벤트 비율. 1 보다 훨씬 크면 그 이벤트가 튐과 함께 온다");
            sb.AppendLine();
            sb.AppendLine("| 이벤트 | 전체 프레임 중 | 튄 프레임 중 | 배수 |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var row in EventLift(runs.Where(x => x.IsBaseline).ToList()).OrderByDescending(x => x.lift))
                sb.AppendLine($"| {row.name} | {row.framePct:0.0}% | {row.popPct:0.0}% | **{row.lift:0.00}** |");
            sb.AppendLine();

            sb.AppendLine("## 5. 실행 전체");
            sb.AppendLine();
            sb.AppendLine("| # | 묶음 | 바닥 | 시점 | FPS | 시나리오 | 설정 | 튐/10초 | Anim/AimIK·왼손/FBBIK | RMS Final | 파일 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < runs.Count; i++)
            {
                RunResult r = runs[i];
                sb.AppendLine($"| {i + 1} | {r.pass} | {r.surface} | {r.view} | {r.targetFps}/{r.avgFps:0} | {r.scenario} | {r.config} | {r.PopsPer10s:0.0} | " +
                              $"{r.origin[0]}/{r.origin[1]}/{r.origin[2]} | {r.rms[2]:0.0} | {CsvName(i, r)} |");
            }
            return sb.ToString();
        }

        public static string CsvName(int index, RunResult r) =>
            $"{index + 1:00}_{r.pass}_{r.surface}_{r.view}_{r.targetFps}_{r.scenario}_{r.config}.csv";

        private static float Rate(List<RunResult> list)
        {
            float duration = list.Sum(x => x.duration);
            return duration > 0f ? list.Sum(x => x.pops) * 10f / duration : 0f;
        }

        private static IEnumerable<(string name, float framePct, float popPct, float lift)> EventLift(List<RunResult> runs)
        {
            int frames = runs.Sum(x => x.frames);
            int pops = runs.Sum(x => x.pops);
            for (int e = 0; e < EventCount; e++)
            {
                float framePct = frames > 0 ? runs.Sum(x => x.eventFrames[e]) * 100f / frames : 0f;
                float popPct = pops > 0 ? runs.Sum(x => x.popWithEvent[e]) * 100f / pops : 0f;
                float lift = framePct > 0f ? popPct / framePct : 0f;
                yield return (EventName(e), framePct, popPct, lift);
            }
        }

        /// <summary>수치에서 바로 읽히는 결론 몇 줄 (사람이 표를 보기 전에 방향을 잡게)</summary>
        private static IEnumerable<string> Verdict(List<RunResult> runs)
        {
            List<RunResult> baseline = runs.Where(x => x.IsBaseline).ToList();
            int pops = baseline.Sum(x => x.pops);
            if (pops == 0)
            {
                yield return "Baseline 실행에서 튐이 잡히지 않았다. popRatio·popFloor 를 낮춰 다시 재거나, 증상이 나는 상황(바닥·속도·시점)을 시나리오에 추가해야 한다";
                yield break;
            }

            yield return $"Baseline 튐 {pops}회 ({Rate(baseline):0.0}/10초). 원인 단계: " +
                         string.Join(", ", Enumerable.Range(0, 3).Select(o => $"{OriginNames[o]} {baseline.Sum(x => x.origin[o]) * 100f / pops:0}%")) +
                         $", 카메라만 튐 {baseline.Sum(x => x.cameraOnlyPops)}회";

            var lifts = EventLift(baseline).Where(x => x.lift >= 1.5f && x.popPct >= 15f).OrderByDescending(x => x.lift).ToList();
            if (lifts.Count > 0)
                yield return "튐과 함께 오는 이벤트: " + string.Join(", ", lifts.Select(x => $"{x.name} (튐의 {x.popPct:0}%, 배수 {x.lift:0.0})"));
            else
                yield return "튐과 뚜렷하게 겹치는 이벤트는 없다 (배수 1.5 이상 + 튐의 15% 이상인 것 없음)";

            List<RunResult> compare = runs.Where(x => x.pass == "IKCompare").ToList();
            float baseRate = Rate(compare.Where(x => x.IsBaseline).ToList());
            if (baseRate > 0f)
            {
                foreach (string config in compare.Select(x => x.config).Distinct().Where(c => c != "Baseline"))
                {
                    float rate = Rate(compare.Where(x => x.config == config).ToList());
                    float change = (rate - baseRate) / baseRate * 100f;
                    if (change <= -30f) yield return $"{config}: 튐이 {-change:0}% 줄어든다 → 끈 부분이 원인에 들어 있다";
                    else if (change >= 30f) yield return $"{config}: 튐이 {change:0}% 늘어난다 → 그 부분이 오히려 흔들림을 가려 주고 있다";
                }
            }

            RunResult worst = baseline.OrderByDescending(x => x.PopsPer10s).First();
            yield return $"가장 심한 실행: {worst.pass}/{worst.surface}/{worst.view}/{worst.targetFps}fps/{worst.scenario} ({worst.PopsPer10s:0.0}/10초, 최대 ×{worst.maxRatio:0.0} {worst.maxWhere})";
        }
        #endregion
    }
}
