using System;
using UnityEngine;
using NewFPG.Monsters;
using NewFPG.Combat;

namespace NewFPG.Level
{
    [DisallowMultipleComponent]
    public sealed class LevelCombatant : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHp = 80f;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField, Min(0f)] private float deathDelay = 0.35f;

        [Header("Hit Feedback")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hitTriggerParameter = "Hit";
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite hitSprite;
        [SerializeField, Min(0.02f)] private float hitFlashDuration = 0.18f;
        [SerializeField] private Color hitTint = new Color(1f, 0.68f, 0.5f, 1f);

        [Header("Movement")]
        [SerializeField] private FishMonsterController fishMonsterController;
        [SerializeField] private bool stopMovementOnDeath = true;
        [SerializeField] private CombatVitals combatVitals;

        private Sprite defaultSprite;
        private Color defaultColor = Color.white;
        private float hp;
        private float hitFlashRemaining;
        private bool isDead;
        private int hitTriggerHash;
        private bool animatorHasHitTrigger;
        private bool forwardingToVitals;

        public event Action<LevelCombatant> Damaged;
        public event Action<LevelCombatant> Died;

        public float Hp => combatVitals != null ? combatVitals.CurrentHealth : hp;
        public float MaxHp => combatVitals != null ? combatVitals.MaxHealth : maxHp;
        public bool IsDead => combatVitals != null ? !combatVitals.IsAlive : isDead;

        public void SetHitSprite(Sprite sprite)
        {
            hitSprite = sprite;
        }

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            hp = maxHp;
            if (spriteRenderer != null)
            {
                defaultSprite = spriteRenderer.sprite;
                defaultColor = spriteRenderer.color;
            }

            CacheAnimatorHitParameter();
        }

        private void OnEnable()
        {
            if (combatVitals != null)
            {
                combatVitals.Damaged += OnCombatVitalsDamaged;
                combatVitals.Died += OnCombatVitalsDied;
            }

            if (hp <= 0f)
            {
                hp = maxHp;
            }
        }

        private void OnDisable()
        {
            if (combatVitals != null)
            {
                combatVitals.Damaged -= OnCombatVitalsDamaged;
                combatVitals.Died -= OnCombatVitalsDied;
            }
        }

        private void OnValidate()
        {
            maxHp = Mathf.Max(1f, maxHp);
            hitFlashDuration = Mathf.Max(0.02f, hitFlashDuration);
            CacheReferences();
        }

        private void Update()
        {
            if (hitFlashRemaining <= 0f || spriteRenderer == null)
            {
                return;
            }

            hitFlashRemaining -= Time.deltaTime;
            if (hitFlashRemaining <= 0f && !isDead)
            {
                spriteRenderer.sprite = defaultSprite != null ? defaultSprite : spriteRenderer.sprite;
                spriteRenderer.color = defaultColor;
            }
        }

        public void ApplyDamage(float amount, Vector3 hitPoint, GameObject source = null)
        {
            if (combatVitals != null && !forwardingToVitals)
            {
                combatVitals.ReceiveDamage(new DamagePayload(amount, source, hitPoint));
                return;
            }

            if (isDead || amount <= 0f)
            {
                return;
            }

            hp = Mathf.Max(0f, hp - amount);
            PlayHitFeedback();
            Damaged?.Invoke(this);

            if (hp <= 0f)
            {
                Die();
            }
        }

        public void ResetHp(float nextMaxHp)
        {
            maxHp = Mathf.Max(1f, nextMaxHp);
            if (combatVitals != null)
            {
                combatVitals.SetMaxHealth(maxHp, true);
                isDead = false;
            }

            hp = maxHp;
            isDead = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.sprite = defaultSprite != null ? defaultSprite : spriteRenderer.sprite;
                spriteRenderer.color = defaultColor;
            }

            if (fishMonsterController != null)
            {
                fishMonsterController.SetMovementEnabled(true);
            }
        }

        private void PlayHitFeedback()
        {
            if (animator != null && animatorHasHitTrigger)
            {
                animator.ResetTrigger(hitTriggerHash);
                animator.SetTrigger(hitTriggerHash);
            }

            if (spriteRenderer == null)
            {
                return;
            }

            if (defaultSprite == null)
            {
                defaultSprite = spriteRenderer.sprite;
            }

            defaultColor = spriteRenderer.color;
            if (hitSprite != null)
            {
                spriteRenderer.sprite = hitSprite;
            }

            spriteRenderer.color = hitTint;
            hitFlashRemaining = hitFlashDuration;
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            if (stopMovementOnDeath && fishMonsterController != null)
            {
                fishMonsterController.SetMovementEnabled(false);
            }

            Died?.Invoke(this);

            if (destroyOnDeath)
            {
                Destroy(gameObject, deathDelay);
            }
            else if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
        }

        private void CacheReferences()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (fishMonsterController == null)
            {
                fishMonsterController = GetComponent<FishMonsterController>();
            }

            if (combatVitals == null)
            {
                combatVitals = GetComponent<CombatVitals>();
            }
        }

        private void OnCombatVitalsDamaged(CombatVitals vitals, DamagePayload payload)
        {
            forwardingToVitals = true;
            hp = vitals.CurrentHealth;
            Damaged?.Invoke(this);
            forwardingToVitals = false;
        }

        private void OnCombatVitalsDied(CombatVitals vitals)
        {
            hp = 0f;
            Die();
        }

        private void CacheAnimatorHitParameter()
        {
            hitTriggerHash = Animator.StringToHash(hitTriggerParameter);
            animatorHasHitTrigger = false;
            if (animator == null || animator.parameters == null)
            {
                return;
            }

            for (int i = 0; i < animator.parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = animator.parameters[i];
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.nameHash == hitTriggerHash)
                {
                    animatorHasHitTrigger = true;
                    return;
                }
            }
        }
    }
}
