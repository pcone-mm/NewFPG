using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum D0SkillTargetDepthAnchor
    {
        ActiveEnemyGameplay = 0,
        ActiveEnemyWeakpoint = 1,
        CameraForward = 2
    }

    /// <summary>
    /// Presentation ownership for a weapon shot.  The combat domain does not
    /// consume this type; it is resolved by the active EntityView at runtime.
    /// Stable socket IDs intentionally use strings so adding a socket does not
    /// require changing a shared enum or rewriting existing assets.
    /// </summary>
    [Serializable]
    public sealed class D0WeaponShotPresentationDefinition
    {
        [SerializeField]
        private string socketId = "weapon.primary.muzzle";

        [SerializeField]
        private string muzzleVfxKey = "player.weapon.primary.muzzle";

        [SerializeField]
        private string tracerVfxKey = "player.weapon.primary.tracer";

        [SerializeField]
        private string animationName = "attack_play1";

        [SerializeField]
        private string alternateAnimationName = "attack_play2";

        [SerializeField]
        private Color effectColor = new Color(0.42f, 0.9f, 1f, 0.96f);

        [SerializeField, Min(0.01f)]
        private float muzzleDuration = 0.07f;

        [SerializeField, Min(0.01f)]
        private float muzzleLength = 0.34f;

        [SerializeField, Min(0.01f)]
        private float muzzleWidth = 0.12f;

        [SerializeField, Min(0f)]
        private float muzzleLightIntensity = 1.25f;

        [SerializeField, Min(0.01f)]
        private float tracerDuration = 0.13f;

        [SerializeField, Min(0.001f)]
        private float tracerWidth = 0.052f;

        [SerializeField, Min(0f)]
        private float tracerEndpointLightIntensity = 0.8f;

        [SerializeField, Min(1)]
        private int muzzlePrewarmCapacity = 2;

        [SerializeField, Min(1)]
        private int tracerPrewarmCapacity = 8;

        public string SocketId => socketId;
        public string MuzzleVfxKey => muzzleVfxKey;
        public string TracerVfxKey => tracerVfxKey;
        public string AnimationName => animationName;
        public string AlternateAnimationName => alternateAnimationName;
        public Color EffectColor => effectColor;
        public float MuzzleDuration => muzzleDuration;
        public float MuzzleLength => muzzleLength;
        public float MuzzleWidth => muzzleWidth;
        public float MuzzleLightIntensity => muzzleLightIntensity;
        public float TracerDuration => tracerDuration;
        public float TracerWidth => tracerWidth;
        public float TracerEndpointLightIntensity => tracerEndpointLightIntensity;
        public int MuzzlePrewarmCapacity => muzzlePrewarmCapacity;
        public int TracerPrewarmCapacity => tracerPrewarmCapacity;

        public static D0WeaponShotPresentationDefinition CreatePrimaryDefaults()
        {
            return new D0WeaponShotPresentationDefinition();
        }

        public static D0WeaponShotPresentationDefinition CreateSecondaryDefaults()
        {
            return new D0WeaponShotPresentationDefinition
            {
                socketId = "weapon.secondary.muzzle",
                muzzleVfxKey = "player.weapon.secondary.muzzle",
                tracerVfxKey = "player.weapon.secondary.tracer",
                animationName = "defense_play",
                alternateAnimationName = string.Empty,
                effectColor = new Color(1f, 0.98f, 0.72f, 1f),
                muzzleDuration = 0.13f,
                muzzleLength = 0.62f,
                muzzleWidth = 0.18f,
                muzzleLightIntensity = 2.1f,
                tracerDuration = 0.36f,
                tracerWidth = 0.12f,
                tracerEndpointLightIntensity = 1.55f,
                muzzlePrewarmCapacity = 1,
                tracerPrewarmCapacity = 4
            };
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(socketId)
                || string.IsNullOrWhiteSpace(muzzleVfxKey)
                || string.IsNullOrWhiteSpace(tracerVfxKey)
                || string.IsNullOrWhiteSpace(animationName)
                || !IsFinite(effectColor.r)
                || !IsFinite(effectColor.g)
                || !IsFinite(effectColor.b)
                || !IsFinite(effectColor.a)
                || !IsFinitePositive(muzzleDuration)
                || !IsFinitePositive(muzzleLength)
                || !IsFinitePositive(muzzleWidth)
                || !IsFiniteNonNegative(muzzleLightIntensity)
                || !IsFinitePositive(tracerDuration)
                || !IsFinitePositive(tracerWidth)
                || !IsFiniteNonNegative(tracerEndpointLightIntensity)
                || muzzlePrewarmCapacity <= 0
                || tracerPrewarmCapacity <= 0)
            {
                error = "Weapon shot presentation requires stable IDs, animation and finite positive visual values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// Secondary weapon presentation owns charge and target-burst visuals in
    /// addition to the release shot.  Damage, range and target selection stay
    /// in the combat definition.
    /// </summary>
    [Serializable]
    public sealed class D0WeaponSecondaryPresentationDefinition
    {
        [SerializeField]
        private D0WeaponShotPresentationDefinition shot =
            D0WeaponShotPresentationDefinition.CreateSecondaryDefaults();

        [SerializeField]
        private string chargeVfxKey = "player.weapon.secondary.charge";

        [SerializeField]
        private string targetBurstVfxKey = "player.weapon.secondary.target-burst";

        [SerializeField]
        private D0SkillTargetDepthAnchor targetDepthAnchor =
            D0SkillTargetDepthAnchor.ActiveEnemyGameplay;

        [SerializeField, Min(0.01f)]
        private float fallbackCameraDistance = 8f;

        [SerializeField, Min(0.01f)]
        private float chargePulseDuration = 0.18f;

        [SerializeField, Min(0f)]
        private float targetBurstRadiusScale = 0.32f;

        [SerializeField, Min(0.01f)]
        private float targetBurstMinRadius = 0.42f;

        [SerializeField, Min(0.01f)]
        private float targetBurstMaxRadius = 1.4f;

        [SerializeField]
        private string chargeAnimation = "u4_attack_ready";

        [SerializeField]
        private string releaseAnimation = "defense_play";

        [SerializeField]
        private string endAnimation = "u4_attack_end";

        [SerializeField, Min(0f)]
        private float hitMarkerTime = 0.033f;

        [SerializeField, Min(0f)]
        private float stopMarkerTime = 1f;

        [SerializeField, Min(1)]
        private int chargePrewarmCapacity = 1;

        [SerializeField, Min(1)]
        private int targetBurstPrewarmCapacity = 4;

        public D0WeaponShotPresentationDefinition Shot => shot;
        public string ChargeVfxKey => chargeVfxKey;
        public string TargetBurstVfxKey => targetBurstVfxKey;
        public D0SkillTargetDepthAnchor TargetDepthAnchor => targetDepthAnchor;
        public float FallbackCameraDistance => fallbackCameraDistance;
        public float ChargePulseDuration => chargePulseDuration;
        public float TargetBurstRadiusScale => targetBurstRadiusScale;
        public float TargetBurstMinRadius => targetBurstMinRadius;
        public float TargetBurstMaxRadius => targetBurstMaxRadius;
        public string ChargeAnimation => chargeAnimation;
        public string ReleaseAnimation => releaseAnimation;
        public string EndAnimation => endAnimation;
        public float HitMarkerTime => hitMarkerTime;
        public float StopMarkerTime => stopMarkerTime;
        public int ChargePrewarmCapacity => chargePrewarmCapacity;
        public int TargetBurstPrewarmCapacity => targetBurstPrewarmCapacity;

        public static D0WeaponSecondaryPresentationDefinition CreateDefaults()
        {
            return new D0WeaponSecondaryPresentationDefinition();
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (shot == null || !shot.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Secondary weapon presentation requires a release shot definition.";
                }

                return false;
            }

            if (string.IsNullOrWhiteSpace(chargeVfxKey)
                || string.IsNullOrWhiteSpace(targetBurstVfxKey)
                || string.IsNullOrWhiteSpace(chargeAnimation)
                || string.IsNullOrWhiteSpace(releaseAnimation)
                || string.IsNullOrWhiteSpace(endAnimation)
                || !Enum.IsDefined(typeof(D0SkillTargetDepthAnchor), targetDepthAnchor)
                || !IsFinitePositive(fallbackCameraDistance)
                || !IsFinitePositive(chargePulseDuration)
                || !IsFiniteNonNegative(targetBurstRadiusScale)
                || !IsFinitePositive(targetBurstMinRadius)
                || !IsFinitePositive(targetBurstMaxRadius)
                || targetBurstMaxRadius < targetBurstMinRadius
                || !IsFiniteNonNegative(hitMarkerTime)
                || !IsFiniteNonNegative(stopMarkerTime)
                || stopMarkerTime < hitMarkerTime
                || chargePrewarmCapacity <= 0
                || targetBurstPrewarmCapacity <= 0)
            {
                error = "Secondary weapon presentation contains invalid timing, VFX or target-burst values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }

    [Serializable]
    public sealed class D0WeaponReloadPresentationDefinition
    {
        [SerializeField]
        private string playAnimation = "u1_buff_play";

        [SerializeField]
        private string readyAnimation = "u1_buff_ready";

        public string PlayAnimation => playAnimation;
        public string ReadyAnimation => readyAnimation;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(playAnimation)
                || string.IsNullOrWhiteSpace(readyAnimation))
            {
                error = "Weapon reload presentation requires play and ready animation names.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Attack-owned presentation contract.  The legacy slot fields on
    /// D0EnemyAttackDefinition remain the compatibility source when this
    /// optional block is absent in an older asset.
    /// </summary>
    [Serializable]
    public sealed class D0EnemyAttackPresentationDefinition
    {
        [SerializeField]
        private string socketId = "attack.default.origin";

        [SerializeField]
        private string animationName = "normal_skill1";

        [SerializeField]
        private string releaseAnimationName = string.Empty;

        [SerializeField]
        private string visualEffectKey = "";

        [SerializeField]
        private GameObject visualEffectPrefab;

        [SerializeField, Min(1)]
        private int prewarmCapacity = 1;

        [SerializeField, Min(0.01f)]
        private float effectDuration = 1f;

        [SerializeField]
        private int sortingOrderOffset;

        [SerializeField]
        private CombatAudioCue audioCue = CombatAudioCue.EnemyFastThreatRelease;

        [SerializeField, Min(0)]
        private int releaseMarkerTicks;

        public string SocketId => socketId;
        public string AnimationName => animationName;
        public string ReleaseAnimationName => releaseAnimationName;
        public string VisualEffectKey => visualEffectKey;
        public GameObject VisualEffectPrefab => visualEffectPrefab;
        public int PrewarmCapacity => prewarmCapacity;
        public float EffectDuration => effectDuration;
        public int SortingOrderOffset => sortingOrderOffset;
        public CombatAudioCue AudioCue => audioCue;
        public int ReleaseMarkerTicks => releaseMarkerTicks;

        public static D0EnemyAttackPresentationDefinition CreateDefaults()
        {
            return new D0EnemyAttackPresentationDefinition();
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(socketId)
                || string.IsNullOrWhiteSpace(animationName)
                || string.IsNullOrWhiteSpace(releaseAnimationName)
                || string.IsNullOrWhiteSpace(visualEffectKey)
                || audioCue <= CombatAudioCue.None
                || audioCue >= CombatAudioCue.Count
                || prewarmCapacity <= 0
                || !IsFinitePositive(effectDuration)
                || releaseMarkerTicks < 0)
            {
                error = "Enemy attack presentation requires socket, animation, VFX, executable audio and finite pool values.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }

    public enum D0CombatVfxCategory
    {
        WeaponMuzzle = 0,
        WeaponTracer = 1,
        WeaponCharge = 2,
        WeaponTargetBurst = 3,
        EnemyAttack = 4,
        Summon = 5,
        ActorState = 6
    }

    /// <summary>
    /// A logical VFX dependency discovered from a scenario definition.  A
    /// prefab is optional for procedural modules (for example the existing
    /// line/tracer renderer), but the key and capacity are always validated.
    /// </summary>
    [Serializable]
    public sealed class D0CombatVfxAssetReference
    {
        [SerializeField]
        private string key;

        [SerializeField]
        private GameObject prefab;

        [SerializeField, Min(1)]
        private int prewarmCapacity = 1;

        [SerializeField, Min(0.01f)]
        private float duration = 1f;

        [SerializeField]
        private string animationName = "animation";

        [SerializeField]
        private int sortingOrderOffset;

        [SerializeField]
        private D0CombatVfxCategory category = D0CombatVfxCategory.ActorState;

        public string Key => key;
        public GameObject Prefab => prefab;
        public int PrewarmCapacity => prewarmCapacity;
        public float Duration => duration;
        public string AnimationName => animationName;
        public int SortingOrderOffset => sortingOrderOffset;
        public D0CombatVfxCategory Category => category;

        public D0CombatVfxAssetReference()
        {
        }

        public D0CombatVfxAssetReference(
            string key,
            GameObject prefab,
            int prewarmCapacity,
            float duration,
            string animationName,
            int sortingOrderOffset,
            D0CombatVfxCategory category)
        {
            this.key = key;
            this.prefab = prefab;
            this.prewarmCapacity = prewarmCapacity;
            this.duration = duration;
            this.animationName = animationName;
            this.sortingOrderOffset = sortingOrderOffset;
            this.category = category;
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(key)
                || prewarmCapacity <= 0
                || !IsFinitePositive(duration)
                || string.IsNullOrWhiteSpace(animationName))
            {
                error = "Combat VFX reference requires a stable key, positive capacity/duration and animation name.";
                return false;
            }

            if (!Enum.IsDefined(typeof(D0CombatVfxCategory), category))
            {
                error = "Combat VFX reference category is invalid.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
