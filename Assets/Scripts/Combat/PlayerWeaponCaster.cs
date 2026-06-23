using System;
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
        [SerializeField] private CombatResourcePool resourcePool;
        [SerializeField] private Transform castOrigin;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private bool allowKeyboardShortcuts = true;
        [SerializeField] private bool combatEnabled;

        private float[] nextCastTimes = Array.Empty<float>();
        private Transform runtimeCastOriginOverride;

        public event Action<WeaponDefinition, bool> CastAttempted;

        public int WeaponCount => weapons == null ? 0 : weapons.Length;
        public bool CombatEnabled => combatEnabled;
        public Transform CastOrigin => runtimeCastOriginOverride != null
            ? runtimeCastOriginOverride
            : castOrigin != null ? castOrigin : transform;

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
            if (weapons == null || index < 0 || index >= weapons.Length)
            {
                return null;
            }

            return weapons[index];
        }

        public bool CanCast(int index)
        {
            WeaponDefinition weapon = GetWeapon(index);
            if (weapon == null || resourcePool == null)
            {
                return false;
            }

            return combatEnabled && Time.time >= GetNextCastTime(index) && resourcePool.CanSpend(weapon.ResourceCost);
        }

        public bool TryCast(int index)
        {
            WeaponDefinition weapon = GetWeapon(index);
            if (!combatEnabled || weapon == null || resourcePool == null || Time.time < GetNextCastTime(index))
            {
                CastAttempted?.Invoke(weapon, false);
                return false;
            }

            if (!resourcePool.TrySpend(weapon.ResourceCost))
            {
                CastAttempted?.Invoke(weapon, false);
                return false;
            }

            SetNextCastTime(index, Time.time + weapon.Cooldown);
            ReleaseWeapon(weapon);
            CastAttempted?.Invoke(weapon, true);
            return true;
        }

        public bool TryCast(int index, CastCommandData command)
        {
            WeaponDefinition weapon = GetWeapon(index);
            if (!combatEnabled || weapon == null || resourcePool == null || Time.time < GetNextCastTime(index) || !command.IsValid)
            {
                CastAttempted?.Invoke(weapon, false);
                return false;
            }

            if (!resourcePool.TrySpend(weapon.ResourceCost))
            {
                CastAttempted?.Invoke(weapon, false);
                return false;
            }

            SetNextCastTime(index, Time.time + weapon.Cooldown);
            ReleaseWeapon(weapon, command);
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
            WeaponDefinition weapon = GetWeapon(index);
            if (weapon == null || weapon.Cooldown <= 0f)
            {
                return 0f;
            }

            float remaining = Mathf.Max(0f, GetNextCastTime(index) - Time.time);
            return Mathf.Clamp01(remaining / weapon.Cooldown);
        }

        private void ReleaseWeapon(WeaponDefinition weapon)
        {
            Vector3 origin = CastOrigin.position;
            Vector3 center = ResolveTargetCenter(origin, weapon);
            ReleaseWeaponAt(weapon, center);
        }

        private void ReleaseWeapon(WeaponDefinition weapon, CastCommandData command)
        {
            Vector3 center = command.HasTargetPoint ? command.TargetPoint : ResolveTargetCenter(command.Origin, weapon);
            ReleaseWeaponAt(weapon, center);
        }

        private void ReleaseWeaponAt(WeaponDefinition weapon, Vector3 center)
        {
            if (weapon.ReleaseEffectPrefab != null)
            {
                Instantiate(weapon.ReleaseEffectPrefab, center, Quaternion.identity);
            }

            Collider[] hits = Physics.OverlapSphere(center, weapon.Radius, targetMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null || hits[i].transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                {
                    continue;
                }

                Vector3 hitPoint = hits[i].ClosestPoint(center);
                damageable.ReceiveDamage(new DamagePayload(weapon.Damage, gameObject, hitPoint));
                if (weapon.HitEffectPrefab != null)
                {
                    Instantiate(weapon.HitEffectPrefab, hitPoint, Quaternion.identity);
                }
            }
        }

        private Vector3 ResolveTargetCenter(Vector3 origin, WeaponDefinition weapon)
        {
            IDamageable nearest = null;
            float nearestDistanceSqr = float.MaxValue;
            Collider[] candidates = Physics.OverlapSphere(origin, weapon.Range, targetMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == null || candidates[i].transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = candidates[i].GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
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

            return origin + forward.normalized * Mathf.Min(weapon.Range, 2.5f);
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
