using BeatSpell.Spell;
using UnityEngine;

namespace BeatSpell.Combat
{
    /// <summary>
    /// 第 3 轮：把施法伤害转到敌人，死亡时记一次胜利。
    /// </summary>
    public class BattleGameManager : MonoBehaviour
    {
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private EnemyHealth enemyHealth;

        public bool IsVictory { get; private set; }
        public int EnemyCurrentHealth => enemyHealth != null ? enemyHealth.CurrentHealth : 0;
        public bool IsEnemyDead => enemyHealth != null && enemyHealth.IsDead;

        private void Awake()
        {
            CacheRefs();
            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        public void ResetBattle()
        {
            IsVictory = false;
            if (enemyHealth != null)
            {
                enemyHealth.ResetHealth();
            }
        }

        private void HandleSpellCast(SpellDefinition spell, float damageFactor, float damage)
        {
            if (enemyHealth == null)
            {
                Debug.LogWarning("[Combat] Ignored | enemyHealth missing");
                return;
            }

            if (enemyHealth.IsDead)
            {
                Debug.Log("[Combat] Ignored | already dead");
                return;
            }

            enemyHealth.TakeDamage(damage);
        }

        private void OnEnemyDied()
        {
            if (IsVictory)
            {
                return;
            }

            IsVictory = true;
            Debug.Log("[Battle] Victory");
        }

        private void Subscribe()
        {
            if (spellCaster != null)
            {
                spellCaster.OnSpellCast += HandleSpellCast;
            }

            if (enemyHealth != null)
            {
                enemyHealth.OnDied += OnEnemyDied;
            }
        }

        private void Unsubscribe()
        {
            if (spellCaster != null)
            {
                spellCaster.OnSpellCast -= HandleSpellCast;
            }

            if (enemyHealth != null)
            {
                enemyHealth.OnDied -= OnEnemyDied;
            }
        }

        private void CacheRefs()
        {
            if (spellCaster == null)
            {
                spellCaster = GetComponent<SpellCaster>();
            }

            if (enemyHealth == null)
            {
                enemyHealth = GetComponentInChildren<EnemyHealth>(true);
            }
        }
    }
}
