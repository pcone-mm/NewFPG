using NewFPG.Combat;
using UnityEngine;

namespace NewFPG.Monsters
{
    public static class MonsterDefinitionApplier
    {
        public static bool Apply(GameObject target, MonsterDefinition definition, bool resetVitals)
        {
            if (target == null || definition == null)
            {
                return false;
            }

            definition.Normalize();

            CombatVitals vitals = target.GetComponent<CombatVitals>();
            if (vitals != null)
            {
                vitals.ApplySettings(ToCombatVitalsSettings(definition.vitals), resetVitals);
            }

            MonsterConfigBinding binding = target.GetComponent<MonsterConfigBinding>();
            if (binding != null)
            {
                binding.ApplyDefinition(definition);
            }
            else
            {
                MonsterSkillController skills = target.GetComponent<MonsterSkillController>();
                if (skills != null)
                {
                    skills.ApplyDefinition(definition);
                }

                MonsterAttackController attack = target.GetComponent<MonsterAttackController>();
                if (attack != null)
                {
                    attack.ApplyDefinition(definition.attack);
                }
            }

            return true;
        }

        private static CombatVitalsSettings ToCombatVitalsSettings(MonsterVitalsDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            definition.Normalize();
            return new CombatVitalsSettings
            {
                maxHealth = definition.maxHealth,
                startingHealth = definition.startingHealth,
                maxShield = definition.maxShield,
                startingShield = definition.startingShield,
                destroyOnDeath = definition.destroyOnDeath,
                deathDelay = definition.deathDelay,
                hitTriggerParameter = definition.hitTriggerParameter,
                hitTint = definition.hitTint,
                hitTintSeconds = definition.hitTintSeconds,
            };
        }
    }
}
