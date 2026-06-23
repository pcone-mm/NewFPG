using System.Collections.Generic;
using UnityEngine;
using NewFPG.Monsters;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FishMonsterController))]
    public sealed class FishAttackController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Min(0.1f)] private float attackRange = 2f;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float requestInterval = 2f;
        [SerializeField, Min(0.05f)] private float attackPrepareTime = 0.8f;
        [SerializeField, Min(0f)] private float damage = 12f;
        [SerializeField, Min(0.05f)] private float damageRadius = 1.15f;
        [SerializeField] private float warningHeightOffset = 1.2f;
        [SerializeField] private LayerMask targetMask = ~0;

        [Header("References")]
        [SerializeField] private FishMonsterController movement;
        [SerializeField] private Animator animator;
        [SerializeField] private AttackWarningIndicator warningIndicator;
        [SerializeField] private string attackTriggerParameter = "Attack";

        private float nextRequestAt;
        private bool preparing;
        private Vector3 attackCenter;
        private float attackAt;
        private int attackTriggerHash;
        private bool animatorHasAttackTrigger;
        private IDamageable lockedTargetDamageable;
        private bool ownsRuntimeWarningIndicator;
        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        public float AttackPrepareTime
        {
            get => attackPrepareTime;
            set => attackPrepareTime = Mathf.Max(0.05f, value);
        }

        public Transform Target
        {
            get => target != null ? target : movement != null ? movement.Target : null;
            set => target = value;
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            CacheAnimatorParameter();
        }

        private void OnEnable()
        {
            nextRequestAt = Time.time + requestInterval;
            preparing = false;
            lockedTargetDamageable = null;
            damagedTargets.Clear();
        }

        private void OnDisable()
        {
            bool wasPreparing = preparing;
            preparing = false;
            lockedTargetDamageable = null;
            damagedTargets.Clear();

            if (wasPreparing && movement != null)
            {
                movement.SetMovementEnabled(true);
            }

            if (warningIndicator != null)
            {
                warningIndicator.Hide();
            }
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

        private void OnValidate()
        {
            requestInterval = Mathf.Max(0f, requestInterval);
            attackPrepareTime = Mathf.Max(0.05f, attackPrepareTime);
            attackRange = Mathf.Max(0.1f, attackRange);
            damageRadius = Mathf.Max(0.05f, damageRadius);
            damage = Mathf.Max(0f, damage);
            CacheReferences();
            CacheAnimatorParameter();
        }

        private void Update()
        {
            if (target == null)
            {
                TryUseMovementTarget();
            }

            if (target == null && autoFindPlayer)
            {
                ResolveTarget();
            }

            if (preparing)
            {
                if (Time.time >= attackAt)
                {
                    CompleteAttack();
                }

                return;
            }

            if (Time.time >= nextRequestAt)
            {
                RequestAttack();
            }
        }

        public void RequestAttack()
        {
            nextRequestAt = Time.time + requestInterval;
            if (target == null)
            {
                TryUseMovementTarget();
            }

            if (target == null && autoFindPlayer)
            {
                ResolveTarget();
            }

            if (target == null || !TargetInRange())
            {
                return;
            }

            preparing = true;
            lockedTargetDamageable = ResolveDamageable(target);
            attackCenter = ResolveAttackCenter();
            attackAt = Time.time + attackPrepareTime;

            if (movement != null)
            {
                movement.SetMovementEnabled(false);
            }

            if (warningIndicator == null)
            {
                warningIndicator = AttackWarningIndicator.CreateRuntime("FishAttackWarning", null);
                ownsRuntimeWarningIndicator = true;
            }

            warningIndicator.PlayFollow(transform, WarningFollowOffset(), damageRadius, attackPrepareTime);
        }

        private void CompleteAttack()
        {
            preparing = false;
            nextRequestAt = Time.time + requestInterval;

            if (animator != null && animatorHasAttackTrigger)
            {
                animator.ResetTrigger(attackTriggerHash);
                animator.SetTrigger(attackTriggerHash);
            }

            damagedTargets.Clear();
            attackCenter = ResolveAttackCenter();
            TryDamageLockedTarget();

            Collider[] hits = Physics.OverlapSphere(attackCenter, damageRadius, targetMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null || hits[i].transform.IsChildOf(transform))
                {
                    continue;
                }

                IDamageable damageable = hits[i].GetComponentInParent<IDamageable>();
                Vector3 hitPoint = hits[i].ClosestPoint(attackCenter);
                TryApplyDamage(damageable, hitPoint, hitPoint);
            }

            damagedTargets.Clear();
            lockedTargetDamageable = null;

            if (movement != null)
            {
                movement.SetMovementEnabled(true);
            }
        }

        private void TryDamageLockedTarget()
        {
            if (lockedTargetDamageable == null && target != null)
            {
                lockedTargetDamageable = ResolveDamageable(target);
            }

            if (!IsDamageableValid(lockedTargetDamageable))
            {
                return;
            }

            Transform aimTransform = lockedTargetDamageable.AimTransform != null ? lockedTargetDamageable.AimTransform : target;
            Vector3 samplePosition = aimTransform != null ? aimTransform.position : attackCenter;
            Vector3 hitPoint = samplePosition;
            hitPoint.y = attackCenter.y;
            TryApplyDamage(lockedTargetDamageable, samplePosition, hitPoint);
        }

        private bool TryApplyDamage(IDamageable damageable, Vector3 samplePosition, Vector3 hitPoint)
        {
            if (!IsDamageableValid(damageable)
                || !IsPointInsideDamageRadius(samplePosition)
                || !damagedTargets.Add(damageable))
            {
                return false;
            }

            damageable.ReceiveDamage(new DamagePayload(damage, gameObject, hitPoint));
            return true;
        }

        private bool IsPointInsideDamageRadius(Vector3 samplePosition)
        {
            Vector3 delta = samplePosition - attackCenter;
            delta.y = 0f;
            return delta.sqrMagnitude <= damageRadius * damageRadius;
        }

        private Vector3 ResolveAttackCenter()
        {
            return transform.position + WarningFollowOffset();
        }

        private Vector3 WarningFollowOffset()
        {
            return Vector3.up * warningHeightOffset;
        }

        private bool TargetInRange()
        {
            if (target == null)
            {
                return false;
            }

            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= attackRange * attackRange;
        }

        private void ResolveTarget()
        {
            if (TryUseMovementTarget())
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(playerTag))
            {
                GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null)
                {
                    target = tagged.transform;
                    return;
                }
            }

            CombatVitals[] vitals = FindObjectsByType<CombatVitals>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < vitals.Length; i++)
            {
                if (vitals[i] != null && !vitals[i].transform.IsChildOf(transform))
                {
                    target = vitals[i].transform;
                    return;
                }
            }

            target = null;
        }

        private bool TryUseMovementTarget()
        {
            if (movement == null)
            {
                movement = GetComponent<FishMonsterController>();
            }

            Transform movementTarget = movement != null ? movement.Target : null;
            if (movementTarget == null || movementTarget.IsChildOf(transform))
            {
                return false;
            }

            target = movementTarget;
            return true;
        }

        private static IDamageable ResolveDamageable(Transform candidate)
        {
            return candidate != null ? candidate.GetComponentInParent<IDamageable>() : null;
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

            return damageable.IsAlive;
        }

        private void CacheReferences()
        {
            if (movement == null)
            {
                movement = GetComponent<FishMonsterController>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void CacheAnimatorParameter()
        {
            attackTriggerHash = Animator.StringToHash(attackTriggerParameter);
            animatorHasAttackTrigger = false;
            if (animator == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger
                    && parameters[i].nameHash == attackTriggerHash)
                {
                    animatorHasAttackTrigger = true;
                    return;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.22f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(attackCenter == Vector3.zero ? transform.position : attackCenter, damageRadius);
        }
    }
}
