using System.Collections.Generic;
using BeatSpell.Rhythm;

namespace BeatSpell.Spell
{
    /// <summary>
    /// 匹配输入类型序列 + 相邻拍间隔（精确整数）。第 2 轮不强制顶点。
    /// </summary>
    public static class SpellMatcher
    {
        public static bool TryMatch(IReadOnlyList<RhythmInputEntry> buffer, SpellDefinition spell, out float damageFactor)
        {
            damageFactor = 0f;
            if (spell == null || buffer == null)
            {
                return false;
            }

            SpellStepDefinition[] steps = spell.Steps;
            if (steps == null || buffer.Count != steps.Length)
            {
                return false;
            }

            int[] intervals = spell.IntervalsBetweenSteps;
            if (intervals == null || intervals.Length != steps.Length - 1)
            {
                return false;
            }

            for (int i = 0; i < steps.Length; i++)
            {
                RhythmInputEntry entry = buffer[i];
                if (entry.InputType != steps[i].inputType)
                {
                    return false;
                }

                if (i > 0 && entry.IntervalBeatsFromPrevious != intervals[i - 1])
                {
                    return false;
                }
            }

            damageFactor = ComputeDamageFactor(buffer);
            return true;
        }

        public static bool TryMatchAny(
            IReadOnlyList<RhythmInputEntry> buffer,
            IReadOnlyList<SpellDefinition> spells,
            out SpellDefinition matched,
            out float damageFactor)
        {
            matched = null;
            damageFactor = 0f;
            if (spells == null)
            {
                return false;
            }

            for (int i = 0; i < spells.Count; i++)
            {
                SpellDefinition spell = spells[i];
                if (spell != null && TryMatch(buffer, spell, out damageFactor))
                {
                    matched = spell;
                    return true;
                }
            }

            return false;
        }

        private static float ComputeDamageFactor(IReadOnlyList<RhythmInputEntry> buffer)
        {
            bool anyGood = false;
            for (int i = 0; i < buffer.Count; i++)
            {
                switch (buffer[i].Grade)
                {
                    case BeatTimingGrade.Miss:
                        return 0f;
                    case BeatTimingGrade.Good:
                        anyGood = true;
                        break;
                }
            }

            return anyGood ? 0.8f : 1f;
        }
    }
}
