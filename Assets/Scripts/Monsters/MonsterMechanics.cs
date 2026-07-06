using System.Collections.Generic;
using NewFPG.Combat;
using UnityEngine;

namespace NewFPG.Monsters
{
    public interface IMonsterState
    {
        bool IsInvincible { get; }
        bool IsInvisible { get; }
        bool IsTargetable { get; }
        float ScaleMultiplier { get; }
        float SpeedMultiplier { get; }
        void PushInvincible(float seconds);
        void PushInvisible(float seconds);
        void PushScaleMultiplier(float multiplier, float seconds);
        void PushSpeedMultiplier(float multiplier, float seconds);
    }

    public interface IMonsterMechanic
    {
        MonsterMechanicKind Kind { get; }
        void Execute(MonsterMechanicContext context, MonsterMechanicDefinition definition);
    }

    public readonly struct MonsterMechanicContext
    {
        public MonsterMechanicContext(
            GameObject source,
            Transform target,
            IDamageable lockedTarget,
            IMonsterState state)
        {
            Source = source;
            SourceTransform = source != null ? source.transform : null;
            Target = target;
            LockedTarget = lockedTarget;
            State = state;
        }

        public GameObject Source { get; }
        public Transform SourceTransform { get; }
        public Transform Target { get; }
        public IDamageable LockedTarget { get; }
        public IMonsterState State { get; }
    }

    internal static class MonsterDamageRules
    {
        public static bool ShouldRejectTransform(Transform candidate, Transform sourceTransform, bool affectSelf)
        {
            if (candidate == null)
            {
                return false;
            }

            if (!affectSelf && sourceTransform != null && candidate.IsChildOf(sourceTransform))
            {
                return true;
            }

            if (!IsMonsterSource(sourceTransform))
            {
                return false;
            }

            return IsMonsterOwned(candidate);
        }

        public static bool ShouldRejectDamageable(IDamageable damageable, Transform sourceTransform, bool affectSelf)
        {
            if (!(damageable is Component component) || component == null)
            {
                return false;
            }

            return ShouldRejectTransform(component.transform, sourceTransform, affectSelf);
        }

        private static bool IsMonsterSource(Transform sourceTransform)
        {
            return IsMonsterOwned(sourceTransform)
                || sourceTransform != null && sourceTransform.GetComponentInParent<MonsterAttackController>() != null;
        }

        private static bool IsMonsterOwned(Transform candidate)
        {
            return candidate != null
                && (candidate.GetComponentInParent<MonsterConfigBinding>() != null
                    || candidate.GetComponentInParent<MonsterState>() != null);
        }
    }

    public sealed class DamageAreaMechanic : IMonsterMechanic
    {
        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        public MonsterMechanicKind Kind => MonsterMechanicKind.DamageArea;

        public void Execute(MonsterMechanicContext context, MonsterMechanicDefinition definition)
        {
            damagedTargets.Clear();
            Vector3 center = ResolveCenter(context, definition);

            TryDamage(context.LockedTarget, context, definition, center, ResolveSamplePosition(context.LockedTarget, context.Target, center));

            Collider[] hits = Physics.OverlapSphere(center, definition.radius, definition.targetMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                if (MonsterDamageRules.ShouldRejectTransform(hit.transform, context.SourceTransform, definition.affectSelf))
                {
                    continue;
                }

                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                Vector3 samplePosition = ResolveColliderSamplePosition(hit, center);
                TryDamage(damageable, context, definition, center, samplePosition);
            }

            damagedTargets.Clear();
        }

        private bool TryDamage(
            IDamageable damageable,
            MonsterMechanicContext context,
            MonsterMechanicDefinition definition,
            Vector3 center,
            Vector3 samplePosition)
        {
            if (!IsDamageableValid(damageable)
                || MonsterDamageRules.ShouldRejectDamageable(damageable, context.SourceTransform, definition.affectSelf)
                || !damagedTargets.Add(damageable))
            {
                return false;
            }

            Vector3 flatDelta = samplePosition - center;
            flatDelta.y = 0f;
            if (flatDelta.sqrMagnitude > definition.radius * definition.radius)
            {
                return false;
            }

            Vector3 hitPoint = samplePosition;
            hitPoint.y = center.y;
            damageable.ReceiveDamage(new DamagePayload(definition.value, context.Source, hitPoint));
            return true;
        }

        private static Vector3 ResolveCenter(MonsterMechanicContext context, MonsterMechanicDefinition definition)
        {
            Transform source = context.SourceTransform;
            return source != null ? source.position + Vector3.up * definition.heightOffset : Vector3.up * definition.heightOffset;
        }

        private static Vector3 ResolveSamplePosition(IDamageable damageable, Transform fallback, Vector3 fallbackPosition)
        {
            Transform aimTransform = damageable != null && damageable.AimTransform != null ? damageable.AimTransform : fallback;
            return aimTransform != null ? aimTransform.position : fallbackPosition;
        }

        private static Vector3 ResolveColliderSamplePosition(Collider collider, Vector3 center)
        {
            if (collider == null)
            {
                return center;
            }

            if (CanUseColliderClosestPoint(collider))
            {
                return collider.ClosestPoint(center);
            }

            return collider.bounds.ClosestPoint(center);
        }

        private static bool CanUseColliderClosestPoint(Collider collider)
        {
            if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
            {
                return true;
            }

            return collider is MeshCollider meshCollider && meshCollider.convex;
        }

        private static bool IsDamageableValid(IDamageable damageable)
        {
            if (damageable == null)
            {
                return false;
            }

            if (damageable is Object unityObject && unityObject == null)
            {
                return false;
            }

            return damageable.IsAlive && damageable.IsTargetable;
        }
    }

    public sealed class InvincibleMechanic : IMonsterMechanic
    {
        public MonsterMechanicKind Kind => MonsterMechanicKind.Invincible;

        public void Execute(MonsterMechanicContext context, MonsterMechanicDefinition definition)
        {
            context.State?.PushInvincible(definition.duration);
        }
    }

    public sealed class InvisibleMechanic : IMonsterMechanic
    {
        public MonsterMechanicKind Kind => MonsterMechanicKind.Invisible;

        public void Execute(MonsterMechanicContext context, MonsterMechanicDefinition definition)
        {
            context.State?.PushInvisible(definition.duration);
        }
    }

    public sealed class ScaleModifierMechanic : IMonsterMechanic
    {
        public MonsterMechanicKind Kind => MonsterMechanicKind.ScaleModifier;

        public void Execute(MonsterMechanicContext context, MonsterMechanicDefinition definition)
        {
            context.State?.PushScaleMultiplier(definition.value <= 0f ? 1f : definition.value, definition.duration);
        }
    }

    public sealed class SpeedModifierMechanic : IMonsterMechanic
    {
        public MonsterMechanicKind Kind => MonsterMechanicKind.SpeedModifier;

        public void Execute(MonsterMechanicContext context, MonsterMechanicDefinition definition)
        {
            context.State?.PushSpeedMultiplier(definition.value <= 0f ? 1f : definition.value, definition.duration);
        }
    }
}
