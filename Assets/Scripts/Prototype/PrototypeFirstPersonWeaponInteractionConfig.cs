using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace NewFPG.Prototype
{
    [CreateAssetMenu(
        fileName = "FirstPersonWeaponHudInteractionConfig",
        menuName = "NewFPG/Prototype/First Person Weapon HUD Interaction Config")]
    public sealed class PrototypeFirstPersonWeaponInteractionConfig : ScriptableObject
    {
#if ODIN_INSPECTOR
        [TitleGroup("Hover")]
        [MinValue(0f)]
        [SuffixLabel("local Y")]
#endif
        [SerializeField] private float hoverLift = 0.18f;

#if ODIN_INSPECTOR
        [TitleGroup("Hover")]
        [MinValue(0.01f)]
        [SuffixLabel("sec")]
#endif
        [SerializeField] private float hoverEnterDuration = 0.18f;

#if ODIN_INSPECTOR
        [TitleGroup("Hover")]
        [MinValue(0.01f)]
        [SuffixLabel("sec")]
#endif
        [SerializeField] private float hoverReturnDuration = 0.15f;

#if ODIN_INSPECTOR
        [TitleGroup("Hover")]
        [SuffixLabel("deg/sec")]
#endif
        [SerializeField] private float hoverSpinSpeed = 70f;

#if ODIN_INSPECTOR
        [TitleGroup("Hover")]
#endif
        [SerializeField] private RotationAxis hoverSpinAxis = RotationAxis.Y;

#if ODIN_INSPECTOR
        [TitleGroup("Hover")]
#endif
        [SerializeField] private AnimationCurve hoverEnterEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

#if ODIN_INSPECTOR
        [TitleGroup("Hover")]
#endif
        [SerializeField] private AnimationCurve hoverReturnEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

#if ODIN_INSPECTOR
        [TitleGroup("Attack")]
        [MinValue(0.01f)]
        [SuffixLabel("sec")]
#endif
        [SerializeField] private float attackDuration = 0.32f;

#if ODIN_INSPECTOR
        [TitleGroup("Attack")]
        [MinValue(0f)]
        [SuffixLabel("local Z")]
#endif
        [SerializeField] private float attackForwardOffset = 0.28f;

#if ODIN_INSPECTOR
        [TitleGroup("Attack")]
        [SuffixLabel("deg")]
#endif
        [SerializeField] private float attackRotation = 28f;

#if ODIN_INSPECTOR
        [TitleGroup("Attack")]
#endif
        [SerializeField] private RotationAxis attackRotationAxis = RotationAxis.Y;

#if ODIN_INSPECTOR
        [TitleGroup("Attack")]
        [MinValue(0.01f)]
#endif
        [SerializeField] private float attackScale = 1.08f;

#if ODIN_INSPECTOR
        [TitleGroup("Attack")]
#endif
        [SerializeField] private AnimationCurve attackRecoverEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

#if ODIN_INSPECTOR
        [TitleGroup("Attack")]
#endif
        [SerializeField] private AnimationCurve attackArc = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.45f, 1f),
            new Keyframe(1f, 0f));

#if ODIN_INSPECTOR
        [TitleGroup("Pointer")]
        [MinValue(0f)]
        [SuffixLabel("local Z")]
#endif
        [SerializeField] private float raycastDistancePadding = 1f;

        public float HoverLift => Mathf.Max(0f, hoverLift);
        public float HoverEnterDuration => Mathf.Max(0.01f, hoverEnterDuration);
        public float HoverReturnDuration => Mathf.Max(0.01f, hoverReturnDuration);
        public float HoverSpinSpeed => hoverSpinSpeed;
        public Vector3 HoverSpinAxis => AxisToVector(hoverSpinAxis);
        public float AttackDuration => Mathf.Max(0.01f, attackDuration);
        public float AttackForwardOffset => Mathf.Max(0f, attackForwardOffset);
        public float AttackRotation => attackRotation;
        public Vector3 AttackRotationAxis => AxisToVector(attackRotationAxis);
        public float AttackScale => Mathf.Max(0.01f, attackScale);
        public float RaycastDistancePadding => Mathf.Max(0f, raycastDistancePadding);

        public float EvaluateHoverEnter(float normalizedTime)
        {
            return Evaluate01(hoverEnterEase, normalizedTime);
        }

        public float EvaluateHoverReturn(float normalizedTime)
        {
            return Evaluate01(hoverReturnEase, normalizedTime);
        }

        public float EvaluateAttackRecover(float normalizedTime)
        {
            return Evaluate01(attackRecoverEase, normalizedTime);
        }

        public float EvaluateAttackArc(float normalizedTime)
        {
            return Evaluate01(attackArc, normalizedTime);
        }

        private void OnValidate()
        {
            hoverLift = Mathf.Max(0f, hoverLift);
            hoverEnterDuration = Mathf.Max(0.01f, hoverEnterDuration);
            hoverReturnDuration = Mathf.Max(0.01f, hoverReturnDuration);
            attackDuration = Mathf.Max(0.01f, attackDuration);
            attackForwardOffset = Mathf.Max(0f, attackForwardOffset);
            attackScale = Mathf.Max(0.01f, attackScale);
            raycastDistancePadding = Mathf.Max(0f, raycastDistancePadding);
        }

        private static float Evaluate01(AnimationCurve curve, float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            if (curve == null || curve.length == 0)
            {
                return Mathf.SmoothStep(0f, 1f, t);
            }

            return Mathf.Clamp01(curve.Evaluate(t));
        }

        private static Vector3 AxisToVector(RotationAxis axis)
        {
            switch (axis)
            {
                case RotationAxis.X:
                    return Vector3.right;
                case RotationAxis.Y:
                    return Vector3.up;
                default:
                    return Vector3.forward;
            }
        }

        private enum RotationAxis
        {
            X,
            Y,
            Z,
        }
    }
}
