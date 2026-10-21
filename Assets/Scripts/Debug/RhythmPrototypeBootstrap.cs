using System;
using BeatSpell.Combat;
using BeatSpell.Rhythm;
using BeatSpell.RhythmInput;
using BeatSpell.Spell;
using UnityEngine;

namespace BeatSpell.Prototype
{
    /// <summary>
    /// 场景入口：节拍判定 + 咏唱缓冲 + 火焰魔法匹配 + 第 3 轮战斗结算查询。
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

        public BeatClock Clock => beatClock;
        public RhythmJudge Judge => rhythmJudge;
        public SpellInputBuffer InputBuffer => spellInputBuffer;
        public SpellCaster Caster => spellCaster;
        public BattleGameManager Battle => battleGameManager;
        public int BeatIndex => beatClock != null ? beatClock.BeatIndex : 0;
        public int VertexIndex => beatClock != null ? beatClock.VertexIndex : 0;
        public bool IsClockRunning => beatClock != null && beatClock.IsRunning;
        public int EnemyCurrentHealth => battleGameManager != null ? battleGameManager.EnemyCurrentHealth : 0;
        public bool IsEnemyDead => battleGameManager != null && battleGameManager.IsEnemyDead;
        public bool IsVictory => battleGameManager != null && battleGameManager.IsVictory;

        private void Awake()
        {
            CacheRefs();
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
            if (spellInputBuffer != null)
            {
                spellInputBuffer.Clear();
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
        }
    }
}
