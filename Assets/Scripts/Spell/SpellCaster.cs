using System;
using UnityEngine;

namespace BeatSpell.Spell
{
    /// <summary>
    /// 第 2 轮：Console 释放日志。第 3 轮可订阅 OnSpellCast 扣血。
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        public event Action<SpellDefinition, float, float> OnSpellCast;

        public SpellDefinition LastMatchedSpell { get; private set; }
        public float LastDamageFactor { get; private set; }
        public string LastSpellLog { get; private set; }

        public void ResetLastResult()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = string.Empty;
        }

        public void LogChantBroken()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = "ChantBroken";
            Debug.Log("[Spell] ChantBroken | sequence cleared");
        }

        public void LogSequenceInvalid(string reason)
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = "SequenceInvalid";
            Debug.Log($"[Spell] SequenceInvalid | {reason}");
        }

        public void LogMatchFailed()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = "MatchFailed";
            Debug.Log("[Spell] MatchFailed | sequence cleared");
        }

        public void LogMatched(SpellDefinition spell, float damageFactor)
        {
            float damage = spell.BaseDamage * damageFactor;
            LastMatchedSpell = spell;
            LastDamageFactor = damageFactor;
            LastSpellLog = "Matched";
            Debug.Log(
                $"[Spell] Matched {spell.SpellId} | {spell.DisplayName} Lv.{spell.Level} | damageFactor={damageFactor:0.##} | damage={damage:0.##}");
            OnSpellCast?.Invoke(spell, damageFactor, damage);
        }
    }
}
