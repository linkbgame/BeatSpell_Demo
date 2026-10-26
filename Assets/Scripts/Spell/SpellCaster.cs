using System;
using UnityEngine;

namespace BeatSpell.Spell
{
    /// <summary>
    /// 匹配结果与伤害数字。第 3 轮由 BattleGameManager 订阅 OnSpellCast 扣血。
    /// </summary>
    public class SpellCaster : MonoBehaviour
    {
        public event Action<SpellDefinition, float, float> OnSpellCast;
        public event Action OnVirtualCastFailed;
        public event Action OnMatchFailed;
        public event Action OnEmptyCastPenalty;

        public SpellDefinition LastMatchedSpell { get; private set; }
        public float LastDamageFactor { get; private set; }
        public string LastSpellLog { get; private set; }
        public int VirtualCastFailureCount { get; private set; }

        public void ResetLastResult()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = string.Empty;
            VirtualCastFailureCount = 0;
        }

        public void LogChantBroken()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = "ChantBroken";
            Debug.Log("[Spell] ChantBroken | sequence cleared");
        }

        public void LogChantExpired()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = "ChantExpired";
            Debug.Log("[Spell] ChantExpired | sequence cleared");
        }

        public void LogEmptyCastPenalty()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = "EmptyCastPenalty";
            Debug.Log("[Spell] EmptyCastPenalty | input locked for half beat");
            OnEmptyCastPenalty?.Invoke();
        }

        public void LogInputRejected()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = "InputRejected";
            Debug.Log("[Spell] InputRejected | input locked");
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
            OnMatchFailed?.Invoke();
        }

        public void LogAutoCastFailed()
        {
            LogVirtualCastFailed();
        }

        public void LogVirtualCastFailed()
        {
            LastMatchedSpell = null;
            LastDamageFactor = 0f;
            LastSpellLog = "VirtualCastFailed";
            VirtualCastFailureCount++;
            Debug.Log("[Spell] VirtualCastFailed | chant lifecycle ended");
            OnVirtualCastFailed?.Invoke();
        }

        public void LogMatched(SpellDefinition spell, float damageFactor, string accuracy)
        {
            float damage = spell.BaseDamage * damageFactor;
            LastMatchedSpell = spell;
            LastDamageFactor = damageFactor;
            LastSpellLog = "Matched";
            Debug.Log(
                $"[Spell] Matched {spell.SpellId} | accuracy={accuracy} | damageFactor={damageFactor:0.##} | damage={damage:0.##}");
            OnSpellCast?.Invoke(spell, damageFactor, damage);
        }
    }
}
