using System;
using System.Collections;
using System.Collections.Generic;
using NewFPG.Combat;
using Pathfinding;
using UnityEngine;

namespace NewFPG.Monsters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AIPath))]
    [RequireComponent(typeof(Seeker))]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CombatVitals))]
    public sealed class MonsterConfigBinding : MonoBehaviour, IMonsterState, IMonsterLocomotion
    {
        public enum MovementState
        {
            Idle,
            Move,
        }

        [SerializeField] private string monsterId = "fish";
        [SerializeField] private TextAsset catalogJson;
        [SerializeField] private bool applyOnAwake = true;

        private readonly List<float> scaleMultipliers = new List<float>();
        private readonly List<float> speedMultipliers = new List<float>();
        private readonly Dictionary<string, MonsterSkillDefinition> skillsById = new Dictionary<string, MonsterSkillDefinition>();
        private readonly Dictionary<string, float> nextReadyAtBySkill = new Dictionary<string, float>();
        private readonly DamageAreaMechanic damageArea = new DamageAreaMechanic();
        private readonly InvincibleMechanic invincible = new InvincibleMechanic();
        private readonly InvisibleMechanic invisible = new InvisibleMechanic();
        private readonly ScaleModifierMechanic scaleModifier = new ScaleModifierMechanic();
        private readonly SpeedModifierMechanic speedModifier = new SpeedModifierMechanic();

        private AIPath aiPath;
        private IAstarAI astarAgent;
        private BoxCollider boxCollider;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer[] renderers;
        private CombatVitals vitals;
        private Animator animator;
        private AttackWarningIndicator warningIndicator;
        private Transform target;
        private Transform autoTarget;
        private int invincibleStacks;
        private int invisibleStacks;
        private Vector3 baseScale = Vector3.one;
        private bool casting;
        private MonsterSkillDefinition activeSkill;
        private Transform activeTarget;
        private IDamageable lockedTarget;
        private int activeTriggerHash;
        private bool animatorHasActiveTrigger;
        private bool ownsRuntimeWarningIndicator;
        private bool castStoppedMovement;
        private bool movementEnabledBeforeCast;
        private float moveSpeed = 2.5f;
        private float acceleration = 16f;
        private float deceleration = 20f;
        private bool movementEnabled = true;
        private bool autoFindTargetByTag = true;
        private string targetTag = "Player";
        private float detectionRadius = 7f;
        private float stoppingDistance = 1.2f;
        private float navMeshAgentRadius = 0.35f;
        private float navMeshAgentHeight = 1.2f;
        private float navMeshAgentAngularSpeed = 720f;
        private float navMeshAgentBaseOffset;
        private int navMeshAreaMask = ~0;
        private float navMeshSampleDistance = 1.5f;
        private float visibilitySampleHeight = 1f;
        private int visiblePositionLineOfSightMask = MonsterLayerMasks.DefaultObstructionMask;
        private int visiblePositionOccupancyMask = MonsterLayerMasks.DefaultObstructionMask;
        private int visiblePositionSampleAttempts = 24;
        private float visiblePositionOccupancyRadius = 0.45f;
        private MonsterBattleZoneGroupDefinition nearZoneGroup = MonsterBattleZoneGroupDefinition.Near();
        private MonsterBattleZoneGroupDefinition midZoneGroup = MonsterBattleZoneGroupDefinition.Mid();
        private MonsterBattleZoneGroupDefinition farZoneGroup = MonsterBattleZoneGroupDefinition.Far();
        private MonsterBattleZoneGroupDefinition leftZoneGroup = MonsterBattleZoneGroupDefinition.Left();
        private MonsterBattleZoneGroupDefinition centerZoneGroup = MonsterBattleZoneGroupDefinition.Center();
        private MonsterBattleZoneGroupDefinition rightZoneGroup = MonsterBattleZoneGroupDefinition.Right();
        private readonly List<string> resolvedBattleZoneIds = new List<string>();
        private float targetRefreshInterval = 0.25f;
        private bool patrolWhenNoTarget = true;
        private float patrolRadius = 3f;
        private float patrolPointTolerance = 0.2f;
        private float patrolPauseDuration = 1f;
        private bool flipSpriteWithHorizontalMovement = true;
        private bool spriteFacesRightByDefault = true;
        private bool autoConfigureCollider = true;
        private float colliderWidthScale = 0.8f;
        private float colliderHeightScale = 0.75f;
        private float colliderDepth = 0.75f;
        private string moveXParameter = "MoveX";
        private string moveZParameter = "MoveZ";
        private string speedParameter = "Speed";
        private string isMovingParameter = "IsMoving";
        private string movementStateParameter = "MovementState";

        private Vector2 velocity;
        private Vector2 desiredDirection;
        private Vector3 homePosition;
        private Vector3 patrolDestination;
        private Vector3 manualDestination;
        private bool hasPatrolDestination;
        private bool hasManualDestination;
        private bool hasActiveMoveCommand;
        private Vector3 currentMoveDestination;
        private float currentMoveStartedAt;
        private float currentMoveLastProgressAt;
        private float currentMoveLastRemainingDistance = float.PositiveInfinity;
        private float patrolPauseRemaining;
        private MovementState movementState;
        private int moveXHash;
        private int moveZHash;
        private int speedHash;
        private int isMovingHash;
        private int movementStateHash;

        public event Action<MonsterConfigBinding> Changed;

        public bool IsInvincible => invincibleStacks > 0;
        public bool IsInvisible => invisibleStacks > 0;
        public bool IsTargetable => !IsInvisible;
        public float ScaleMultiplier { get; private set; } = 1f;
        public float SpeedMultiplier { get; private set; } = 1f;
        public bool IsCasting => casting;
        public Transform ActiveTarget => activeTarget;
        public Vector2 DesiredDirection => desiredDirection;
        public Vector2 Velocity => velocity;
        public Vector3 WorldVelocity => new Vector3(velocity.x, 0f, velocity.y);
        public MovementState State => movementState;
        public bool HasArrived => HasAgentArrived();
        public bool HasActiveMoveCommand => hasActiveMoveCommand;
        public Vector3 HomePosition => homePosition;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        public float DetectionRadius
        {
            get => detectionRadius;
            set => detectionRadius = Mathf.Max(0f, value);
        }

        public Transform Target
        {
            get => target != null ? target : autoTarget;
            set
            {
                target = value;
                autoTarget = null;
            }
        }

        public string MonsterId
        {
            get => monsterId;
            set => monsterId = value;
        }

        public TextAsset CatalogJson
        {
            get => catalogJson;
            set => catalogJson = value;
        }

        public bool ApplyOnAwake
        {
            get => applyOnAwake;
            set => applyOnAwake = value;
        }

        private void Reset()
        {
            CacheRuntimeReferences();
            CacheMovementAnimatorHashes();
            ConfigureAgent();
            ConfigureCollider();
        }

        private void Awake()
        {
            CacheRuntimeReferences();
            CacheMovementAnimatorHashes();
            ConfigureAgent();
            ConfigureCollider();
            baseScale = transform.localScale;
            homePosition = transform.position;
            RefreshDerivedState();

            if (applyOnAwake)
            {
                ApplyConfig(true);
            }
        }

        private void OnEnable()
        {
            hasPatrolDestination = false;
            hasManualDestination = false;
            ResetMoveTracking();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            FinishCastCleanup();
            HideWarning();
            ClearStateModifiers();
        }

        private void OnDestroy()
        {
            if (!ownsRuntimeWarningIndicator || warningIndicator == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(warningIndicator.gameObject);
            }
            else
            {
                DestroyImmediate(warningIndicator.gameObject);
            }
        }

        private void Update()
        {
            UpdateAstarMovement();
            UpdateSpriteFacing();
            UpdateMovementAnimator();
        }

        public bool ApplyConfig(bool resetVitals)
        {
            MonsterDefinition definition = ResolveDefinition();
            if (definition == null)
            {
                return false;
            }

            return MonsterDefinitionApplier.Apply(gameObject, definition, resetVitals);
        }

        public void ApplyDefinition(MonsterDefinition definition)
        {
            if (definition == null)
            {
                ApplyRuntimeDefinition(null);
                return;
            }

            definition.Normalize();
            ApplyMovementDefinition(definition.movement);
            ApplyRuntimeDefinition(definition);
        }

        public void ApplyRuntimeDefinition(MonsterDefinition definition)
        {
            CacheRuntimeReferences();
            skillsById.Clear();
            nextReadyAtBySkill.Clear();

            if (definition == null)
            {
                return;
            }

            definition.Normalize();

            for (int i = 0; i < definition.skills.Count; i++)
            {
                MonsterSkillDefinition skill = definition.skills[i];
                if (skill == null || string.IsNullOrWhiteSpace(skill.skillId))
                {
                    continue;
                }

                skillsById[skill.skillId] = skill;
                nextReadyAtBySkill[skill.skillId] = 0f;
            }

        }

        public void ApplyMovementDefinition(MonsterMovementDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            definition.Normalize();
            moveSpeed = definition.moveSpeed;
            acceleration = definition.acceleration;
            deceleration = definition.deceleration;
            movementEnabled = definition.movementEnabled;
            autoFindTargetByTag = definition.autoFindTargetByTag;
            targetTag = definition.targetTag;
            detectionRadius = definition.detectionRadius;
            stoppingDistance = definition.stoppingDistance;
            navMeshAgentRadius = definition.navMeshAgentRadius;
            navMeshAgentHeight = definition.navMeshAgentHeight;
            navMeshAgentAngularSpeed = definition.navMeshAgentAngularSpeed;
            navMeshAgentBaseOffset = definition.navMeshAgentBaseOffset;
            navMeshAreaMask = definition.navMeshAreaMask;
            navMeshSampleDistance = definition.navMeshSampleDistance;
            visibilitySampleHeight = definition.visibilitySampleHeight;
            visiblePositionLineOfSightMask = definition.visiblePositionLineOfSightMask;
            visiblePositionOccupancyMask = definition.visiblePositionOccupancyMask;
            visiblePositionSampleAttempts = definition.visiblePositionSampleAttempts;
            visiblePositionOccupancyRadius = definition.visiblePositionOccupancyRadius;
            nearZoneGroup = definition.nearZoneGroup;
            midZoneGroup = definition.midZoneGroup;
            farZoneGroup = definition.farZoneGroup;
            leftZoneGroup = definition.leftZoneGroup;
            centerZoneGroup = definition.centerZoneGroup;
            rightZoneGroup = definition.rightZoneGroup;
            targetRefreshInterval = definition.targetRefreshInterval;
            patrolWhenNoTarget = definition.patrolWhenNoTarget;
            patrolRadius = definition.patrolRadius;
            patrolPointTolerance = definition.patrolPointTolerance;
            patrolPauseDuration = definition.patrolPauseDuration;
            flipSpriteWithHorizontalMovement = definition.flipSpriteWithHorizontalMovement;
            spriteFacesRightByDefault = definition.spriteFacesRightByDefault;
            autoConfigureCollider = definition.autoConfigureCollider;
            colliderWidthScale = definition.colliderWidthScale;
            colliderHeightScale = definition.colliderHeightScale;
            colliderDepth = definition.colliderDepth;
            moveXParameter = definition.moveXParameter;
            moveZParameter = definition.moveZParameter;
            speedParameter = definition.speedParameter;
            isMovingParameter = definition.isMovingParameter;
            movementStateParameter = definition.movementStateParameter;
            CacheMovementAnimatorHashes();
            ConfigureAgent();
            ConfigureCollider();
        }

        public MonsterMovementDefinition ToMovementDefinition()
        {
            return new MonsterMovementDefinition
            {
                moveSpeed = moveSpeed,
                acceleration = acceleration,
                deceleration = deceleration,
                movementEnabled = movementEnabled,
                autoFindTargetByTag = autoFindTargetByTag,
                targetTag = targetTag,
                detectionRadius = detectionRadius,
                stoppingDistance = stoppingDistance,
                navMeshAgentRadius = navMeshAgentRadius,
                navMeshAgentHeight = navMeshAgentHeight,
                navMeshAgentAngularSpeed = navMeshAgentAngularSpeed,
                navMeshAgentBaseOffset = navMeshAgentBaseOffset,
                navMeshAreaMask = navMeshAreaMask,
                navMeshSampleDistance = navMeshSampleDistance,
                visibilitySampleHeight = visibilitySampleHeight,
                visiblePositionLineOfSightMask = visiblePositionLineOfSightMask,
                visiblePositionOccupancyMask = visiblePositionOccupancyMask,
                visiblePositionSampleAttempts = visiblePositionSampleAttempts,
                visiblePositionOccupancyRadius = visiblePositionOccupancyRadius,
                nearZoneGroup = nearZoneGroup,
                midZoneGroup = midZoneGroup,
                farZoneGroup = farZoneGroup,
                leftZoneGroup = leftZoneGroup,
                centerZoneGroup = centerZoneGroup,
                rightZoneGroup = rightZoneGroup,
                targetRefreshInterval = targetRefreshInterval,
                patrolWhenNoTarget = patrolWhenNoTarget,
                patrolRadius = patrolRadius,
                patrolPointTolerance = patrolPointTolerance,
                patrolPauseDuration = patrolPauseDuration,
                flipSpriteWithHorizontalMovement = flipSpriteWithHorizontalMovement,
                spriteFacesRightByDefault = spriteFacesRightByDefault,
                autoConfigureCollider = autoConfigureCollider,
                colliderWidthScale = colliderWidthScale,
                colliderHeightScale = colliderHeightScale,
                colliderDepth = colliderDepth,
                moveXParameter = moveXParameter,
                moveZParameter = moveZParameter,
                speedParameter = speedParameter,
                isMovingParameter = isMovingParameter,
                movementStateParameter = movementStateParameter,
            };
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (movementEnabled)
            {
                if (CanUseAgent())
                {
                    astarAgent.isStopped = false;
                }

                return;
            }

            desiredDirection = Vector2.zero;
            velocity = Vector2.zero;
            movementState = MovementState.Idle;
            ClearMoveTracking();
            ClearAstarPath();

            UpdateMovementAnimator();
        }

        public void SetHomePositionToCurrent()
        {
            SetHomePosition(transform.position);
        }

        public void SetHomePosition(Vector3 position)
        {
            homePosition = position;
            hasPatrolDestination = false;
            patrolPauseRemaining = 0f;
        }

        public bool TryMoveTo(Vector3 destination)
        {
            return TryMoveTo(destination, false);
        }

        private bool TryMoveTo(Vector3 destination, bool isPatrolMove)
        {
            if (!CanUseAgent())
            {
                return false;
            }

            if (!MonsterAstarNavigation.TryProjectReachable(
                    transform.position,
                    destination,
                    ToMovementDefinition(),
                    out Vector3 sampledPosition))
            {
                return false;
            }

            manualDestination = sampledPosition;
            hasManualDestination = true;
            hasPatrolDestination = isPatrolMove;
            if (isPatrolMove)
            {
                patrolDestination = sampledPosition;
            }

            BeginMoveTracking(sampledPosition);
            SetAstarDestination(sampledPosition, true);
            return true;
        }

        public bool IsCurrentMoveStuck(float stuckSeconds, float minProgressDistance)
        {
            if (!hasActiveMoveCommand || HasAgentArrived())
            {
                return false;
            }

            if (stuckSeconds <= 0f)
            {
                return false;
            }

            if (!CanUseAgent())
            {
                return true;
            }

            UpdateMoveProgress(minProgressDistance);
            return Time.time - currentMoveLastProgressAt >= stuckSeconds;
        }

        public bool HasCurrentMoveTimedOut(float timeoutSeconds)
        {
            return hasActiveMoveCommand
                && timeoutSeconds > 0f
                && !HasAgentArrived()
                && Time.time - currentMoveStartedAt >= timeoutSeconds;
        }

        public void Stop()
        {
            Stop(false);
        }

        public void Stop(bool clearTarget)
        {
            hasManualDestination = false;
            if (clearTarget)
            {
                Target = null;
            }

            ClearAstarPath();
            ClearMoveTracking();

            desiredDirection = Vector2.zero;
            velocity = Vector2.zero;
        }

        public void SetTarget(Transform newTarget)
        {
            Target = newTarget;
        }

        public void ClearTarget()
        {
            Target = null;
        }

        public bool RefreshTargetByTag(string tag, float radius, bool clearWhenMissing)
        {
            return RefreshTargetByTag(tag, radius, clearWhenMissing, out _);
        }

        public bool RefreshTargetByTag(string tag, float radius, bool clearWhenMissing, out Transform foundTarget)
        {
            foundTarget = null;
            string resolvedTag = string.IsNullOrWhiteSpace(tag) ? targetTag : tag.Trim();
            if (string.IsNullOrWhiteSpace(resolvedTag))
            {
                if (clearWhenMissing)
                {
                    Target = null;
                }

                return false;
            }

            GameObject[] taggedObjects;
            try
            {
                taggedObjects = GameObject.FindGameObjectsWithTag(resolvedTag);
            }
            catch (UnityException)
            {
                if (clearWhenMissing)
                {
                    Target = null;
                }

                return false;
            }

            float searchRadius = radius > 0f ? radius : detectionRadius;
            float bestDistanceSqr = float.MaxValue;
            Vector3 currentPosition = transform.position;
            for (int i = 0; i < taggedObjects.Length; i++)
            {
                GameObject tagged = taggedObjects[i];
                if (tagged == null || !IsTargetValid(tagged.transform))
                {
                    continue;
                }

                Vector3 delta = tagged.transform.position - currentPosition;
                delta.y = 0f;
                float distanceSqr = delta.sqrMagnitude;
                if (searchRadius > 0f && distanceSqr > searchRadius * searchRadius)
                {
                    continue;
                }

                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                foundTarget = tagged.transform;
                bestDistanceSqr = distanceSqr;
            }

            if (foundTarget != null)
            {
                Target = foundTarget;
                return true;
            }

            if (clearWhenMissing)
            {
                Target = null;
            }

            return false;
        }

        public bool HasTarget()
        {
            return Target != null;
        }

        public bool HasValidTarget()
        {
            return IsTargetValid(Target);
        }

        public bool IsValidTarget(Transform candidate)
        {
            return IsTargetValid(candidate);
        }

        public bool IsSkillUsable(string skillId, Transform skillTarget = null)
        {
            Transform resolvedTarget = skillTarget != null ? skillTarget : Target;
            return CanUse(skillId, resolvedTarget);
        }

        public bool IsTargetInSkillRange(string skillId, Transform skillTarget = null)
        {
            MonsterSkillDefinition skill = GetSkill(skillId);
            Transform resolvedTarget = skillTarget != null ? skillTarget : Target;
            return skill != null && TargetInRange(resolvedTarget, skill.castRange);
        }

        public bool HasLineOfSightToTarget(string skillId, Transform skillTarget = null)
        {
            MonsterSkillDefinition skill = GetSkill(skillId);
            Transform resolvedTarget = skillTarget != null ? skillTarget : Target;
            return skill != null && HasSkillLineOfSight(skill, resolvedTarget);
        }

        public bool CanReleaseSkill(string skillId, Transform skillTarget = null)
        {
            MonsterSkillDefinition skill = GetSkill(skillId);
            Transform resolvedTarget = skillTarget != null ? skillTarget : Target;
            return CanReleaseSkill(skill, resolvedTarget);
        }

        public bool TryMoveToTarget(Transform moveTarget = null)
        {
            Transform resolvedTarget = moveTarget != null ? moveTarget : Target;
            if (!IsTargetValid(resolvedTarget))
            {
                return false;
            }

            Target = resolvedTarget;
            SetMovementEnabled(true);
            return TryMoveTo(resolvedTarget.position);
        }

        public bool TryMoveToVisibleCameraBand(
            Transform observerRoot,
            IReadOnlyList<string> distanceBands,
            int sampleAttemptsOverride)
        {
            return TryMoveToBattleZoneGroup(observerRoot, distanceBands, sampleAttemptsOverride);
        }

        public bool TryMoveToBattleZoneGroup(
            Transform skillTarget,
            IReadOnlyList<string> zoneGroupsOrIds,
            int sampleAttemptsOverride)
        {
            MonsterMovementDefinition movement = ToMovementDefinition();
            if (!movement.TryExpandBattleZoneGroups(zoneGroupsOrIds, resolvedBattleZoneIds))
            {
                return false;
            }

            return TryMoveToBattleZoneAny(
                skillTarget,
                resolvedBattleZoneIds,
                BattleZoneSampler.RandomReachableSampleMode,
                sampleAttemptsOverride);
        }

        public bool TryMoveToBattleZone(
            Transform skillTarget,
            string zoneId,
            string sampleMode,
            int sampleAttemptsOverride)
        {
            resolvedBattleZoneIds.Clear();
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                resolvedBattleZoneIds.Add(BattleArenaZoneMap.NormalizeZoneId(zoneId));
            }

            return TryMoveToBattleZoneAny(skillTarget, resolvedBattleZoneIds, sampleMode, sampleAttemptsOverride);
        }

        private bool TryMoveToBattleZoneAny(
            Transform skillTarget,
            IReadOnlyList<string> zoneIds,
            string sampleMode,
            int sampleAttemptsOverride)
        {
            Transform resolvedTarget = skillTarget != null ? skillTarget : Target;
            if (resolvedTarget == null)
            {
                return false;
            }

            if (zoneIds == null || zoneIds.Count == 0)
            {
                return false;
            }

            MonsterMovementDefinition movement = ToMovementDefinition();
            BattleArenaZoneMap zoneMap = BattleArenaZoneMap.Current;
            int startIndex = UnityEngine.Random.Range(0, zoneIds.Count);
            for (int i = 0; i < zoneIds.Count; i++)
            {
                string zoneId = zoneIds[(startIndex + i) % zoneIds.Count];
                if (!BattleZoneSampler.TryFindZonePosition(
                        transform,
                        resolvedTarget,
                        movement,
                        zoneMap,
                        zoneId,
                        sampleMode,
                        sampleAttemptsOverride,
                        out Vector3 destination))
                {
                    continue;
                }

                SetMovementEnabled(true);
                return TryMoveTo(destination);
            }

            return false;
        }

        public bool StartPatrol()
        {
            if (!patrolWhenNoTarget || patrolRadius <= 0f)
            {
                return false;
            }

            Target = null;
            SetMovementEnabled(true);
            if (hasPatrolDestination)
            {
                if (hasManualDestination && !HasAgentArrived())
                {
                    return true;
                }

                if (patrolPauseDuration > 0f)
                {
                    if (patrolPauseRemaining <= 0f)
                    {
                        patrolPauseRemaining = patrolPauseDuration;
                    }

                    patrolPauseRemaining = Mathf.Max(0f, patrolPauseRemaining - Time.deltaTime);
                    if (patrolPauseRemaining > 0f)
                    {
                        return true;
                    }
                }

                hasPatrolDestination = false;
                patrolPauseRemaining = 0f;
            }

            if (!TryPickPatrolDestination(out Vector3 nextPatrolDestination))
            {
                return false;
            }

            return TryMoveTo(nextPatrolDestination, true);
        }

        public void GetKnownSkills(List<MonsterSkillDefinition> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            foreach (MonsterSkillDefinition skill in skillsById.Values)
            {
                if (skill != null)
                {
                    results.Add(skill);
                }
            }

            if (results.Count > 0 || catalogJson == null)
            {
                return;
            }

            MonsterDefinition definition = MonsterCatalog.FromJson(catalogJson.text).FindMonster(monsterId);
            if (definition == null)
            {
                return;
            }

            definition.Normalize();
            for (int i = 0; i < definition.skills.Count; i++)
            {
                MonsterSkillDefinition skill = definition.skills[i];
                if (skill != null && !string.IsNullOrWhiteSpace(skill.skillId))
                {
                    results.Add(skill);
                }
            }
        }

        public bool CanUse(string skillId, Transform skillTarget)
        {
            if (casting || skillTarget == null || string.IsNullOrWhiteSpace(skillId) || !skillsById.ContainsKey(skillId))
            {
                return false;
            }

            return !nextReadyAtBySkill.TryGetValue(skillId, out float nextReadyAt) || Time.time >= nextReadyAt;
        }

        public bool TryUseSkill(string skillId, Transform skillTarget)
        {
            if (!CanUse(skillId, skillTarget))
            {
                return false;
            }

            MonsterSkillDefinition skill = skillsById[skillId];
            IDamageable damageable = ResolveDamageable(skillTarget);
            nextReadyAtBySkill[skill.skillId] = Time.time + skill.cooldown;
            BeginCast(skill, skillTarget, damageable);
            StartCoroutine(CastRoutine(skill, skillTarget, damageable));
            return true;
        }

        public MonsterSkillDefinition GetSkill(string skillId)
        {
            return !string.IsNullOrWhiteSpace(skillId) && skillsById.TryGetValue(skillId, out MonsterSkillDefinition skill)
                ? skill
                : null;
        }

        public Coroutine Run(MonsterMechanicDefinition mechanic, Transform mechanicTarget, IDamageable mechanicLockedTarget)
        {
            if (mechanic == null)
            {
                return null;
            }

            return StartCoroutine(RunMechanicRoutine(mechanic, mechanicTarget, mechanicLockedTarget));
        }

        public void ExecuteNow(MonsterMechanicDefinition mechanic, Transform mechanicTarget, IDamageable mechanicLockedTarget)
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

            executable.Execute(new MonsterMechanicContext(gameObject, mechanicTarget, mechanicLockedTarget, this), mechanic);
        }

        public void PushInvincible(float seconds)
        {
            StartCoroutine(PushFlag(seconds, () => invincibleStacks++, () => invincibleStacks--));
        }

        public void PushInvisible(float seconds)
        {
            StartCoroutine(PushFlag(seconds, () => invisibleStacks++, () => invisibleStacks--));
        }

        public void PushScaleMultiplier(float multiplier, float seconds)
        {
            StartCoroutine(PushMultiplier(scaleMultipliers, Mathf.Max(0.01f, multiplier), seconds));
        }

        public void PushSpeedMultiplier(float multiplier, float seconds)
        {
            StartCoroutine(PushMultiplier(speedMultipliers, Mathf.Max(0.01f, multiplier), seconds));
        }

        private MonsterDefinition ResolveDefinition()
        {
            if (catalogJson == null)
            {
                Debug.LogWarning($"{nameof(MonsterConfigBinding)} on {name} has no monster catalog JSON assigned.", this);
                return null;
            }

            MonsterCatalog catalog = MonsterCatalog.FromJson(catalogJson.text);
            MonsterDefinition definition = catalog.FindMonster(monsterId);
            if (definition == null)
            {
                Debug.LogWarning($"{nameof(MonsterConfigBinding)} could not find monster '{monsterId}' in {catalogJson.name}.", this);
            }

            return definition;
        }

        private void BeginCast(MonsterSkillDefinition skill, Transform skillTarget, IDamageable damageable)
        {
            hasManualDestination = false;
            ClearMoveTracking();
            casting = true;
            activeSkill = skill;
            activeTarget = skillTarget;
            lockedTarget = damageable;
            CacheAnimatorParameter(skill.animationTriggerParameter);

            castStoppedMovement = skill.stopMovementDuringCast;
            movementEnabledBeforeCast = movementEnabled;
            if (castStoppedMovement)
            {
                SetMovementEnabled(false);
            }

            if (skill.showWarning)
            {
                PlayWarning(skill);
            }
        }

        private IEnumerator CastRoutine(MonsterSkillDefinition skill, Transform skillTarget, IDamageable damageable)
        {
            if (skill.windup > 0f)
            {
                yield return new WaitForSeconds(skill.windup);
            }

            if (!CanReleaseSkill(skill, skillTarget))
            {
                FinishCastCleanup();
                yield break;
            }

            TriggerAnimation();

            for (int i = 0; i < skill.mechanics.Count; i++)
            {
                Run(skill.mechanics[i], skillTarget, damageable);
            }

            float tailSeconds = skill.activeDuration + skill.recovery;
            if (tailSeconds > 0f)
            {
                yield return new WaitForSeconds(tailSeconds);
            }

            FinishCastCleanup();
        }

        private IEnumerator RunMechanicRoutine(MonsterMechanicDefinition mechanic, Transform mechanicTarget, IDamageable mechanicLockedTarget)
        {
            mechanic.Normalize(null);
            if (mechanic.delay > 0f)
            {
                yield return new WaitForSeconds(mechanic.delay);
            }

            ExecuteNow(mechanic, mechanicTarget, mechanicLockedTarget);
        }

        private IEnumerator PushFlag(float seconds, Action add, Action remove)
        {
            add?.Invoke();
            RefreshDerivedState();

            if (seconds > 0f)
            {
                yield return new WaitForSeconds(seconds);
            }

            remove?.Invoke();
            invincibleStacks = Mathf.Max(0, invincibleStacks);
            invisibleStacks = Mathf.Max(0, invisibleStacks);
            RefreshDerivedState();
        }

        private IEnumerator PushMultiplier(List<float> multipliers, float multiplier, float seconds)
        {
            multipliers.Add(multiplier);
            RefreshDerivedState();

            if (seconds > 0f)
            {
                yield return new WaitForSeconds(seconds);
            }

            multipliers.Remove(multiplier);
            RefreshDerivedState();
        }

        private bool TargetInRange(Transform skillTarget, float range)
        {
            if (skillTarget == null)
            {
                return false;
            }

            Vector3 delta = skillTarget.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= range * range;
        }

        private bool HasSkillLineOfSight(MonsterSkillDefinition skill, Transform skillTarget)
        {
            if (skill == null || !skill.requireLineOfSight)
            {
                return true;
            }

            if (skillTarget == null)
            {
                return false;
            }

            return MonsterVisionUtility.IsTransformVisible(
                Camera.main,
                transform,
                skill.lineOfSightHeightOffset > 0f ? skill.lineOfSightHeightOffset : visibilitySampleHeight,
                skill.lineOfSightMask,
                skillTarget);
        }

        private bool CanReleaseSkill(MonsterSkillDefinition skill, Transform skillTarget)
        {
            if (skill == null || skillTarget == null || !IsTargetValid(skillTarget))
            {
                return false;
            }

            return TargetInRange(skillTarget, skill.castRange)
                && HasSkillLineOfSight(skill, skillTarget);
        }

        private void UpdateAstarMovement()
        {
            if (!CanUseAgent())
            {
                desiredDirection = Vector2.zero;
                velocity = Vector2.zero;
                movementState = MovementState.Idle;
                ClearMoveTracking();
                return;
            }

            if (!movementEnabled)
            {
                ClearAstarPath();
                ClearMoveTracking();
                desiredDirection = Vector2.zero;
                velocity = Vector2.zero;
                movementState = MovementState.Idle;
                return;
            }

            ConfigureAgent();

            Vector3 destination;
            bool hasDestination = TryResolveDestination(out destination);
            if (hasDestination)
            {
                SetAstarDestination(destination, !hasManualDestination || !HasAgentArrived());
            }
            else
            {
                ClearAstarPath();
            }

            Vector3 desired = astarAgent.desiredVelocity;
            desired.y = 0f;
            desiredDirection = desired.sqrMagnitude > 0.001f
                ? new Vector2(desired.x, desired.z).normalized
                : Vector2.zero;

            Vector3 agentVelocity = astarAgent.velocity;
            velocity = new Vector2(agentVelocity.x, agentVelocity.z);
            movementState = velocity.sqrMagnitude > 0.001f ? MovementState.Move : MovementState.Idle;
        }

        private bool TryResolveDestination(out Vector3 destination)
        {
            destination = default;

            // 行为树节点是唯一能下发移动目的地的入口；这里只消费节点已经写入的手动目的地。
            if (hasManualDestination)
            {
                destination = manualDestination;
                if (HasAgentArrived())
                {
                    hasManualDestination = false;
                    ClearMoveTracking();
                    return false;
                }

                return true;
            }

            return false;
        }

        private bool TryPickPatrolDestination(out Vector3 destination)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = homePosition + new Vector3(offset.x, 0f, offset.y);
            if (MonsterAstarNavigation.TryProjectReachable(
                    transform.position,
                    candidate,
                    ToMovementDefinition(),
                    out Vector3 sampledPosition))
            {
                patrolDestination = sampledPosition;
                hasPatrolDestination = true;
                destination = sampledPosition;
                return true;
            }

            hasPatrolDestination = false;
            destination = default;
            return false;
        }

        private void UpdateSpriteFacing()
        {
            if (!flipSpriteWithHorizontalMovement || spriteRenderer == null || Mathf.Abs(velocity.x) <= 0.001f)
            {
                return;
            }

            bool movingRight = velocity.x > 0f;
            spriteRenderer.flipX = spriteFacesRightByDefault ? !movingRight : movingRight;
        }

        private void UpdateMovementAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(moveXHash, desiredDirection.x);
            animator.SetFloat(moveZHash, desiredDirection.y);
            animator.SetFloat(speedHash, velocity.magnitude);
            animator.SetBool(isMovingHash, movementState == MovementState.Move);
            animator.SetInteger(movementStateHash, (int)movementState);
        }

        private void PlayWarning(MonsterSkillDefinition skill)
        {
            MonsterMechanicDefinition damageMechanic = FindDamageMechanic(skill);
            float radius = damageMechanic != null ? damageMechanic.radius : 1f;
            float heightOffset = damageMechanic != null ? damageMechanic.heightOffset : skill.warningHeightOffset;

            if (warningIndicator == null)
            {
                warningIndicator = AttackWarningIndicator.CreateRuntime("MonsterAttackWarning", null);
                ownsRuntimeWarningIndicator = true;
            }

            warningIndicator.PlayFollow(transform, Vector3.up * heightOffset, radius, Mathf.Max(0.01f, skill.windup));
        }

        private void HideWarning()
        {
            if (warningIndicator != null)
            {
                warningIndicator.Hide();
            }
        }

        private void TriggerAnimation()
        {
            if (animator == null || !animatorHasActiveTrigger)
            {
                return;
            }

            animator.ResetTrigger(activeTriggerHash);
            animator.SetTrigger(activeTriggerHash);
        }

        private void FinishCastCleanup()
        {
            bool wasCasting = casting;
            casting = false;
            activeSkill = null;
            activeTarget = null;
            lockedTarget = null;

            if (wasCasting && castStoppedMovement)
            {
                SetMovementEnabled(movementEnabledBeforeCast);
            }

            castStoppedMovement = false;
        }

        private void ClearStateModifiers()
        {
            invincibleStacks = 0;
            invisibleStacks = 0;
            scaleMultipliers.Clear();
            speedMultipliers.Clear();
            RefreshDerivedState();
        }

        private void RefreshDerivedState()
        {
            ScaleMultiplier = Product(scaleMultipliers);
            SpeedMultiplier = Product(speedMultipliers);
            ConfigureAgent();
            transform.localScale = baseScale * ScaleMultiplier;
            RefreshVisibility();
            Changed?.Invoke(this);
        }

        private void RefreshVisibility()
        {
            if (renderers == null)
            {
                return;
            }

            bool visible = !IsInvisible && (vitals == null || vitals.IsAlive);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible;
                }
            }
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

        private void CacheRuntimeReferences()
        {
            if (aiPath == null)
            {
                aiPath = GetComponent<AIPath>();
            }

            if (astarAgent == null && aiPath != null)
            {
                astarAgent = aiPath;
            }

            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            if (vitals == null)
            {
                vitals = GetComponent<CombatVitals>();
            }
        }

        private void CacheAnimatorParameter(string parameter)
        {
            activeTriggerHash = Animator.StringToHash(parameter);
            animatorHasActiveTrigger = false;
            if (animator == null || string.IsNullOrWhiteSpace(parameter))
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger
                    && parameters[i].nameHash == activeTriggerHash)
                {
                    animatorHasActiveTrigger = true;
                    return;
                }
            }
        }

        private void CacheMovementAnimatorHashes()
        {
            moveXHash = Animator.StringToHash(moveXParameter);
            moveZHash = Animator.StringToHash(moveZParameter);
            speedHash = Animator.StringToHash(speedParameter);
            isMovingHash = Animator.StringToHash(isMovingParameter);
            movementStateHash = Animator.StringToHash(movementStateParameter);
        }

        private void ConfigureAgent()
        {
            if (aiPath == null)
            {
                return;
            }

            aiPath.maxSpeed = moveSpeed * SpeedMultiplier;
            aiPath.maxAcceleration = Mathf.Max(0f, acceleration);
            aiPath.rotationSpeed = navMeshAgentAngularSpeed;
            aiPath.radius = navMeshAgentRadius;
            aiPath.height = navMeshAgentHeight;
            aiPath.endReachedDistance = Mathf.Max(stoppingDistance, patrolPointTolerance);
            aiPath.slowdownDistance = Mathf.Max(aiPath.endReachedDistance, stoppingDistance);
            aiPath.updateRotation = false;
            aiPath.gravity = Vector3.zero;
            aiPath.constrainInsideGraph = true;
            astarAgent = aiPath;
        }

        private void SetAstarDestination(Vector3 destination, bool searchImmediately)
        {
            if (astarAgent == null)
            {
                return;
            }

            bool destinationChanged = (astarAgent.destination - destination).sqrMagnitude > 0.04f;
            astarAgent.destination = destination;
            astarAgent.isStopped = false;
            if (searchImmediately && destinationChanged && !astarAgent.pathPending)
            {
                astarAgent.SearchPath();
            }
        }

        private void ClearAstarPath()
        {
            if (astarAgent != null)
            {
                astarAgent.isStopped = true;
                astarAgent.destination = transform.position;
                astarAgent.desiredVelocityWithoutLocalAvoidance = Vector3.zero;
            }

            if (aiPath != null)
            {
                aiPath.SetPath(null);
            }
        }

        private void BeginMoveTracking(Vector3 destination)
        {
            hasActiveMoveCommand = true;
            currentMoveDestination = destination;
            currentMoveStartedAt = Time.time;
            currentMoveLastProgressAt = Time.time;
            currentMoveLastRemainingDistance = float.PositiveInfinity;
        }

        private void ClearMoveTracking()
        {
            hasActiveMoveCommand = false;
            currentMoveDestination = default;
            currentMoveStartedAt = 0f;
            currentMoveLastProgressAt = 0f;
            currentMoveLastRemainingDistance = float.PositiveInfinity;
        }

        private void ResetMoveTracking()
        {
            ClearMoveTracking();
            patrolPauseRemaining = 0f;
        }

        private void UpdateMoveProgress(float minProgressDistance)
        {
            if (!hasActiveMoveCommand)
            {
                return;
            }

            float remainingDistance = CurrentRemainingDistance(currentMoveDestination);
            if (float.IsNaN(remainingDistance) || float.IsInfinity(remainingDistance))
            {
                return;
            }

            float requiredProgress = Mathf.Max(0.001f, minProgressDistance);
            if (float.IsInfinity(currentMoveLastRemainingDistance)
                || currentMoveLastRemainingDistance - remainingDistance >= requiredProgress)
            {
                currentMoveLastRemainingDistance = remainingDistance;
                currentMoveLastProgressAt = Time.time;
                return;
            }

            if (remainingDistance < currentMoveLastRemainingDistance)
            {
                currentMoveLastRemainingDistance = remainingDistance;
            }
        }

        private float CurrentRemainingDistance(Vector3 destination)
        {
            if (astarAgent != null
                && !float.IsNaN(astarAgent.remainingDistance)
                && !float.IsInfinity(astarAgent.remainingDistance))
            {
                return astarAgent.remainingDistance;
            }

            Vector3 flatDelta = destination - transform.position;
            flatDelta.y = 0f;
            return flatDelta.magnitude;
        }

        private bool CanUseAgent()
        {
            return aiPath != null
                && astarAgent != null
                && aiPath.enabled
                && MonsterAstarNavigation.HasActiveGraph;
        }

        private bool HasAgentArrived()
        {
            if (!CanUseAgent())
            {
                return false;
            }

            if (astarAgent.pathPending)
            {
                return false;
            }

            if (astarAgent.reachedDestination)
            {
                return true;
            }

            float tolerance = Mathf.Max(stoppingDistance, patrolPointTolerance, aiPath.endReachedDistance);
            if (!float.IsNaN(astarAgent.remainingDistance)
                && !float.IsInfinity(astarAgent.remainingDistance)
                && astarAgent.remainingDistance > tolerance)
            {
                return false;
            }

            Vector3 destination = hasManualDestination ? manualDestination : astarAgent.destination;
            Vector3 flatDelta = destination - transform.position;
            flatDelta.y = 0f;
            return flatDelta.sqrMagnitude <= tolerance * tolerance
                && astarAgent.velocity.sqrMagnitude <= 0.01f;
        }

        private void ConfigureCollider()
        {
            if (boxCollider != null)
            {
                // 大鱼的外形碰撞只用于受击/技能检测；移动通行按 AIPath.radius 的小核心计算。
                boxCollider.isTrigger = true;
            }

            if (!autoConfigureCollider || boxCollider == null || spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            Vector3 size = spriteRenderer.sprite.bounds.size;
            boxCollider.center = Vector3.zero;
            boxCollider.size = new Vector3(
                Mathf.Max(0.05f, size.x * colliderWidthScale),
                Mathf.Max(0.05f, size.y * colliderHeightScale),
                colliderDepth);
        }

        private static MonsterMechanicDefinition FindDamageMechanic(MonsterSkillDefinition skill)
        {
            if (skill == null || skill.mechanics == null)
            {
                return null;
            }

            for (int i = 0; i < skill.mechanics.Count; i++)
            {
                MonsterMechanicDefinition mechanic = skill.mechanics[i];
                if (mechanic != null && MonsterMechanicTypes.Parse(mechanic.type) == MonsterMechanicKind.DamageArea)
                {
                    return mechanic;
                }
            }

            return null;
        }

        private static IDamageable ResolveDamageable(Transform candidate)
        {
            return candidate != null ? candidate.GetComponentInParent<IDamageable>() : null;
        }

        private static bool IsTargetValid(Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            IDamageable damageable = candidate.GetComponentInParent<IDamageable>();
            return damageable == null || damageable.IsAlive && damageable.IsTargetable;
        }

        private static float Product(List<float> values)
        {
            float result = 1f;
            for (int i = 0; i < values.Count; i++)
            {
                result *= values[i];
            }

            return result;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(monsterId))
            {
                monsterId = "fish";
            }

            CacheRuntimeReferences();
            CacheMovementAnimatorHashes();
            ConfigureAgent();
            ConfigureCollider();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;

            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(center, detectionRadius);

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.3f);
            Gizmos.DrawWireSphere(homePosition == Vector3.zero ? center : homePosition, patrolRadius);
        }
    }
}
