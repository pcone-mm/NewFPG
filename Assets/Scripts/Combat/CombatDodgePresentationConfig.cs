using UnityEngine;

namespace NewFPG.Combat
{
    [CreateAssetMenu(fileName = "CombatDodgePresentationConfig", menuName = "NewFPG/Combat/Dodge Presentation Config")]
    public sealed class CombatDodgePresentationConfig : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float dodgeDuration = 0.34f;
        [SerializeField, Min(0f)] private float returnDuration = 0.22f;

        [Header("Cooldown")]
        [SerializeField, Min(0f)] private float cooldownDuration = 0.85f;

        [Header("Curves")]
        [SerializeField] private AnimationCurve dodgeCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0f));
        [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Battle Camera")]
        [SerializeField] private Vector3 backCameraOffset = new Vector3(0f, -0.025f, -0.48f);
        [SerializeField] private Vector3 backCameraEuler = new Vector3(-1.5f, 0f, 0f);
        [SerializeField] private Vector3 sideCameraOffset = new Vector3(0.36f, -0.015f, 0.04f);
        [SerializeField] private Vector3 sideCameraEuler = new Vector3(0.5f, 0f, -2.6f);
        [SerializeField] private float battleCameraFovKick = 4f;

        [Header("Weapon Camera")]
        [SerializeField] private Vector3 backWeaponOffset = new Vector3(0f, -0.08f, -0.1f);
        [SerializeField] private Vector3 backWeaponEuler = new Vector3(4f, 0f, 0f);
        [SerializeField] private Vector3 sideWeaponLag = new Vector3(0.16f, -0.02f, 0.03f);
        [SerializeField] private Vector3 sideWeaponEuler = new Vector3(0f, 0f, 6.5f);
        [SerializeField] private float weaponCameraFovKick = 2f;

        [Header("Speed Lines")]
        [SerializeField] private bool createRuntimeSpeedLines = true;
        [SerializeField, Min(0f)] private float speedLineDuration = 0.16f;
        [SerializeField, Range(0f, 1f)] private float speedLineOpacity = 0.72f;

        public float DodgeDuration => Mathf.Max(0f, dodgeDuration);
        public float ReturnDuration => Mathf.Max(0f, returnDuration);
        public float CooldownDuration => Mathf.Max(0f, cooldownDuration);
        public Vector3 BackCameraOffset => backCameraOffset;
        public Vector3 BackCameraEuler => backCameraEuler;
        public Vector3 SideCameraOffset => sideCameraOffset;
        public Vector3 SideCameraEuler => sideCameraEuler;
        public float BattleCameraFovKick => battleCameraFovKick;
        public Vector3 BackWeaponOffset => backWeaponOffset;
        public Vector3 BackWeaponEuler => backWeaponEuler;
        public Vector3 SideWeaponLag => sideWeaponLag;
        public Vector3 SideWeaponEuler => sideWeaponEuler;
        public float WeaponCameraFovKick => weaponCameraFovKick;
        public bool CreateRuntimeSpeedLines => createRuntimeSpeedLines;
        public float SpeedLineDuration => Mathf.Max(0f, speedLineDuration);
        public float SpeedLineOpacity => Mathf.Clamp01(speedLineOpacity);

        public float EvaluateDodge(float normalizedTime)
        {
            return dodgeCurve != null
                ? dodgeCurve.Evaluate(Mathf.Clamp01(normalizedTime))
                : Mathf.Sin(Mathf.Clamp01(normalizedTime) * Mathf.PI);
        }

        public float EvaluateReturn(float normalizedTime)
        {
            return returnCurve != null
                ? returnCurve.Evaluate(Mathf.Clamp01(normalizedTime))
                : Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(normalizedTime));
        }

        private void OnValidate()
        {
            dodgeDuration = Mathf.Max(0f, dodgeDuration);
            returnDuration = Mathf.Max(0f, returnDuration);
            cooldownDuration = Mathf.Max(0f, cooldownDuration);
            speedLineDuration = Mathf.Max(0f, speedLineDuration);
            speedLineOpacity = Mathf.Clamp01(speedLineOpacity);

            if (dodgeCurve == null || dodgeCurve.length == 0)
            {
                dodgeCurve = new AnimationCurve(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(1f, 0f));
            }

            if (returnCurve == null || returnCurve.length == 0)
            {
                returnCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
            }
        }
    }
}
