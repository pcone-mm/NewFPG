using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Unity
{
    [DefaultExecutionOrder(-9900)]
    [DisallowMultipleComponent]
    public sealed class FpgFormalPlayerComposer : MonoBehaviour
    {
        [Header("Scene Ownership")]
        [SerializeField] private Transform actorsRoot;
        [SerializeField] private CombatPresentationProfile presentationProfile;

        [Header("Formal Runtime")]
        [SerializeField] private FpgFormalCombatPortFactory combatPortFactory;
        [SerializeField] private FpgFormalPlayerTickDriver playerTickDriver;
        [SerializeField] private FpgRoomEncounterDirector encounterDirector;
        [SerializeField] private FpgFormalPlayerPresentationBridge presentationBridge;

        private FpgPlayableCharacterSelection activeSelection;
        private D0CharacterDefinition activeDefinition;
        private FpgPlayerEntityView activeEntity;
        private bool presentationActivated;

        public Transform ActorsRoot => actorsRoot;
        public CombatPresentationProfile PresentationProfile => presentationProfile;
        public FpgFormalCombatPortFactory CombatPortFactory => combatPortFactory;
        public FpgFormalPlayerTickDriver PlayerTickDriver => playerTickDriver;
        public FpgRoomEncounterDirector EncounterDirector => encounterDirector;
        public FpgFormalPlayerPresentationBridge PresentationBridge => presentationBridge;
        public FpgPlayableCharacterSelection ActiveSelection => activeSelection;
        public D0CharacterDefinition ActiveDefinition => activeDefinition;
        public FpgPlayerEntityView ActiveEntity => activeEntity;
        public bool IsComposed => activeDefinition != null && activeEntity != null;
        public bool IsPresentationActive => presentationActivated;

        public bool TryValidateAuthoring(out string error)
        {
            if (actorsRoot == null || presentationProfile == null
                || combatPortFactory == null || playerTickDriver == null
                || encounterDirector == null || presentationBridge == null)
            {
                error = "Formal player composer requires explicit roots, profile, factory, driver, director and presentation bridge references.";
                return false;
            }

            if (!presentationProfile.TryValidateStatic(out error))
            {
                return false;
            }

            if (!IsOwnedByScene(actorsRoot.gameObject)
                || !IsOwnedByScene(combatPortFactory.gameObject)
                || !IsOwnedByScene(playerTickDriver.gameObject)
                || !IsOwnedByScene(encounterDirector.gameObject)
                || !IsOwnedByScene(presentationBridge.gameObject))
            {
                error = "Formal player composer references must belong to its scene.";
                return false;
            }

            if (playerTickDriver.EncounterDirector != encounterDirector)
            {
                error = "Formal player composer driver must target its encounter director.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryValidateRuntime(out string error)
        {
            if (!TryValidateAuthoring(out error))
            {
                return false;
            }

            if (!IsComposed || activeSelection.CharacterDefinition != activeDefinition)
            {
                error = "Formal player composer has no complete active selection.";
                return false;
            }

            if (activeEntity.transform.parent != actorsRoot
                || activeEntity.gameObject.scene != gameObject.scene)
            {
                error = "Formal player entity must be owned directly by ActorsRoot in the composer scene.";
                return false;
            }

            if (!activeDefinition.TryValidate(out error)
                || !activeEntity.TryValidate(out error)
                || !playerTickDriver.TryValidate(out error))
            {
                return false;
            }

            Actor2DPresenter presenter = activeEntity.ActorPresenter;
            if (presenter == null || !presenter.IsInitialized
                || presenter.PresentationProfile != presentationProfile
                || presenter.RuntimePresentationOverride
                    != activeDefinition.ActorPresentation
                || presenter.RuntimeWeaponDefinition != activeDefinition.Weapon)
            {
                error = "Formal player presenter is not initialized from the active character definition.";
                return false;
            }

            if (activeEntity.Bounds == null || activeEntity.Bounds.enabled)
            {
                error = "Formal player must keep the authored bounds component disabled.";
                return false;
            }

            if (combatPortFactory.PlayerDefinition != activeDefinition
                || combatPortFactory.PlayerEntity != activeEntity
                || encounterDirector.ConfiguredPlayerEntity != activeEntity)
            {
                error = "Formal player runtime ports do not share the composed player binding.";
                return false;
            }

            if (presentationActivated && !activeEntity.gameObject.activeInHierarchy)
            {
                error = "Formal player presentation is marked active while its entity is inactive.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryCompose(
            FpgPlayableCharacterSelection selection,
            out string error)
        {
            if (IsComposed)
            {
                error = "Formal player composer supports one player composition per scene lifetime.";
                return false;
            }

            if (!TryValidateAuthoring(out error))
            {
                return false;
            }

            if (!selection.TryValidate(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Formal player composition requires a valid playable character selection.";
                }

                return false;
            }

            D0CharacterDefinition definition = selection.CharacterDefinition;

            GameObject stagingRoot = null;
            FpgPlayerEntityView stagedEntity = null;
            bool factoryTouched = false;
            bool driverTouched = false;
            bool directorTouched = false;
            bool bridgeTouched = false;
            try
            {
                stagingRoot = CreateInactiveStagingRoot();
                stagedEntity = Instantiate(
                    definition.EntityPrefab,
                    stagingRoot.transform,
                    false);
                stagedEntity.gameObject.SetActive(false);
                stagedEntity.transform.SetParent(actorsRoot, false);
                stagedEntity.gameObject.name =
                    definition.EntityPrefab.gameObject.name
                    + " [Runtime:" + definition.CharacterId + "]";
                stagedEntity.CaptureAuthoredLocalPose();
                stagedEntity.SetGameplayCollidersEnabled(false);stagedEntity.Bounds.enabled = false;

                if (!stagedEntity.TryValidate(out error))
                {
                    return FailComposition(
                        stagedEntity,
                        factoryTouched,
                        driverTouched,
                        directorTouched,
                        bridgeTouched,
                        error,
                        out error);
                }

                Actor2DPresenter presenter = stagedEntity.ActorPresenter;
                if (presenter == null
                    || !presenter.TryConfigureRuntime(
                        stagedEntity.SkeletonAnimation,
                        presentationProfile,
                        true,
                        stagedEntity.VisualRoot,
                        definition.ActorPresentation,
                        out error)
                    || !presenter.TrySetRuntimeWeaponDefinition(
                        definition.Weapon,
                        out error)
                    || !presenter.TryInitialize(out error))
                {
                    return FailComposition(
                        stagedEntity,
                        factoryTouched,
                        driverTouched,
                        directorTouched,
                        bridgeTouched,
                        string.IsNullOrWhiteSpace(error)
                            ? "Formal player presenter composition failed."
                            : error,
                        out error);
                }

                factoryTouched = true;
                if (!combatPortFactory.TryConfigurePlayer(
                        definition,
                        stagedEntity,
                        selection.ThreeCProfile,
                        selection.CombatFeelProfile,
                        out error))
                {
                    return FailComposition(
                        stagedEntity,
                        factoryTouched,
                        driverTouched,
                        directorTouched,
                        bridgeTouched,
                        error,
                        out error);
                }

                driverTouched = true;
                if (!playerTickDriver.TryConfigurePlayer(
                        definition,
                        stagedEntity,
                        selection.ThreeCProfile,
                        combatPortFactory.EffectiveAttackQuerySettings,
                        out error))
                {
                    return FailComposition(
                        stagedEntity,
                        factoryTouched,
                        driverTouched,
                        directorTouched,
                        bridgeTouched,
                        error,
                        out error);
                }

                directorTouched = true;
                if (!encounterDirector.TryConfigurePlayer(stagedEntity, out error))
                {
                    return FailComposition(
                        stagedEntity,
                        factoryTouched,
                        driverTouched,
                        directorTouched,
                        bridgeTouched,
                        error,
                        out error);
                }

                if (presentationBridge != null)
                {
                    bridgeTouched = true;
                    if (!presentationBridge.TryPrepare(
                            selection,
                            stagedEntity,
                            out error))
                    {
                        return FailComposition(
                            stagedEntity,
                            factoryTouched,
                            driverTouched,
                            directorTouched,
                            bridgeTouched,
                            error,
                            out error);
                    }
                }

                activeSelection = selection;
                activeDefinition = definition;
                activeEntity = stagedEntity;
                presentationActivated = false;
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return FailComposition(
                    stagedEntity,
                    factoryTouched,
                    driverTouched,
                    directorTouched,
                    bridgeTouched,
                    "Formal player composition failed: " + exception.Message,
                    out error);
            }
            finally
            {
                DestroyRuntimeObject(stagingRoot);
            }
        }

        public bool TryActivatePlayerPresentation(out string error)
        {
            if (presentationActivated)
            {
                error = string.Empty;
                return true;
            }

            if (!TryValidateRuntime(out error))
            {
                return false;
            }

            activeEntity.gameObject.SetActive(true);
            if (presentationBridge != null
                && !presentationBridge.TryActivate(out error))
            {
                string activationError = string.IsNullOrWhiteSpace(error)
                    ? "Formal player presentation activation failed."
                    : error;
                ClearPlayerComposition();
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = activationError;
                }

                return false;
            }

            presentationActivated = true;
            error = string.Empty;
            return true;
        }

        public void ClearPlayerComposition()
        {
            FpgPlayerEntityView entity = activeEntity;

            // Director owns the session/runtime bundle and must release it
            // before the other bindings can forget or destroy the entity.
            encounterDirector?.ClearPlayerBinding();
            presentationBridge?.Clear();
            playerTickDriver?.ClearPlayerBinding();
            combatPortFactory?.ClearPlayerBinding();

            if (entity != null)
            {
                entity.SetGameplayCollidersEnabled(false);
                entity.gameObject.SetActive(false);
            }

            activeSelection = default(FpgPlayableCharacterSelection);
            activeDefinition = null;
            activeEntity = null;
            presentationActivated = false;
            DestroyRuntimeObject(entity == null ? null : entity.gameObject);
        }

        private GameObject CreateInactiveStagingRoot()
        {
            GameObject stagingRoot = new GameObject("FormalPlayerCompositionStaging");
            stagingRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(stagingRoot, gameObject.scene);
            stagingRoot.transform.SetParent(actorsRoot, false);
            return stagingRoot;
        }

        private bool FailComposition(
            FpgPlayerEntityView stagedEntity,
            bool factoryTouched,
            bool driverTouched,
            bool directorTouched,
            bool bridgeTouched,
            string message,
            out string error)
        {
            if (directorTouched)
            {
                encounterDirector?.ClearPlayerBinding();
            }

            if (bridgeTouched)
            {
                presentationBridge?.Clear();
            }

            if (driverTouched)
            {
                playerTickDriver?.ClearPlayerBinding();
            }

            if (factoryTouched)
            {
                combatPortFactory?.ClearPlayerBinding();
            }

            DestroyRuntimeObject(
                stagedEntity == null ? null : stagedEntity.gameObject);
            activeSelection = default(FpgPlayableCharacterSelection);
            activeDefinition = null;
            activeEntity = null;
            presentationActivated = false;
            error = string.IsNullOrWhiteSpace(message)
                ? "Formal player composition failed."
                : message;
            return false;
        }

        private bool IsOwnedByScene(GameObject candidate)
        {
            return candidate != null
                && candidate.scene.IsValid()
                && candidate.scene == gameObject.scene;
        }

        private static void DestroyRuntimeObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
