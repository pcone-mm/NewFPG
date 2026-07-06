using System.Collections;
using NewFPG.Combat;
using UnityEngine;

namespace NewFPG.Monsters
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class MonsterMechanicRunner : MonoBehaviour
    {
        [SerializeField] private MonsterState state;

        private readonly DamageAreaMechanic damageArea = new DamageAreaMechanic();
        private readonly InvincibleMechanic invincible = new InvincibleMechanic();
        private readonly InvisibleMechanic invisible = new InvisibleMechanic();
        private readonly ScaleModifierMechanic scaleModifier = new ScaleModifierMechanic();
        private readonly SpeedModifierMechanic speedModifier = new SpeedModifierMechanic();

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        public Coroutine Run(MonsterMechanicDefinition mechanic, Transform target, IDamageable lockedTarget)
        {
            if (mechanic == null)
            {
                return null;
            }

            return StartCoroutine(RunRoutine(mechanic, target, lockedTarget));
        }

        public void ExecuteNow(MonsterMechanicDefinition mechanic, Transform target, IDamageable lockedTarget)
        {
            if (mechanic == null)
            {
                return;
            }

            mechanic.Normalize(null);
            IMonsterMechanic executable = ResolveMechanic(mechanic);
            if (executable == null)
            {
                Debug.LogWarning($"Unknown monster mechanic type '{mechanic.type}' on {name}.", this);
                return;
            }

            executable.Execute(new MonsterMechanicContext(gameObject, target, lockedTarget, state), mechanic);
        }

        private IEnumerator RunRoutine(MonsterMechanicDefinition mechanic, Transform target, IDamageable lockedTarget)
        {
            mechanic.Normalize(null);
            if (mechanic.delay > 0f)
            {
                yield return new WaitForSeconds(mechanic.delay);
            }

            ExecuteNow(mechanic, target, lockedTarget);
        }

        private IMonsterMechanic ResolveMechanic(MonsterMechanicDefinition mechanic)
        {
            switch (MonsterMechanicTypes.Parse(mechanic.type))
            {
                case MonsterMechanicKind.DamageArea:
                    return damageArea;
                case MonsterMechanicKind.Invincible:
                    return invincible;
                case MonsterMechanicKind.Invisible:
                    return invisible;
                case MonsterMechanicKind.ScaleModifier:
                    return scaleModifier;
                case MonsterMechanicKind.SpeedModifier:
                    return speedModifier;
                default:
                    return null;
            }
        }

        private void CacheReferences()
        {
            if (state == null)
            {
                state = GetComponent<MonsterState>();
            }
        }
    }
}
