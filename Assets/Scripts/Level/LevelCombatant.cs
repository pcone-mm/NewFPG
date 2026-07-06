using System;
using UnityEngine;
using NewFPG.Combat;

namespace NewFPG.Level
{
    [RequireComponent(typeof(CombatVitals))]
    [DisallowMultipleComponent]
    public sealed class LevelCombatant : MonoBehaviour
    {
        [SerializeField] private CombatVitals combatVitals;

        public event Action<LevelCombatant> Died;

        public float Hp => combatVitals != null ? combatVitals.CurrentHealth : 0f;
        public float MaxHp => combatVitals != null ? combatVitals.MaxHealth : 0f;
        public bool IsDead => combatVitals == null || !combatVitals.IsAlive;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            if (combatVitals != null)
            {
                combatVitals.Died += OnCombatVitalsDied;
            }
        }

        private void OnDisable()
        {
            if (combatVitals != null)
            {
                combatVitals.Died -= OnCombatVitalsDied;
            }
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        public void ResetHp(float nextMaxHp)
        {
            CacheReferences();
            if (combatVitals != null)
            {
                combatVitals.SetMaxHealth(Mathf.Max(1f, nextMaxHp), true);
            }
        }

        private void CacheReferences()
        {
            if (combatVitals == null)
            {
                combatVitals = GetComponent<CombatVitals>();
            }
        }

        private void OnCombatVitalsDied(CombatVitals vitals)
        {
            Died?.Invoke(this);
        }
    }
}
