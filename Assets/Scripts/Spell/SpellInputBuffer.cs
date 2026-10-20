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
    /// 咏唱序列缓冲：Miss 打断；多余 Chant 视为非法；Cast 时匹配。
    /// </summary>
    [Serializable]
    public class SpellInputBuffer
    {
        private readonly List<RhythmInputEntry> entries = new List<RhythmInputEntry>(8);

        public IReadOnlyList<RhythmInputEntry> Entries => entries;

        public void Clear()
        {
            entries.Clear();
        }

        public SpellBufferResult TryAppend(
            BeatInputType type,
            BeatTimingGrade grade,
            BeatTimingInfo timing,
            IReadOnlyList<SpellDefinition> spellCatalog,
            SpellCaster caster)
        {
            if (grade == BeatTimingGrade.Miss)
            {
                Clear();
                caster?.LogChantBroken();
                return SpellBufferResult.ChantBroken;
            }

            int intervalBeats = 0;
            if (entries.Count > 0)
            {
                intervalBeats = timing.BeatIndex - entries[entries.Count - 1].BeatIndex;
            }

            if (type == BeatInputType.Chant && entries.Count > 0)
            {
                Clear();
                caster?.LogSequenceInvalid("extra Chant before Cast");
                return SpellBufferResult.SequenceInvalid;
            }

            entries.Add(new RhythmInputEntry(
                type,
                grade,
                timing.BeatIndex,
                timing.VertexIndex,
                timing.DeltaMs,
                intervalBeats));

            if (type != BeatInputType.Cast)
            {
                return SpellBufferResult.Added;
            }

            if (SpellMatcher.TryMatchAny(entries, spellCatalog, out SpellDefinition matched, out float damageFactor))
            {
                caster?.LogMatched(matched, damageFactor);
                Clear();
                return SpellBufferResult.Matched;
            }

            Clear();
            caster?.LogMatchFailed();
            return SpellBufferResult.MatchFailed;
        }
    }
}
