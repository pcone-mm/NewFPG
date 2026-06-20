using UnityEngine;
using NewFPG.Prototype;

namespace NewFPG.Level
{
    [DisallowMultipleComponent]
    public sealed class LevelWeaponProjectileShooter : MonoBehaviour
    {
        [SerializeField] private PrototypeFirstPersonWeaponView weaponView;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform fallbackMuzzle;
        [SerializeField] private LevelProjectile projectilePrefab;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField, Min(1f)] private float damage = 40f;
        [SerializeField, Min(0.1f)] private float damageMultiplier = 1f;
        [SerializeField, Min(1f)] private float projectileSpeed = 22f;
        [SerializeField, Min(0.1f)] private float aimDistance = 80f;
        [SerializeField] private bool requireActiveCombat = true;
        [SerializeField] private LevelFlowDirector flowDirector;

        private GameObject runtimeProjectilePrefab;

        public bool IsEnabledByCombat { get; set; } = true;

        public void SetDamageMultiplier(float multiplier)
        {
            damageMultiplier = Mathf.Max(0.1f, multiplier);
        }

        private void Reset()
        {
            weaponView = GetComponent<PrototypeFirstPersonWeaponView>();
            aimCamera = Camera.main;
        }

        private void Awake()
        {
            if (weaponView == null)
            {
                weaponView = GetComponent<PrototypeFirstPersonWeaponView>();
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (weaponView != null)
            {
                weaponView.WeaponAttackStarted += OnWeaponAttackStarted;
            }
        }

        private void OnDisable()
        {
            if (weaponView != null)
            {
                weaponView.WeaponAttackStarted -= OnWeaponAttackStarted;
            }
        }

        public void SetFlowDirector(LevelFlowDirector director)
        {
            flowDirector = director;
        }

        public void SetAimCamera(Camera camera)
        {
            if (camera != null)
            {
                aimCamera = camera;
            }
        }

        private void OnWeaponAttackStarted(PrototypeFirstPersonWeaponView.WeaponAttackContext context)
        {
            TryFire(context.weaponTransform, context.hitTransform);
        }

        private bool TryFire(Transform weaponTransform, Transform hitTransform)
        {
            if (!IsEnabledByCombat)
            {
                return false;
            }

            if (requireActiveCombat && flowDirector != null && !flowDirector.IsInCombat)
            {
                return false;
            }

            Camera cameraForAim = aimCamera != null ? aimCamera : Camera.main;
            Vector3 spawnPosition = ResolveSpawnPosition(weaponTransform, hitTransform);
            Vector3 direction = ResolveAimDirection(cameraForAim, spawnPosition, out Transform target);
            LevelProjectile projectile = CreateProjectile(spawnPosition, direction);
            projectile.Initialize(spawnPosition, direction, target, damage * damageMultiplier, projectileSpeed, hitEffectPrefab);
            return true;
        }

        private Vector3 ResolveSpawnPosition(Transform weaponTransform, Transform hitTransform)
        {
            if (hitTransform != null)
            {
                return hitTransform.position;
            }

            if (fallbackMuzzle != null)
            {
                return fallbackMuzzle.position;
            }

            Camera cameraForAim = aimCamera != null ? aimCamera : Camera.main;
            if (cameraForAim != null)
            {
                return cameraForAim.transform.position + cameraForAim.transform.forward * 1.2f + Vector3.down * 0.35f;
            }

            return weaponTransform != null ? weaponTransform.position : transform.position;
        }

        private Vector3 ResolveAimDirection(Camera cameraForAim, Vector3 spawnPosition, out Transform target)
        {
            target = null;
            if (cameraForAim != null)
            {
                Ray ray = cameraForAim.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, aimDistance, ~0, QueryTriggerInteraction.Collide))
                {
                    LevelCombatant combatant = hit.collider.GetComponentInParent<LevelCombatant>();
                    if (combatant != null && !combatant.IsDead)
                    {
                        target = combatant.transform;
                        return (hit.point - spawnPosition).normalized;
                    }
                }
            }

            LevelCombatant nearest = FindNearestCombatant(spawnPosition);
            if (nearest != null)
            {
                target = nearest.transform;
                return (TargetPoint(nearest.transform) - spawnPosition).normalized;
            }

            if (cameraForAim != null)
            {
                return cameraForAim.transform.forward;
            }

            return transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.forward;
        }

        private LevelCombatant FindNearestCombatant(Vector3 origin)
        {
            LevelCombatant[] combatants = FindObjectsByType<LevelCombatant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            LevelCombatant nearest = null;
            float nearestDistanceSqr = float.MaxValue;
            for (int i = 0; i < combatants.Length; i++)
            {
                LevelCombatant combatant = combatants[i];
                if (combatant == null || combatant.IsDead)
                {
                    continue;
                }

                float distanceSqr = (TargetPoint(combatant.transform) - origin).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearest = combatant;
                    nearestDistanceSqr = distanceSqr;
                }
            }

            return nearest;
        }

        private LevelProjectile CreateProjectile(Vector3 spawnPosition, Vector3 direction)
        {
            if (projectilePrefab != null)
            {
                return Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
            }

            if (runtimeProjectilePrefab == null)
            {
                runtimeProjectilePrefab = BuildRuntimeProjectilePrefab();
            }

            GameObject projectileObject = Instantiate(runtimeProjectilePrefab, spawnPosition, Quaternion.LookRotation(direction, Vector3.up));
            projectileObject.name = "LevelWeaponProjectile";
            projectileObject.SetActive(true);
            return projectileObject.GetComponent<LevelProjectile>();
        }

        private static GameObject BuildRuntimeProjectilePrefab()
        {
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "RuntimeLevelWeaponProjectilePrefab";
            projectileObject.transform.localScale = Vector3.one * 0.16f;
            Collider collider = projectileObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            Renderer renderer = projectileObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                material.color = new Color(0.38f, 0.82f, 1f, 1f);
                renderer.sharedMaterial = material;
            }

            Rigidbody body = projectileObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            projectileObject.AddComponent<LevelProjectile>();
            projectileObject.SetActive(false);
            return projectileObject;
        }

        private static Vector3 TargetPoint(Transform targetTransform)
        {
            Collider collider = targetTransform.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                return collider.bounds.center;
            }

            Renderer renderer = targetTransform.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds.center;
            }

            return targetTransform.position + Vector3.up;
        }
    }
}
