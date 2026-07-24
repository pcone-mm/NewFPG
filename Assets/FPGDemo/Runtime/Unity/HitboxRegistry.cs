using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum HitboxTargetReference
    {
        Environment = 0,
        Player,
        Enemy,
        ExplicitDynamic
    }

    [Serializable]
    public sealed class HitboxBinding
    {
        [SerializeField]
        private bool enabled = true;

        [SerializeField]
        private Collider collider;

        [SerializeField]
        private HitboxTargetReference targetReference;

        [NonSerialized]
        private long explicitRuntimeId;

        [SerializeField]
        private QueryTargetKind targetKind;

        [SerializeField]
        private HitPart hitPart;

        [SerializeField]
        private int geometryId;

        [SerializeField]
        private Team team;

        [SerializeField]
        private bool allowTrigger;

        public HitboxBinding(
            Collider collider,
            RuntimeId runtimeId,
            QueryTargetKind targetKind,
            HitPart hitPart,
            GeometryId geometryId,
            Team team,
            bool allowTrigger = false)
        {
            enabled = true;
            this.collider = collider;
            targetReference = targetKind == QueryTargetKind.EnvironmentBlocker
                ? HitboxTargetReference.Environment
                : HitboxTargetReference.ExplicitDynamic;
            explicitRuntimeId = runtimeId.Value;
            this.targetKind = targetKind;
            this.hitPart = hitPart;
            this.geometryId = geometryId.Value;
            this.team = team;
            this.allowTrigger = allowTrigger;
        }

        public HitboxBinding(
            Collider collider,
            HitboxTargetReference targetReference,
            QueryTargetKind targetKind,
            HitPart hitPart,
            GeometryId geometryId,
            bool allowTrigger = false)
        {
            enabled = true;
            this.collider = collider;
            this.targetReference = targetReference;
            explicitRuntimeId = 0L;
            this.targetKind = targetKind;
            this.hitPart = hitPart;
            this.geometryId = geometryId.Value;
            team = targetReference == HitboxTargetReference.Player
                ? Team.Player
                : targetReference == HitboxTargetReference.Enemy
                    ? Team.Enemy
                    : Team.Neutral;
            this.allowTrigger = allowTrigger;
        }

        public bool Enabled => enabled;
        public Collider Collider => collider;
        public HitboxTargetReference TargetReference => targetReference;
        public QueryTargetKind TargetKind => targetKind;
        public HitPart HitPart => hitPart;
        public GeometryId GeometryId => new GeometryId(geometryId);
        public Team Team => targetReference == HitboxTargetReference.Player
            ? Team.Player
            : targetReference == HitboxTargetReference.Enemy
                ? Team.Enemy
                : targetReference == HitboxTargetReference.Environment
                    ? Team.Neutral
                    : team;
        public bool AllowTrigger => allowTrigger;

        internal bool TryRebindExplicitDynamic(RuntimeId runtimeId)
        {
            if (targetReference != HitboxTargetReference.ExplicitDynamic || !runtimeId.IsValid)
            {
                return false;
            }

            explicitRuntimeId = runtimeId.Value;
            return true;
        }

        public bool IsDefinitionValid
        {
            get
            {
                if (!enabled
                    || collider == null
                    || !GeometryId.IsValid
                    || !Enum.IsDefined(typeof(HitboxTargetReference), targetReference)
                    || !Enum.IsDefined(typeof(QueryTargetKind), targetKind)
                    || !Enum.IsDefined(typeof(HitPart), hitPart)
                    || targetReference == HitboxTargetReference.ExplicitDynamic
                    && !Enum.IsDefined(typeof(Team), team))
                {
                    return false;
                }

                if (targetKind == QueryTargetKind.EnvironmentBlocker)
                {
                    return targetReference == HitboxTargetReference.Environment
                        && hitPart == HitPart.Body
                        && team == Team.Neutral;
                }

                if (Team == Team.Neutral)
                {
                    return false;
                }

                if (targetReference == HitboxTargetReference.Player
                    && targetKind != QueryTargetKind.Combatant
                    || targetReference == HitboxTargetReference.Enemy
                    && targetKind != QueryTargetKind.Combatant
                    || targetReference == HitboxTargetReference.Environment
                    || targetReference == HitboxTargetReference.ExplicitDynamic
                    && !new RuntimeId(explicitRuntimeId).IsValid)
                {
                    return false;
                }

                return targetKind == QueryTargetKind.Projectile
                    ? hitPart == HitPart.Projectile
                    : hitPart != HitPart.Projectile;
            }
        }

        internal bool TryCreateRegisteredHitbox(
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            out RegisteredHitbox registered)
        {
            registered = default(RegisteredHitbox);
            if (!IsDefinitionValid)
            {
                return false;
            }

            RuntimeId resolvedRuntimeId;
            switch (targetReference)
            {
                case HitboxTargetReference.Environment:
                    resolvedRuntimeId = RuntimeId.Invalid;
                    break;
                case HitboxTargetReference.Player:
                    resolvedRuntimeId = playerRuntimeId;
                    break;
                case HitboxTargetReference.Enemy:
                    resolvedRuntimeId = enemyRuntimeId;
                    break;
                case HitboxTargetReference.ExplicitDynamic:
                    resolvedRuntimeId = new RuntimeId(explicitRuntimeId);
                    break;
                default:
                    return false;
            }

            if (targetReference != HitboxTargetReference.Environment && !resolvedRuntimeId.IsValid)
            {
                return false;
            }

            registered = new RegisteredHitbox(
                collider,
                resolvedRuntimeId,
                targetKind,
                hitPart,
                GeometryId,
                Team,
                allowTrigger);
            return registered.IsValid;
        }
    }

    public readonly struct RegisteredHitbox
    {
        internal RegisteredHitbox(
            Collider collider,
            RuntimeId runtimeId,
            QueryTargetKind targetKind,
            HitPart hitPart,
            GeometryId geometryId,
            Team team,
            bool allowTrigger)
        {
            Collider = collider;
            RuntimeId = runtimeId;
            TargetKind = targetKind;
            HitPart = hitPart;
            GeometryId = geometryId;
            Team = team;
            AllowTrigger = allowTrigger;
        }

        public Collider Collider { get; }
        public RuntimeId RuntimeId { get; }
        public QueryTargetKind TargetKind { get; }
        public HitPart HitPart { get; }
        public GeometryId GeometryId { get; }
        public Team Team { get; }
        public bool AllowTrigger { get; }

        public bool IsValid => Collider != null
            && GeometryId.IsValid
            && (TargetKind == QueryTargetKind.EnvironmentBlocker
                ? !RuntimeId.IsValid && HitPart == HitPart.Body && Team == Team.Neutral
                : RuntimeId.IsValid
                    && Team != Team.Neutral
                    && (TargetKind == QueryTargetKind.Projectile
                        ? HitPart == HitPart.Projectile
                        : HitPart != HitPart.Projectile));
    }

    [DisallowMultipleComponent]
    public sealed class HitboxRegistry : MonoBehaviour
    {
        [SerializeField]
        private HitboxBinding[] staticBindings = Array.Empty<HitboxBinding>();

        private readonly Dictionary<int, RegisteredHitbox> byColliderInstanceId =
            new Dictionary<int, RegisteredHitbox>();
        private readonly Dictionary<int, int> colliderInstanceIdByGeometryId =
            new Dictionary<int, int>();
        private bool initialized;
        private bool staticBindingsRegistered;
        private RuntimeId staticPlayerRuntimeId;
        private RuntimeId staticEnemyRuntimeId;
        private FpgPlayerEntityView boundPlayerEntity;
        private Collider boundPlayerEntityCollider;

        public int Count => byColliderInstanceId.Count;
        public bool IsInitialized => initialized;
        public bool IsReadyForQueries => initialized
            && (StaticBindingCount == 0 || staticBindingsRegistered);
        public bool StaticBindingsRegistered => staticBindingsRegistered;
        public int StaticBindingCount => staticBindings == null ? 0 : staticBindings.Length;
        public FpgPlayerEntityView BoundPlayerEntity => boundPlayerEntity;

        public bool TryValidateStaticBindings(
            UnityAttackQuerySettings settings,
            out string error)
        {
            if (!settings.IsValid)
            {
                error = "Attack query settings are invalid.";
                return false;
            }

            HitboxBinding[] bindings = staticBindings ?? Array.Empty<HitboxBinding>();
            int enabledCount = 0;
            for (int index = 0; index < bindings.Length; index++)
            {
                HitboxBinding binding = bindings[index];
                if (binding == null || !binding.Enabled)
                {
                    continue;
                }

                enabledCount++;
                if (!binding.IsDefinitionValid)
                {
                    error = $"Static hitbox binding {index} is invalid.";
                    return false;
                }

                Collider collider = binding.Collider;
                if (collider.gameObject.scene != gameObject.scene)
                {
                    error = $"Static hitbox binding {index} belongs to another scene.";
                    return false;
                }

                int expectedMask = binding.TargetKind == QueryTargetKind.EnvironmentBlocker
                    ? settings.BlockerLayerMask
                    : settings.HitboxLayerMask;
                int layer = collider.gameObject.layer;
                if (layer < 0 || layer >= 32 || (expectedMask & (1 << layer)) == 0)
                {
                    error = $"Static hitbox binding {index} uses the wrong physics layer.";
                    return false;
                }

                if (collider.isTrigger && !binding.AllowTrigger)
                {
                    error = $"Static hitbox binding {index} is a trigger without explicit permission.";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    HitboxBinding previous = bindings[previousIndex];
                    if (previous == null || !previous.Enabled)
                    {
                        continue;
                    }

                    if (previous.Collider == collider
                        || previous.GeometryId == binding.GeometryId)
                    {
                        error = $"Static hitbox binding {index} duplicates a Collider or GeometryId.";
                        return false;
                    }
                }
            }

            if (enabledCount == 0)
            {
                error = "At least one enabled static binding is required.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryInitialize(out string error)
        {
            if (initialized)
            {
                error = string.Empty;
                return true;
            }

            initialized = true;
            error = string.Empty;
            return true;
        }

        public bool TryRegisterStaticBindings(
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            out string error)
        {
            TryInitialize(out string ignored);
            if (!playerRuntimeId.IsValid || !enemyRuntimeId.IsValid
                || playerRuntimeId == enemyRuntimeId)
            {
                error = "Player and enemy RuntimeIds must be valid and distinct.";
                return false;
            }

            if (staticBindingsRegistered)
            {
                bool sameSession = staticPlayerRuntimeId == playerRuntimeId
                    && staticEnemyRuntimeId == enemyRuntimeId;
                error = sameSession
                    ? string.Empty
                    : "Static bindings are registered for another session; reset the registry before rebinding.";
                return sameSession;
            }

            if (byColliderInstanceId.Count != 0)
            {
                error = "Static bindings must be registered before dynamic bindings.";
                return false;
            }

            HitboxBinding[] bindings = staticBindings ?? Array.Empty<HitboxBinding>();
            for (int index = 0; index < bindings.Length; index++)
            {
                HitboxBinding binding = bindings[index];
                if (binding == null || !binding.Enabled)
                {
                    continue;
                }

                DomainResult registered = RegisterCore(
                    binding,
                    playerRuntimeId,
                    enemyRuntimeId);
                if (!registered.IsSuccess)
                {
                    byColliderInstanceId.Clear();
                    colliderInstanceIdByGeometryId.Clear();
                    error = $"Static hitbox binding {index} is invalid or duplicates a Collider/GeometryId.";
                    return false;
                }
            }

            staticBindingsRegistered = true;
            staticPlayerRuntimeId = playerRuntimeId;
            staticEnemyRuntimeId = enemyRuntimeId;
            error = string.Empty;
            return true;
        }

        public bool ResetForSession(
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            out string error)
        {
            ClearDynamicAndStaticBindings();
            return TryRegisterStaticBindings(playerRuntimeId, enemyRuntimeId, out error);
        }

        /// <summary>
        /// Rebinds only authored enemy hitboxes to a newly spawned enemy
        /// runtime. Dynamic projectile proxies remain untouched, which is
        /// important because their frozen paths and ownership belong to the
        /// projectile runtime that was already registered.
        /// </summary>
        public bool TryRebindEnemyRuntimeId(
            RuntimeId nextEnemyRuntimeId,
            out string error)
        {
            if (!initialized || !staticBindingsRegistered)
            {
                error = "HitboxRegistry must be initialized with static bindings before enemy rebinding.";
                return false;
            }

            if (!nextEnemyRuntimeId.IsValid || nextEnemyRuntimeId == staticPlayerRuntimeId)
            {
                error = "A spawned enemy RuntimeId must be valid and distinct from the player RuntimeId.";
                return false;
            }

            if (staticEnemyRuntimeId == nextEnemyRuntimeId)
            {
                error = string.Empty;
                return true;
            }

            // Entity prefabs own the active combatant colliders at runtime.
            // Rebind those records by ownership; in-flight projectile proxies
            // remain registered against their frozen paths.
            List<int> registeredEnemyColliders = new List<int>();
            foreach (KeyValuePair<int, RegisteredHitbox> pair in byColliderInstanceId)
            {
                RegisteredHitbox registered = pair.Value;
                if (registered.TargetKind == QueryTargetKind.Combatant
                    && registered.Team == Team.Enemy)
                {
                    registeredEnemyColliders.Add(pair.Key);
                }
            }

            if (registeredEnemyColliders.Count > 0)
            {
                for (int index = 0; index < registeredEnemyColliders.Count; index++)
                {
                    int colliderInstanceId = registeredEnemyColliders[index];
                    RegisteredHitbox registered = byColliderInstanceId[colliderInstanceId];
                    byColliderInstanceId[colliderInstanceId] = new RegisteredHitbox(
                        registered.Collider,
                        nextEnemyRuntimeId,
                        registered.TargetKind,
                        registered.HitPart,
                        registered.GeometryId,
                        registered.Team,
                        registered.AllowTrigger);
                }

                staticEnemyRuntimeId = nextEnemyRuntimeId;
                error = string.Empty;
                return true;
            }

            HitboxBinding[] bindings = staticBindings ?? Array.Empty<HitboxBinding>();
            for (int index = 0; index < bindings.Length; index++)
            {
                HitboxBinding binding = bindings[index];
                if (binding == null
                    || !binding.Enabled
                    || binding.TargetReference != HitboxTargetReference.Enemy
                    || binding.Collider == null)
                {
                    continue;
                }

                int colliderInstanceId = binding.Collider.GetInstanceID();
                if (!byColliderInstanceId.TryGetValue(
                        colliderInstanceId,
                        out RegisteredHitbox registered))
                {
                    error = $"Enemy hitbox binding {index} is not currently registered.";
                    return false;
                }

                byColliderInstanceId[colliderInstanceId] = new RegisteredHitbox(
                    registered.Collider,
                    nextEnemyRuntimeId,
                    registered.TargetKind,
                    registered.HitPart,
                    registered.GeometryId,
                    registered.Team,
                    registered.AllowTrigger);
            }

            staticEnemyRuntimeId = nextEnemyRuntimeId;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Registers the active player entity prefab's body collider. Any
        /// compatibility player combatant binding is replaced, while authored
        /// environment blockers, enemies and projectile proxies remain intact.
        /// </summary>
        public bool TryBindPlayerEntity(
            RuntimeId playerRuntimeId,
            FpgPlayerEntityView playerEntity,
            GeometryId bodyGeometryId,
            out string error)
        {
            error = string.Empty;
            if (!initialized || !staticBindingsRegistered)
            {
                error = "HitboxRegistry must be initialized with static bindings before binding a player entity.";
                return false;
            }

            if (!playerRuntimeId.IsValid || playerRuntimeId != staticPlayerRuntimeId)
            {
                error = "Player entity RuntimeId does not match the active registry session.";
                return false;
            }

            Collider bodyCollider = playerEntity == null
                ? null
                : playerEntity.BodyHitbox;
            if (bodyCollider == null || !bodyGeometryId.IsValid)
            {
                error = "Player entity requires a valid body collider and geometry id.";
                return false;
            }

            int colliderInstanceId = bodyCollider.GetInstanceID();
            if (byColliderInstanceId.TryGetValue(
                    colliderInstanceId,
                    out RegisteredHitbox colliderOwner)
                && !IsPlayerCombatant(colliderOwner))
            {
                error = "Player entity body collider is already owned by another registered hitbox.";
                return false;
            }

            if (colliderInstanceIdByGeometryId.TryGetValue(
                    bodyGeometryId.Value,
                    out int geometryOwnerColliderId)
                && byColliderInstanceId.TryGetValue(
                    geometryOwnerColliderId,
                    out RegisteredHitbox geometryOwner)
                && !IsPlayerCombatant(geometryOwner))
            {
                error = "Player entity body geometry id is already owned by another registered hitbox.";
                return false;
            }

            RemoveRegisteredPlayerCombatantBindings();
            DomainResult bodyResult = RegisterCore(
                new HitboxBinding(
                    bodyCollider,
                    HitboxTargetReference.Player,
                    QueryTargetKind.Combatant,
                    HitPart.Body,
                    bodyGeometryId),
                playerRuntimeId,
                staticEnemyRuntimeId);
            if (!bodyResult.IsSuccess)
            {
                error = "Player entity body collider is invalid or duplicates a registered geometry.";
                return false;
            }

            boundPlayerEntity = playerEntity;
            boundPlayerEntityCollider = bodyCollider;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Unregisters the currently bound player entity. An entity which does
        /// not own the active binding is already unbound from this registry.
        /// </summary>
        public bool TryUnbindPlayerEntity(FpgPlayerEntityView playerEntity)
        {
            if (playerEntity == null || playerEntity != boundPlayerEntity)
            {
                return true;
            }

            Collider bodyCollider = boundPlayerEntityCollider;
            boundPlayerEntity = null;
            boundPlayerEntityCollider = null;
            if (bodyCollider == null || !initialized
                || !byColliderInstanceId.TryGetValue(
                    bodyCollider.GetInstanceID(),
                    out RegisteredHitbox registered)
                || !IsPlayerCombatant(registered))
            {
                return true;
            }

            return Unregister(bodyCollider).IsSuccess;
        }

        /// <summary>
        /// Registers the active spawned enemy prefab's combatant colliders.
        /// Any compatibility enemy bindings are removed; player hitboxes,
        /// environment blockers and in-flight projectile proxies are left
        /// intact.
        /// </summary>
        public bool TryBindEnemyEntity(
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            Collider bodyCollider,
            Collider weakpointCollider,
            GeometryId bodyGeometryId,
            GeometryId weakpointGeometryId,
            out string error)
        {
            error = string.Empty;
            if (!initialized || !staticBindingsRegistered)
            {
                error = "HitboxRegistry must be initialized with static bindings before binding an enemy entity.";
                return false;
            }

            if (!playerRuntimeId.IsValid || !enemyRuntimeId.IsValid
                || playerRuntimeId == enemyRuntimeId)
            {
                error = "Enemy entity RuntimeIds must be valid and distinct.";
                return false;
            }

            if (bodyCollider == null || !bodyGeometryId.IsValid
                || (weakpointCollider != null && !weakpointGeometryId.IsValid))
            {
                error = "Enemy entity requires a valid body collider and geometry id.";
                return false;
            }

            if (weakpointCollider != null
                && (bodyCollider == weakpointCollider
                    || bodyGeometryId == weakpointGeometryId))
            {
                error = "Enemy entity body and weakpoint collider/geometry must be distinct.";
                return false;
            }

            if (staticPlayerRuntimeId != playerRuntimeId)
            {
                error = "Enemy entity player RuntimeId does not match the active registry session.";
                return false;
            }

            int bodyColliderId = bodyCollider.GetInstanceID();
            if (byColliderInstanceId.TryGetValue(
                    bodyColliderId,
                    out RegisteredHitbox bodyColliderOwner)
                && !(bodyColliderOwner.TargetKind == QueryTargetKind.Combatant
                    && bodyColliderOwner.Team == Team.Enemy))
            {
                error = "Enemy entity body collider is owned by another registered hitbox.";
                return false;
            }

            if (colliderInstanceIdByGeometryId.TryGetValue(
                    bodyGeometryId.Value,
                    out int bodyGeometryOwnerColliderId)
                && (!byColliderInstanceId.TryGetValue(
                        bodyGeometryOwnerColliderId,
                        out RegisteredHitbox bodyGeometryOwner)
                    || !(bodyGeometryOwner.TargetKind == QueryTargetKind.Combatant
                        && bodyGeometryOwner.Team == Team.Enemy)))
            {
                error = "Enemy entity body geometry id is owned by another registered hitbox.";
                return false;
            }

            if (weakpointCollider != null)
            {
                int weakpointColliderId = weakpointCollider.GetInstanceID();
                if (byColliderInstanceId.TryGetValue(
                        weakpointColliderId,
                        out RegisteredHitbox weakpointColliderOwner)
                    && !(weakpointColliderOwner.TargetKind == QueryTargetKind.Combatant
                        && weakpointColliderOwner.Team == Team.Enemy))
                {
                    error = "Enemy entity weakpoint collider is owned by another registered hitbox.";
                    return false;
                }

                if (colliderInstanceIdByGeometryId.TryGetValue(
                        weakpointGeometryId.Value,
                        out int weakpointGeometryOwnerColliderId)
                    && (!byColliderInstanceId.TryGetValue(
                            weakpointGeometryOwnerColliderId,
                            out RegisteredHitbox weakpointGeometryOwner)
                        || !(weakpointGeometryOwner.TargetKind == QueryTargetKind.Combatant
                            && weakpointGeometryOwner.Team == Team.Enemy)))
                {
                    error = "Enemy entity weakpoint geometry id is owned by another registered hitbox.";
                    return false;
                }
            }

            RemoveRegisteredEnemyCombatantBindings();
            staticEnemyRuntimeId = RuntimeId.Invalid;
            DomainResult bodyResult = RegisterCore(
                new HitboxBinding(
                    bodyCollider,
                    HitboxTargetReference.Enemy,
                    QueryTargetKind.Combatant,
                    HitPart.Body,
                    bodyGeometryId),
                playerRuntimeId,
                enemyRuntimeId);
            if (!bodyResult.IsSuccess)
            {
                error = "Enemy entity body registration failed; the new binding was rolled back.";
                return false;
            }

            if (weakpointCollider != null)
            {
                DomainResult weakpointResult = RegisterCore(
                    new HitboxBinding(
                        weakpointCollider,
                        HitboxTargetReference.Enemy,
                        QueryTargetKind.Combatant,
                        HitPart.Weakpoint,
                        weakpointGeometryId),
                    playerRuntimeId,
                    enemyRuntimeId);
                if (!weakpointResult.IsSuccess)
                {
                    Unregister(bodyCollider);
                    error = "Enemy entity weakpoint registration failed; the new binding was rolled back.";
                    return false;
                }
            }

            staticEnemyRuntimeId = enemyRuntimeId;
            error = string.Empty;
            return true;
        }

        /// <summary>
        /// Removes one prefab-owned enemy collider. Missing colliders are
        /// already unbound and therefore make teardown idempotent.
        /// </summary>
        public bool TryUnbindEnemyEntity(Collider collider)
        {
            if (collider == null || !initialized
                || !byColliderInstanceId.TryGetValue(
                    collider.GetInstanceID(),
                    out RegisteredHitbox registered))
            {
                return true;
            }

            if (registered.Collider != collider
                || registered.TargetKind != QueryTargetKind.Combatant
                || registered.Team != Team.Enemy)
            {
                return false;
            }

            return Unregister(collider).IsSuccess;
        }

        public DomainResult Register(HitboxBinding binding)
        {
            if (!TryInitialize(out string ignored))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            return RegisterCore(binding, RuntimeId.Invalid, RuntimeId.Invalid);
        }

        public DomainResult Register(
            HitboxBinding binding,
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId)
        {
            if (!TryInitialize(out string ignored))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            return RegisterCore(binding, playerRuntimeId, enemyRuntimeId);
        }

        public DomainResult Unregister(Collider collider)
        {
            if (!initialized || collider == null)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            int colliderInstanceId = collider.GetInstanceID();
            if (!byColliderInstanceId.TryGetValue(colliderInstanceId, out RegisteredHitbox registered))
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            byColliderInstanceId.Remove(colliderInstanceId);
            colliderInstanceIdByGeometryId.Remove(registered.GeometryId.Value);
            return DomainResult.Success;
        }

        public bool TryResolve(Collider collider, out RegisteredHitbox registered)
        {
            registered = default(RegisteredHitbox);
            if (!initialized || collider == null
                || !byColliderInstanceId.TryGetValue(collider.GetInstanceID(), out RegisteredHitbox candidate)
                || candidate.Collider != collider
                || !candidate.IsValid)
            {
                return false;
            }

            registered = candidate;
            return true;
        }

        public bool TryResolve(GeometryId geometryId, out RegisteredHitbox registered)
        {
            registered = default(RegisteredHitbox);
            return initialized
                && geometryId.IsValid
                && colliderInstanceIdByGeometryId.TryGetValue(geometryId.Value, out int colliderInstanceId)
                && byColliderInstanceId.TryGetValue(colliderInstanceId, out registered)
                && registered.IsValid;
        }

        public void ClearDynamicAndStaticBindings()
        {
            byColliderInstanceId.Clear();
            colliderInstanceIdByGeometryId.Clear();
            initialized = true;
            staticBindingsRegistered = false;
            staticPlayerRuntimeId = RuntimeId.Invalid;
            staticEnemyRuntimeId = RuntimeId.Invalid;
            boundPlayerEntity = null;
            boundPlayerEntityCollider = null;
        }

        private DomainResult RegisterCore(
            HitboxBinding binding,
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId)
        {
            if (binding == null
                || !binding.TryCreateRegisteredHitbox(
                    playerRuntimeId,
                    enemyRuntimeId,
                    out RegisteredHitbox registered))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            int colliderInstanceId = registered.Collider.GetInstanceID();
            if (byColliderInstanceId.ContainsKey(colliderInstanceId)
                || colliderInstanceIdByGeometryId.ContainsKey(registered.GeometryId.Value))
            {
                return DomainResult.Rejected(RejectReason.InvalidDefinition);
            }

            byColliderInstanceId.Add(colliderInstanceId, registered);
            colliderInstanceIdByGeometryId.Add(registered.GeometryId.Value, colliderInstanceId);
            return DomainResult.Success;
        }

        private void RemoveRegisteredEnemyCombatantBindings()
        {
            List<int> removeColliderIds = new List<int>();
            foreach (KeyValuePair<int, RegisteredHitbox> pair in byColliderInstanceId)
            {
                RegisteredHitbox registered = pair.Value;
                if (registered.TargetKind == QueryTargetKind.Combatant
                    && registered.Team == Team.Enemy)
                {
                    removeColliderIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < removeColliderIds.Count; index++)
            {
                int colliderInstanceId = removeColliderIds[index];
                if (!byColliderInstanceId.TryGetValue(
                        colliderInstanceId,
                        out RegisteredHitbox registered))
                {
                    continue;
                }

                byColliderInstanceId.Remove(colliderInstanceId);
                colliderInstanceIdByGeometryId.Remove(registered.GeometryId.Value);
            }
        }


        private void RemoveRegisteredPlayerCombatantBindings()
        {
            List<int> removeColliderIds = new List<int>();
            foreach (KeyValuePair<int, RegisteredHitbox> pair in byColliderInstanceId)
            {
                if (IsPlayerCombatant(pair.Value))
                {
                    removeColliderIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < removeColliderIds.Count; index++)
            {
                int colliderInstanceId = removeColliderIds[index];
                if (!byColliderInstanceId.TryGetValue(
                        colliderInstanceId,
                        out RegisteredHitbox registered))
                {
                    continue;
                }

                byColliderInstanceId.Remove(colliderInstanceId);
                colliderInstanceIdByGeometryId.Remove(registered.GeometryId.Value);
            }

            boundPlayerEntity = null;
            boundPlayerEntityCollider = null;
        }

        private static bool IsPlayerCombatant(RegisteredHitbox registered)
        {
            return registered.TargetKind == QueryTargetKind.Combatant
                && registered.Team == Team.Player;
        }

        private void Awake()
        {
            TryInitialize(out string ignored);
        }
    

        public bool TryBindEnemyEntity(
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            D0EnemyEntityView entityView,
            out string error)
        {
            error = string.Empty;
            if (!initialized || !staticBindingsRegistered)
            {
                error = "HitboxRegistry must be initialized with static bindings before binding an enemy entity.";
                return false;
            }

            if (!playerRuntimeId.IsValid || !enemyRuntimeId.IsValid
                || playerRuntimeId == enemyRuntimeId)
            {
                error = "Enemy entity RuntimeIds must be valid and distinct.";
                return false;
            }

            if (staticPlayerRuntimeId != playerRuntimeId)
            {
                error = "Enemy entity player RuntimeId does not match the active registry session.";
                return false;
            }

            if (entityView == null || !entityView.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Enemy entity view is missing or invalid.";
                }

                return false;
            }

            int hitPartCount = entityView.HitPartCount;
            if (hitPartCount <= 0 || hitPartCount > D0EnemyEntityView.MaxHitPartCount)
            {
                error = "Enemy entity exposes an invalid hit-part count.";
                return false;
            }

            for (int ordinal = 0; ordinal < hitPartCount; ordinal++)
            {
                if (!entityView.TryGetHitPart(
                        ordinal,
                        out Collider collider,
                        out HitPart hitPart,
                        out GeometryId geometryId)
                    || hitPart != HitPart.Body && hitPart != HitPart.Weakpoint)
                {
                    error = $"Enemy entity hit part {ordinal} is invalid.";
                    return false;
                }

                int colliderInstanceId = collider.GetInstanceID();
                if (byColliderInstanceId.TryGetValue(
                        colliderInstanceId,
                        out RegisteredHitbox colliderOwner)
                    && !(colliderOwner.TargetKind == QueryTargetKind.Combatant
                        && colliderOwner.Team == Team.Enemy))
                {
                    error = $"Enemy entity hit part {ordinal} collider is owned by another registered hitbox.";
                    return false;
                }

                if (colliderInstanceIdByGeometryId.TryGetValue(
                        geometryId.Value,
                        out int geometryOwnerColliderId)
                    && (!byColliderInstanceId.TryGetValue(
                            geometryOwnerColliderId,
                            out RegisteredHitbox geometryOwner)
                        || !(geometryOwner.TargetKind == QueryTargetKind.Combatant
                            && geometryOwner.Team == Team.Enemy)))
                {
                    error = $"Enemy entity hit part {ordinal} geometry id is owned by another registered hitbox.";
                    return false;
                }

                for (int previous = 0; previous < ordinal; previous++)
                {
                    if (!entityView.TryGetHitPart(
                            previous,
                            out Collider previousCollider,
                            out HitPart ignoredHitPart,
                            out GeometryId previousGeometryId)
                        || collider == previousCollider
                        || geometryId == previousGeometryId)
                    {
                        error = $"Enemy entity hit part {ordinal} duplicates another collider or geometry id.";
                        return false;
                    }
                }
            }

            RemoveRegisteredEnemyCombatantBindings();
            staticEnemyRuntimeId = RuntimeId.Invalid;
            Collider[] registeredColliders = new Collider[hitPartCount];
            int registeredCount = 0;
            for (int ordinal = 0; ordinal < hitPartCount; ordinal++)
            {
                entityView.TryGetHitPart(
                    ordinal,
                    out Collider collider,
                    out HitPart hitPart,
                    out GeometryId geometryId);
                DomainResult result = RegisterCore(
                    new HitboxBinding(
                        collider,
                        HitboxTargetReference.Enemy,
                        QueryTargetKind.Combatant,
                        hitPart,
                        geometryId),
                    playerRuntimeId,
                    enemyRuntimeId);
                if (!result.IsSuccess)
                {
                    for (int rollback = registeredCount - 1; rollback >= 0; rollback--)
                    {
                        Unregister(registeredColliders[rollback]);
                    }

                    error = $"Enemy entity hit part {ordinal} failed registration; the new binding was rolled back.";
                    return false;
                }

                registeredColliders[registeredCount++] = collider;
            }

            staticEnemyRuntimeId = enemyRuntimeId;
            error = string.Empty;
            return true;
        }
    }
}
