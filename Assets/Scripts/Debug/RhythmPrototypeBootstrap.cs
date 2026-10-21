using System;
using BeatSpell.Combat;
using BeatSpell.Rhythm;
using BeatSpell.RhythmInput;
using BeatSpell.Spell;
using BeatSpell.UI;
using UnityEngine;

namespace BeatSpell.Prototype
{
    /// <summary>
    /// 场景入口：节拍判定 + 咏唱生命周期 + 空施法封锁 + 火焰魔法匹配 + 战斗结算查询。
    /// </summary>
    public class RhythmPrototypeBootstrap : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private PlayerRhythmInputController inputController;
        [SerializeField] private RhythmJudge rhythmJudge = new RhythmJudge();
        [SerializeField] private SpellInputBuffer spellInputBuffer = new SpellInputBuffer();
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private SpellDefinition[] spellCatalog;
        [SerializeField] private BattleGameManager battleGameManager;
        [SerializeField] private BeatInputMarkerView markerView;
        [SerializeField] private float chantLifetimeBeats = 4f;
        [SerializeField] private float emptyCastPenaltyBeats = 0.5f;

        private double blockedUntilDspTime;
        private double queryDspTime;

        public BeatClock Clock => beatClock;
        public RhythmJudge Judge => rhythmJudge;
        public SpellInputBuffer InputBuffer => spellInputBuffer;
        public SpellCaster Caster => spellCaster;
        public BattleGameManager Battle => battleGameManager;
        public int BeatIndex => beatClock != null ? beatClock.BeatIndex : 0;
        public int VertexIndex => beatClock != null ? beatClock.VertexIndex : 0;
        public float CurrentBeatFloat => beatClock != null ? beatClock.BeatFloat : 0f;
        public float CurrentTrackPhase => beatClock != null ? beatClock.TrackPhase : 0f;
        public bool IsClockRunning => beatClock != null && beatClock.IsRunning;
        public int EnemyCurrentHealth => battleGameManager != null ? battleGameManager.EnemyCurrentHealth : 0;
        public bool IsEnemyDead => battleGameManager != null && battleGameManager.IsEnemyDead;
        public bool IsVictory => battleGameManager != null && battleGameManager.IsVictory;
        public bool IsInputBlocked => queryDspTime < blockedUntilDspTime;
        public int BufferCount => spellInputBuffer != null ? spellInputBuffer.Count : 0;
        public bool HasActiveChantLifecycle => spellInputBuffer != null && spellInputBuffer.HasActiveChantLifecycle;
        public int MarkerCount => markerView != null ? markerView.MarkerCount : 0;
        public string LastSpellLog => spellCaster != null ? spellCaster.LastSpellLog : string.Empty;
        public float ChantLifetimeSeconds => chantLifetimeBeats * ResolveSecondsPerBeat();
        public float EmptyCastPenaltySeconds => emptyCastPenaltyBeats * ResolveSecondsPerBeat();

        private void Awake()
        {
            CacheRefs();
        }

        private void Update()
        {
            if (beatClock == null || !beatClock.IsRunning)
            {
                return;
            }

            EvaluateLifecycle(AudioSettings.dspTime);
        }

        public void ProcessLiveInput(BeatInputType type)
        {
            if (beatClock == null || !beatClock.IsRunning)
            {
                Debug.LogWarning("[Beat] 时钟尚未启动，忽略输入。");
                return;
            }

            ProcessInput(type, beatClock.GetTimingAt(AudioSettings.dspTime));
        }

        public BeatTimingGrade SimulateInputAtOffsetMs(BeatInputType type, float offsetMs)
        {
            if (beatClock == null)
            {
                return BeatTimingGrade.Miss;
            }

            if (!beatClock.IsRunning)
            {
                beatClock.StartClock();
            }

            return ProcessInput(type, beatClock.GetSimulatedTiming(offsetMs));
        }

        public BeatTimingGrade SimulateStep(BeatInputType type, int beatIndex, float offsetMs)
        {
            if (beatClock == null)
            {
                return BeatTimingGrade.Miss;
            }

            if (!beatClock.IsRunning)
            {
                beatClock.StartClock();
            }

            return ProcessInput(type, beatClock.GetTimingForBeatIndex(beatIndex, offsetMs));
        }

        /// <summary>
        /// MCP 小步 1：采样连续拍位置，不产生输入。
        /// </summary>
        public BeatTimingInfo SampleTimingForBeatFloat(float beatFloat)
        {
            if (beatClock == null)
            {
                return default;
            }

            if (!beatClock.IsRunning)
            {
                beatClock.StartClock();
            }

            return beatClock.GetTimingForBeatFloat(beatFloat);
        }

