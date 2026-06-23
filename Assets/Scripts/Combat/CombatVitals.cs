using System;
using UnityEngine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatVitals : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0f)] private float startingHealth = 100f;

        [Header("Shield")]
        [SerializeField, Min(0f)] private float maxShield = 50f;
        [SerializeField, Min(0f)] private float startingShield;

        [Header("Death")]
        [SerializeField] private bool destroyOnDeath;
        [SerializeField, Min(0f)] private float deathDelay = 0.2f;
        [SerializeField] private Behaviour[] disableOnDeath;

        [Header("Feedback")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hitTriggerParameter = "Hit";
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitTint = new Color(1f, 0.65f, 0.55f, 1f);
        [SerializeField, Min(0.02f)] private float hitTintSeconds = 0.12f;

        private float currentHealth;
        private float currentShield;
        private bool dead;
        private Color defaultColor = Color.white;
        private float tintRemaining;
        private int hitTriggerHash;
        private bool animatorHasHitTrigger;

        public event Action<CombatVitals> Changed;
        public event Action<CombatVitals, DamagePayload> Damaged;
        public event Action<CombatVitals> Died;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthRatio => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        public float CurrentShield => currentShield;
        public float MaxShield => maxShield;
        public float ShieldRatio => maxShield <= 0f ? 0f : currentShield / maxShield;
        public bool IsAlive => !dead && currentHealth > 0f;
        public Transform AimTransform => transform;

        private void Reset()
        {
            CacheReferences();
            startingHealth = maxHealth;
        }

        private void Awake()
        {
            CacheReferences();
            CacheAnimatorParameter();
            defaultColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            ResetVitals();
        }

        private void OnEnable()
        {
            if (currentHealth <= 0f)
            {
                ResetVitals();
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            startingHealth = Mathf.Clamp(startingHealth <= 0f ? maxHealth : startingHealth, 1f, maxHealth);
            maxShield = Mathf.Max(0f, maxShield);
            startingShield = Mathf.Clamp(startingShield, 0f, maxShield);
            hitTintSeconds = Mathf.Max(0.02f, hitTintSeconds);
            CacheReferences();
            CacheAnimatorParameter();
        }

        private void Update()
        {
            if (tintRemaining <= 0f || spriteRenderer == null)
            {
                return;
            }

            tintRemaining -= Time.deltaTime;
            if (tintRemaining <= 0f)
            {
                spriteRenderer.color = defaultColor;
            }
        }

        public void ResetVitals()
        {
            dead = false;
            currentHealth = Mathf.Clamp(startingHealth <= 0f ? maxHealth : startingHealth, 1f, maxHealth);
            currentShield = Mathf.Clamp(startingShield, 0f, maxShield);

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = defaultColor;
            }

            SetDeathBehavioursEnabled(true);
            Changed?.Invoke(this);
        }

        public void SetMaxHealth(float value, bool fill)
        {
            maxHealth = Mathf.Max(1f, value);
            if (fill)
            {
                dead = false;
                SetDeathBehavioursEnabled(true);
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = true;
                    spriteRenderer.color = defaultColor;
                }
            }

            currentHealth = fill ? maxHealth : Mathf.Clamp(currentHealth, 0f, maxHealth);
            Changed?.Invoke(this);
        }

        public void SetMaxShield(float value, bool fill)
        {
            maxShield = Mathf.Max(0f, value);
            currentShield = fill ? maxShield : Mathf.Clamp(currentShield, 0f, maxShield);
            Changed?.Invoke(this);
        }

        public void AddShield(float amount)
        {
            if (amount <= 0f || dead)
            {
                return;
            }

            currentShield = Mathf.Clamp(currentShield + amount, 0f, maxShield);
            Changed?.Invoke(this);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || dead)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
            Changed?.Invoke(this);
        }

        public void ReceiveDamage(DamagePayload payload)
        {
            if (dead || payload.Amount <= 0f)
            {
                return;
            }

            float remaining = payload.Amount;
            if (currentShield > 0f)
            {
                float absorbed = Mathf.Min(currentShield, remaining);
                currentShield -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0f)
            {
                currentHealth = Mathf.Max(0f, currentHealth - remaining);
            }

            PlayHitFeedback();
            Damaged?.Invoke(this, payload);
            Changed?.Invoke(this);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (dead)
            {
                return;
            }

            dead = true;
            SetDeathBehavioursEnabled(false);
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

            defaultColor = spriteRenderer.color;
            spriteRenderer.color = hitTint;
            tintRemaining = hitTintSeconds;
        }

        private void SetDeathBehavioursEnabled(bool enabled)
        {
            if (disableOnDeath == null)
            {
                return;
            }

            for (int i = 0; i < disableOnDeath.Length; i++)
            {
                if (disableOnDeath[i] != null)
                {
                    disableOnDeath[i].enabled = enabled;
                }
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
        }

        private void CacheAnimatorParameter()
        {
            hitTriggerHash = Animator.StringToHash(hitTriggerParameter);
            animatorHasHitTrigger = false;
            if (animator == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger
                    && parameters[i].nameHash == hitTriggerHash)
                {
                    animatorHasHitTrigger = true;
                    return;
                }
            }
        }
    }
}
