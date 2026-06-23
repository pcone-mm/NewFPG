using UnityEngine;

namespace NewFPG.Combat
{
    public readonly struct DamagePayload
    {
        public DamagePayload(float amount, GameObject source = null, Vector3 hitPoint = default)
        {
            Amount = Mathf.Max(0f, amount);
            Source = source;
            HitPoint = hitPoint;
        }

        public float Amount { get; }
        public GameObject Source { get; }
        public Vector3 HitPoint { get; }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        Transform AimTransform { get; }
        void ReceiveDamage(DamagePayload payload);
    }
}