        [Serializable]
        public struct SimulatedSequenceStep
        {
            public BeatInputType type;
            public int beatIndex;
            public float offsetMs;
        }

        public void SimulateSequence(SimulatedSequenceStep[] steps)
        {
            if (steps == null)
            {
                return;
            }

            for (int i = 0; i < steps.Length; i++)
            {
                SimulateStep(steps[i].type, steps[i].beatIndex, steps[i].offsetMs);
            }
        }

        public void ResetCombat()
        {
            blockedUntilDspTime = 0d;
            queryDspTime = 0d;

            if (spellInputBuffer != null)
            {
                spellInputBuffer.Clear();
            }

            if (markerView != null)
            {
                markerView.ClearMarkers();
            }

            if (spellCaster != null)
            {
                spellCaster.ResetLastResult();
            }

            if (battleGameManager != null)
            {
                battleGameManager.ResetBattle();
            }
        }

        /// <summary>
        /// MCP：把生命周期推进到指定拍，不产生新输入。过期时打 [Spell] ChantExpired。
        /// </summary>
        public bool EvaluateLifecycleAtBeat(int beatIndex, float offsetMs)
        {
            if (beatClock == null)
            {
                return false;
            }

            if (!beatClock.IsRunning)
            {
                beatClock.StartClock();
            }

            BeatTimingInfo timing = beatClock.GetTimingForBeatIndex(beatIndex, offsetMs);
            return EvaluateLifecycle(timing.DspTime);
        }

        public void SetGoodWindowMs(float goodWindowMs)
        {
            rhythmJudge.GoodWindowMs = goodWindowMs;
        }

        public void SetPerfectWindowMs(float perfectWindowMs)
        {
            rhythmJudge.PerfectWindowMs = perfectWindowMs;
        }

        private BeatTimingGrade ProcessInput(BeatInputType type, BeatTimingInfo timing)
        {
            EvaluateLifecycle(timing.DspTime);

            if (timing.DspTime < blockedUntilDspTime)
            {
                spellCaster?.LogInputRejected();
                return BeatTimingGrade.Miss;
            }

            if (type == BeatInputType.Cast && (spellInputBuffer == null || !spellInputBuffer.HasActiveChantLifecycle))
            {
                blockedUntilDspTime = timing.DspTime + EmptyCastPenaltySeconds;
                queryDspTime = timing.DspTime;
                spellCaster?.LogEmptyCastPenalty();
                return BeatTimingGrade.Miss;
            }

            BeatTimingGrade grade = rhythmJudge.Evaluate(timing.DeltaMs);
            string sign = timing.DeltaMs >= 0f ? "+" : string.Empty;
            Debug.Log(
                $"[Beat] {type} | {grade} | delta={sign}{timing.DeltaMs:0}ms | beat={timing.BeatIndex} | vertex={timing.VertexIndex}({BeatTimingInfo.VertexName(timing.VertexIndex)})");

            if (inputController != null)
            {
                inputController.RaiseRhythmInput(type, grade, timing.DeltaMs, timing.BeatIndex);
            }

            if (spellCaster != null)
            {
                spellInputBuffer.TryAppend(type, grade, timing, spellCatalog, spellCaster);
            }

            return grade;
        }

        private bool EvaluateLifecycle(double dspNow)
        {
            queryDspTime = dspNow;
            bool expired = spellInputBuffer != null &&
                           spellInputBuffer.ClearIfExpired(dspNow, ChantLifetimeSeconds);
            if (expired)
            {
                spellCaster?.LogChantExpired();
            }

            if (markerView != null)
            {
                markerView.ExpireIfNeeded(dspNow, ChantLifetimeSeconds);
            }

            return expired;
        }

        private float ResolveSecondsPerBeat()
        {
            return beatClock != null ? beatClock.SecondsPerBeat : 0.5f;
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

            if (spellCaster == null)
            {
                spellCaster = GetComponent<SpellCaster>();
            }

            if (battleGameManager == null)
            {
                battleGameManager = GetComponent<BattleGameManager>();
            }

            if (markerView == null)
            {
                markerView = FindFirstObjectByType<BeatInputMarkerView>();
            }

            if (beatClock != null)
            {
                DiamondBeatTrackView trackView = FindFirstObjectByType<DiamondBeatTrackView>();
                if (trackView != null)
                {
                    trackView.BindClock(beatClock);
                }
            }
        }
    }
}
