using System;
using NewFPG.Monsters;
using UnityEngine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatVitals : MonoBehaviour, IDamageable
    {
        [Header("References")]
        [SerializeField] private Behaviour[] disableOnDeath;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private CombatVitalsSettings settings = new CombatVitalsSettings();
        private IMonsterState monsterState;
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
        public float MaxHealth => settings.maxHealth;
        public float HealthRatio => settings.maxHealth <= 0f ? 0f : currentHealth / settings.maxHealth;
        public float CurrentShield => currentShield;
        public float MaxShield => settings.maxShield;
        public float ShieldRatio => settings.maxShield <= 0f ? 0f : currentShield / settings.maxShield;
        public bool IsAlive => !dead && currentHealth > 0f;
        public bool IsTargetable => monsterState == null || monsterState.IsTargetable;
        public Transform AimTransform => IsTargetable ? transform : null;

        public CombatVitalsSettings ToSettings()
        {
            return settings.Clone();
        }

        public void ApplySettings(CombatVitalsSettings nextSettings, bool resetVitals = false)
        {
            if (nextSettings == null)
            {
                return;
            }

            settings = nextSettings.Clone();
            settings.Normalize();
            CacheAnimatorParameter();

            if (resetVitals)
            {
                ResetVitals();
            }
        }

        private void Reset()
        {
            CacheReferences();
            settings.startingHealth = settings.maxHealth;
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
            settings.Normalize();
            currentHealth = Mathf.Clamp(settings.startingHealth <= 0f ? settings.maxHealth : settings.startingHealth, 1f, settings.maxHealth);
            currentShield = Mathf.Clamp(settings.startingShield, 0f, settings.maxShield);

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
            settings.maxHealth = Mathf.Max(1f, value);
            settings.startingHealth = Mathf.Clamp(settings.startingHealth <= 0f ? settings.maxHealth : settings.startingHealth, 1f, settings.maxHealth);
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

            currentHealth = fill ? settings.maxHealth : Mathf.Clamp(currentHealth, 0f, settings.maxHealth);
            Changed?.Invoke(this);
        }

        public void SetMaxShield(float value, bool fill)
        {
            settings.maxShield = Mathf.Max(0f, value);
            settings.startingShield = Mathf.Clamp(settings.startingShield, 0f, settings.maxShield);
            currentShield = fill ? settings.maxShield : Mathf.Clamp(currentShield, 0f, settings.maxShield);
            Changed?.Invoke(this);
        }

        public void AddShield(float amount)
        {
            if (amount <= 0f || dead)
            {
                return;
            }

            currentShield = Mathf.Clamp(currentShield + amount, 0f, settings.maxShield);
            Changed?.Invoke(this);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || dead)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, settings.maxHealth);
            Changed?.Invoke(this);
        }

        public void ReceiveDamage(DamagePayload payload)
        {
            if (dead || payload.Amount <= 0f || monsterState != null && monsterState.IsInvincible)
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

            if (settings.destroyOnDeath)
            {
                Destroy(gameObject, settings.deathDelay);
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
            spriteRenderer.color = settings.hitTint;
            tintRemaining = settings.hitTintSeconds;
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

            monsterState = GetComponent<MonsterConfigBinding>();
            if (monsterState == null)
            {
                monsterState = GetComponent<MonsterState>();
            }
        }

        private void CacheAnimatorParameter()
        {
            hitTriggerHash = Animator.StringToHash(settings.hitTriggerParameter);
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
