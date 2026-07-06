using System;
using UnityEngine;

namespace NewFPG.Combat
{
    [Serializable]
    public sealed class CombatVitalsSettings
    {
        [Min(1f)] public float maxHealth = 100f;
        [Min(0f)] public float startingHealth = 100f;
        [Min(0f)] public float maxShield = 50f;
        [Min(0f)] public float startingShield;
        public bool destroyOnDeath;
        [Min(0f)] public float deathDelay = 0.2f;
        public string hitTriggerParameter = "Hit";
        public Color hitTint = new Color(1f, 0.65f, 0.55f, 1f);
        [Min(0.02f)] public float hitTintSeconds = 0.12f;

        public CombatVitalsSettings Clone()
        {
            return new CombatVitalsSettings
            {
                maxHealth = maxHealth,
                startingHealth = startingHealth,
                maxShield = maxShield,
                startingShield = startingShield,
                destroyOnDeath = destroyOnDeath,
                deathDelay = deathDelay,
                hitTriggerParameter = hitTriggerParameter,
                hitTint = hitTint,
                hitTintSeconds = hitTintSeconds,
            };
        }

        public void Normalize()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            startingHealth = Mathf.Clamp(startingHealth <= 0f ? maxHealth : startingHealth, 1f, maxHealth);
            maxShield = Mathf.Max(0f, maxShield);
            startingShield = Mathf.Clamp(startingShield, 0f, maxShield);
            deathDelay = Mathf.Max(0f, deathDelay);
            hitTintSeconds = Mathf.Max(0.02f, hitTintSeconds);
        }
    }
}
