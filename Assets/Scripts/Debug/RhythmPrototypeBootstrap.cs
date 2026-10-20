using BeatSpell.Rhythm;
using BeatSpell.RhythmInput;
using UnityEngine;

namespace BeatSpell.Prototype
{
    /// <summary>
    /// 第 1 轮场景入口：串联时钟、判定与 Console 日志。
    /// SimulateInputAtOffsetMs 供 UnityMCP 验收，不依赖真实按键。
    /// </summary>
    public class RhythmPrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private PlayerRhythmInputController inputController;
        [SerializeField] private RhythmJudge rhythmJudge = new RhythmJudge();

        public BeatClock Clock => beatClock;
        public RhythmJudge Judge => rhythmJudge;
        public int BeatIndex => beatClock != null ? beatClock.BeatIndex : 0;
        public int VertexIndex => beatClock != null ? beatClock.VertexIndex : 0;
        public bool IsClockRunning => beatClock != null && beatClock.IsRunning;

        private void Awake()
        {
            CacheRefs();
        }

        public void Bind(BeatClock clock, PlayerRhythmInputController controller)
        {
            beatClock = clock;
            inputController = controller;
        }

        /// <summary>
        /// 真实按键路径：用当前 dspTime 判定。
        /// </summary>
        public void ProcessLiveInput(BeatInputType type)
        {
            if (beatClock == null || !beatClock.IsRunning)
            {
                Debug.LogWarning("[Beat] 时钟尚未启动，忽略输入。");
                return;
            }

            BeatTimingInfo timing = beatClock.GetTimingAt(AudioSettings.dspTime);
            Emit(type, timing);
        }

        /// <summary>
        /// MCP / Editor 测试入口：注入与最近节拍的毫秒偏差，验证判定窗口。
        /// offset 0 → Perfect；约 80 → Good；约 200 → Miss（默认窗口）。
        /// </summary>
        public BeatTimingGrade SimulateInputAtOffsetMs(BeatInputType type, float offsetMs)
        {
            if (beatClock == null)
            {
                Debug.LogWarning("[Beat] 缺少 BeatClock。");
                return BeatTimingGrade.Miss;
            }

            if (!beatClock.IsRunning)
            {
                beatClock.StartClock();
            }

            BeatTimingInfo timing = beatClock.GetSimulatedTiming(offsetMs);
            return Emit(type, timing);
        }

        public void SetGoodWindowMs(float goodWindowMs)
        {
            rhythmJudge.GoodWindowMs = goodWindowMs;
        }

        public void SetPerfectWindowMs(float perfectWindowMs)
        {
            rhythmJudge.PerfectWindowMs = perfectWindowMs;
        }

        private BeatTimingGrade Emit(BeatInputType type, BeatTimingInfo timing)
        {
            BeatTimingGrade grade = rhythmJudge.Evaluate(timing.DeltaMs);
            string sign = timing.DeltaMs >= 0f ? "+" : string.Empty;
            Debug.Log(
                $"[Beat] {type} | {grade} | delta={sign}{timing.DeltaMs:0}ms | beat={timing.BeatIndex} | vertex={timing.VertexIndex}({BeatTimingInfo.VertexName(timing.VertexIndex)})");

            if (inputController != null)
            {
                inputController.RaiseRhythmInput(type, grade, timing.DeltaMs, timing.BeatIndex);
            }

            return grade;
        }

        private void CacheRefs()
        {
            if (beatClock == null)
            {
                beatClock = GetComponent<BeatClock>();
            }

            if (inputController == null)
            {
                inputController = GetComponent<PlayerRhythmInputController>();
            }
        }
    }
}
