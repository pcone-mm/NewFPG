using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Applies the authored D0 3C profile to the scene-facing presentation
    /// components. The installer still prepares the initial scene structure,
    /// but runtime startup and restart are the authoritative application path.
    /// </summary>
    public static class D0ThreeCRuntimeProfileApplier
    {
        public static bool TryApplyAuthoredPresentation(
            BattleSceneContext context,
            out string error)
        {
            error = string.Empty;
            if (context == null)
            {
                error = "D0 3C runtime application requires a BattleSceneContext.";
                return false;
            }

            BattleScenarioConfig scenarioConfig = context.ScenarioConfig;
            D0CombatScenarioDefinition scenario = scenarioConfig == null
                ? null
                : scenarioConfig.AuthoredScenario;
            D0ThreeCProfile profile = scenario == null ? null : scenario.ThreeCProfile;
            if (profile == null)
            {
                error = "D0 3C runtime application requires an authored D0 3C profile.";
                return false;
            }

            return TryApplyPresentation(context, profile, out error);
        }

        /// <summary>
        /// Applies only presentation/input-facing values. Deterministic combat
        /// values such as aim distance and input buffering are rebuilt by the
        /// owning BattleSessionHost when it initializes or restarts a session.
        /// </summary>
        public static bool TryApplyPresentation(
            BattleSceneContext context,
            D0ThreeCProfile profile,
            out string error)
        {
            error = string.Empty;
            if (context == null)
            {
                error = "D0 3C runtime application requires a BattleSceneContext.";
                return false;
            }

            if (profile == null)
            {
                error = "D0 3C runtime application requires a profile.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                return false;
            }

            D0CombatScenarioDefinition scenario = context.ScenarioConfig == null
                ? null
                : context.ScenarioConfig.AuthoredScenario;
            if (scenario == null || scenario.ThreeCProfile != profile)
            {
                error = "The supplied D0 3C profile is not the active authored profile for this scene.";
                return false;
            }

            if (context.MainCamera == null)
            {
                error = "D0 3C runtime application requires the scene Main Camera.";
                return false;
            }

            if (context.PlayerAnchor == null)
            {
                error = "D0 3C runtime application requires the scene PlayerAnchor.";
                return false;
            }

            CombatLabPlayerController playerController =
                context.PlayerAnchor.GetComponent<CombatLabPlayerController>();
            if (playerController == null)
            {
                error = "D0 3C runtime application requires CombatLabPlayerController on PlayerAnchor.";
                return false;
            }

            CombatAimReticle reticle = context.CombatAimReticle;
            D0ShotCameraFeedbackController shotFeedback =
                context.D0ShotCameraFeedbackController;
            D0PlayerBarrierPresentationController barrier =
                ResolveBarrier(context);
            LayeredAimIndicatorGraphic indicatorGraphic = reticle == null
                ? null
                : reticle.GetComponent<LayeredAimIndicatorGraphic>();
            PlayerAimIndicatorPresenter indicatorPresenter = reticle == null
                ? null
                : reticle.GetComponent<PlayerAimIndicatorPresenter>();
            D0WeaponDefinition weapon = scenario.Player == null
                ? null
                : scenario.Player.Weapon;

            if (reticle == null || shotFeedback == null || barrier == null
                || indicatorGraphic == null || indicatorPresenter == null
                || weapon == null)
            {
                error = "D0 runtime presentation requires reticle, aim indicator, shot feedback, barrier and player weapon bindings.";
                return false;
            }

            if (!playerController.TryValidateConfigurationForD0Preview(out error))
            {
                return false;
            }

            // Scene components persist only structural references. The active
            // Scenario injects the authoritative 3C asset before strict runtime
            // binding validation.
            shotFeedback.Configure(
                context.SessionHost,
                profile,
                context.MainCamera);
            indicatorPresenter.Configure(
                context.SessionHost,
                indicatorGraphic,
                weapon);
            if (!reticle.TrySetThreeCProfile(profile, out error)
                || !barrier.TrySetThreeCProfile(profile, out error)
                || !playerController.TryApplyTwoPointFiveDCameraProfile(
                    profile,
                    context.MainCamera,
                    out error))
            {
                return false;
            }

            if (!shotFeedback.TryValidate(out error)
                || !reticle.TryValidate(out error)
                || !indicatorPresenter.TryValidate(out error)
                || !barrier.TryValidate(out error))
            {
                return false;
            }

            return true;
        }

        private static D0PlayerBarrierPresentationController ResolveBarrier(
            BattleSceneContext context)
        {
            Actor2DPresenter playerPresenter = context.D0PlayerActorPresenter;
            if (playerPresenter == null)
            {
                return null;
            }

            D0PlayerBarrierPresentationController[] barriers =
                playerPresenter.GetComponentsInChildren<D0PlayerBarrierPresentationController>(true);
            return barriers.Length == 1 ? barriers[0] : null;
        }
    }
}
