using UnityEngine;

namespace NewFPG.Level
{
    public sealed class LevelProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 18f;
        [SerializeField, Min(0.1f)] private float damage = 40f;
        [SerializeField, Min(0.02f)] private float hitRadius = 0.35f;
        [SerializeField, Min(0.1f)] private float lifetime = 3f;
        [SerializeField] private GameObject hitEffectPrefab;

        private Transform target;
        private Vector3 direction;
        private float expireAt;
        private bool initialized;

        public void Initialize(Vector3 spawnPosition, Vector3 initialDirection, Transform targetTransform, float projectileDamage, float projectileSpeed, GameObject effectPrefab)
        {
            transform.position = spawnPosition;
            direction = initialDirection.sqrMagnitude > 0.001f ? initialDirection.normalized : Vector3.forward;
            target = targetTransform;
            damage = Mathf.Max(0.1f, projectileDamage);
            speed = Mathf.Max(0.1f, projectileSpeed);
            hitEffectPrefab = effectPrefab;
            expireAt = Time.time + lifetime;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
            {
                expireAt = Time.time + lifetime;
                initialized = true;
            }

            if (Time.time >= expireAt)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 previous = transform.position;
            Vector3 currentDirection = ResolveDirection();
            Vector3 next = previous + currentDirection * (speed * Time.deltaTime);

            if (TryHitAlongSegment(previous, next, out LevelCombatant combatant, out Vector3 hitPoint))
            {
                Hit(combatant, hitPoint);
                return;
            }

            transform.position = next;
            if (currentDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(currentDirection, Vector3.up);
            }
        }

        private Vector3 ResolveDirection()
        {
            if (target == null)
            {
                return direction;
            }

            LevelCombatant combatant = target.GetComponentInParent<LevelCombatant>();
            if (combatant != null && combatant.IsDead)
            {
                target = null;
                return direction;
            }

            Vector3 toTarget = TargetPoint(target) - transform.position;
            if (toTarget.sqrMagnitude <= hitRadius * hitRadius)
            {
                return toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : direction;
            }

            direction = toTarget.normalized;
            return direction;
        }

        private bool TryHitAlongSegment(Vector3 start, Vector3 end, out LevelCombatant combatant, out Vector3 hitPoint)
        {
            combatant = null;
            hitPoint = end;

            Vector3 segment = end - start;
            float distance = segment.magnitude;
            if (distance <= 0.001f)
            {
                return false;
            }

            RaycastHit[] hits = Physics.SphereCastAll(start, hitRadius, segment / distance, distance, ~0, QueryTriggerInteraction.Collide);
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                LevelCombatant hitCombatant = hit.collider.GetComponentInParent<LevelCombatant>();
                if (hitCombatant == null || hitCombatant.IsDead || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                combatant = hitCombatant;
                hitPoint = hit.point;
            }

            if (combatant != null)
            {
                return true;
            }

            if (target != null)
            {
                LevelCombatant targetCombatant = target.GetComponentInParent<LevelCombatant>();
                if (targetCombatant != null && !targetCombatant.IsDead && Vector3.Distance(end, TargetPoint(target)) <= hitRadius)
                {
                    combatant = targetCombatant;
                    hitPoint = TargetPoint(target);
                    return true;
                }
            }

            return false;
        }

        private void Hit(LevelCombatant combatant, Vector3 hitPoint)
        {
            combatant.ApplyDamage(damage, hitPoint, gameObject);
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, hitPoint, Quaternion.identity);
            }

            Destroy(gameObject);
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
