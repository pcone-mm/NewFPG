using System;
using System.Collections.Generic;
using UnityEngine;
using NewFPG.Combat.SkillIndicators;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatResourcePool))]
    public sealed class PlayerWeaponCaster : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition[] weapons;
        [SerializeField] private WeaponInstanceData[] weaponInstances;
        [SerializeField] private List<WeaponModifier> activeModifiers = new List<WeaponModifier>();
        [SerializeField] private CombatResourcePool resourcePool;
        [SerializeField] private Transform castOrigin;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool allowKeyboardShortcuts = true;
        [SerializeField] private bool combatEnabled;

        private float[] nextCastTimes = Array.Empty<float>();
        private Transform runtimeCastOriginOverride;
        private CombatVitals ownerVitals;

        public event Action<WeaponDefinition, bool> CastAttempted;

        public int WeaponCount => HasWeaponInstances ? weaponInstances.Length : weapons == null ? 0 : weapons.Length;
        public bool CombatEnabled => combatEnabled;
        public Transform CastOrigin => runtimeCastOriginOverride != null
            ? runtimeCastOriginOverride
            : castOrigin != null ? castOrigin : transform;

        private bool HasWeaponInstances => weaponInstances != null && weaponInstances.Length > 0;

        private void Reset()
        {
            resourcePool = GetComponent<CombatResourcePool>();
            castOrigin = transform;
        }

        private void Awake()
        {
            if (resourcePool == null)
            {
                resourcePool = GetComponent<CombatResourcePool>();
            }

            if (castOrigin == null)
            {
                castOrigin = transform;
            }

            ownerVitals = GetComponent<CombatVitals>();
            EnsureCooldownCapacity();
        }

        private void Update()
        {
            if (!combatEnabled || !allowKeyboardShortcuts)
            {
                return;
            }

            int index = ReadWeaponShortcut();
            if (index >= 0)
            {
                TryCast(index);
            }
        }

        public WeaponDefinition GetWeapon(int index)
        {
            if (index < 0)
            {
                return null;
            }

            if (HasWeaponInstances)
            {
                if (index >= weaponInstances.Length)
                {
                    return null;
                }

                WeaponInstanceData instance = weaponInstances[index];
                if (instance == null)
                {
                    return null;
                }

                if (WeaponInventorySaveData.TryFindDefinition(weapons, instance.baseWeaponId, out WeaponDefinition instanceWeapon))
                {
                    return instanceWeapon;
                }

                Debug.LogWarning("PlayerWeaponCaster could not find weapon definition for id: " + instance.baseWeaponId, this);
                return null;
            }

            if (weapons == null || index >= weapons.Length)
            {
                return null;
            }

            return weapons[index];
        }

        public WeaponRuntimeStats GetRuntimeStats(int index)
        {
            WeaponDefinition weapon = GetWeapon(index);
            if (weapon == null)
            {
                return null;
            }

            return WeaponRuntimeResolver.Resolve(weapon, GetWeaponInstance(index), activeModifiers);
        }

        public void SetWeaponInstances(WeaponInstanceData[] instances)
        {
            weaponInstances = instances ?? Array.Empty<WeaponInstanceData>();
            EnsureCooldownCapacity();
        }

        public void SetActiveModifiers(IEnumerable<WeaponModifier> modifiers)
        {
            activeModifiers.Clear();
            if (modifiers == null)
            {
                return;
            }

            activeModifiers.AddRange(modifiers);
        }

        public void AddActiveModifier(WeaponModifier modifier)
        {
            if (modifier == null)
            {
                return;
            }

            activeModifiers.Add(modifier);
        }

        public bool RemoveActiveModifier(WeaponModifier modifier)
        {
            return modifier != null && activeModifiers.Remove(modifier);
        }

        public bool CanCast(int index)
        {
            WeaponRuntimeStats stats = GetRuntimeStats(index);
            if (stats == null || resourcePool == null)
            {
                return false;
            }

            return combatEnabled && Time.time >= GetNextCastTime(index) && resourcePool.CanSpend(stats.ResourceCost);
        }

        public bool TryCast(int index)
        {
            WeaponRuntimeStats stats = GetRuntimeStats(index);
            WeaponDefinition weapon = stats != null ? stats.Definition : GetWeapon(index);
            if (!combatEnabled || stats == null || resourcePool == null || Time.time < GetNextCastTime(index))
            {
                CastAttempted?.Invoke(weapon, false);
                return false;
            }

            if (!resourcePool.TrySpend(stats.ResourceCost))
            {
                CastAttempted?.Invoke(weapon, false);
                return false;
            }

            SetNextCastTime(index, Time.time + stats.Cooldown);
            ReleaseWeapon(stats);
            CastAttempted?.Invoke(weapon, true);
            return true;
        }

        public bool TryCast(int index, CastCommandData command)
        {
            WeaponRuntimeStats stats = GetRuntimeStats(index);
            WeaponDefinition weapon = stats != null ? stats.Definition : GetWeapon(index);
            if (!combatEnabled || stats == null || resourcePool == null || Time.time < GetNextCastTime(index) || !command.IsValid)
            {
                CastAttempted?.Invoke(weapon, false);
                return false;
            }

            if (!resourcePool.TrySpend(stats.ResourceCost))
            {
                CastAttempted?.Invoke(weapon, false);
                return false;
            }

            SetNextCastTime(index, Time.time + stats.Cooldown);
            ReleaseWeapon(stats, command);
            CastAttempted?.Invoke(weapon, true);
            return true;
        }

        public void SetCombatEnabled(bool enabled)
        {
            combatEnabled = enabled;
        }

        public void SetRuntimeCastOriginOverride(Transform origin)
        {
            runtimeCastOriginOverride = origin;
        }

        public float GetCooldownRatio(int index)
        {
            WeaponRuntimeStats stats = GetRuntimeStats(index);
            if (stats == null || stats.Cooldown <= 0f)
            {
                return 0f;
            }

            float remaining = Mathf.Max(0f, GetNextCastTime(index) - Time.time);
            return Mathf.Clamp01(remaining / stats.Cooldown);
        }

        private void ReleaseWeapon(WeaponRuntimeStats stats)
        {
            ReleaseWeaponAt(stats, CreateDefaultCastCommand(stats));
        }

        private void ReleaseWeapon(WeaponRuntimeStats stats, CastCommandData command)
        {
            ReleaseWeaponAt(stats, command);
        }

        private CastCommandData CreateDefaultCastCommand(WeaponRuntimeStats stats)
        {
            Vector3 origin = CastOrigin.position;
            Vector3 sceneOrigin = ResolveDefaultSceneOrigin(origin, stats);
            Vector3 shapeOrigin = stats.PlacementMode == SkillIndicatorPlacementMode.GroundSurface
                ? sceneOrigin
                : origin;
            Vector3 center = ResolveDefaultTargetPoint(shapeOrigin, stats);
            Vector3 direction = center - shapeOrigin;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = ResolveForward();
            }

            CastCommandData command = new CastCommandData
            {
                AbilityId = stats.WeaponId,
                Origin = origin,
                SceneOrigin = sceneOrigin,
                TargetPoint = center,
                SurfaceNormal = Vector3.up,
                PlacementMode = stats.PlacementMode,
                Direction = direction.normalized,
                ShapeType = stats.ShapeType,
                Radius = stats.Radius,
                Width = stats.Width,
                Length = stats.Length,
                Angle = stats.Angle,
                Height = stats.Height,
                GroundOffset = stats.GroundOffset,
                HasTargetPoint = true,
                IsValid = true,
            };
            return command;
        }

        private Vector3 ResolveDefaultSceneOrigin(Vector3 origin, WeaponRuntimeStats stats)
        {
            if (stats.PlacementMode != SkillIndicatorPlacementMode.GroundSurface)
            {
                return origin;
            }

            LayerMask surfaceMask = SkillIndicatorAimSolver.ResolveSceneSurfaceMask(stats.SurfaceMask);
            Vector3 rayOrigin = origin + Vector3.up * 0.5f;
            float maxDistance = Mathf.Max(8f, Mathf.Max(stats.Range, stats.Height) + 8f);
            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                maxDistance,
                surfaceMask,
                QueryTriggerInteraction.Collide);
            if (hits != null && hits.Length > 0)
            {
                Array.Sort(hits, CompareRaycastHitDistance);
                for (int i = 0; i < hits.Length; i++)
                {
                    Collider hitCollider = hits[i].collider;
                    if (hitCollider == null
                        || hitCollider.transform.IsChildOf(transform)
                        || hitCollider.GetComponentInParent<IDamageable>() != null)
                    {
                        continue;
                    }

                    return hits[i].point;
                }
            }

            return new Vector3(origin.x, 0f, origin.z);
        }

        private void ReleaseWeaponAt(WeaponRuntimeStats stats, CastCommandData command)
        {
            Vector3 center = command.HasTargetPoint ? command.TargetPoint : ResolveTargetCenter(command.Origin, stats);
            if (stats.ReleaseEffectPrefab != null)
            {
                Instantiate(stats.ReleaseEffectPrefab, center, Quaternion.identity);
            }

            if (ownerVitals != null && stats.Shield > 0f)
            {
                ownerVitals.AddShield(stats.Shield);
            }

            HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
            Collider[] hits = WeaponCastHitResolver.QueryCandidates(command, targetMask);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!WeaponCastHitResolver.TryResolveHit(command, hits[i], targetMask, transform, out WeaponCastHitResult hit)
                    || !damagedTargets.Add(hit.Damageable))
                {
                    continue;
                }

                hit.Damageable.ReceiveDamage(new DamagePayload(stats.RuntimeTotalDamage, gameObject, hit.HitPoint));
                if (stats.HitEffectPrefab != null)
                {
                    Instantiate(stats.HitEffectPrefab, hit.HitPoint, Quaternion.identity);
                }
            }
        }

        private Vector3 ResolveTargetCenter(Vector3 origin, WeaponRuntimeStats stats)
        {
            IDamageable nearest = null;
            float nearestDistanceSqr = float.MaxValue;
            float searchRadius = ResolveAutoSelectSearchRadius(stats);
            Collider[] candidates = Physics.OverlapSphere(origin, searchRadius, targetMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == null || candidates[i].transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = candidates[i].GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || !damageable.IsTargetable)
                {
                    continue;
                }

                Vector3 aimPoint = damageable.AimTransform != null ? damageable.AimTransform.position : candidates[i].bounds.center;
                float distanceSqr = (aimPoint - origin).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearest = damageable;
                    nearestDistanceSqr = distanceSqr;
                }
            }

            if (nearest != null && nearest.AimTransform != null)
            {
                return nearest.AimTransform.position;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = Vector3.forward;
            }

            return origin + forward.normalized * Mathf.Min(stats.Range, 2.5f);
        }

        private static float ResolveAutoSelectSearchRadius(WeaponRuntimeStats stats)
        {
            switch (stats.ShapeType)
            {
                case SkillIndicatorShapeType.Line:
                case SkillIndicatorShapeType.Rectangle:
                case SkillIndicatorShapeType.Cone:
                    return Mathf.Max(0.1f, stats.Length);
                default:
                    return Mathf.Max(0.1f, stats.Range);
            }
        }

        private static int CompareRaycastHitDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }

        private Vector3 ResolveDefaultTargetPoint(Vector3 origin, WeaponRuntimeStats stats)
        {
            switch (stats.TapPolicy)
            {
                case SkillIndicatorDefaultReleasePolicy.CastOnSelf:
                    return origin;
                case SkillIndicatorDefaultReleasePolicy.CastForwardMaxRange:
                case SkillIndicatorDefaultReleasePolicy.CastAtGroundUnderCrosshair:
                case SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit:
                    return origin + ResolveForward() * stats.Range;
                default:
                    return ResolveTargetCenter(origin, stats);
            }
        }

        private Vector3 ResolveForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private float GetNextCastTime(int index)
        {
            return index >= 0 && index < nextCastTimes.Length ? nextCastTimes[index] : 0f;
        }

        private void SetNextCastTime(int index, float time)
        {
            if (index < 0)
            {
                return;
            }

            if (index >= nextCastTimes.Length)
            {
                Array.Resize(ref nextCastTimes, index + 1);
            }

            nextCastTimes[index] = time;
        }

        private void EnsureCooldownCapacity()
        {
            int weaponCount = WeaponCount;
            if (nextCastTimes == null || nextCastTimes.Length < weaponCount)
            {
                Array.Resize(ref nextCastTimes, weaponCount);
            }
        }

        private WeaponInstanceData GetWeaponInstance(int index)
        {
            if (!HasWeaponInstances || index < 0 || index >= weaponInstances.Length)
            {
                return null;
            }

            return weaponInstances[index];
        }

        private static int ReadWeaponShortcut()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return -1;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                return 0;
            }

            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                return 1;
            }

            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                return 2;
            }

            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            {
                return 3;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                return 0;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                return 1;
            }

            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                return 2;
            }

            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
            {
                return 3;
            }
#endif

            return -1;
        }
    }
}
