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
        [SerializeField] private DiamondBeatTrackView trackView;
        [SerializeField] private float chantLifetimeBeats = 4f;
        [SerializeField] private float emptyCastPenaltyBeats = 0.5f;
        [SerializeField] private float failedCastCooldownBeats = 0.25f;
        [SerializeField] private float failedInputMarkerClearDelayBeats = 0.5f;
        [SerializeField] private float chantRolloverGraceBeats = 0.125f;

        private double blockedUntilDspTime;
        private double queryDspTime;
        private double failedInputMarkersClearDspTime;
        private bool hasFailedInputMarkersPendingClear;
        private float lastVirtualCastFailedBeatFloat;
        private BeatTimingGrade lastPromptGrade = BeatTimingGrade.Weak;
        private float lastPromptAnchorBeatFloat;
        private float lastPromptDeltaMs;

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
        public int QueueMarkerCount => markerView != null ? markerView.QueueMarkerCount : 0;
        public int TransientMarkerCount => markerView != null ? markerView.TransientMarkerCount : 0;
        public string LastMarkerKind => markerView != null ? markerView.LastMarkerKind : string.Empty;
        public bool LastQueueMarkerHasPerfectRing =>
            markerView != null && markerView.LastQueueMarkerHasPerfectRing;
        public int ActivePerfectRingCount => markerView != null ? markerView.ActivePerfectRingCount : 0;
        public bool LastQueueMarkerHasRays => LastQueueMarkerHasPerfectRing;
        public string LastQueueMarkerGrade => markerView != null ? markerView.LastQueueMarkerGrade : string.Empty;
        public string LastQueueMarkerPromptGrade => markerView != null ? markerView.LastQueueMarkerPromptGrade : string.Empty;
        public string LastPromptGrade => lastPromptGrade.ToString();
        public float LastPromptAnchorBeatFloat => lastPromptAnchorBeatFloat;
        public float LastPromptDeltaMs => lastPromptDeltaMs;
        public Vector2 LastPromptAnchorPosition => markerView != null ? markerView.LastPromptAnchorPosition : Vector2.zero;
        public float HeadMarkerBeatFloat => markerView != null ? markerView.HeadMarkerBeatFloat : 0f;
        public float HeadBufferBeatFloat =>
            spellInputBuffer != null ? spellInputBuffer.LifecycleStartBeatFloat : 0f;
        public float ChantLifetimeBeats => chantLifetimeBeats;
        public int ActiveBeatVertex => trackView != null ? trackView.ActiveBeatVertex : VertexIndex;
        public int ActiveBeatPulseCount => trackView != null ? trackView.ActiveBeatPulseCount : 0;
        public string LastSpellLog => spellCaster != null ? spellCaster.LastSpellLog : string.Empty;
        public string LastMatchedSpellId =>
            spellCaster != null && spellCaster.LastMatchedSpell != null
                ? spellCaster.LastMatchedSpell.SpellId
                : string.Empty;
        public float LastDamageFactor => spellCaster != null ? spellCaster.LastDamageFactor : 0f;
        public bool HasFailedInputMarkersPendingClear => hasFailedInputMarkersPendingClear;
        public float FailedCastCooldownSeconds => failedCastCooldownBeats * ResolveSecondsPerBeat();
        public float FailedInputMarkerClearDelaySeconds => failedInputMarkerClearDelayBeats * ResolveSecondsPerBeat();
        public float ChantLifetimeSeconds => chantLifetimeBeats * ResolveSecondsPerBeat();
        public float EmptyCastPenaltySeconds => emptyCastPenaltyBeats * ResolveSecondsPerBeat();
        public float ChantRolloverGraceBeats => chantRolloverGraceBeats;
        public float LastVirtualCastFailedBeatFloat => lastVirtualCastFailedBeatFloat;
        public int VirtualCastFailureCount => spellCaster != null ? spellCaster.VirtualCastFailureCount : 0;
        public int CurrentBeatAnimFrame => trackView != null ? trackView.CurrentBeatAnimFrame : 0;
        public float CurrentBeatAnimScale => trackView != null ? trackView.CurrentBeatAnimScale : 1f;
        public float CurrentBeatAnimWhiteAmount => trackView != null ? trackView.CurrentBeatAnimWhiteAmount : 0f;
        public string CurrentBeatAnimFill => trackView != null ? trackView.CurrentBeatAnimFill : "Idle";
        public bool IsCurrentBeatAnimPlaying => trackView != null && trackView.IsCurrentBeatAnimPlaying;
        public string LastMarkerAnimKind => markerView != null ? markerView.LastMarkerAnimKind : string.Empty;
        public string LastCastAnimVariant => markerView != null ? markerView.LastCastAnimVariant : string.Empty;
        public string LastMarkerAnimScaleSequence =>
            markerView != null ? markerView.LastMarkerAnimScaleSequence : string.Empty;
        public int LastMarkerAnimFrame => markerView != null ? markerView.LastMarkerAnimFrame : 0;
        public float LastMarkerAnimScale => markerView != null ? markerView.LastMarkerAnimScale : 1f;
        public bool IsLastMarkerAnimPlaying => markerView != null && markerView.IsLastMarkerAnimPlaying;

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

            EvaluateLifecycle(beatClock.GetTimingAt(AudioSettings.dspTime));
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
        /// MCP 小步 3：按连续拍位置输入，用于 0.5 / 0.25 拍配方验收。
        /// </summary>
        public BeatTimingGrade SimulateStepAtBeatFloat(BeatInputType type, float beatFloat)
        {
            if (beatClock == null)
            {
                return BeatTimingGrade.Miss;
            }

            if (!beatClock.IsRunning)
            {
                beatClock.StartClock();
            }

            return ProcessInput(type, beatClock.GetTimingForBeatFloat(beatFloat));
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
            CacheRefs();
            blockedUntilDspTime = 0d;
            queryDspTime = 0d;
            failedInputMarkersClearDspTime = 0d;
            hasFailedInputMarkersPendingClear = false;
            lastVirtualCastFailedBeatFloat = 0f;
            lastPromptGrade = BeatTimingGrade.Weak;
            lastPromptAnchorBeatFloat = 0f;
            lastPromptDeltaMs = 0f;

            if (spellInputBuffer != null)
            {
                spellInputBuffer.Clear();
            }

            if (markerView != null)
            {
                markerView.ClearMarkers();
                markerView.ClearTransientMarkers();
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

            return EvaluateLifecycle(beatClock.GetTimingForBeatIndex(beatIndex, offsetMs));
        }

        /// <summary>
        /// MCP 小步 5：按连续拍位置推进队首生命周期，用于 4.24 / 4.25 边界。
        /// </summary>
        public bool EvaluateLifecycleAtBeatFloat(float beatFloat)
        {
            if (beatClock == null)
            {
                return false;
            }

            if (!beatClock.IsRunning)
            {
                beatClock.StartClock();
            }

            return EvaluateLifecycle(beatClock.GetTimingForBeatFloat(beatFloat));
        }

        public void SetGoodWindowMs(float goodWindowMs)
        {
            rhythmJudge.GoodWindowMs = goodWindowMs;
        }

        public void SetPerfectWindowMs(float perfectWindowMs)
        {
            rhythmJudge.PerfectWindowMs = perfectWindowMs;
        }

        /// <summary>
        /// MCP：按秒推进当前拍与 Marker 帧动画，不改判定时间轴。
        /// </summary>
        public void TickBeatUiAnimations(float seconds)
        {
            trackView?.TickCurrentBeatAnimation(seconds);
            markerView?.TickMarkerAnimations(seconds);
        }

        public void AdvanceCurrentBeatAnimFrames(int frames)
        {
            trackView?.AdvanceCurrentBeatAnimFrames(frames);
        }

        public void ReplayCurrentBeatAnimation()
        {
            trackView?.ReplayCurrentBeatAnimation();
        }

        public void AdvanceLastMarkerAnimFrames(int frames)
        {
            markerView?.AdvanceLastMarkerAnimFrames(frames);
        }

        public void SetNextCastAnimVariant(string variant)
        {
            markerView?.SetNextCastAnimVariant(variant);
        }

        private BeatTimingGrade ProcessInput(BeatInputType type, BeatTimingInfo timing)
        {
            EvaluateLifecycle(timing, type);

            bool hadFailureResidue = hasFailedInputMarkersPendingClear;
            bool castReplacesFailureResidue = type == BeatInputType.Cast && hadFailureResidue;
            if (hadFailureResidue && (type == BeatInputType.Chant || type == BeatInputType.Cast))
            {
                ClearFailureResidue(type == BeatInputType.Cast ? "NextCast" : "NextChant");
            }

            if (timing.DspTime < blockedUntilDspTime && !castReplacesFailureResidue)
            {
                spellCaster?.LogInputRejected();
                return BeatTimingGrade.Miss;
            }

            if (type == BeatInputType.Cast && (spellInputBuffer == null || !spellInputBuffer.HasActiveChantLifecycle))
            {
                BeatTimingGrade emptyCastGrade = rhythmJudge.Evaluate(timing.DeltaMs);
                LogBeatInput(type, emptyCastGrade, timing);
                if (inputController != null)
                {
                    inputController.RaiseRhythmInput(type, emptyCastGrade, timing.DeltaMs, timing.BeatIndex);
                }

                markerView?.ShowTransientCastMarker(timing, FailedInputMarkerClearDelaySeconds);
                StartFailureCooldownAndMarkerClear(timing.DspTime);
                queryDspTime = timing.DspTime;
                spellCaster?.LogEmptyCastPenalty();
                return emptyCastGrade;
            }

            BeatTimingGrade grade = rhythmJudge.Evaluate(timing.DeltaMs);
            BeatTimingGrade promptGrade = EvaluatePromptGrade(timing, out float promptAnchorBeatFloat);
            LogBeatInput(type, grade, timing);

            if (inputController != null)
            {
                inputController.RaiseRhythmInput(type, grade, timing.DeltaMs, timing.BeatIndex);
            }

            if (spellCaster != null)
            {
                SpellBufferResult result = spellInputBuffer.TryAppend(
                    type,
                    grade,
                    promptGrade,
                    promptAnchorBeatFloat,
                    timing,
                    spellCatalog,
                    spellCaster);
                if (result == SpellBufferResult.MatchFailed)
                {
                    StartFailureCooldownAndMarkerClear(timing.DspTime);
                }
            }

            return grade;
        }

        private void LogBeatInput(BeatInputType type, BeatTimingGrade grade, BeatTimingInfo timing)
        {
            string sign = timing.DeltaMs >= 0f ? "+" : string.Empty;
            Debug.Log(
                $"[Beat] {type} | {grade} | delta={sign}{timing.DeltaMs:0}ms | beat={timing.BeatIndex} | beatFloat={timing.BeatFloat:0.###} | vertex={timing.VertexIndex}({BeatTimingInfo.VertexName(timing.VertexIndex)})");
        }

        private BeatTimingGrade EvaluatePromptGrade(BeatTimingInfo timing, out float promptAnchorBeatFloat)
        {
            BeatTimingGrade promptGrade = rhythmJudge.EvaluateHalfBeatPrompt(
                timing.BeatFloat,
                ResolveSecondsPerBeat(),
                out promptAnchorBeatFloat,
                out lastPromptDeltaMs);

            lastPromptGrade = promptGrade;
            lastPromptAnchorBeatFloat = promptAnchorBeatFloat;
            Debug.Log(
                $"[Track] Prompt {promptGrade} | beatFloat={timing.BeatFloat:0.###} | anchorBeatFloat={promptAnchorBeatFloat:0.###} | delta={lastPromptDeltaMs:+0;-0;0}ms");
            return promptGrade;
        }

        private bool EvaluateLifecycle(BeatTimingInfo timing, BeatInputType? incomingType = null)
        {
            queryDspTime = timing.DspTime;
            ExpireFailureInputMarkersIfNeeded(timing.DspTime);

            bool expired = spellInputBuffer != null &&
                           spellInputBuffer.HasActiveChantLifecycle &&
                           chantLifetimeBeats > 0f &&
                           timing.BeatFloat >= spellInputBuffer.LifecycleStartBeatFloat + chantLifetimeBeats;
            if (expired)
            {
                TriggerVirtualCastFailed(timing, incomingType.HasValue ? incomingType.Value.ToString() : "Passive");
            }

            if (markerView != null)
            {
                markerView.ExpireIfNeeded(timing.BeatFloat);
                markerView.ExpireTransientIfNeeded(timing.DspTime);
            }

            if (trackView != null)
            {
                trackView.SetBeatVertex(timing.VertexIndex);
            }

            return expired;
        }

        private void StartFailureCooldownAndMarkerClear(double dspTime)
        {
            StartFailureCooldown(dspTime);
            hasFailedInputMarkersPendingClear = true;
            failedInputMarkersClearDspTime = dspTime + FailedInputMarkerClearDelaySeconds;
            if (FailedInputMarkerClearDelaySeconds <= 0f)
            {
                ClearFailedInputMarkers("Immediate");
                return;
            }

            Debug.Log($"[Track] FailedInputMarkersPendingClear life={FailedInputMarkerClearDelaySeconds:0.###}s");
        }

        private void TriggerVirtualCastFailed(BeatTimingInfo timing, string reason)
        {
            spellInputBuffer?.Clear(notify: false);
            markerView?.ClearAllInputMarkers();
            lastVirtualCastFailedBeatFloat = timing.BeatFloat;
            hasFailedInputMarkersPendingClear = true;
            failedInputMarkersClearDspTime = timing.DspTime + FailedInputMarkerClearDelaySeconds;
            spellCaster?.LogVirtualCastFailed();
            Debug.Log($"[Spell] VirtualCastFailedReason | reason={reason} | beatFloat={timing.BeatFloat:0.###}");
        }

        private void StartFailureCooldown(double dspTime)
        {
            blockedUntilDspTime = dspTime + FailedCastCooldownSeconds;
        }

        private void ExpireFailureInputMarkersIfNeeded(double dspTime)
        {
            if (!hasFailedInputMarkersPendingClear || dspTime < failedInputMarkersClearDspTime)
            {
                return;
            }

            ClearFailedInputMarkers("DelayElapsed");
        }

        private void ClearFailedInputMarkers(string reason)
        {
            ClearFailureResidue(reason);
        }

        private void ClearFailureResidue(string reason)
        {
            spellInputBuffer?.Clear(notify: false);
            markerView?.ClearAllInputMarkers();
            hasFailedInputMarkersPendingClear = false;
            failedInputMarkersClearDspTime = 0d;
            Debug.Log($"[Track] FailedInputMarkersCleared | reason={reason}");
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

            if (trackView == null)
            {
                trackView = FindFirstObjectByType<DiamondBeatTrackView>();
            }

            if (markerView != null)
            {
                markerView.Bind(this);
            }

            if (beatClock != null && trackView != null)
            {
                trackView.BindClock(beatClock);
            }
        }
    }
}
