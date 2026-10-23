using System.Collections.Generic;
using BeatSpell.Rhythm;
using UnityEngine;

namespace BeatSpell.Spell
{
    /// <summary>
    /// 匹配输入类型序列 + 相邻小数拍间隔。间隔差值不超过 matchToleranceBeats 即成功。
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

            float[] intervals = spell.IntervalsBetweenSteps;
            if (intervals == null || intervals.Length != steps.Length - 1)
            {
                return false;
            }

            float tolerance = Mathf.Max(0f, spell.MatchToleranceBeats);
            for (int i = 0; i < steps.Length; i++)
            {
                RhythmInputEntry entry = buffer[i];
                if (entry.InputType != steps[i].inputType)
                {
                    return false;
                }

                if (i > 0 && Mathf.Abs(entry.IntervalBeatsFromPrevious - intervals[i - 1]) > tolerance)
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

        /// <summary>
        /// 匹配成功后的准确度标签：全 Perfect 为 Perfect，否则 Mixed（含 Good / Weak）。
        /// </summary>
        public static string GetMatchAccuracyLabel(IReadOnlyList<RhythmInputEntry> buffer)
        {
            if (buffer == null || buffer.Count == 0)
            {
                return "Mixed";
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i].Grade != BeatTimingGrade.Perfect)
                {
                    return "Mixed";
                }
            }

            return "Perfect";
        }

        private static float ComputeDamageFactor(IReadOnlyList<RhythmInputEntry> buffer)
        {
            return GetMatchAccuracyLabel(buffer) == "Perfect" ? 1f : 0.8f;
        }
    }
}
