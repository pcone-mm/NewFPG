using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public enum CombatThreatPresentationKind
    {
        FastUninterceptable = 0,
        InterceptableVolley = 1,
        HeavyWeakpoint = 2
    }

    public enum CombatThreatTelegraphShape
    {
        SourcePulse = 0,
        ProjectileOutline = 1,
        WeakpointLock = 2
    }

    public enum CombatHitPresentationKind
    {
        Body = 0,
        Weakpoint = 1,
        Intercept = 2
    }

    public enum CombatHitFeedbackShape
    {
        Burst = 0,
        Diamond = 1,
        Shatter = 2
    }

    [Serializable]
    public sealed class CombatThreatPresentationDefinition
    {
        [SerializeField] private CombatThreatPresentationKind kind;
        [SerializeField, Min(1)] private int presentationKey = 1;
        [SerializeField] private CombatThreatTelegraphShape telegraphShape;
        [SerializeField] private bool showsInterceptionMarker;
        [SerializeField] private bool allowsWeakpointInterrupt;
        [SerializeField] private Color primaryColor = Color.white;
        [SerializeField] private Color secondaryColor = Color.white;
        [SerializeField, Min(0.01f)] private float telegraphDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.2f;
        [SerializeField] private int sortingOrder;

        public CombatThreatPresentationDefinition()
        {
        }

        public CombatThreatPresentationDefinition(
            CombatThreatPresentationKind kind,
            int presentationKey,
            CombatThreatTelegraphShape telegraphShape,
            bool showsInterceptionMarker,
            bool allowsWeakpointInterrupt,
            Color primaryColor,
            Color secondaryColor,
            float telegraphDuration,
            float releaseDuration,
            int sortingOrder)
        {
            this.kind = kind;
            this.presentationKey = presentationKey;
            this.telegraphShape = telegraphShape;
            this.showsInterceptionMarker = showsInterceptionMarker;
            this.allowsWeakpointInterrupt = allowsWeakpointInterrupt;
            this.primaryColor = primaryColor;
            this.secondaryColor = secondaryColor;
            this.telegraphDuration = telegraphDuration;
            this.releaseDuration = releaseDuration;
            this.sortingOrder = sortingOrder;
        }

        public CombatThreatPresentationKind Kind => kind;
        public int PresentationKey => presentationKey;
        public CombatThreatTelegraphShape TelegraphShape => telegraphShape;
        public bool ShowsInterceptionMarker => showsInterceptionMarker;
        public bool AllowsWeakpointInterrupt => allowsWeakpointInterrupt;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public float TelegraphDuration => telegraphDuration;
        public float ReleaseDuration => releaseDuration;
        public int SortingOrder => sortingOrder;
    }

    [Serializable]
    public sealed class CombatHitPresentationDefinition
    {
        [SerializeField] private CombatHitPresentationKind kind;
        [SerializeField] private CombatHitFeedbackShape feedbackShape;
        [SerializeField] private Color primaryColor = Color.white;
        [SerializeField] private Color secondaryColor = Color.white;
        [SerializeField, Min(0.01f)] private float duration = 0.16f;
        [SerializeField] private int sortingOrder;

        public CombatHitPresentationDefinition()
        {
        }

        public CombatHitPresentationDefinition(
            CombatHitPresentationKind kind,
            CombatHitFeedbackShape feedbackShape,
            Color primaryColor,
            Color secondaryColor,
            float duration,
            int sortingOrder)
        {
            this.kind = kind;
            this.feedbackShape = feedbackShape;
            this.primaryColor = primaryColor;
            this.secondaryColor = secondaryColor;
            this.duration = duration;
            this.sortingOrder = sortingOrder;
        }

        public CombatHitPresentationKind Kind => kind;
        public CombatHitFeedbackShape FeedbackShape => feedbackShape;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public float Duration => duration;
        public int SortingOrder => sortingOrder;
    }

    [Serializable]
    public sealed class CombatPresentationSorting
    {
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int backgroundOrder = -100;
        [SerializeField] private int actorOrder;
        [SerializeField] private int worldEffectsOrder = 20;
        [SerializeField] private int screenEffectsOrder = 40;
        [SerializeField] private int hudOrder = 100;
        [SerializeField] private int reticleOrder = 150;
        [SerializeField] private int developmentOverlayOrder = 200;

        public string SortingLayerName => sortingLayerName;
        public int BackgroundOrder => backgroundOrder;
        public int ActorOrder => actorOrder;
        public int WorldEffectsOrder => worldEffectsOrder;
        public int ScreenEffectsOrder => screenEffectsOrder;
        public int HudOrder => hudOrder;
        public int ReticleOrder => reticleOrder;
        public int DevelopmentOverlayOrder => developmentOverlayOrder;

        internal bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(sortingLayerName))
            {
                error = "Combat presentation sorting layer name is required.";
                return false;
            }

            if (backgroundOrder >= actorOrder
                || actorOrder >= worldEffectsOrder
                || worldEffectsOrder >= screenEffectsOrder
                || screenEffectsOrder >= hudOrder
                || hudOrder >= reticleOrder
                || reticleOrder >= developmentOverlayOrder)
            {
                error = "Combat presentation sorting orders must be strictly back-to-front.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class CombatPresentationPoolCapacities
    {
        [SerializeField, Min(1)] private int playerShotCapacity = 32;
        [SerializeField, Min(1)] private int worldEffectCapacity = 48;
        [SerializeField, Min(1)] private int hitTipCapacity = 32;
        [SerializeField, Min(1)] private int threatTelegraphCapacity = 8;
        [SerializeField, Min(1)] private int enemyProjectileCapacity = 32;
        [SerializeField, Min(1)] private int screenEffectCapacity = 16;
        [SerializeField, Min(1)]
        private int audioSourceCapacity = CombatPresentationProfile.RequiredAudioSourceCapacity;

        public int PlayerShotCapacity => playerShotCapacity;
        public int WorldEffectCapacity => worldEffectCapacity;
        public int HitTipCapacity => hitTipCapacity;
        public int ThreatTelegraphCapacity => threatTelegraphCapacity;
        public int EnemyProjectileCapacity => enemyProjectileCapacity;
        public int ScreenEffectCapacity => screenEffectCapacity;
        public int AudioSourceCapacity => audioSourceCapacity;

        internal bool TryValidate(out string error)
        {
            if (playerShotCapacity <= 0
                || worldEffectCapacity <= 0
                || hitTipCapacity <= 0
                || threatTelegraphCapacity <= 0
                || enemyProjectileCapacity < CombatPresentationProfile.RequiredEnemyProjectileCapacity
                || screenEffectCapacity <= 0
                || audioSourceCapacity < CombatPresentationProfile.RequiredAudioSourceCapacity)
            {
                error = "Combat presentation pool capacities are below the required fixed budgets.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Global combat presentation language only. Actor identity, state
    /// animations, weapon shots and enemy attacks belong to concrete
    /// definitions and Entity Prefabs.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CombatPresentationProfile",
        menuName = "FPG Demo/Combat Presentation Profile")]
    public sealed class CombatPresentationProfile : ScriptableObject
    {
        public const int FastThreatPresentationKey = 1;
        public const int InterceptableVolleyThreatPresentationKey = 2;
        public const int HeavyWeakpointThreatPresentationKey = 3;
        public const int RequiredEnemyProjectileCapacity = 32;
        public const int RequiredAudioSourceCapacity =
            CombatAudioBank.DefaultConcurrentVoiceLimit;

        [SerializeField]
        private CombatThreatPresentationDefinition[] threatDefinitions =
        {
            new CombatThreatPresentationDefinition(
                CombatThreatPresentationKind.FastUninterceptable,
                FastThreatPresentationKey,
                CombatThreatTelegraphShape.SourcePulse,
                false,
                false,
                new Color(1f, 0.25f, 0.1f, 1f),
                new Color(1f, 0.62f, 0.16f, 1f),
                0.4f,
                0.22f,
                20),
            new CombatThreatPresentationDefinition(
                CombatThreatPresentationKind.InterceptableVolley,
                InterceptableVolleyThreatPresentationKey,
                CombatThreatTelegraphShape.ProjectileOutline,
                true,
                false,
                new Color(1f, 0.66f, 0.12f, 1f),
                new Color(0.7f, 1f, 0.95f, 1f),
                0.8f,
                0.32f,
                20),
            new CombatThreatPresentationDefinition(
                CombatThreatPresentationKind.HeavyWeakpoint,
                HeavyWeakpointThreatPresentationKey,
                CombatThreatTelegraphShape.WeakpointLock,
                false,
                true,
                new Color(1f, 0.15f, 0.14f, 1f),
                new Color(1f, 0.95f, 0.95f, 1f),
                1.5f,
                0.45f,
                20)
        };

        [SerializeField]
        private CombatHitPresentationDefinition[] hitDefinitions =
        {
            new CombatHitPresentationDefinition(
                CombatHitPresentationKind.Body,
                CombatHitFeedbackShape.Burst,
                new Color(0.42f, 0.9f, 1f, 0.96f),
                new Color(0.82f, 0.98f, 1f, 0.8f),
                0.16f,
                40),
            new CombatHitPresentationDefinition(
                CombatHitPresentationKind.Weakpoint,
                CombatHitFeedbackShape.Diamond,
                new Color(1f, 0.9f, 0.22f, 1f),
                new Color(1f, 0.98f, 0.72f, 1f),
                0.22f,
                40),
            new CombatHitPresentationDefinition(
                CombatHitPresentationKind.Intercept,
                CombatHitFeedbackShape.Shatter,
                new Color(0.32f, 1f, 0.92f, 1f),
                new Color(0.88f, 1f, 1f, 1f),
                0.24f,
                40)
        };

        [SerializeField]
        private FpgHudResourcePresentation[] formalHudResources =
        {
            new FpgHudResourcePresentation(
                FpgHudResourceKind.Life,
                "LIFE",
                new Color(0.95f, 0.24f, 0.2f, 1f),
                0,
                "{0}/{1}",
                0.16f),
            new FpgHudResourcePresentation(
                FpgHudResourceKind.Barrier,
                "BARRIER",
                new Color(0.22f, 0.75f, 1f, 1f),
                1,
                "{0}/{1}",
                0.18f),
            new FpgHudResourcePresentation(
                FpgHudResourceKind.Ammo,
                "AMMO",
                new Color(1f, 0.78f, 0.16f, 1f),
                2,
                "{0}/{1}",
                0.12f)
        };

        [SerializeField]
        private FpgDamagePopupPresentation formalDamagePopup =
            new FpgDamagePopupPresentation();

        [SerializeField]
        private FpgReticlePresentation formalReticle =
            new FpgReticlePresentation();

        [SerializeField]
        private CombatPresentationSorting sorting = new CombatPresentationSorting();

        [SerializeField]
        private CombatPresentationPoolCapacities poolCapacities =
            new CombatPresentationPoolCapacities();

        public CombatPresentationSorting Sorting => sorting;
        public CombatPresentationPoolCapacities PoolCapacities => poolCapacities;
        public FpgDamagePopupPresentation FormalDamagePopup => formalDamagePopup;
        public FpgReticlePresentation FormalReticle => formalReticle;
        public int FormalHudResourceCount =>
            formalHudResources == null ? 0 : formalHudResources.Length;
        public int ThreatDefinitionCount =>
            threatDefinitions == null ? 0 : threatDefinitions.Length;
        public int HitDefinitionCount =>
            hitDefinitions == null ? 0 : hitDefinitions.Length;

        public CombatThreatPresentationDefinition GetThreatDefinition(int index)
        {
            if (index < 0 || index >= ThreatDefinitionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return threatDefinitions[index];
        }

        public FpgHudResourcePresentation GetFormalHudResource(int index)
        {
            if (index < 0 || index >= FormalHudResourceCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return formalHudResources[index];
        }

        public bool TryGetFormalHudResource(
            FpgHudResourceKind kind,
            out FpgHudResourcePresentation definition)
        {
            FpgHudResourcePresentation[] definitions = formalHudResources
                ?? Array.Empty<FpgHudResourcePresentation>();
            for (int index = 0; index < definitions.Length; index++)
            {
                FpgHudResourcePresentation candidate = definitions[index];
                if (candidate != null && candidate.Kind == kind)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool TryGetThreatDefinition(
            int presentationKey,
            out CombatThreatPresentationDefinition definition)
        {
            CombatThreatPresentationDefinition[] definitions = threatDefinitions
                ?? Array.Empty<CombatThreatPresentationDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                CombatThreatPresentationDefinition candidate = definitions[index];
                if (candidate != null && candidate.PresentationKey == presentationKey)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool TryGetHitDefinition(
            CombatHitPresentationKind kind,
            out CombatHitPresentationDefinition definition)
        {
            CombatHitPresentationDefinition[] definitions = hitDefinitions
                ?? Array.Empty<CombatHitPresentationDefinition>();
            for (int index = 0; index < definitions.Length; index++)
            {
                CombatHitPresentationDefinition candidate = definitions[index];
                if (candidate != null && candidate.Kind == kind)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool TryValidateStatic(out string error)
        {
            error = string.Empty;
            if (sorting == null || !sorting.TryValidate(out error)
                || poolCapacities == null || !poolCapacities.TryValidate(out error)
                || !TryValidateThreatDefinitions(out error)
                || !TryValidateHitDefinitions(out error)
                || !TryValidateFormalHudResources(out error)
                || formalDamagePopup == null
                || !formalDamagePopup.TryValidate(out error)
                || formalReticle == null
                || !formalReticle.TryValidate(out error))
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = "Combat presentation global configuration is missing.";
                }

                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidate(out string error)
        {
            return TryValidateStatic(out error);
        }

        private bool TryValidateThreatDefinitions(out string error)
        {
            if (threatDefinitions == null || threatDefinitions.Length != 3)
            {
                error = "Combat presentation profile requires exactly three D0 threat definitions.";
                return false;
            }

            bool hasFast = false;
            bool hasVolley = false;
            bool hasHeavy = false;
            for (int index = 0; index < threatDefinitions.Length; index++)
            {
                CombatThreatPresentationDefinition definition = threatDefinitions[index];
                if (definition == null
                    || !Enum.IsDefined(typeof(CombatThreatPresentationKind), definition.Kind)
                    || !Enum.IsDefined(typeof(CombatThreatTelegraphShape), definition.TelegraphShape)
                    || definition.PresentationKey <= 0
                    || !IsFinitePositive(definition.TelegraphDuration)
                    || !IsFinitePositive(definition.ReleaseDuration)
                    || !IsVisible(definition.PrimaryColor)
                    || !IsVisible(definition.SecondaryColor)
                    || definition.SortingOrder != sorting.WorldEffectsOrder)
                {
                    error = $"Threat presentation definition {index} is invalid.";
                    return false;
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    CombatThreatPresentationDefinition previous =
                        threatDefinitions[previousIndex];
                    if (previous != null
                        && previous.PresentationKey == definition.PresentationKey)
                    {
                        error =
                            $"Threat presentation key {definition.PresentationKey} is duplicated.";
                        return false;
                    }
                }

                switch (definition.Kind)
                {
                    case CombatThreatPresentationKind.FastUninterceptable:
                        if (hasFast
                            || definition.PresentationKey != FastThreatPresentationKey
                            || definition.TelegraphShape !=
                                CombatThreatTelegraphShape.SourcePulse
                            || definition.ShowsInterceptionMarker
                            || definition.AllowsWeakpointInterrupt)
                        {
                            error = "Fast threat presentation language is invalid.";
                            return false;
                        }

                        hasFast = true;
                        break;

                    case CombatThreatPresentationKind.InterceptableVolley:
                        if (hasVolley
                            || definition.PresentationKey !=
                                InterceptableVolleyThreatPresentationKey
                            || definition.TelegraphShape !=
                                CombatThreatTelegraphShape.ProjectileOutline
                            || !definition.ShowsInterceptionMarker
                            || definition.AllowsWeakpointInterrupt)
                        {
                            error =
                                "Interceptable volley presentation language is invalid.";
                            return false;
                        }

                        hasVolley = true;
                        break;

                    case CombatThreatPresentationKind.HeavyWeakpoint:
                        if (hasHeavy
                            || definition.PresentationKey !=
                                HeavyWeakpointThreatPresentationKey
                            || definition.TelegraphShape !=
                                CombatThreatTelegraphShape.WeakpointLock
                            || definition.ShowsInterceptionMarker
                            || !definition.AllowsWeakpointInterrupt)
                        {
                            error =
                                "Heavy weakpoint presentation language is invalid.";
                            return false;
                        }

                        hasHeavy = true;
                        break;
                }
            }

            if (!hasFast || !hasVolley || !hasHeavy)
            {
                error =
                    "Combat presentation profile must cover every D0 threat kind.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateHitDefinitions(out string error)
        {
            if (hitDefinitions == null || hitDefinitions.Length != 3)
            {
                error =
                    "Combat presentation profile requires body, weakpoint and intercept hit definitions.";
                return false;
            }

            bool hasBody = false;
            bool hasWeakpoint = false;
            bool hasIntercept = false;
            for (int index = 0; index < hitDefinitions.Length; index++)
            {
                CombatHitPresentationDefinition definition = hitDefinitions[index];
                if (definition == null
                    || !Enum.IsDefined(typeof(CombatHitPresentationKind), definition.Kind)
                    || !Enum.IsDefined(typeof(CombatHitFeedbackShape), definition.FeedbackShape)
                    || !IsFinitePositive(definition.Duration)
                    || !IsVisible(definition.PrimaryColor)
                    || !IsVisible(definition.SecondaryColor)
                    || definition.SortingOrder != sorting.ScreenEffectsOrder)
                {
                    error = $"Hit presentation definition {index} is invalid.";
                    return false;
                }

                switch (definition.Kind)
                {
                    case CombatHitPresentationKind.Body:
                        if (hasBody
                            || definition.FeedbackShape != CombatHitFeedbackShape.Burst)
                        {
                            error =
                                "Body hit presentation must be a single burst.";
                            return false;
                        }

                        hasBody = true;
                        break;

                    case CombatHitPresentationKind.Weakpoint:
                        if (hasWeakpoint
                            || definition.FeedbackShape !=
                                CombatHitFeedbackShape.Diamond)
                        {
                            error =
                                "Weakpoint hit presentation must be a single diamond.";
                            return false;
                        }

                        hasWeakpoint = true;
                        break;

                    case CombatHitPresentationKind.Intercept:
                        if (hasIntercept
                            || definition.FeedbackShape !=
                                CombatHitFeedbackShape.Shatter)
                        {
                            error =
                                "Intercept hit presentation must be a single shatter.";
                            return false;
                        }

                        hasIntercept = true;
                        break;
                }
            }

            if (!hasBody || !hasWeakpoint || !hasIntercept)
            {
                error =
                    "Combat presentation profile must cover every shared hit kind.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateFormalHudResources(out string error)
        {
            error = string.Empty;
            if (formalHudResources == null || formalHudResources.Length != 3)
            {
                error = "Formal HUD requires life, barrier and ammo definitions.";
                return false;
            }

            bool hasLife = false;
            bool hasBarrier = false;
            bool hasAmmo = false;
            for (int index = 0; index < formalHudResources.Length; index++)
            {
                FpgHudResourcePresentation definition = formalHudResources[index];
                if (definition == null)
                {
                    error = "Formal HUD resource definition is missing.";
                    return false;
                }

                if (!definition.TryValidate(out error))
                {
                    return false;
                }

                for (int previousIndex = 0; previousIndex < index; previousIndex++)
                {
                    FpgHudResourcePresentation previous =
                        formalHudResources[previousIndex];
                    if (previous != null
                        && (previous.Kind == definition.Kind
                            || previous.Order == definition.Order))
                    {
                        error = "Formal HUD resource kinds and orders must be unique.";
                        return false;
                    }
                }

                switch (definition.Kind)
                {
                    case FpgHudResourceKind.Life:
                        hasLife = true;
                        break;
                    case FpgHudResourceKind.Barrier:
                        hasBarrier = true;
                        break;
                    case FpgHudResourceKind.Ammo:
                        hasAmmo = true;
                        break;
                }
            }

            if (!hasLife || !hasBarrier || !hasAmmo)
            {
                error = "Formal HUD resource coverage is incomplete.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsVisible(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g)
                && IsFinite(value.b) && IsFinite(value.a) && value.a > 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
