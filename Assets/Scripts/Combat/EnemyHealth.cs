using System;
using UnityEngine;

namespace BeatSpell.Combat
{
    /// <summary>
    /// 第 3 轮：敌人生命与扣血。死亡只触发一次，不负责反击。
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 20;
        [SerializeField] private int currentHealth;

        public event Action OnDied;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsDead { get; private set; }

        private void Awake()
        {
            ResetHealth();
        }

        public void TakeDamage(float damage)
        {
            if (IsDead)
            {
                return;
            }

            int amount = Mathf.RoundToInt(damage);
            currentHealth = Mathf.Max(0, currentHealth - amount);
            Debug.Log($"[Combat] DamageTaken | damage={amount} | remaining={currentHealth}");

            if (currentHealth > 0)
            {
                return;
            }

            IsDead = true;
            Debug.Log("[Combat] Dead");
            OnDied?.Invoke();
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            IsDead = false;
        }
    }
}
