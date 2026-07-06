using System;
using System.Collections;
using System.Collections.Generic;
using NewFPG.Combat;
using UnityEngine;

namespace NewFPG.Monsters
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class MonsterState : MonoBehaviour, IMonsterState
    {
        [SerializeField] private SpriteRenderer[] renderers;

        private readonly List<float> scaleMultipliers = new List<float>();
        private readonly List<float> speedMultipliers = new List<float>();
        private int invincibleStacks;
        private int invisibleStacks;
        private Vector3 baseScale = Vector3.one;
        private CombatVitals vitals;

        public event Action<MonsterState> Changed;

        public bool IsInvincible => invincibleStacks > 0;
        public bool IsInvisible => invisibleStacks > 0;
        public bool IsTargetable => !IsInvisible;
        public float ScaleMultiplier { get; private set; } = 1f;
        public float SpeedMultiplier { get; private set; } = 1f;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
            baseScale = transform.localScale;
            RefreshDerivedState();
        }

        private void OnValidate()
        {
            CacheReferences();
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
            float safeMultiplier = Mathf.Max(0.01f, multiplier);
            StartCoroutine(PushMultiplier(scaleMultipliers, safeMultiplier, seconds));
        }

        public void PushSpeedMultiplier(float multiplier, float seconds)
        {
            float safeMultiplier = Mathf.Max(0.01f, multiplier);
            StartCoroutine(PushMultiplier(speedMultipliers, safeMultiplier, seconds));
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

        private void RefreshDerivedState()
        {
            ScaleMultiplier = Product(scaleMultipliers);
            SpeedMultiplier = Product(speedMultipliers);
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

        private void CacheReferences()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            if (vitals == null)
            {
                vitals = GetComponent<CombatVitals>();
            }
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
    }
}
