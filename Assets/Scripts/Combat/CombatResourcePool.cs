using System;
using UnityEngine;

namespace NewFPG.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatResourcePool : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxResource = 10f;
        [SerializeField, Min(0f)] private float startingResource = 5f;
        [SerializeField, Min(0f)] private float recoveryPerSecond = 1f;
        [SerializeField] private bool recoverOverTime = true;

        private float currentResource;

        public event Action<CombatResourcePool> Changed;

        public float Current => currentResource;
        public float Max => maxResource;
        public float RecoveryPerSecond => recoveryPerSecond;
        public float Ratio => maxResource <= 0f ? 0f : currentResource / maxResource;

        private void Awake()
        {
            currentResource = Mathf.Clamp(startingResource, 0f, maxResource);
            Changed?.Invoke(this);
        }

        private void OnValidate()
        {
            maxResource = Mathf.Max(1f, maxResource);
            startingResource = Mathf.Clamp(startingResource, 0f, maxResource);
            recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
        }

        private void Update()
        {
            if (!recoverOverTime || recoveryPerSecond <= 0f || currentResource >= maxResource)
            {
                return;
            }

            currentResource = Mathf.Min(maxResource, currentResource + recoveryPerSecond * Time.deltaTime);
            Changed?.Invoke(this);
        }

        public bool CanSpend(float amount)
        {
            return currentResource + 0.0001f >= Mathf.Max(0f, amount);
        }

        public bool TrySpend(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (!CanSpend(amount))
            {
                return false;
            }

            currentResource = Mathf.Clamp(currentResource - amount, 0f, maxResource);
            Changed?.Invoke(this);
            return true;
        }

        public void Fill()
        {
            currentResource = maxResource;
            Changed?.Invoke(this);
        }

        public void SetCurrent(float value)
        {
            currentResource = Mathf.Clamp(value, 0f, maxResource);
            Changed?.Invoke(this);
        }
    }
}
