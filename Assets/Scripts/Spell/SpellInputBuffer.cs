using System;
using System.Collections.Generic;
using BeatSpell.Rhythm;

namespace BeatSpell.Spell
{
    public enum SpellBufferResult
    {
        Added,
        ChantBroken,
        SequenceInvalid,
        Matched,
        MatchFailed
    }

    /// <summary>
    /// 咏唱序列缓冲：任意准确度均可入队；首个输入开启一圈生命周期；Cast 时匹配。
    /// 允许多段 Chant，由 SpellMatcher 按配方长度与间隔判定。
    /// </summary>
    [Serializable]
    public class SpellInputBuffer
    {
        private readonly List<RhythmInputEntry> entries = new List<RhythmInputEntry>(8);
        private bool hasLifecycle;
        private double lifecycleStartDspTime;
        private float lifecycleStartBeatFloat;

        public IReadOnlyList<RhythmInputEntry> Entries => entries;
        public int Count => entries.Count;
        public bool HasActiveChantLifecycle => hasLifecycle;
        public double LifecycleStartDspTime => lifecycleStartDspTime;
        public float LifecycleStartBeatFloat => lifecycleStartBeatFloat;
        public event Action OnCleared;
        public event Action<RhythmInputEntry> OnEntryAdded;

        public void Clear(bool notify = true)
        {
            entries.Clear();
            hasLifecycle = false;
            lifecycleStartDspTime = 0d;
            lifecycleStartBeatFloat = 0f;
            if (notify)
            {
                OnCleared?.Invoke();
            }
        }

        /// <summary>
        /// 队首 BeatFloat 起算满一圈（默认 4 拍）后清空。由核心节拍数据判断，不依赖 UI。
        /// </summary>
        public bool ClearIfExpired(float currentBeatFloat, float lifetimeBeats)
        {
            if (!hasLifecycle || lifetimeBeats <= 0f)
            {
                return false;
            }

            if (currentBeatFloat < lifecycleStartBeatFloat + lifetimeBeats)
            {
                return false;
            }

            Clear();
            return true;
        }

        public SpellBufferResult TryAppend(
            BeatInputType type,
            BeatTimingGrade grade,
            BeatTimingInfo timing,
            IReadOnlyList<SpellDefinition> spellCatalog,
            SpellCaster caster)
        {
            float intervalBeats = 0f;
            if (entries.Count > 0)
            {
                intervalBeats = timing.BeatFloat - entries[entries.Count - 1].BeatFloat;
            }

            if (!hasLifecycle)
            {
                hasLifecycle = true;
                lifecycleStartDspTime = timing.DspTime;
                lifecycleStartBeatFloat = timing.BeatFloat;
            }

            RhythmInputEntry entry = new RhythmInputEntry(
                type,
                grade,
                timing.BeatIndex,
                timing.VertexIndex,
                timing.DeltaMs,
                intervalBeats,
                timing.DspTime,
                timing.BeatFloat);
            entries.Add(entry);
            OnEntryAdded?.Invoke(entry);

            if (type != BeatInputType.Cast)
            {
                return SpellBufferResult.Added;
            }

            if (SpellMatcher.TryMatchAny(entries, spellCatalog, out SpellDefinition matched, out float damageFactor))
            {
                string accuracy = SpellMatcher.GetMatchAccuracyLabel(entries);
                caster?.LogMatched(matched, damageFactor, accuracy);
                Clear(notify: false);
                return SpellBufferResult.Matched;
            }

            Clear();
            caster?.LogMatchFailed();
            return SpellBufferResult.MatchFailed;
        }
    }
}
