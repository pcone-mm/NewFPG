using System;
using System.Collections;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Enemy;
using FPG.Demo.Player;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CombatLabRuntimeHarness = FPG.Demo.Tests.PlayMode.CombatLabPlayModeRuntime;
using UnityEngine.UI;

namespace FPG.Demo.Tests.PlayMode
{
    public sealed class SceneContractTests
    {
        private int _originalTargetFrameRate;
        private int _originalVSyncCount;

        [SetUp]
        public void SetUp()
        {
            _originalTargetFrameRate = Application.targetFrameRate;
            _originalVSyncCount = QualitySettings.vSyncCount;
        }

        [TearDown]
        public void TearDown()
        {
            Application.targetFrameRate = _originalTargetFrameRate;
            QualitySettings.vSyncCount = _originalVSyncCount;
        }

[UnityTest]
        public IEnumerator BootSelectionComposesFeiIntoFormalRoom()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);

            Scene bootScene = SceneManager.GetSceneByName("Boot");
            GameBootstrap bootstrap =
                FindComponentInScene<GameBootstrap>(bootScene);
            Assert.That(bootstrap, Is.Not.Null);

            yield return WaitForBootstrapState(
                bootstrap,
                BootstrapState.WaitingForCharacterSelection,
                5f);

            Assert.That(bootstrap.State,
                Is.EqualTo(BootstrapState.WaitingForCharacterSelection),
                bootstrap.LastError);
            Assert.That(
                FindComponentsInScene<D0PlayerEntityView>(bootScene),
                Is.Empty);
            Assert.That(bootstrap.CharacterChoices.Count, Is.EqualTo(1));
            Assert.That(bootstrap.CharacterChoices[0].IsSelectable, Is.True);
            Assert.That(bootstrap.RoomEntrances.Count, Is.GreaterThan(0));
            for (int entranceIndex = 0;
                entranceIndex < bootstrap.RoomEntrances.Count;
                entranceIndex++)
            {
                Assert.That(
                    bootstrap.RoomEntrances[entranceIndex].IsSelectable,
                    Is.False);
            }

            Assert.That(
                bootstrap.TrySelectCharacter(
                    bootstrap.CharacterChoices[0],
                    out string characterError),
                Is.True,
                characterError);
            Assert.That(bootstrap.SelectedPlayerSelection.CharacterId,
                Is.EqualTo("fei"));
            Assert.That(bootstrap.State,
                Is.EqualTo(BootstrapState.WaitingForRoomSelection));
            Assert.That(bootstrap.RoomEntrances, Is.Not.Empty);
            Assert.That(bootstrap.RoomEntrances[0].IsSelectable, Is.True);

            Assert.That(
                bootstrap.TryEnterRoom(
                    bootstrap.RoomEntrances[0],
                    out string roomError),
                Is.True,
                roomError);
            yield return WaitForBootstrap(bootstrap, 10f);
            yield return new WaitForEndOfFrame();

            Scene formalRoom = SceneManager.GetSceneByName("FormalRoom");
            Assert.That(bootScene.isLoaded, Is.True);
            Assert.That(formalRoom.isLoaded, Is.True);
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(formalRoom));
            Assert.That(bootstrap.State, Is.EqualTo(BootstrapState.Running),
                bootstrap.LastError);
            Assert.That(bootstrap.ActiveFormalHost, Is.Not.Null);
            Assert.That(bootstrap.ActiveFormalSceneHost, Is.Not.Null);
            Assert.That(bootstrap.ActiveEncounterDirector, Is.Not.Null);

            D0PlayerEntityView[] players =
                FindComponentsInScene<D0PlayerEntityView>(formalRoom);
            Assert.That(players, Has.Length.EqualTo(1));
            D0PlayerEntityView player = players[0];
            FpgFormalEncounterHost formalHost =
                bootstrap.ActiveFormalSceneHost;
            FpgFormalPlayerComposer composer = formalHost.PlayerComposer;
            FpgFormalCombatPortFactory factory =
                formalHost.CombatPortFactory as FpgFormalCombatPortFactory;
            FpgFormalPlayerTickDriver playerDriver =
                formalHost.PlayerInputPort as FpgFormalPlayerTickDriver;

            Assert.That(formalHost.ActivePlayerEntity, Is.SameAs(player));
            Assert.That(formalHost.ActivePlayerDefinition,
                Is.SameAs(bootstrap.SelectedPlayerSelection.CharacterDefinition));
            Assert.That(composer, Is.Not.Null);
            Assert.That(factory, Is.Not.Null);
            Assert.That(playerDriver, Is.Not.Null);
            Assert.That(factory.PlayerEntity, Is.SameAs(player));
            Assert.That(factory.PlayerDefinition,
                Is.SameAs(formalHost.ActivePlayerDefinition));
            Assert.That(playerDriver.PlayerEntity, Is.SameAs(player));
            Assert.That(playerDriver.PlayerDefinition,
                Is.SameAs(formalHost.ActivePlayerDefinition));
            Assert.That(
                formalHost.EncounterDirector.ConfiguredPlayerEntity,
                Is.SameAs(player));
            Assert.That(player.ActorPresenter, Is.Not.Null);
            Assert.That(player.ActorPresenter.IsInitialized, Is.True);
            Assert.That(player.ActorPresenter.RuntimePresentationOverride,
                Is.SameAs(formalHost.ActivePlayerDefinition.ActorPresentation));
            Assert.That(player.ActorPresenter.PresentationProfile,
                Is.SameAs(composer.PresentationProfile));

            Camera formalCamera = FindComponentInScene<Camera>(formalRoom);
            Assert.That(formalCamera, Is.Not.Null);
            Plane[] planes =
                GeometryUtility.CalculateFrustumPlanes(formalCamera);
            Renderer[] playerRenderers =
                player.GetComponentsInChildren<Renderer>(false);
            bool visible = false;
            for (int index = 0; index < playerRenderers.Length; index++)
            {
                Renderer renderer = playerRenderers[index];
                visible |= renderer.enabled
                    && GeometryUtility.TestPlanesAABB(
                        planes,
                        renderer.bounds);
            }

            Assert.That(visible, Is.True,
                "The composed Fei presentation must be inside the formal camera frustum.");
            Assert.That(
                formalHost.TryValidate(out string formalError),
                Is.True,
                formalError);

            CombatAimReticle reticle =
                playerDriver.AimViewportSourceComponent as CombatAimReticle;
            Assert.That(reticle, Is.Not.Null);
            Assert.That(
                bootstrap.ActiveFormalHost.TryPause(out string pauseError),
                Is.True,
                pauseError);
            yield return null;
            Assert.That(reticle.IsInputFrozen, Is.True);
            Assert.That(
                bootstrap.ActiveFormalHost.TryResume(out string resumeError),
                Is.True,
                resumeError);
            yield return null;
            Assert.That(reticle.IsInputFrozen, Is.False);

            Assert.That(
                bootstrap.ActiveFormalHost.TryRestart(out string restartError),
                Is.True,
                restartError);
            yield return new WaitForEndOfFrame();

            D0PlayerEntityView[] restartedPlayers =
                FindComponentsInScene<D0PlayerEntityView>(formalRoom);
            Assert.That(restartedPlayers, Has.Length.EqualTo(1));
            Assert.That(restartedPlayers[0], Is.SameAs(player));
            Assert.That(formalHost.ActivePlayerEntity, Is.SameAs(player));
            Assert.That(
                bootstrap.SelectedPlayerSelection.CharacterDefinition,
                Is.SameAs(formalHost.ActivePlayerDefinition));
            Assert.That(player.ActorPresenter.IsTerminal, Is.False);
            Assert.That(
                composer.PresentationBridge.CameraFeedback.CurrentKick,
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                formalHost.TryValidate(out string restartValidationError),
                Is.True,
                restartValidationError);

            FpgEncounterFailureReason observedFailureReason =
                FpgEncounterFailureReason.None;
            string observedFailureMessage = string.Empty;
            Action<FpgEncounterFailureReason, string> failureHandler =
                (reason, message) =>
                {
                    observedFailureReason = reason;
                    observedFailureMessage = message;
                };
            formalHost.EncounterDirector.Failed += failureHandler;
            try
            {
                float tickDeadline = Time.realtimeSinceStartup + 10f;
                while (formalHost.Session != null
                    && formalHost.Session.ExecutedTickCount < 120
                    && !formalHost.EncounterDirector.IsTerminal
                    && Time.realtimeSinceStartup < tickDeadline)
                {
                    yield return null;
                }
            }
            finally
            {
                formalHost.EncounterDirector.Failed -= failureHandler;
            }

            Assert.That(formalHost.Session, Is.Not.Null);
            Assert.That(
                formalHost.Session.ExecutedTickCount,
                Is.GreaterThanOrEqualTo(120),
                "Formal encounter did not advance 120 ticks before timeout. "
                + observedFailureReason + ": " + observedFailureMessage);
            Assert.That(
                formalHost.EncounterDirector.Phase,
                Is.Not.EqualTo(FpgEncounterPhase.Faulted)
                    .And.Not.EqualTo(FpgEncounterPhase.Failed),
                observedFailureReason + ": " + observedFailureMessage);
        }

[UnityTest]
        public IEnumerator MissingFormalRoomRestoresBootRoomSelection()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);

            Scene bootScene = SceneManager.GetSceneByName("Boot");
            GameBootstrap bootstrap =
                FindComponentInScene<GameBootstrap>(bootScene);
            Assert.That(bootstrap, Is.Not.Null);
            yield return WaitForBootstrapState(
                bootstrap,
                BootstrapState.WaitingForCharacterSelection,
                5f);

            GameBootstrapConfig config = bootstrap.Config;
            string originalSceneName = config.RoomSceneName;
            try
            {
                SetPrivateField(
                    config,
                    "combatLabSceneName",
                    "__MissingFormalRoom__");
                Assert.That(
                    bootstrap.TrySelectCharacter(
                        bootstrap.CharacterChoices[0],
                        out string characterError),
                    Is.True,
                    characterError);

                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        @"Scene '__MissingFormalRoom__' couldn't be loaded"));
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        @"\[GameBootstrap\] Unable to start loading scene '__MissingFormalRoom__'"));
                Assert.That(
                    bootstrap.TryEnterRoom(
                        bootstrap.RoomEntrances[0],
                        out string roomError),
                    Is.True,
                    roomError);

                yield return WaitForBootstrapState(
                    bootstrap,
                    BootstrapState.WaitingForRoomSelection,
                    5f);

                Assert.That(bootstrap.SelectedPlayerSelection.CharacterId,
                    Is.EqualTo("fei"));
                Assert.That(bootstrap.LastError,
                    Does.StartWith(
                        "Unable to start loading scene '__MissingFormalRoom__'"));
                Assert.That(bootstrap.RoomEntrances[0].IsSelectable, Is.True);
                Camera bootCamera = FindComponentInScene<Camera>(bootScene);
                Assert.That(bootCamera, Is.Not.Null);
                Assert.That(bootCamera.enabled, Is.True);
            }
            finally
            {
                SetPrivateField(
                    config,
                    "combatLabSceneName",
                    originalSceneName);
            }
        }

[UnityTest]
        public IEnumerator PreloadedFormalFailureHidesPresentationAndCanRetry()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);

            Scene bootScene = SceneManager.GetSceneByName("Boot");
            GameBootstrap bootstrap =
                FindComponentInScene<GameBootstrap>(bootScene);
            Assert.That(bootstrap, Is.Not.Null);
            yield return WaitForBootstrapState(
                bootstrap,
                BootstrapState.WaitingForCharacterSelection,
                5f);

            yield return SceneManager.LoadSceneAsync(
                "FormalRoom",
                LoadSceneMode.Additive);
            Scene formalScene = SceneManager.GetSceneByName("FormalRoom");
            FpgFormalEncounterHost formalHost =
                FindComponentInScene<FpgFormalEncounterHost>(formalScene);
            Assert.That(formalHost, Is.Not.Null);

            FpgFormalPlayerComposer composer = formalHost.PlayerComposer;
            FpgFormalPlayerPresentationBridge originalBridge =
                composer.PresentationBridge;
            SetPrivateField(composer, "presentationBridge", null);
            try
            {
                Assert.That(
                    bootstrap.TrySelectCharacter(
                        bootstrap.CharacterChoices[0],
                        out string characterError),
                    Is.True,
                    characterError);

                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        @"\[GameBootstrap\] Formal room player composition is invalid:"));
                Assert.That(
                    bootstrap.TryEnterRoom(
                        bootstrap.RoomEntrances[0],
                        out string roomError),
                    Is.True,
                    roomError);
                yield return WaitForBootstrapState(
                    bootstrap,
                    BootstrapState.WaitingForRoomSelection,
                    5f);

                Assert.That(formalScene.isLoaded, Is.True);
                Assert.That(formalHost.CameraRoot.gameObject.activeSelf, Is.False);
                Assert.That(
                    formalHost.PresentationRoot.gameObject.activeSelf,
                    Is.False);
                Camera bootCamera = FindComponentInScene<Camera>(bootScene);
                Assert.That(bootCamera, Is.Not.Null);
                Assert.That(bootCamera.enabled, Is.True);

                SetPrivateField(
                    composer,
                    "presentationBridge",
                    originalBridge);
                Assert.That(
                    bootstrap.TryEnterRoom(
                        bootstrap.RoomEntrances[0],
                        out string retryError),
                    Is.True,
                    retryError);
                yield return WaitForBootstrap(bootstrap, 10f);
                yield return new WaitForEndOfFrame();

                Assert.That(bootstrap.State, Is.EqualTo(BootstrapState.Running),
                    bootstrap.LastError);
                Assert.That(formalHost.CameraRoot.gameObject.activeSelf, Is.True);
                Assert.That(
                    formalHost.PresentationRoot.gameObject.activeSelf,
                    Is.True);
                Assert.That(bootCamera.enabled, Is.False);
            }
            finally
            {
                if (composer != null)
                {
                    SetPrivateField(
                        composer,
                        "presentationBridge",
                        originalBridge);
                }
            }
        }



[UnityTest]
        public IEnumerator FormalRoomDirectPlaytestComposesCatalogDefaultFei()
        {
            yield return SceneManager.LoadSceneAsync(
                "FormalRoom",
                LoadSceneMode.Single);

            Scene scene = SceneManager.GetSceneByName("FormalRoom");
            FpgFormalEncounterHost formalHost =
                FindComponentInScene<FpgFormalEncounterHost>(scene);
            FpgEncounterHost encounterHost =
                FindComponentInScene<FpgEncounterHost>(scene);
            Assert.That(formalHost, Is.Not.Null);
            Assert.That(encounterHost, Is.Not.Null);
            Assert.That(formalHost.IsPlayerComposed, Is.False);

            Assert.That(
                formalHost.TryComposeDefaultPlayer(out string composeError),
                Is.True,
                composeError);
            Assert.That(
                formalHost.ActivePlayerDefinition,
                Is.SameAs(formalHost.PlayableCharacterCatalog.DefaultCharacter));
            Assert.That(
                formalHost.TryValidateRuntime(out string runtimeError),
                Is.True,
                runtimeError);
            Assert.That(
                encounterHost.TryPrepareAndStart(out string startError),
                Is.True,
                startError);
            Assert.That(
                formalHost.TryActivatePlayerPresentation(
                    out string presentationError),
                Is.True,
                presentationError);
            yield return new WaitForEndOfFrame();

            Assert.That(formalHost.ActivePlayerEntity, Is.Not.Null);
            Assert.That(
                formalHost.ActivePlayerEntity.gameObject.activeInHierarchy,
                Is.True);
            Assert.That(formalHost.ActivePlayerEntity.ActorPresenter.IsInitialized,
                Is.True);
            Assert.That(
                formalHost.TryValidate(out string finalError),
                Is.True,
                finalError);

            D0PlayerEntityView firstPlayer = formalHost.ActivePlayerEntity;
            encounterHost.StopAndClear();
            formalHost.ClearPlayerComposition();
            yield return new WaitForEndOfFrame();

            Assert.That(formalHost.IsPlayerComposed, Is.False);
            Assert.That(firstPlayer == null, Is.True,
                "Clearing a retained FormalRoom must destroy its runtime player.");
            Assert.That(
                formalHost.TryComposeDefaultPlayer(out string retryComposeError),
                Is.True,
                retryComposeError);
            Assert.That(
                encounterHost.TryPrepareAndStart(out string retryStartError),
                Is.True,
                retryStartError);
            Assert.That(
                formalHost.TryActivatePlayerPresentation(
                    out string retryPresentationError),
                Is.True,
                retryPresentationError);
            yield return new WaitForEndOfFrame();

            Assert.That(formalHost.ActivePlayerEntity, Is.Not.Null);
            Assert.That(formalHost.ActivePlayerEntity, Is.Not.SameAs(firstPlayer));
            Assert.That(
                formalHost.TryValidate(out string retryValidationError),
                Is.True,
                retryValidationError);
        }

[UnityTest]
        public IEnumerator FormalHostRejectsSelectionOutsideItsCatalog()
        {
            yield return SceneManager.LoadSceneAsync(
                "FormalRoom",
                LoadSceneMode.Single);

            Scene scene = SceneManager.GetSceneByName("FormalRoom");
            FpgFormalEncounterHost formalHost =
                FindComponentInScene<FpgFormalEncounterHost>(scene);
            Assert.That(formalHost, Is.Not.Null);

            FpgPlayableCharacterSelection catalogSelection =
                formalHost.PlayableCharacterCatalog.DefaultSelection;
            D0CharacterDefinition clonedDefinition =
                UnityEngine.Object.Instantiate(
                    catalogSelection.CharacterDefinition);
            try
            {
                FpgPlayableCharacterSelection foreignSelection =
                    new FpgPlayableCharacterSelection(
                        clonedDefinition,
                        catalogSelection.ThreeCProfile,
                        catalogSelection.SelectionPreviewPrefab);
                Assert.That(foreignSelection.TryValidate(out string selectionError),
                    Is.True,
                    selectionError);
                Assert.That(
                    formalHost.TryComposePlayer(
                        foreignSelection,
                        out string composeError),
                    Is.False);
                Assert.That(composeError,
                    Does.Contain("does not match the FormalRoom catalog entry"));
                Assert.That(formalHost.IsPlayerComposed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clonedDefinition);
            }
        }


[UnityTest]
        public IEnumerator DirectFormalRestartResetsHostTickSequence()
        {
            yield return SceneManager.LoadSceneAsync(
                "FormalRoom",
                LoadSceneMode.Single);

            Scene scene = SceneManager.GetSceneByName("FormalRoom");
            FpgFormalEncounterHost formalHost =
                FindComponentInScene<FpgFormalEncounterHost>(scene);
            FpgEncounterHost encounterHost =
                FindComponentInScene<FpgEncounterHost>(scene);
            Assert.That(formalHost, Is.Not.Null);
            Assert.That(encounterHost, Is.Not.Null);
            Assert.That(
                formalHost.TryComposeDefaultPlayer(out string composeError),
                Is.True,
                composeError);
            Assert.That(
                encounterHost.TryPrepareAndStart(out string startError),
                Is.True,
                startError);
            Assert.That(
                formalHost.TryActivatePlayerPresentation(
                    out string presentationError),
                Is.True,
                presentationError);

            float firstDeadline = Time.realtimeSinceStartup + 5f;
            while (formalHost.Session.ExecutedTickCount < 30
                && Time.realtimeSinceStartup < firstDeadline)
            {
                yield return null;
            }
            Assert.That(formalHost.Session.ExecutedTickCount,
                Is.GreaterThanOrEqualTo(30));

            Assert.That(
                formalHost.EncounterDirector.TryRestart(out string restartError),
                Is.True,
                restartError);

            float secondDeadline = Time.realtimeSinceStartup + 5f;
            while (formalHost.Session.ExecutedTickCount < 20
                && !formalHost.EncounterDirector.IsTerminal
                && Time.realtimeSinceStartup < secondDeadline)
            {
                yield return null;
            }

            Assert.That(formalHost.Session.ExecutedTickCount,
                Is.GreaterThanOrEqualTo(20));
            Assert.That(
                formalHost.Session.CurrentTick.Value,
                Is.EqualTo(formalHost.Session.ExecutedTickCount),
                "A direct Director/F5 restart must reset the host-owned tick sequence.");
            Assert.That(
                formalHost.EncounterDirector.Phase,
                Is.Not.EqualTo(FpgEncounterPhase.Faulted)
                    .And.Not.EqualTo(FpgEncounterPhase.Failed));
        }

[UnityTest]
        public IEnumerator FormalPlayerInputCommitsCombatReloadAndHudState()
        {
            yield return SceneManager.LoadSceneAsync(
                "FormalRoom",
                LoadSceneMode.Single);

            Scene scene = SceneManager.GetSceneByName("FormalRoom");
            FpgFormalEncounterHost formalHost =
                FindComponentInScene<FpgFormalEncounterHost>(scene);
            FpgEncounterHost encounterHost =
                FindComponentInScene<FpgEncounterHost>(scene);
            Assert.That(formalHost, Is.Not.Null);
            Assert.That(encounterHost, Is.Not.Null);
            Assert.That(
                formalHost.TryComposeDefaultPlayer(out string composeError),
                Is.True,
                composeError);

            FpgFormalPlayerTickDriver driver =
                formalHost.PlayerComposer.PlayerTickDriver;
            SetPrivateField(driver, "captureFromDevices", false);
            Assert.That(
                encounterHost.TryPrepareAndStart(out string startError),
                Is.True,
                startError);
            Assert.That(
                formalHost.TryActivatePlayerPresentation(
                    out string presentationError),
                Is.True,
                presentationError);

            int primaryCommitted = 0;
            int secondaryStarted = 0;
            int secondaryCommitted = 0;
            int reloadStarted = 0;
            int reloadCompleted = 0;
            Action<FpgFormalPlayerActionEvent> actionHandler = action =>
            {
                switch (action.Type)
                {
                    case FpgFormalPlayerActionType.PrimaryReleaseCommitted:
                        primaryCommitted++;
                        break;
                    case FpgFormalPlayerActionType.SecondaryChargeStarted:
                        secondaryStarted++;
                        break;
                    case FpgFormalPlayerActionType.SecondaryReleaseCommitted:
                        secondaryCommitted++;
                        break;
                    case FpgFormalPlayerActionType.ReloadStarted:
                        reloadStarted++;
                        break;
                    case FpgFormalPlayerActionType.ReloadCompleted:
                        reloadCompleted++;
                        break;
                }
            };
            driver.ActionCommitted += actionHandler;

            try
            {
                FpgEnemySlot activeSlot = null;
                EnemyRuntime enemyRuntime = null;
                Collider enemyCollider = null;
                float spawnDeadline = Time.realtimeSinceStartup + 8f;
                while (activeSlot == null
                    && !formalHost.EncounterDirector.IsTerminal
                    && Time.realtimeSinceStartup < spawnDeadline)
                {
                    FpgEnemyRoster roster = formalHost.Session.Roster;
                    for (int index = 0; index < roster.Capacity; index++)
                    {
                        FpgEnemySlot candidate = roster.GetSlot(index);
                        if (!candidate.IsActive
                            || !formalHost.CombatRuntime.CombatPort.TryGetEnemyRuntime(
                                candidate.RuntimeId,
                                out EnemyRuntime candidateRuntime)
                            || !formalHost.EnemyEntityPool.TryGet(
                                candidate.RuntimeId,
                                out FpgEnemyEntityHandle handle)
                            || !handle.Binder.TryGetHitPart(
                                0,
                                out Collider candidateCollider,
                                out HitPart ignoredHitPart))
                        {
                            continue;
                        }

                        activeSlot = candidate;
                        enemyRuntime = candidateRuntime;
                        enemyCollider = candidateCollider;
                        break;
                    }

                    if (activeSlot == null)
                    {
                        yield return null;
                    }
                }

                Assert.That(activeSlot, Is.Not.Null,
                    "Formal encounter did not activate a target enemy.");
                Assert.That(enemyRuntime, Is.Not.Null);
                Assert.That(enemyCollider, Is.Not.Null);

                Camera camera = FindComponentInScene<Camera>(scene);
                CombatAimReticle reticle =
                    driver.AimViewportSourceComponent as CombatAimReticle;
                Assert.That(camera, Is.Not.Null);
                Assert.That(reticle, Is.Not.Null);

                Vector3 targetViewport =
                    camera.WorldToViewportPoint(enemyCollider.bounds.center);
                Assert.That(targetViewport.z, Is.GreaterThan(0f));
                reticle.SetViewport(targetViewport);
                yield return null;

                int primaryAmmoBefore =
                    formalHost.CombatRuntime.Player.Weapon.Magazine.Ammo;
                int primaryLifeBefore = enemyRuntime.Combatant.Life;
                float primaryDeadline = Time.realtimeSinceStartup + 5f;
                while (enemyRuntime.Combatant.Life >= primaryLifeBefore
                    && formalHost.CombatRuntime.Player.Weapon.Magazine.Ammo > 0
                    && !formalHost.EncounterDirector.IsTerminal
                    && Time.realtimeSinceStartup < primaryDeadline)
                {
                    targetViewport =
                        camera.WorldToViewportPoint(enemyCollider.bounds.center);
                    reticle.SetViewport(targetViewport);
                    driver.Capture(new UnityInputSnapshot(
                        aimHeld: false,
                        primaryHeld: true,
                        secondaryPressed: false,
                        secondaryReleased: false,
                        reloadPressed: false,
                        pausePressed: false,
                        restartPressed: false));
                    yield return null;
                }
                driver.Capture(new UnityInputSnapshot(
                    aimHeld: false,
                    primaryHeld: false,
                    secondaryPressed: false,
                    secondaryReleased: false,
                    reloadPressed: false,
                    pausePressed: false,
                    restartPressed: false));

                int primaryAmmoAfter =
                    formalHost.CombatRuntime.Player.Weapon.Magazine.Ammo;
                Assert.That(primaryCommitted, Is.GreaterThan(0));
                Assert.That(primaryAmmoAfter, Is.LessThan(primaryAmmoBefore));
                Assert.That(enemyRuntime.Combatant.Life,
                    Is.LessThan(primaryLifeBefore));

                float readyDeadline = Time.realtimeSinceStartup + 5f;
                while (formalHost.CombatRuntime.Player.Weapon.State
                        != WeaponState.Ready
                    && Time.realtimeSinceStartup < readyDeadline)
                {
                    yield return null;
                }
                Assert.That(
                    formalHost.CombatRuntime.Player.Weapon.State,
                    Is.EqualTo(WeaponState.Ready));

                driver.Capture(new UnityInputSnapshot(
                    aimHeld: false,
                    primaryHeld: false,
                    secondaryPressed: false,
                    secondaryReleased: false,
                    reloadPressed: true,
                    pausePressed: false,
                    restartPressed: false));
                yield return null;
                driver.Capture(new UnityInputSnapshot(
                    aimHeld: false,
                    primaryHeld: false,
                    secondaryPressed: false,
                    secondaryReleased: false,
                    reloadPressed: false,
                    pausePressed: false,
                    restartPressed: false));

                int magazineCapacity =
                    formalHost.CombatRuntime.Player.Weapon.Magazine.Capacity;
                float reloadDeadline = Time.realtimeSinceStartup + 8f;
                while (formalHost.CombatRuntime.Player.Weapon.Magazine.Ammo
                        < magazineCapacity
                    && !formalHost.EncounterDirector.IsTerminal
                    && Time.realtimeSinceStartup < reloadDeadline)
                {
                    yield return null;
                }

                Assert.That(reloadStarted, Is.GreaterThan(0));
                Assert.That(reloadCompleted, Is.GreaterThan(0));
                Assert.That(
                    formalHost.CombatRuntime.Player.Weapon.Magazine.Ammo,
                    Is.EqualTo(magazineCapacity));

                targetViewport =
                    camera.WorldToViewportPoint(enemyCollider.bounds.center);
                reticle.SetViewport(targetViewport);
                driver.Capture(new UnityInputSnapshot(
                    aimHeld: true,
                    primaryHeld: false,
                    secondaryPressed: true,
                    secondaryReleased: false,
                    reloadPressed: false,
                    pausePressed: false,
                    restartPressed: false));

                float chargeDeadline = Time.realtimeSinceStartup + 3f;
                while (secondaryStarted == 0
                    && !formalHost.EncounterDirector.IsTerminal
                    && Time.realtimeSinceStartup < chargeDeadline)
                {
                    yield return null;
                }
                Assert.That(secondaryStarted, Is.GreaterThan(0));

                int secondaryAmmoBefore =
                    formalHost.CombatRuntime.Player.Weapon.Magazine.Ammo;
                int secondaryLifeBefore = enemyRuntime.Combatant.Life;
                targetViewport =
                    camera.WorldToViewportPoint(enemyCollider.bounds.center);
                reticle.SetViewport(targetViewport);
                driver.Capture(new UnityInputSnapshot(
                    aimHeld: false,
                    primaryHeld: false,
                    secondaryPressed: false,
                    secondaryReleased: true,
                    reloadPressed: false,
                    pausePressed: false,
                    restartPressed: false));

                float secondaryDeadline = Time.realtimeSinceStartup + 5f;
                while (secondaryCommitted == 0
                    && !formalHost.EncounterDirector.IsTerminal
                    && Time.realtimeSinceStartup < secondaryDeadline)
                {
                    yield return null;
                }

                Assert.That(secondaryCommitted, Is.GreaterThan(0));
                Assert.That(
                    formalHost.CombatRuntime.Player.Weapon.Magazine.Ammo,
                    Is.LessThan(secondaryAmmoBefore));
                Assert.That(enemyRuntime.Combatant.Life,
                    Is.LessThan(secondaryLifeBefore));

                yield return null;
                FpgFormalPlayerPresentationBridge bridge =
                    formalHost.PlayerComposer.PresentationBridge;
                Assert.That(bridge.Snapshot.IsValid, Is.True);
                Assert.That(bridge.PlayerHud.Snapshot.IsValid, Is.True);
                Assert.That(
                    bridge.PlayerHud.Snapshot.Ammo,
                    Is.EqualTo(
                        formalHost.CombatRuntime.Player.Weapon.Magazine.Ammo));
                Assert.That(
                    bridge.PlayerHud.Snapshot.Life,
                    Is.EqualTo(formalHost.CombatRuntime.Player.Combatant.Life));
                Assert.That(
                    bridge.PlayerHud.Snapshot.Barrier,
                    Is.EqualTo(
                        formalHost.CombatRuntime.Player.Combatant.Barrier));
            }
            finally
            {
                driver.ActionCommitted -= actionHandler;
            }
        }




        [UnityTest]
        public IEnumerator CombatLabSceneSatisfiesItsContextContract()
        {
            yield return SceneManager.LoadSceneAsync("CombatLab", LoadSceneMode.Single);

            Scene scene = SceneManager.GetSceneByName("CombatLab");
            BattleSceneContext context = FindComponentInScene<BattleSceneContext>(scene);
            Camera camera = FindComponentInScene<Camera>(scene);
            Light light = FindComponentInScene<Light>(scene);

            Assert.That(context, Is.Not.Null);
            FpgRoomCombatLabBinding roomBinding =
                RequireDefaultCombatLabRoomBinding(context);
            Assert.That(roomBinding.IsInitialized, Is.False,
                "Opening CombatLab directly must not instantiate room content before host initialization.");
            Assert.That(roomBinding.RoomInstance.IsInitialized, Is.False);
            Assert.That(roomBinding.RoomInstance.EnvironmentInstance, Is.Null);
            Assert.That(roomBinding.SpawnPoints.Count, Is.Zero);
            Assert.That(roomBinding.LegacyEnvironmentRoot, Is.Not.Null);
            Assert.That(roomBinding.LegacyEnvironmentRoot.activeInHierarchy, Is.True,
                "The retained D0 Stage environment is the direct-open compatibility fallback.");
            Assert.That(
                context.EncounterSpawnPoints.Count,
                Is.EqualTo(context.ScenarioConfig.AuthoredScenario.StageDefinition.SpawnPoints.Count));
            Assert.That(
                context.TryGetEncounterSpawnPoint(
                    "player-main",
                    out D0SpawnPoint fallbackPlayerSpawn),
                Is.True);
            Assert.That(
                context.TryGetEncounterSpawnPoint(
                    "enemy-main",
                    out D0SpawnPoint fallbackEnemySpawn),
                Is.True);
            Assert.That(fallbackPlayerSpawn, Is.Not.Null);
            Assert.That(fallbackEnemySpawn, Is.Not.Null);
            Assert.That(
                roomBinding.TryGetSpawnPoint("player-main", out _),
                Is.False,
                "Before host initialization the context must resolve the serialized legacy SpawnPoints.");
            RequireD0EnemyBehavior(context);
            RequireD0ShotCameraFeedback(context);
            Assert.That(context.SessionHost, Is.Not.Null);
            Assert.That(context.ScenarioConfig, Is.Not.Null);
            Assert.That(context.HitboxRegistry, Is.Not.Null);
            Assert.That(context.HitboxRegistry.StaticBindingCount, Is.EqualTo(2));
            Assert.That(context.DiagnosticsPresenter, Is.Not.Null);
            Assert.That(context.DiagnosticsPresenter.SessionHost, Is.SameAs(context.SessionHost));
            Assert.That(camera, Is.Not.Null);
            Assert.That(camera, Is.SameAs(context.MainCamera));
            Assert.That(light, Is.Not.Null);
            Assert.That(light.type, Is.EqualTo(LightType.Directional));
            Assert.That(context.AimAnchor.IsChildOf(context.PlayerAnchor), Is.True);
            AssertInitialSpawnAimFacesEnemy(context);

            GameObject playerBody = FindGameObjectInScene(scene, "PlayerBodyHitbox");
            GameObject blocker = FindGameObjectInScene(scene, "SideBlocker");
            D0EnemyEntityView enemyPrefab = context.ScenarioConfig.AuthoredScenario
                .Encounter.InitialSpawnSlot.Enemy.EntityPrefab;
            Assert.That(playerBody, Is.Not.Null);
            Assert.That(enemyPrefab, Is.Not.Null);
            Assert.That(enemyPrefab.TryValidate(out string enemyPrefabError), Is.True, enemyPrefabError);
            Assert.That(blocker, Is.Not.Null);
            Assert.That(playerBody.layer, Is.EqualTo(29));
            Assert.That(enemyPrefab.BodyHitbox.gameObject.layer, Is.EqualTo(29));
            Assert.That(blocker.layer, Is.EqualTo(28));

            string error;
            Assert.That(context.TryValidate(out error), Is.True, error);
        }

        [UnityTest]
        public IEnumerator CombatLabRejectsMissingOrMiswiredD0ShotCameraFeedbackBinding()
        {
            yield return SceneManager.LoadSceneAsync("CombatLab", LoadSceneMode.Single);

            Scene scene = SceneManager.GetSceneByName("CombatLab");
            BattleSceneContext context = FindComponentInScene<BattleSceneContext>(scene);
            D0ShotCameraFeedbackController feedback = RequireD0ShotCameraFeedback(context);
            BattleSessionHost expectedHost = context.SessionHost;
            Camera expectedCamera = context.MainCamera;
            D0ThreeCProfile expectedProfile =
                context.ScenarioConfig.AuthoredScenario.ThreeCProfile;
            GameObject alternateHostObject = new GameObject("AlternateBattleSessionHost");
            BattleSessionHost alternateHost =
                alternateHostObject.AddComponent<BattleSessionHost>();
            GameObject alternateCameraObject = new GameObject("AlternateD0Camera");
            Camera alternateCamera = alternateCameraObject.AddComponent<Camera>();
            D0ThreeCProfile alternateProfile =
                ScriptableObject.CreateInstance<D0ThreeCProfile>();

            try
            {
                SetD0ShotCameraFeedbackBinding(context, null);
                AssertInvalidD0ShotCameraFeedbackBinding(
                    context,
                    "d0ShotCameraFeedbackController");

                SetD0ShotCameraFeedbackBinding(context, feedback);
                feedback.Configure(alternateHost, expectedProfile, expectedCamera);
                AssertInvalidD0ShotCameraFeedbackBinding(
                    context,
                    "must reference sessionHost");

                feedback.Configure(expectedHost, alternateProfile, expectedCamera);
                AssertInvalidD0ShotCameraFeedbackBinding(
                    context,
                    "authoritative D0 3C profile");

                feedback.Configure(expectedHost, expectedProfile, alternateCamera);
                AssertInvalidD0ShotCameraFeedbackBinding(
                    context,
                    "must reference mainCamera");

                D0ShotCameraFeedbackController alternateFeedback =
                    alternateCameraObject.AddComponent<D0ShotCameraFeedbackController>();
                alternateFeedback.Configure(expectedHost, expectedProfile, alternateCamera);
                SetD0ShotCameraFeedbackBinding(context, alternateFeedback);
                AssertInvalidD0ShotCameraFeedbackBinding(
                    context,
                    "must be attached to mainCamera");
            }
            finally
            {
                feedback.Configure(expectedHost, expectedProfile, expectedCamera);
                SetD0ShotCameraFeedbackBinding(context, feedback);
                UnityEngine.Object.Destroy(alternateHostObject);
                UnityEngine.Object.Destroy(alternateCameraObject);
                UnityEngine.Object.Destroy(alternateProfile);
            }

            Assert.That(
                context.TryValidateD0RuntimeBindings(out string restoredError),
                Is.True,
                restoredError);
        }

        [UnityTest]
        public IEnumerator BurstbugPatrolSynchronizesVisualAndGameplayAnchorsAndStopsForAttack()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);
            yield return null;

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            D0EnemyBehaviorController behavior = RequireD0EnemyBehavior(context);
            D0ShotCameraFeedbackController cameraFeedback =
                RequireD0ShotCameraFeedback(context);

            Assert.That(behavior, Is.Not.Null,
                "CombatLab must serialize the enemy behavior bridge on EnemyEntityWorld.");
            Assert.That(behavior.IsInitialized, Is.True);
            Assert.That(behavior.TryValidate(out string behaviorError), Is.True, behaviorError);
            Assert.That(cameraFeedback, Is.Not.Null,
                "CombatLab must serialize the D0 shot-feedback bridge on Main Camera.");
            Assert.That(cameraFeedback.TryValidate(out string cameraFeedbackError), Is.True,
                cameraFeedbackError);

            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            BattleSession session = host.Session;
            UnityBattleInputSource input = new UnityBattleInputSource();
            input.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            input.CaptureAimPose(context.AimAnchor);

            Vector3 visualStart = behavior.VisualRoot.position;
            Vector3 gameplayStart = behavior.GameplayAnchor.position;
            for (int tick = 0; tick <= 120; tick++)
            {
                Assert.That(session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    behavior,
                    out int executedSteps).IsSuccess, Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            Vector3 visualAtThreatStart = behavior.VisualRoot.position;
            Vector3 gameplayAtThreatStart = behavior.GameplayAnchor.position;
            Assert.That(Vector3.Distance(visualAtThreatStart, visualStart), Is.GreaterThan(0.01f),
                "Burstbug must enter from offscreen and begin the authored patrol before the first attack.");

            for (int tick = 121; tick <= 150; tick++)
            {
                Assert.That(session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    behavior,
                    out int executedSteps).IsSuccess, Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            Assert.That(behavior.IsHoldingForAttack, Is.True);
            Assert.That(Vector3.Distance(behavior.VisualRoot.position, visualAtThreatStart),
                Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(behavior.GameplayAnchor.position, gameplayAtThreatStart),
                Is.LessThan(0.0001f));

            for (int tick = 151; tick <= 205; tick++)
            {
                Assert.That(session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    behavior,
                    out int executedSteps).IsSuccess, Is.True);
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            Assert.That(behavior.IsPatrolling, Is.True,
                "Burstbug must resume the original patrol after recovery, rather than hold at the attack point.");
            Assert.That(Vector3.Distance(behavior.VisualRoot.position, visualAtThreatStart),
                Is.GreaterThan(0.01f));
            Vector3 visualDelta = behavior.VisualRoot.position - visualStart;
            Vector3 gameplayDelta = behavior.GameplayAnchor.position - gameplayStart;
            Assert.That(Vector3.Distance(visualDelta, gameplayDelta), Is.LessThan(0.0001f),
                "Visual Burstbug and spatial hitbox anchor must receive the same deterministic patrol offset.");
        }

        [UnityTest]
        public IEnumerator BootedCombatLabViewportAimSelectsVisibleWeakpointAndBodySeparately()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            Assert.That(context, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            D0EnemyEntityView activeEnemy = RequireActiveEnemyEntity(context);
            Assert.That(
                activeEnemy.WeakpointAnchor.IsChildOf(activeEnemy.GameplayAnchor),
                Is.True);
            Assert.That(activeEnemy.WeakpointHitbox.gameObject.layer, Is.EqualTo(29));

            SphereCollider weakpointCollider =
                activeEnemy.WeakpointHitbox as SphereCollider;
            Assert.That(weakpointCollider, Is.Not.Null);
            Assert.That(weakpointCollider.enabled, Is.True);
            Assert.That(weakpointCollider.isTrigger, Is.False);
            D0WeakpointPresentationController weakpointPresentation =
                context.D0WeakpointPresentationController;
            Assert.That(weakpointPresentation, Is.Not.Null);
            Assert.That(weakpointPresentation.IsPrepared, Is.True);
            Assert.That(
                weakpointPresentation.TryValidate(out string weakpointPresentationError),
                Is.True,
                weakpointPresentationError);

            Assert.That(
                context.HitboxRegistry.TryResolve(
                    new GeometryId(2002),
                    out RegisteredHitbox registered),
                Is.True);
            Assert.That(registered.Collider, Is.SameAs(weakpointCollider));
            Assert.That(registered.RuntimeId, Is.EqualTo(host.Session.EnemyRuntimeId));
            Assert.That(registered.TargetKind, Is.EqualTo(QueryTargetKind.Combatant));
            Assert.That(registered.HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(registered.Team, Is.EqualTo(Team.Enemy));

            Physics.SyncTransforms();
            AssertViewportAimSelectsHitbox(
                context,
                host,
                weakpointCollider,
                HitPart.Weakpoint,
                new GeometryId(2002),
                9001L);
            weakpointPresentation.Advance(0f, isRunning: true);
            Assert.That(weakpointPresentation.IsReticleLocked, Is.True,
                "The D0 lock frame must use the same viewport ray that selected geometry 2002.");

            BoxCollider bodyCollider = activeEnemy.BodyHitbox as BoxCollider;
            Assert.That(bodyCollider, Is.Not.Null,
                "The active enemy entity must own its authored BoxCollider body hitbox.");
            AssertViewportAimSelectsHitbox(
                context,
                host,
                bodyCollider,
                HitPart.Body,
                new GeometryId(2001),
                9002L);
            weakpointPresentation.Advance(0f, isRunning: true);
            Assert.That(weakpointPresentation.IsReticleLocked, Is.False,
                "A body-only viewport ray must not incorrectly lock the weakpoint UI.");
        }

        [UnityTest]
        public IEnumerator ScheduledD0HeavyWarningUsesActiveEnemyEntityWeakpointAtTick540()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);

            UnityBattleInputSource input = new UnityBattleInputSource();
            input.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            input.CaptureAimPose(context.AimAnchor);

            BattleSession session = host.Session;
            D0EnemyBehaviorController behavior = RequireD0EnemyBehavior(context);
            Assert.That(behavior, Is.Not.Null);
            for (int tick = 0; tick <= 540; tick++)
            {
                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    behavior,
                    out int executedSteps);
                Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            Assert.That(session.CurrentTick, Is.EqualTo(new TickIndex(540L)));
            ThreatSnapshot[] snapshots = new ThreatSnapshot[session.ThreatCount];
            Assert.That(session.CopyThreatSnapshots(snapshots, out int threatCount).IsSuccess, Is.True);

            RuntimeId heavyRuntimeId = RuntimeId.Invalid;
            for (int index = 0; index < threatCount; index++)
            {
                ThreatSnapshot snapshot = snapshots[index];
                if (snapshot.PayloadKind == ThreatPayloadKind.TimedImpact
                    && snapshot.PresentationKey
                        == BattlePresentationCatalog.WeakpointWarningPresentationKey)
                {
                    heavyRuntimeId = snapshot.RuntimeId;
                    Assert.That(snapshot.State, Is.EqualTo(ThreatState.Telegraph));
                    break;
                }
            }

            Assert.That(heavyRuntimeId.IsValid, Is.True, "D0 tick 540 must start the key-3 heavy warning.");

            ThreatTelegraph2DPresenter threatPresenter = context.D0ThreatTelegraphPresenter;
            D0WeakpointPresentationController weakpointPresenter =
                context.D0WeakpointPresentationController;
            Assert.That(threatPresenter, Is.Not.Null);
            Assert.That(weakpointPresenter, Is.Not.Null);
            ThreatTelegraph2DView telegraphView = null;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!threatPresenter.TryGetView(heavyRuntimeId, out telegraphView)
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(telegraphView, Is.Not.Null, "The scheduled heavy threat must acquire a D0 telegraph view.");
            Assert.That(telegraphView.gameObject.activeInHierarchy, Is.True);
            Assert.That(telegraphView.Kind, Is.EqualTo(CombatThreatPresentationKind.HeavyWeakpoint));
            Assert.That(weakpointPresenter.IsHeavyThreatActive, Is.True);
            Assert.That(weakpointPresenter.DisplayedCountdownSeconds, Is.GreaterThan(0));
            Assert.That(
                Vector3.Distance(
                    threatPresenter.LastWeakpointPosition,
                    context.ActiveEnemyWeakpointAnchor.position),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    threatPresenter.LastEnemySourcePosition,
                    context.ActiveEnemyGameplayAnchor.position),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    threatPresenter.LastWeakpointPosition,
                    context.PlayerGroundAnchor.position),
                Is.GreaterThan(1f),
                "Key-3 heavy feedback must not fall back to the player-ground danger pulse location.");
        }

        [UnityTest]
        public IEnumerator BootedD0SlowTripleUsesThreeViewsAndInterceptCleansOne()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            BattleSession session = host.Session;
            D0EnemyBehaviorController behavior = RequireD0EnemyBehavior(context);
            Assert.That(behavior, Is.Not.Null);
            UnityBattleInputSource idleInput = new UnityBattleInputSource();
            idleInput.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            idleInput.CaptureAimPose(context.AimAnchor);

            while (session.CurrentTick.Value < 390L)
            {
                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    idleInput,
                    behavior,
                    out int executedSteps);
                Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            // Let the coordinator consume the spawn feed after the authoritative
            // session has reached a point where all three key-2 projectiles are
            // between their release and arrival ticks.
            yield return new WaitForEndOfFrame();

            ProjectileSnapshot[] snapshots = new ProjectileSnapshot[session.ProjectileSlotCount];
            Assert.That(session.CopyActiveProjectileSnapshots(snapshots, out int activeCount).IsSuccess, Is.True);
            RuntimeId[] volleyRuntimeIds = new RuntimeId[3];
            int volleyCount = 0;
            for (int index = 0; index < activeCount; index++)
            {
                ProjectileSnapshot snapshot = snapshots[index];
                if (snapshot.PresentationKey == CombatPresentationProfile.InterceptableVolleyThreatPresentationKey
                    && snapshot.State == ProjectileState.Travelling)
                {
                    Assert.That(volleyCount, Is.LessThan(volleyRuntimeIds.Length));
                    volleyRuntimeIds[volleyCount++] = snapshot.RuntimeId;
                }
            }

            Assert.That(volleyCount, Is.EqualTo(3),
                "Tick 390 must keep the scheduled key-2 triple active after its tick-366 release.");
            ProjectileViewPool viewPool = context.PresentationCoordinator.ProjectileViewPool;
            Assert.That(viewPool.ActiveViewCount, Is.EqualTo(3));
            Vector3[] visualPositions = new Vector3[3];
            bool hasLeftLane = false;
            bool hasCenterLane = false;
            bool hasRightLane = false;
            for (int index = 0; index < volleyRuntimeIds.Length; index++)
            {
                RuntimeId runtimeId = volleyRuntimeIds[index];
                Assert.That(viewPool.TryGet(runtimeId, out ProjectileView view), Is.True);
                Assert.That(view.ShowsInterceptableMarker, Is.True);
                Assert.That(view.VolleyLane, Is.EqualTo(ProjectileView.ResolveInterceptableVolleyLane(runtimeId)));
                visualPositions[index] = view.VisualPosition;
                hasLeftLane |= view.VolleyLane == -1;
                hasCenterLane |= view.VolleyLane == 0;
                hasRightLane |= view.VolleyLane == 1;
            }

            Assert.That(hasLeftLane && hasCenterLane && hasRightLane, Is.True,
                "The three same-path simulation projectiles must expose stable -1/0/+1 visual lanes.");
            Assert.That((visualPositions[0] - visualPositions[1]).sqrMagnitude, Is.GreaterThan(0.0001f));
            Assert.That((visualPositions[0] - visualPositions[2]).sqrMagnitude, Is.GreaterThan(0.0001f));
            Assert.That((visualPositions[1] - visualPositions[2]).sqrMagnitude, Is.GreaterThan(0.0001f));

            RuntimeId interceptedRuntimeId = volleyRuntimeIds[0];
            ProjectileCollisionProxyPool collisionPool = host.ProjectileCollisionProxyPool;
            Assert.That(collisionPool, Is.Not.Null);
            Assert.That(
                collisionPool.TryGetActiveProxy(interceptedRuntimeId, out ProjectileCollisionProxySnapshot proxy),
                Is.True);
            GeometryId interceptedGeometryId = proxy.GeometryId;
            UnityBattleInputSource primaryInput = new UnityBattleInputSource();
            bool intercepted = false;
            for (int step = 0; step < 80; step++)
            {
                if (!collisionPool.TryGetActiveProxy(
                        interceptedRuntimeId,
                        out ProjectileCollisionProxySnapshot activeProxy))
                {
                    intercepted = true;
                    break;
                }

                Physics.SyncTransforms();
                CapturePrimaryInputAtPoint(primaryInput, context, activeProxy.Collider.bounds.center);
                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    primaryInput,
                    behavior,
                    out int executedSteps);
                Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            Assert.That(intercepted, Is.True,
                "Repeated real primary shots aimed at the collision proxy must intercept one key-2 projectile before arrival.");
            yield return new WaitForEndOfFrame();

            Assert.That(viewPool.ActiveViewCount, Is.EqualTo(2));
            Assert.That(viewPool.TryGet(interceptedRuntimeId, out _), Is.False);
            Assert.That(collisionPool.TryGetActiveProxy(interceptedRuntimeId, out _), Is.False);
            Assert.That(context.HitboxRegistry.TryResolve(interceptedGeometryId, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator BootedD0HeavyThreatBreakCancelsTelegraphBeforeRelease()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            BattleSession session = host.Session;
            SphereCollider weakpointCollider = RequireActiveEnemyEntity(context).WeakpointHitbox as SphereCollider;
            Assert.That(weakpointCollider, Is.Not.Null);
            D0EnemyBehaviorController behavior = RequireD0EnemyBehavior(context);
            Assert.That(behavior, Is.Not.Null);

            UnityBattleInputSource input = new UnityBattleInputSource();
            UnityBattleInputSource idleInput = new UnityBattleInputSource();
            idleInput.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            idleInput.CaptureAimPose(context.AimAnchor);

            // Three secondary weakpoint hits provide 150/160 Break before the
            // heavy starts, without entering groggy. This preserves one loaded
            // cast for the actual in-window interruption.
            FireSecondaryAtCurrentWeakpoint(input, context, session, weakpointCollider, behavior);
            PumpIdleTicks(
                session,
                idleInput,
                session.Definition.PlayerWeapon.SecondaryRecovery.Value + 1,
                behavior);
            FireSecondaryAtCurrentWeakpoint(input, context, session, weakpointCollider, behavior);
            PumpIdleTicks(
                session,
                idleInput,
                session.Definition.PlayerWeapon.SecondaryRecovery.Value + 1,
                behavior);

            CaptureReloadInput(input, context);
            Assert.That(
                session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    behavior,
                    out int reloadStartSteps).IsSuccess,
                Is.True);
            Assert.That(reloadStartSteps, Is.EqualTo(1));
            PumpIdleTicks(
                session,
                idleInput,
                session.Definition.PlayerWeapon.ReloadDuration.Value + 1,
                behavior);
            FireSecondaryAtCurrentWeakpoint(input, context, session, weakpointCollider, behavior);

            FinalSnapshot primedSnapshot = session.GetFinalSnapshot();
            Assert.That(primedSnapshot.EnemyBreak, Is.GreaterThan(0));
            Assert.That(primedSnapshot.EnemyBreak, Is.LessThan(160),
                "The setup casts must leave the enemy one weakpoint hit short of a Break.");

            while (session.CurrentTick.Value < 540L)
            {
                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    idleInput,
                    behavior,
                    out int executedSteps);
                Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            yield return new WaitForEndOfFrame();
            ThreatSnapshot[] threats = new ThreatSnapshot[session.ThreatCount];
            Assert.That(session.CopyThreatSnapshots(threats, out int threatCount).IsSuccess, Is.True);
            RuntimeId heavyRuntimeId = RuntimeId.Invalid;
            for (int index = 0; index < threatCount; index++)
            {
                ThreatSnapshot threat = threats[index];
                if (threat.PresentationKey == CombatPresentationProfile.HeavyWeakpointThreatPresentationKey
                    && threat.PayloadKind == ThreatPayloadKind.TimedImpact)
                {
                    heavyRuntimeId = threat.RuntimeId;
                    Assert.That(threat.State, Is.EqualTo(ThreatState.Telegraph));
                    break;
                }
            }

            Assert.That(heavyRuntimeId.IsValid, Is.True);
            ThreatTelegraph2DPresenter telegraphPresenter = context.D0ThreatTelegraphPresenter;
            Assert.That(telegraphPresenter.TryGetView(heavyRuntimeId, out _), Is.True);
            Assert.That(context.D0WeakpointPresentationController.IsHeavyThreatActive, Is.True);

            FireSecondaryAtCurrentWeakpoint(input, context, session, weakpointCollider, behavior);
            Assert.That(session.CopyThreatSnapshots(threats, out threatCount).IsSuccess, Is.True);
            ThreatSnapshot canceledHeavy = default(ThreatSnapshot);
            for (int index = 0; index < threatCount; index++)
            {
                if (threats[index].RuntimeId == heavyRuntimeId)
                {
                    canceledHeavy = threats[index];
                    break;
                }
            }

            Assert.That(canceledHeavy.RuntimeId, Is.EqualTo(heavyRuntimeId));
            Assert.That(canceledHeavy.State, Is.EqualTo(ThreatState.Canceled));
            Assert.That(canceledHeavy.HasReleased, Is.False);
            Assert.That(session.GetFinalSnapshot().EnemyBreak, Is.Zero);
            Assert.That(session.PendingImpactCount, Is.Zero);

            yield return new WaitForEndOfFrame();
            Assert.That(telegraphPresenter.TryGetView(heavyRuntimeId, out _), Is.False);
            Assert.That(context.D0WeakpointPresentationController.IsHeavyThreatActive, Is.False);

            int playerLifeAfterCancel = session.GetFinalSnapshot().PlayerLife;
            while (session.CurrentTick.Value <= 676L)
            {
                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    idleInput,
                    behavior,
                    out int executedSteps);
                Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
                // This loop resumes after a presentation frame has been yielded.
                // The fixed-step accumulator may legitimately catch up by more
                // than one tick; the contract here is that it advances and that
                // the canceled threat never schedules or applies its impact.
                Assert.That(
                    executedSteps,
                    Is.InRange(1, GameplayClock.DefaultMaxCatchUpSteps));
            }

            Assert.That(session.PendingImpactCount, Is.Zero);
            Assert.That(session.GetFinalSnapshot().PlayerLife, Is.EqualTo(playerLifeAfterCancel),
                "The canceled heavy threat must not damage the player on or after its original release tick.");
        }

        [UnityTest]
        public IEnumerator D0DefeatRestartResetsCombatPresentationAndWeakpointRegistry()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            Assert.That(context, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            BattlePresentationCoordinator presentation = context.PresentationCoordinator;
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.IsFeedbackPrepared, Is.True);
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);

            ProjectileViewPool projectilePool = presentation.ProjectileViewPool;
            WarningViewPool warningPool = presentation.WarningViewPool;
            ImpactViewPool impactPool = presentation.ImpactViewPool;
            ThreatTelegraph2DPresenter threatPresenter = context.D0ThreatTelegraphPresenter;
            Assert.That(projectilePool, Is.Not.Null);
            Assert.That(warningPool, Is.Not.Null);
            Assert.That(impactPool, Is.Not.Null);
            Assert.That(threatPresenter, Is.Not.Null);

            UnityBattleInputSource idleInput = new UnityBattleInputSource();
            idleInput.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            idleInput.CaptureAimPose(context.AimAnchor);
            Physics.SyncTransforms();

            BattleSession defeatedSession = host.Session;
            for (int tick = 0; tick <= 675 && defeatedSession.State == BattleSessionState.Running; tick++)
            {
                DomainResult pumped = defeatedSession.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    idleInput,
                    out int executedSteps);
                Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
                Assert.That(executedSteps, Is.EqualTo(1));
            }

            Assert.That(defeatedSession.State, Is.EqualTo(BattleSessionState.Completed));
            Assert.That(defeatedSession.CompletionReason, Is.EqualTo(BattleCompletionReason.Defeat));

            CombatHud2DPresenter hud = context.D0CombatHud2DPresenter;
            Assert.That(hud, Is.Not.Null,
                "The D0 slice must display completion through the formal HUD, not the hidden legacy BattleHud.");
            Assert.That(hud.TryValidate(out string hudError), Is.True, hudError);

            float deadline = Time.realtimeSinceStartup + 2f;
            while (!hud.IsTerminalLatched && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(hud.IsTerminalLatched, Is.True);
            Assert.That(hud.IsTerminalPanelVisible, Is.True);
            Assert.That(hud.TerminalReason, Is.EqualTo(BattleCompletionReason.Defeat));
            Assert.That(hud.TerminalScreenFx, Is.Not.Null);
            Assert.That(hud.TerminalScreenFx.IsShowing, Is.True);
            Assert.That(hud.TerminalScreenFx.DimmingColor.a, Is.GreaterThan(0.8f),
                "Defeat must strongly darken the stage before the restart prompt appears.");
            Transform defeatTitleTransform = FindDescendantByName(hud.transform, "TerminalTitleText");
            Transform defeatPromptTransform = FindDescendantByName(hud.transform, "TerminalPromptText");
            Text defeatTitle = defeatTitleTransform == null ? null : defeatTitleTransform.GetComponent<Text>();
            Text defeatPrompt = defeatPromptTransform == null ? null : defeatPromptTransform.GetComponent<Text>();
            Assert.That(defeatTitle, Is.Not.Null);
            Assert.That(defeatTitle.text, Is.EqualTo("DEFEAT"));
            Assert.That(defeatPrompt, Is.Not.Null);
            Assert.That(defeatPrompt.text, Does.Contain("F5 RESTART"));
            Assert.That(context.D0PlayerActorPresenter.IsTerminal, Is.True,
                "The Fei presenter must receive the authoritative Defeat terminal event.");

            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            BattleSession restartedSession = host.Session;
            Assert.That(defeatedSession.State, Is.EqualTo(BattleSessionState.Disposed));
            Assert.That(restartedSession, Is.Not.SameAs(defeatedSession));
            Assert.That(restartedSession.State, Is.EqualTo(BattleSessionState.Running));

            FinalSnapshot restartedSnapshot = restartedSession.GetFinalSnapshot();
            Assert.That(restartedSnapshot.PlayerLife, Is.EqualTo(100));
            Assert.That(restartedSnapshot.PlayerBarrier, Is.EqualTo(100));
            Assert.That(restartedSnapshot.EnemyLife, Is.EqualTo(800));
            Assert.That(restartedSnapshot.EnemyBreak, Is.EqualTo(160));
            Assert.That(projectilePool.ActiveViewCount, Is.Zero);
            Assert.That(warningPool.ActiveViewCount, Is.Zero);
            Assert.That(impactPool.ActiveViewCount, Is.Zero);
            Assert.That(presentation.IsBound, Is.True);
            Assert.That(
                context.HitboxRegistry.TryResolve(
                    new GeometryId(2002),
                    out RegisteredHitbox restartedWeakpoint),
                Is.True);
            Assert.That(restartedWeakpoint.RuntimeId, Is.EqualTo(restartedSession.EnemyRuntimeId));
            Assert.That(restartedWeakpoint.HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(hud.IsTerminalLatched, Is.False,
                "F5 must synchronously clear the formal terminal latch.");
            Assert.That(hud.IsTerminalPanelVisible, Is.False);
            Assert.That(hud.TerminalScreenFx.IsShowing, Is.False);
            Assert.That(hud.TerminalScreenFx.CurrentAlpha, Is.Zero);
            Assert.That(hud.TerminalScreenFx.DimmingColor, Is.EqualTo(Color.clear));
            Assert.That(context.D0PlayerActorPresenter.IsTerminal, Is.False);
            Assert.That(context.D0HitTipPresenter.ActiveCount, Is.Zero);
            Assert.That(context.D0ThreatTelegraphPresenter.ActiveTelegraphCount, Is.Zero);
            Assert.That(context.CombatVfxWorld.ActiveInstanceCount, Is.Zero);
            Assert.That(context.D0CombatAudioPresenter.ActiveVoiceCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator D0VictoryResultAndRestartResetFormalHud()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            Assert.That(context, Is.Not.Null);
            Assert.That(host, Is.Not.Null);
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);

            CombatHud2DPresenter hud = context.D0CombatHud2DPresenter;
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.TryValidate(out string hudError), Is.True, hudError);

            SphereCollider weakpoint = context.ActiveEnemyWeakpointAnchor.GetComponent<SphereCollider>();
            Assert.That(weakpoint, Is.Not.Null);
            D0EnemyBehaviorController behavior = RequireD0EnemyBehavior(context);
            Assert.That(behavior, Is.Not.Null);
            CompleteD0VictoryThroughSecondaryWeakpointCasts(
                context,
                host.Session,
                weakpoint,
                behavior);

            float deadline = Time.realtimeSinceStartup + 2f;
            while ((!hud.IsTerminalLatched
                    || !context.D0PlayerActorPresenter.IsTerminal
                    || !context.ActiveD0EnemyActorPresenter.IsTerminal
                    || context.CombatVfxWorld.ActiveInstanceCount == 0)
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(hud.IsTerminalLatched, Is.True);
            Assert.That(hud.IsTerminalPanelVisible, Is.True);
            Assert.That(hud.TerminalReason, Is.EqualTo(BattleCompletionReason.Victory));
            Assert.That(hud.TerminalScreenFx, Is.Not.Null);
            Assert.That(hud.TerminalScreenFx.IsShowing, Is.True);
            Assert.That(hud.TerminalScreenFx.DimmingColor.a, Is.LessThan(0.3f),
                "Victory uses a light screen treatment; only Defeat heavily darkens the stage.");
            Assert.That(context.D0PlayerActorPresenter.IsTerminal, Is.True);
            Assert.That(context.ActiveD0EnemyActorPresenter.IsTerminal, Is.True);
            Assert.That(context.CombatVfxWorld.ActiveInstanceCount, Is.GreaterThan(0),
                "A real Victory must expose Burstbug's layered death treatment, not only a text result.");
            Transform titleTransform = FindDescendantByName(hud.transform, "TerminalTitleText");
            Transform promptTransform = FindDescendantByName(hud.transform, "TerminalPromptText");
            Text titleText = titleTransform == null ? null : titleTransform.GetComponent<Text>();
            Text promptText = promptTransform == null ? null : promptTransform.GetComponent<Text>();
            Assert.That(titleText, Is.Not.Null);
            Assert.That(titleText.text, Is.EqualTo("VICTORY"));
            Assert.That(promptText, Is.Not.Null);
            Assert.That(promptText.text, Does.Contain("F5 RESTART"));

            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            Assert.That(host.Session.State, Is.EqualTo(BattleSessionState.Running));
            Assert.That(hud.IsTerminalLatched, Is.False,
                "The host restart event must clear a visible Victory surface in the same restart path as F5.");
            Assert.That(hud.IsTerminalPanelVisible, Is.False);
            Assert.That(hud.TerminalScreenFx.IsShowing, Is.False);
            Assert.That(hud.TerminalScreenFx.CurrentAlpha, Is.Zero);
            Assert.That(context.D0PlayerActorPresenter.IsTerminal, Is.False);
            Assert.That(context.ActiveD0EnemyActorPresenter.IsTerminal, Is.False);
            Assert.That(context.CombatVfxWorld.ActiveInstanceCount, Is.Zero);
            Assert.That(context.D0HitTipPresenter.ActiveCount, Is.Zero);
            Assert.That(context.D0CombatAudioPresenter.ActiveVoiceCount, Is.Zero);
            Assert.That(context.CombatAimReticle.Viewport, Is.EqualTo(CombatAimViewportMath.Center));
        }

        [UnityTest]
        public IEnumerator CombatLabSceneSatisfiesPlayableVisualAndPresentationContracts()
        {
            yield return SceneManager.LoadSceneAsync("CombatLab", LoadSceneMode.Single);

            Scene scene = SceneManager.GetSceneByName("CombatLab");
            BattleSceneContext context = FindComponentInScene<BattleSceneContext>(scene);
            Assert.That(context, Is.Not.Null);
            Assert.That(context.WorldRoot, Is.Not.Null);
            Assert.That(context.PlayerAnchor, Is.Not.Null);
            Assert.That(context.AimAnchor, Is.Not.Null);
            Assert.That(context.MainCamera, Is.Not.Null);
            Assert.That(context.PresentationCanvas, Is.Not.Null);
            D0StageDefinition stageDefinition = RequireAuthoredStage(context);
            D0ThreeCProfile threeCProfile = RequireAuthoredThreeCProfile(context);

            Assert.That(context.PlayerEntity, Is.Not.Null);
            Assert.That(
                context.PlayerEntity.TryValidate(out string playerEntityError),
                Is.True,
                playerEntityError);
            Assert.That(context.PlayerAnchor, Is.SameAs(context.PlayerEntity.transform));

            CharacterController characterController =
                context.PlayerEntity.CharacterController;
            CombatLabPlayerController playerController =
                context.PlayerEntity.Controller;
            Assert.That(characterController, Is.Not.Null);
            Assert.That(playerController, Is.Not.Null);
            Assert.That(playerController.CharacterController, Is.SameAs(characterController));
            Assert.That(playerController.AimAnchor, Is.SameAs(context.PlayerEntity.AimAnchor));
            Assert.That(playerController.AimAnchor, Is.SameAs(context.AimAnchor));
            Assert.That(playerController.SessionHost, Is.SameAs(context.SessionHost));
            Assert.That(playerController.UsesTwoPointFiveDPresentation, Is.True);
            Assert.That(playerController.PlanarMovementEnabled, Is.False);

            CombatLabPlayerBounds playerBounds = context.PlayerEntity.Bounds;
            Assert.That(playerBounds, Is.Not.Null);
            Assert.That(playerBounds.CharacterController, Is.SameAs(characterController));
            Assert.That(playerBounds.IsInitialized, Is.True, playerBounds.LastError);
            Assert.That(playerBounds.HasInitialSafePosition, Is.True);
            Assert.That(
                playerBounds.IsInsidePlayableArea(context.PlayerEntity.transform.position),
                Is.True);

            Transform cameraPivot = context.PlayerEntity.CameraPivot;
            Assert.That(cameraPivot, Is.Not.Null);
            Assert.That(playerController.CameraPivot, Is.SameAs(cameraPivot));
            Assert.That(cameraPivot.parent, Is.SameAs(context.PlayerEntity.transform));

            Transform playerGroundAnchor = context.PlayerEntity.GroundAnchor;
            Assert.That(playerGroundAnchor, Is.Not.Null);
            Assert.That(context.PlayerGroundAnchor, Is.SameAs(playerGroundAnchor));
            Assert.That(playerGroundAnchor, Is.Not.SameAs(context.PlayerEntity.transform));
            Assert.That(
                playerGroundAnchor.IsChildOf(context.PlayerEntity.transform),
                Is.True);
            Assert.That(
                playerGroundAnchor.position.y,
                Is.LessThanOrEqualTo(context.PlayerEntity.transform.position.y),
                "GroundAnchor must not float above the player Entity root.");

            Assert.That(
                context.MainCamera.transform.parent,
                Is.SameAs(cameraPivot),
                "The fixed frontal camera remains under the scene-owned pivot.");
            Assert.That(
                cameraPivot.parent,
                Is.SameAs(context.PlayerEntity.transform),
                "CameraPivot must be a sibling of AimAnchor on the player Entity.");
            AssertCameraInstallationMatchesThreeCProfile(context, threeCProfile);
            AssertInitialSpawnAimFacesEnemy(context);

            GameObject playerVisual = context.PlayerEntity.VisualRoot.gameObject;
            GameObject combatGround = FindGameObjectInScene(scene, "CombatGround");
            GameObject sideBlocker = FindGameObjectInScene(scene, "SideBlocker");
            D0EnemyEntityView enemyEntityPrefab = context.ScenarioConfig.AuthoredScenario
                .Encounter.InitialSpawnSlot.Enemy.EntityPrefab;
            Assert.That(playerVisual, Is.Not.Null, "Expected the player Entity VisualRoot.");
            Assert.That(enemyEntityPrefab, Is.Not.Null, "Expected an enemy-owned EntityPrefab.");
            Assert.That(enemyEntityPrefab.VisualRoot, Is.Not.Null);
            Assert.That(enemyEntityPrefab.GameplayAnchor, Is.Not.Null);
            Assert.That(combatGround, Is.Not.Null, "Expected World/CombatGround.");
            Assert.That(sideBlocker, Is.Not.Null, "Expected World/Blockers/SideBlocker.");
            Assert.That(playerVisual.transform.IsChildOf(context.PlayerEntity.transform), Is.True);
            Assert.That(combatGround.transform.IsChildOf(context.WorldRoot), Is.True);
            Assert.That(sideBlocker.transform.IsChildOf(context.WorldRoot), Is.True);

            Collider groundCollider = combatGround.GetComponent<Collider>();
            Assert.That(groundCollider, Is.Not.Null, "CombatGround must provide the walkable Default-layer collider.");
            Assert.That(groundCollider.isTrigger, Is.False);
            Assert.That(combatGround.layer, Is.EqualTo(0), "CombatGround must use Unity's Default layer.");

            int blockerLayer = LayerMask.NameToLayer("FPG_Blocker");
            Assert.That(blockerLayer, Is.GreaterThanOrEqualTo(0), "The FPG_Blocker layer must be configured.");
            AssertHiddenGameplayBlockerContract(sideBlocker, blockerLayer, "World/Blockers/SideBlocker");

            Assert.That(
                FindGameObjectInScene(scene, "RightCover"),
                Is.Null,
                "The greybox may not add unregistered physical cover; SideBlocker is the scene's registered blocker.");

            Transform d0SliceRoot = FindDescendantByName(context.PresentationRoot, "D0Slice2D");
            Assert.That(d0SliceRoot, Is.Not.Null);
            D0SliceInstallationMarker marker = d0SliceRoot.GetComponent<D0SliceInstallationMarker>();
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.TryValidate(out string markerError), Is.True, markerError);
            AssertSpawnedActorOwnershipMatchesAuthoredDefinitions(
                context,
                d0SliceRoot,
                context.ScenarioConfig.AuthoredScenario,
                stageDefinition);
            Assert.That(context.D0ThreatTelegraphPresenter, Is.Not.Null);
            Assert.That(
                context.D0ThreatTelegraphPresenter.TryValidate(out string threatError),
                Is.True,
                threatError);
            Assert.That(context.D0WeakpointPresentationController, Is.Not.Null);
            Assert.That(
                context.D0WeakpointPresentationController.TryValidate(out string weakpointError),
                Is.True,
                weakpointError);
            Assert.That(context.CombatVfxWorld, Is.Not.Null);
            Assert.That(context.CombatVfxWorld.IsPrepared, Is.True);
            Assert.That(
                context.CombatVfxWorld.TryValidate(out string combatVfxError),
                Is.True,
                combatVfxError);
            AssertCombatVfxWorldMatchesAuthoredPresentation(context);
            Assert.That(context.D0CombatAudioPresenter, Is.Not.Null);
            Assert.That(
                context.D0CombatAudioPresenter.TryValidate(out string audioError),
                Is.True,
                audioError);

            CombatHud2DPresenter formalHud = context.D0CombatHud2DPresenter;
            Assert.That(formalHud, Is.Not.Null);
            Assert.That(formalHud.TryValidate(out string formalHudError), Is.True, formalHudError);
            Assert.That(formalHud.PresentationProfile, Is.SameAs(marker.PresentationProfile));
            Transform formalHudRoot = d0SliceRoot.Find("D0Canvas/D0OverlayCanvas/D0Hud/D0FormalHud");
            Assert.That(formalHudRoot, Is.Not.Null,
                "The installed D0 slice must own the formal HUD under its dedicated overlay canvas.");
            Assert.That(formalHud.transform, Is.SameAs(formalHudRoot));
            Assert.That(FindDescendantByName(formalHudRoot, "D0EnemyReadout"), Is.Not.Null);
            RectTransform playerReadout = FindDescendantByName(formalHudRoot, "D0PlayerReadout")
                as RectTransform;
            RectTransform actionReadout = FindDescendantByName(formalHudRoot, "D0ActionReadout")
                as RectTransform;
            Assert.That(playerReadout, Is.Not.Null);
            Assert.That(actionReadout, Is.Not.Null);
            AssertRectStaysOnScreen(playerReadout, "D0PlayerReadout");
            AssertRectStaysOnScreen(actionReadout, "D0ActionReadout");
            AssertRectContainsChild(playerReadout,
                FindDescendantByName(playerReadout, "PlayerNameText") as RectTransform,
                "D0PlayerReadout/PlayerNameText");
            AssertRectContainsChild(playerReadout,
                FindDescendantByName(playerReadout, "PlayerLifeBar") as RectTransform,
                "D0PlayerReadout/PlayerLifeBar");
            AssertRectContainsChild(playerReadout,
                FindDescendantByName(playerReadout, "PlayerBarrierBar") as RectTransform,
                "D0PlayerReadout/PlayerBarrierBar");
            AssertRectContainsChild(playerReadout,
                FindDescendantByName(playerReadout, "AmmoBar") as RectTransform,
                "D0PlayerReadout/AmmoBar");
            Transform resultPanel = FindDescendantByName(formalHudRoot, "D0ResultPanel");
            Assert.That(resultPanel, Is.Not.Null);
            Assert.That(resultPanel.gameObject.activeSelf, Is.False);

            Transform screenFxCanvasTransform = d0SliceRoot.Find("D0ScreenFx/D0ScreenFxCanvas");
            Assert.That(screenFxCanvasTransform, Is.Not.Null);
            Canvas screenFxCanvas = screenFxCanvasTransform.GetComponent<Canvas>();
            D0TerminalScreenFxPresenter screenFx =
                screenFxCanvasTransform.GetComponent<D0TerminalScreenFxPresenter>();
            Assert.That(screenFxCanvas, Is.Not.Null);
            Assert.That(screenFxCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(screenFxCanvas.sortingOrder,
                Is.EqualTo(marker.PresentationProfile.Sorting.ScreenEffectsOrder));
            Assert.That(screenFx, Is.SameAs(formalHud.TerminalScreenFx));
            Assert.That(FindDescendantByName(screenFxCanvasTransform, "D0TerminalDimming"), Is.Not.Null);
            Assert.That(screenFx.IsShowing, Is.False);
            Assert.That(screenFx.CurrentAlpha, Is.Zero);

            Transform developmentCanvasTransform =
                d0SliceRoot.Find("D0DevelopmentOverlay/D0DevelopmentOverlayCanvas");
            Assert.That(developmentCanvasTransform, Is.Not.Null);
            Canvas developmentCanvas = developmentCanvasTransform.GetComponent<Canvas>();
            Assert.That(developmentCanvas, Is.Not.Null);
            Assert.That(developmentCanvas.sortingOrder,
                Is.EqualTo(marker.PresentationProfile.Sorting.DevelopmentOverlayOrder));
            Assert.That(developmentCanvasTransform.gameObject.activeSelf, Is.False);

            D0ForestParallax forest = d0SliceRoot.GetComponentInChildren<D0ForestParallax>(true);
            Assert.That(forest, Is.Not.Null);
            Assert.That(forest.LayerCount, Is.EqualTo(stageDefinition.ForestLayers.Count));
            Assert.That(forest.TryValidate(out string forestError), Is.True, forestError);
            for (int index = 0; index < stageDefinition.ForestLayers.Count; index++)
            {
                D0StageForestLayerDefinition expectedLayer = stageDefinition.ForestLayers[index];
                Transform layer = FindDescendantByName(forest.transform, expectedLayer.LayerId);
                Assert.That(layer, Is.Not.Null, $"Missing D0 forest layer '{expectedLayer.LayerId}'.");
                SpriteRenderer renderer = layer.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sprite, Is.SameAs(expectedLayer.Sprite));
                Assert.That(renderer.sortingOrder, Is.EqualTo(expectedLayer.SortingOrder));
                Assert.That(layer.GetComponent<D0ForestParallaxLayer>(), Is.Not.Null);
            }

            CombatAimReticle reticle = context.CombatAimReticle;
            Assert.That(reticle, Is.Not.Null);
            Assert.That(reticle.SessionHost, Is.SameAs(context.SessionHost));
            Assert.That(reticle.TryGetViewport(out Vector2 reticleViewport), Is.True);
            Assert.That(reticleViewport, Is.EqualTo(CombatAimViewportMath.Center));
            Assert.That(reticle.TryValidate(out string reticleError), Is.True, reticleError);
            RectTransform reticleRect = reticle.transform as RectTransform;
            Assert.That(reticleRect, Is.Not.Null);
            Assert.That(reticleRect.anchorMin, Is.EqualTo(CombatAimViewportMath.Center));
            Assert.That(reticleRect.anchorMax, Is.EqualTo(CombatAimViewportMath.Center));
            Assert.That(reticle.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(reticle.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(100));

            AssertPresentationTreeHasNoPhysics(context.PresentationRoot, "PresentationRoot");
            AssertPresentationTreeHasNoPhysics(d0SliceRoot, "D0Slice2D");
            AssertPresentationTreeHasNoPhysics(context.ProjectileViewRoot, "ProjectileViewRoot");
            AssertPresentationTreeHasNoPhysics(context.WarningViewRoot, "WarningViewRoot");
            AssertPresentationTreeHasNoPhysics(context.ImpactViewRoot, "ImpactViewRoot");
            AssertPresentationTreeHasNoPhysics(context.PresentationCanvas.transform, "PresentationCanvas");
        }

        [UnityTest]
        public IEnumerator CombatLabCameraAppliesEveryNonDefaultThreeCProfileInstallationValue()
        {
            yield return SceneManager.LoadSceneAsync("CombatLab", LoadSceneMode.Single);

            BattleSceneContext context = FindComponentInScene<BattleSceneContext>(
                SceneManager.GetSceneByName("CombatLab"));
            D0ThreeCProfile authoredProfile = RequireAuthoredThreeCProfile(context);
            D0ThreeCProfile overrideProfile = UnityEngine.Object.Instantiate(authoredProfile);

            try
            {
                SetPrivateField(
                    overrideProfile,
                    "cameraPivotLocalPosition",
                    new Vector3(1.25f, 3.4f, -7.2f));
                SetPrivateField(
                    overrideProfile,
                    "cameraPivotLocalEulerAngles",
                    new Vector3(-8.5f, 16f, 0f));
                SetPrivateField(
                    overrideProfile,
                    "cameraLocalPosition",
                    new Vector3(0.15f, -0.2f, 0.35f));
                SetPrivateField(
                    overrideProfile,
                    "cameraLocalEulerAngles",
                    new Vector3(2f, -3f, 1f));
                SetPrivateField(overrideProfile, "cameraFieldOfView", 61f);
                SetPrivateField(overrideProfile, "cameraNearClipPlane", 0.35f);
                SetPrivateField(overrideProfile, "cameraFarClipPlane", 132.7f);

                Assert.That(overrideProfile.TryValidate(out string profileError), Is.True, profileError);

                ApplyFixedFrontalCameraConfiguration(context, overrideProfile);
                AssertCameraInstallationMatchesThreeCProfile(context, overrideProfile);
            }
            finally
            {
                ApplyFixedFrontalCameraConfiguration(context, authoredProfile);
                UnityEngine.Object.Destroy(overrideProfile);
            }
        }

        [UnityTest]
        public IEnumerator BootedPlaytestPresentationKeepsHudAndFirstThreatFeedbackPlayable()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);
Assert.That(bootstrap.ActiveHost, Is.Not.Null);
            Assert.That(
                bootstrap.ActiveHost.TryRestart().IsSuccess,
                Is.True,
                "The playtest gate needs a fresh session so it observes the authored first threat rather than bootstrap timing.");
            // Let the presentation coordinator complete one LateUpdate after
            // binding the session. This keeps the assertion about visible HUD
            // state independent of bootstrap's exact frame ordering.
            yield return null;

Assert.That(bootstrap.ActiveHost.Session.State, Is.EqualTo(BattleSessionState.Running));
            BattleSceneContext context = bootstrap.ActiveContext;
            Assert.That(context, Is.Not.Null);
            BattlePresentationCoordinator presentation = context.PresentationCoordinator;
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.IsPrepared, Is.True);
            Assert.That(presentation.IsFeedbackPrepared, Is.True);
            Assert.That(presentation.IsBound, Is.True);
            AssertPlayableHudAndD0Reticle(context);
            AssertFixedFrontalD0Composition(context);

            ProjectileViewPool projectilePool = presentation.ProjectileViewPool;
            WarningViewPool warningPool = presentation.WarningViewPool;
            ImpactViewPool impactPool = presentation.ImpactViewPool;
            ThreatTelegraph2DPresenter threatPresenter = context.D0ThreatTelegraphPresenter;
            Assert.That(projectilePool, Is.Not.Null);
            Assert.That(warningPool, Is.Not.Null);
            Assert.That(impactPool, Is.Not.Null);
            Assert.That(threatPresenter, Is.Not.Null);
            Assert.That(projectilePool.ViewPoolRejectCount, Is.Zero);
            Assert.That(warningPool.WarningPoolRejectCount, Is.Zero);
            Assert.That(impactPool.ImpactPoolRejectCount, Is.Zero);

            // The D0 scene no longer uses a legacy ground circle. Its first
            // threat must visibly progress through a source/player pulse,
            // projectile, and terminal impact. Use fixed-pool state rather
            // than a particular tick so the test remains robust to frame rate.
            bool telegraphObserved = false;
            bool projectileObserved = false;
            bool impactObserved = false;
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!impactObserved && Time.realtimeSinceStartup < deadline)
            {
                if (!telegraphObserved && threatPresenter.ActiveTelegraphCount > 0)
                {
                    telegraphObserved = true;
                }

                if (telegraphObserved && !projectileObserved && projectilePool.ActiveViewCount > 0)
                {
                    projectileObserved = true;
                }

                if (projectileObserved && impactPool.ActiveViewCount > 0)
                {
                    impactObserved = true;
                }

                if (!impactObserved)
                {
                    yield return null;
                }
            }

            Assert.That(
                telegraphObserved,
                Is.True,
                "The first playable enemy threat must show its D0 telegraph before it releases.");
            Assert.That(
                projectileObserved,
                Is.True,
                "The first playable enemy threat must produce a visible projectile after its warning.");
            Assert.That(
                impactObserved,
                Is.True,
                "The first playable enemy projectile must produce a terminal impact effect.");
            AssertPlayerImpactFeedbackEscapesAvatarOcclusion(context);
            Assert.That(
                presentation.PresentationFaultCount,
                Is.Zero,
                "Presentation exceptions must fail the playtest gate rather than being silently accepted.");
            Assert.That(projectilePool.ViewPoolRejectCount, Is.Zero);
            Assert.That(warningPool.WarningPoolRejectCount, Is.Zero);
            Assert.That(impactPool.ImpactPoolRejectCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SessionHostSupportsPauseResumeAndRestart()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSessionHost host = bootstrap.ActiveHost;
            Assert.That(host.TryPause().IsSuccess, Is.True);
            Assert.That(host.Session.State, Is.EqualTo(BattleSessionState.Paused));
            Assert.That(host.TryResume().IsSuccess, Is.True);
            Assert.That(host.Session.State, Is.EqualTo(BattleSessionState.Running));
            Assert.That(host.IsSpatialQueryReady, Is.True);
            Assert.That(host.IsProjectileWorldReady, Is.True);

            BattleSession previous = host.Session;
            Assert.That(host.TryRestart().IsSuccess, Is.True);
            Assert.That(previous.State, Is.EqualTo(BattleSessionState.Disposed));
            Assert.That(previous.ControlCommandCount, Is.EqualTo(4));
            Assert.That(host.Session, Is.Not.SameAs(previous));
            Assert.That(host.Session.State, Is.EqualTo(BattleSessionState.Running));
            Assert.That(host.Session.ControlCommandCount, Is.EqualTo(1));
            Assert.That(host.TryPause().IsSuccess, Is.True);
            Assert.That(host.Session.ControlCommandCount, Is.EqualTo(2));
            Assert.That(host.TryResume().IsSuccess, Is.True);

            BattleSessionDiagnosticsPresenter presenter = bootstrap.ActiveContext.DiagnosticsPresenter;
            string diagnostics = presenter.RefreshText();
            Assert.That(diagnostics, Does.Contain("State: Running"));
            Assert.That(diagnostics, Does.Contain("Trace:"));
        }

        [UnityTest]
        public IEnumerator SessionRestartRestoresPlayerPoseSynchronously()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            CombatLabPlayerController controller =
                context.PlayerAnchor.GetComponent<CombatLabPlayerController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.TryInitialize(out string initializationError), Is.True, initializationError);

            Vector3 initialPosition = context.PlayerAnchor.position;
            Quaternion initialRootRotation = context.PlayerAnchor.rotation;
            Quaternion initialAimRotation = context.AimAnchor.localRotation;
            Quaternion initialCameraPivotRotation = controller.CameraPivot.localRotation;

            CharacterController characterController = controller.CharacterController;
            bool controllerWasEnabled = characterController.enabled;
            characterController.enabled = false;
            context.PlayerAnchor.SetPositionAndRotation(
                initialPosition + new Vector3(2.5f, 0f, 1.5f),
                Quaternion.Euler(0f, 72f, 0f));
            context.AimAnchor.localRotation = Quaternion.Euler(-31f, 0f, 0f);
            controller.CameraPivot.localRotation = Quaternion.Euler(-31f, 0f, 0f);
            characterController.enabled = controllerWasEnabled;

            Assert.That(bootstrap.ActiveHost.TryRestart().IsSuccess, Is.True, bootstrap.ActiveHost.LastError);

            Assert.That(context.PlayerAnchor.position, Is.EqualTo(initialPosition));
            Assert.That(context.PlayerAnchor.rotation, Is.EqualTo(initialRootRotation));
            Assert.That(context.AimAnchor.localRotation, Is.EqualTo(initialAimRotation));
            Assert.That(controller.CameraPivot.localRotation, Is.EqualTo(initialCameraPivotRotation));
        }

        [UnityTest]
        public IEnumerator FreeAimReticleFreezesOnPauseAndRecentersOnRestart()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            CombatAimReticle reticle = context.CombatAimReticle;
            Assert.That(reticle, Is.Not.Null);

            reticle.SetViewport(new Vector2(0.82f, 0.21f));
            Vector2 beforePause = reticle.Viewport;
            Assert.That(host.TryPause().IsSuccess, Is.True);
            yield return null;
            Assert.That(host.Session.State, Is.EqualTo(BattleSessionState.Paused));
            Assert.That(reticle.Viewport, Is.EqualTo(beforePause),
                "Pausing freezes free-aim sampling without moving the virtual reticle.");

            Assert.That(host.TryResume().IsSuccess, Is.True);
            reticle.SetViewport(new Vector2(0.18f, 0.82f));
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            Assert.That(reticle.Viewport, Is.EqualTo(CombatAimViewportMath.Center),
                "F5/restart must explicitly recenter the free reticle.");
        }

        [UnityTest]
        public IEnumerator BootedD0SecondaryCastRoutesFreeAimPresentationAndF5ClearsIt()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BattleSessionHost host = bootstrap.ActiveHost;
            PlayerWeaponPresentationController controller =
                context.PlayerWeaponPresentationController;
            Actor2DPresenter playerPresenter = context.D0PlayerActorPresenter;
            CombatAimReticle reticle = context.CombatAimReticle;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsInitialized, Is.True);
            Assert.That(controller.PresentationProfile, Is.SameAs(playerPresenter.PresentationProfile));
            Assert.That(controller.ActorPresenter, Is.SameAs(playerPresenter));
            Assert.That(controller.PresentationCamera, Is.SameAs(context.MainCamera));
            Assert.That(controller.PlayerEntity, Is.SameAs(context.PlayerEntity));
            Assert.That(
                controller.WeaponDefinition,
                Is.SameAs(context.ScenarioConfig.AuthoredScenario.Player.Weapon));
            Assert.That(controller.SocketRegistry, Is.SameAs(context.PlayerEntity.SocketRegistry));
            Assert.That(
                controller.SocketRegistry.TryResolve(
                    controller.WeaponDefinition.SecondaryPresentation.Shot.SocketId,
                    out Transform visualMuzzle),
                Is.True);

            Transform d0SliceRoot = FindDescendantByName(context.PresentationRoot, "D0Slice2D");
            Assert.That(d0SliceRoot, Is.Not.Null);
            Assert.That(
                controller.SocketRegistry.transform.IsChildOf(
                    context.PlayerEntity.transform),
                Is.True);
            Assert.That(visualMuzzle.IsChildOf(controller.SocketRegistry.transform), Is.True);
            Assert.That(d0SliceRoot.Find("D0Actors"), Is.Null);

            // Let the controller bind its freshly booted feed before injecting
            // the first authored gameplay edge into the real session. A bare
            // `yield return null` resumes before LateUpdate on some Test Runner
            // schedules, which could make this test create a shot before its
            // intended consumer has established a cursor.
            float bindDeadline = Time.realtimeSinceStartup + 2f;
            while (!ReferenceEquals(controller.BoundFeed, host.PlayerShotPresentationFeed)
                && Time.realtimeSinceStartup < bindDeadline)
            {
                yield return new WaitForEndOfFrame();
            }

            Assert.That(
                controller.BoundFeed,
                Is.SameAs(host.PlayerShotPresentationFeed),
                "The player-shot presenter must bind the fresh host feed before this end-to-end input sequence begins.");
            Assert.That(controller.PresentationFaultCount, Is.Zero);

            reticle.SetViewport(new Vector2(0.74f, 0.32f));
            UnityBattleInputSource input = new UnityBattleInputSource();
            CaptureSecondaryInput(input, context.AimAnchor, true, true, false);
            Assert.That(
                host.Session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    out int beginSteps).IsSuccess,
                Is.True);
            Assert.That(beginSteps, Is.GreaterThanOrEqualTo(1));
            TickPlayerWeaponPresentation(controller);

            D0SecondaryChargeView chargeView =
                context.PlayerShotViewRoot.GetComponentInChildren<D0SecondaryChargeView>(true);
            Assert.That(chargeView, Is.Not.Null);
            Assert.That(
                controller.IsSecondaryChargeVisualActive,
                Is.True,
                $"Secondary charge was not consumed by the presenter within the bounded wait. "
                + $"faults={controller.PresentationFaultCount}, presentedShots={controller.PresentedShotCount}, "
                + $"feedBound={ReferenceEquals(controller.BoundFeed, host.PlayerShotPresentationFeed)}, "
                + $"feedSequence={host.PlayerShotPresentationFeed.LastSequence}, "
                + $"traceCount={host.Session.Trace.Count}, session={host.Session.State}.");
            Assert.That(chargeView.IsCharging, Is.True);
            Assert.That(chargeView.ReleaseSource, Is.EqualTo(visualMuzzle.position));
            Assert.That(playerPresenter.IsChargingSecondary, Is.True);
            AssertPresentationTreeHasNoPhysics(
                chargeView.transform,
                "D0 secondary charge visual");

            Vector3 lockedViewportPoint = context.MainCamera.WorldToViewportPoint(
                chargeView.LockedTarget);
            Assert.That(lockedViewportPoint.z, Is.GreaterThan(context.MainCamera.nearClipPlane));
            Assert.That(
                Vector2.Distance(
                    new Vector2(lockedViewportPoint.x, lockedViewportPoint.y),
                    reticle.Viewport),
                Is.LessThan(0.002f),
                "The lock frame must use the free reticle's viewport projection at the active enemy depth.");
            Vector3 activeEnemyViewportPoint = context.MainCamera.WorldToViewportPoint(
                context.ActiveEnemyGameplayAnchor.position);
            Assert.That(
                lockedViewportPoint.z,
                Is.EqualTo(activeEnemyViewportPoint.z).Within(0.002f),
                "Secondary charge depth must follow the active enemy gameplay anchor.");

            Vector2 viewportBeforePause = reticle.Viewport;
            Vector3 targetBeforePause = chargeView.LockedTarget;
            Assert.That(host.TryPause().IsSuccess, Is.True, host.LastError);
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(host.Session.State, Is.EqualTo(BattleSessionState.Paused));
            Assert.That(reticle.Viewport, Is.EqualTo(viewportBeforePause));
            Assert.That(chargeView.IsCharging, Is.True,
                "Pause freezes the visual charge instead of advancing or clearing it.");
            Assert.That(chargeView.LockedTarget, Is.EqualTo(targetBeforePause));
            Assert.That(host.TryResume().IsSuccess, Is.True, host.LastError);

            // Aim withdrawal uses the existing domain cancellation route.
            CaptureSecondaryInput(input, context.AimAnchor, false, false, false);
            Assert.That(
                host.Session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    out int cancelSteps).IsSuccess,
                Is.True);
            Assert.That(cancelSteps, Is.GreaterThanOrEqualTo(1));
            TickPlayerWeaponPresentation(controller);

            Assert.That(
                host.Session.PlayerExposureState,
                Is.EqualTo(PlayerExposureState.Withdrawn),
                "Aim withdrawal must cancel an in-progress secondary and allow the barrier posture to return.");
            Assert.That(controller.IsSecondaryChargeVisualActive, Is.False);
            Assert.That(playerPresenter.IsChargingSecondary, Is.False);

            // Re-enter the real AltCharging state and commit its release.
            CaptureSecondaryInput(input, context.AimAnchor, true, true, false);
            Assert.That(
                host.Session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    out int restartChargeSteps).IsSuccess,
                Is.True);
            Assert.That(restartChargeSteps, Is.GreaterThanOrEqualTo(1));
            TickPlayerWeaponPresentation(controller);

            Assert.That(chargeView.IsCharging, Is.True);

            // The release must satisfy the authored weapon's charge duration.
            // Keep the test configuration-driven so Fei tuning does not leave
            // this visual lifecycle path silently early-releasing.
            for (int elapsedTicks = 1;
                 elapsedTicks < host.Session.Definition.PlayerWeapon.SecondaryMinimumCharge.Value;
                 elapsedTicks++)
            {
                CaptureSecondaryInput(input, context.AimAnchor, true, false, false);
                Assert.That(
                    host.Session.PumpWithBattleInput(
                        OneGameplayTickWallTime(),
                        input,
                        out int holdSteps).IsSuccess,
                    Is.True);
                Assert.That(holdSteps, Is.GreaterThanOrEqualTo(1));
                TickPlayerWeaponPresentation(controller);
            }

            int hitMarkerBeforeRelease = controller.SecondaryHitMarkerCount;
            int stopMarkerBeforeRelease = controller.SecondaryStopMarkerCount;
            CaptureSecondaryInput(input, context.AimAnchor, true, false, true);
            Physics.SyncTransforms();
            Assert.That(
                host.Session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    out int releaseSteps).IsSuccess,
                Is.True);
            Assert.That(releaseSteps, Is.GreaterThanOrEqualTo(1));
            TickPlayerWeaponPresentation(controller);

            Assert.That(controller.SecondaryHitMarkerCount, Is.EqualTo(hitMarkerBeforeRelease + 1));
            Assert.That(chargeView.IsReleasing, Is.True);
            Assert.That(controller.ActiveTracerCount, Is.GreaterThan(0));
            Assert.That(controller.ActiveTargetBurstCount, Is.GreaterThan(0));
            Assert.That(playerPresenter.IsChargingSecondary, Is.False);
            Assert.That(
                playerPresenter.CurrentAnimationName,
                Is.EqualTo(controller.WeaponDefinition.SecondaryPresentation.ReleaseAnimation));

            float stopDeadline = Time.realtimeSinceStartup + 2f;
            while (controller.SecondaryStopMarkerCount == stopMarkerBeforeRelease
                && Time.realtimeSinceStartup < stopDeadline)
            {
                yield return null;
            }

            Assert.That(controller.SecondaryStopMarkerCount, Is.EqualTo(stopMarkerBeforeRelease + 1));

            // Keyboard F5 delegates to this exact host restart path. It must
            // synchronously release every presentation-only pooled view.
            Assert.That(host.TryRestart().IsSuccess, Is.True, host.LastError);
            Assert.That(controller.ActiveTracerCount, Is.Zero);
            Assert.That(controller.ActiveTargetBurstCount, Is.Zero);
            Assert.That(controller.ActiveSecondaryChargeVisualCount, Is.Zero);
            Assert.That(playerPresenter.IsChargingSecondary, Is.False);
            Assert.That(reticle.Viewport, Is.EqualTo(CombatAimViewportMath.Center));
        }

        [UnityTest]
        public IEnumerator RestartEventResetsReenabledPlayerInTheSameFrame()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            CombatLabPlayerController playerController =
                context.PlayerAnchor.GetComponent<CombatLabPlayerController>();
            Assert.That(playerController, Is.Not.Null);
            Assert.That(playerController.IsInitialized, Is.True, playerController.LastError);

            Transform playerAnchor = context.PlayerAnchor;
            Transform aimAnchor = context.AimAnchor;
            Transform cameraPivot = playerController.CameraPivot;
            CombatAimReticle reticle = context.CombatAimReticle;
            Vector3 initialPlayerPosition = playerAnchor.position;
            Quaternion initialPlayerRotation = playerAnchor.rotation;
            Quaternion initialAimRotation = aimAnchor.localRotation;
            Quaternion initialCameraPivotRotation = cameraPivot.localRotation;
            Vector3 initialCameraPivotPosition = cameraPivot.localPosition;

            // Verify that OnEnable restores the synchronous subscription after a
            // temporary UI/scene disable, rather than relying on next-frame
            // observed-session polling.
            playerController.enabled = false;
            playerController.enabled = true;

            CharacterController characterController = playerController.CharacterController;
            bool wasControllerEnabled = characterController.enabled;
            characterController.enabled = false;
            playerAnchor.SetPositionAndRotation(
                initialPlayerPosition + new Vector3(2f, 0f, -3f),
                Quaternion.Euler(0f, 45f, 0f));
            aimAnchor.localRotation = Quaternion.Euler(31f, 0f, 0f);
            cameraPivot.localRotation = Quaternion.Euler(-27f, 0f, 0f);
            cameraPivot.localPosition += new Vector3(0.5f, 0f, 1f);
            reticle.SetViewport(new Vector2(0.85f, 0.15f));
            characterController.enabled = wasControllerEnabled;

            BattleSession previousSession = bootstrap.ActiveHost.Session;
            Assert.That(bootstrap.ActiveHost.TryRestart().IsSuccess, Is.True, bootstrap.ActiveHost.LastError);

            // Intentionally no yield: F5 uses BattleSessionHost.TryRestart and
            // this verifies its SessionRestarted callback restores the player in
            // the same rendered frame.
            Assert.That(
                (playerAnchor.position - initialPlayerPosition).sqrMagnitude,
                Is.LessThan(0.01f),
                "The controller must restore the player in the same frame; CharacterController precision is allowed within one millimetre.");
            Assert.That(Quaternion.Angle(playerAnchor.rotation, initialPlayerRotation), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(aimAnchor.localRotation, initialAimRotation), Is.LessThan(0.01f));
            Assert.That(
                (cameraPivot.localPosition - initialCameraPivotPosition).sqrMagnitude,
                Is.LessThan(0.000001f));
            Assert.That(
                Quaternion.Angle(cameraPivot.localRotation, initialCameraPivotRotation),
                Is.LessThan(0.01f));
            Assert.That(
                reticle.Viewport,
                Is.EqualTo(CombatAimViewportMath.Center),
                "The same-frame restart reset must also clear the free reticle state.");
            Assert.That(bootstrap.ActiveHost.Session, Is.Not.SameAs(previousSession));
        }

        [UnityTest]
        public IEnumerator BootedSessionUsesRealPhysicsQueryToDamageEnemy()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BoxCollider enemyBodyCollider =
                RequireActiveEnemyEntity(context).BodyHitbox as BoxCollider;
            Assert.That(enemyBodyCollider, Is.Not.Null);

            BattleSessionHost host = bootstrap.ActiveHost;
            int lifeBefore = host.Session.GetFinalSnapshot().EnemyLife;
            D0EnemyBehaviorController behavior = RequireD0EnemyBehavior(context);
            Assert.That(behavior, Is.Not.Null);
            UnityBattleInputSource idleInput = new UnityBattleInputSource();
            idleInput.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            idleInput.CaptureAimPose(context.AimAnchor);
            while (host.Session.CurrentTick.Value < 112L)
            {
                DomainResult entryPumped = host.Session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    idleInput,
                    behavior,
                    out int entrySteps);
                Assert.That(entryPumped.IsSuccess, Is.True, entryPumped.ToString());
                Assert.That(entrySteps, Is.EqualTo(1));
            }

            Assert.That(behavior.IsPatrolling, Is.True,
                "The real spatial-query shot must be evaluated after Burstbug has completed its authored entry.");
            Physics.SyncTransforms();
            UnityBattleInputSource input = new UnityBattleInputSource();
            CapturePrimaryInputAtPoint(input, context, enemyBodyCollider.bounds.center);
            DomainResult pumped = host.Session.PumpWithBattleInput(
                OneGameplayTickWallTime(),
                input,
                behavior,
                out int executedSteps);

            Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
            Assert.That(executedSteps, Is.GreaterThanOrEqualTo(1));
            Assert.That(host.Session.GetFinalSnapshot().EnemyLife, Is.LessThan(lifeBefore));
            Assert.That(host.Session.SelectedAttackHits.Count, Is.GreaterThan(0));
            Assert.That(host.SpatialTranscript.Count, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator EnvironmentBlockerPreventsRealPhysicsDamage()
        {
            CombatLabRuntimeHarness bootstrap = null;
            yield return LoadCombatLabHarness(
                value => bootstrap = value);

            BattleSceneContext context = bootstrap.ActiveContext;
            BoxCollider enemyBodyCollider =
                RequireActiveEnemyEntity(context).BodyHitbox as BoxCollider;
            Assert.That(enemyBodyCollider, Is.Not.Null);

            Scene combatLab = SceneManager.GetSceneByName("CombatLab");
            GameObject blocker = FindGameObjectInScene(combatLab, "SideBlocker");
            Assert.That(blocker, Is.Not.Null);
            blocker.transform.position = Vector3.Lerp(
                context.AimAnchor.position,
                enemyBodyCollider.bounds.center,
                0.5f);
            Physics.SyncTransforms();

            BattleSessionHost host = bootstrap.ActiveHost;
            int lifeBefore = host.Session.GetFinalSnapshot().EnemyLife;
            UnityBattleInputSource input = new UnityBattleInputSource();
            CapturePrimaryInputAtPoint(input, context, enemyBodyCollider.bounds.center);
            DomainResult pumped = host.Session.PumpWithBattleInput(
                OneGameplayTickWallTime(),
                input,
                out int executedSteps);

            Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
            Assert.That(executedSteps, Is.GreaterThanOrEqualTo(1));
            Assert.That(host.Session.GetFinalSnapshot().EnemyLife, Is.EqualTo(lifeBefore));
            Assert.That(host.Session.SelectedAttackHits.Count, Is.Zero);
            Assert.That(host.SpatialTranscript.Count, Is.GreaterThan(0));
        }

        private static void TickPlayerWeaponPresentation(
            PlayerWeaponPresentationController controller)
        {
            Assert.That(controller, Is.Not.Null);
            MethodInfo lateUpdate = typeof(PlayerWeaponPresentationController).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null,
                "The controller must retain its presentation-only LateUpdate pump.");
            lateUpdate.Invoke(controller, null);
        }

        private static IEnumerator LoadCombatLabHarness(
            Action<CombatLabRuntimeHarness> completed)
        {
            return CombatLabPlayModeHarness.Load(completed);
        }

        private static IEnumerator WaitForBootstrapState(
            GameBootstrap bootstrap,
            BootstrapState expectedState,
            float timeoutSeconds)
        {
            Assert.That(bootstrap, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (bootstrap.State != expectedState
                && bootstrap.State != BootstrapState.Failed)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Assert.Fail(
                        $"GameBootstrap did not reach {expectedState} within {timeoutSeconds} seconds. Current state: {bootstrap.State}. Error: {bootstrap.LastError}");
                }

                yield return null;
            }

            Assert.That(
                bootstrap.State,
                Is.EqualTo(expectedState),
                bootstrap.LastError);
        }

        private static IEnumerator WaitForBootstrap(GameBootstrap bootstrap, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (bootstrap.State != BootstrapState.Running && bootstrap.State != BootstrapState.Failed)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Assert.Fail($"GameBootstrap did not finish within {timeoutSeconds} seconds.");
                }

                yield return null;
            }
        }

        private static IEnumerator WaitForExecutedTick(BattleSessionHost host, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (host.Session.ExecutedTickCount == 0)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Assert.Fail($"BattleSessionHost did not execute a tick within {timeoutSeconds} seconds.");
                }

                yield return null;
            }
        }

        private static T[] FindComponentsInScene<T>(Scene scene)
            where T : Component
        {
            System.Collections.Generic.List<T> values =
                new System.Collections.Generic.List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                values.AddRange(
                    roots[index].GetComponentsInChildren<T>(true));
            }

            return values.ToArray();
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static GameObject FindGameObjectInScene(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < transforms.Length; index++)
                {
                    if (transforms[index].name == name)
                    {
                        return transforms[index].gameObject;
                    }
                }
            }

            return null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == name)
                {
                    return transforms[index];
                }
            }

            return null;
        }

        private static void AssertRectStaysOnScreen(RectTransform rect, string contractPath)
        {
            Assert.That(rect, Is.Not.Null, $"{contractPath} must be a RectTransform.");
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(-0.5f),
                $"{contractPath} must not extend off the left edge of the authored 16:9 HUD.");
            Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(-0.5f),
                $"{contractPath} must not extend below the authored 16:9 HUD.");
            Assert.That(corners[2].x, Is.LessThanOrEqualTo(Screen.width + 0.5f),
                $"{contractPath} must not extend off the right edge of the authored 16:9 HUD.");
            Assert.That(corners[2].y, Is.LessThanOrEqualTo(Screen.height + 0.5f),
                $"{contractPath} must not extend above the authored 16:9 HUD.");
        }

        private static void AssertRectContainsChild(
            RectTransform container,
            RectTransform child,
            string contractPath)
        {
            Assert.That(container, Is.Not.Null);
            Assert.That(child, Is.Not.Null, $"{contractPath} must be a RectTransform.");
            Vector3[] containerCorners = new Vector3[4];
            Vector3[] childCorners = new Vector3[4];
            container.GetWorldCorners(containerCorners);
            child.GetWorldCorners(childCorners);
            Assert.That(childCorners[0].x, Is.GreaterThanOrEqualTo(containerCorners[0].x - 0.5f),
                $"{contractPath} must remain inside its HUD panel.");
            Assert.That(childCorners[0].y, Is.GreaterThanOrEqualTo(containerCorners[0].y - 0.5f),
                $"{contractPath} must remain inside its HUD panel.");
            Assert.That(childCorners[2].x, Is.LessThanOrEqualTo(containerCorners[2].x + 0.5f),
                $"{contractPath} must remain inside its HUD panel.");
            Assert.That(childCorners[2].y, Is.LessThanOrEqualTo(containerCorners[2].y + 0.5f),
                $"{contractPath} must remain inside its HUD panel.");
        }

        private static void AssertInitialSpawnAimFacesEnemy(BattleSceneContext context)
        {
            Transform enemyTarget = context.EnemyEntityWorld == null
                ? null
                : context.EnemyEntityWorld.ActiveGameplayAnchor;
            if (enemyTarget == null)
            {
                D0EncounterSpawnSlot initialSlot =
                    context.ScenarioConfig.AuthoredScenario.Encounter.InitialSpawnSlot;
                Assert.That(
                    context.TryGetEncounterSpawnPoint(
                        initialSlot.SpawnPointId,
                        out D0SpawnPoint spawnPoint),
                    Is.True);
                enemyTarget = spawnPoint.transform;
            }

            Vector3 toEnemy = enemyTarget.position - context.AimAnchor.position;
            Assert.That(
                Vector3.Dot(context.AimAnchor.forward, toEnemy),
                Is.GreaterThan(0f),
                "At scene load, AimAnchor must initially face the enemy; runtime mouse aim is allowed to rotate freely afterward.");
        }

        private static void AssertPlayableHudAndD0Reticle(BattleSceneContext context)
        {
            Canvas canvas = context.PresentationCanvas;
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.isActiveAndEnabled, Is.True);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));

            BattleHudPresenter legacyHud = context.BattleHudPresenter;
            Assert.That(legacyHud, Is.Not.Null,
                "The compatibility HUD remains bound for legacy tests and non-D0 scenes.");
            Assert.That(legacyHud.gameObject.activeInHierarchy, Is.False,
                "The old HUD must remain hidden so it cannot compete with the D0 formal presentation.");

            CombatHud2DPresenter hud = context.D0CombatHud2DPresenter;
            Assert.That(hud, Is.Not.Null,
                "The D0 slice must wire the formal player HUD through BattleSceneContext.");
            Assert.That(hud.isActiveAndEnabled, Is.True);
            Assert.That(hud.TryValidate(out string hudError), Is.True, hudError);
            Assert.That(hud.IsTerminalLatched, Is.False);
            Assert.That(hud.IsTerminalPanelVisible, Is.False);
            Assert.That(hud.IsDevelopmentOverlayVisible, Is.False);

            Transform legacyCrosshair = FindDescendantByName(canvas.transform, "Crosshair");
            Assert.That(legacyCrosshair, Is.Not.Null, "The legacy cursor stays present for migration safety.");
            Assert.That(legacyCrosshair.gameObject.activeSelf, Is.False,
                "The old centered text cursor must not compete with the free D0 reticle.");

            CombatAimReticle reticle = context.CombatAimReticle;
            Assert.That(reticle, Is.Not.Null);
            Assert.That(reticle.TryGetViewport(out Vector2 viewport), Is.True);
            Assert.That(viewport, Is.EqualTo(CombatAimViewportMath.Center));
            RectTransform reticleRect = reticle.transform as RectTransform;
            Assert.That(reticleRect, Is.Not.Null);
            Assert.That(reticleRect.anchorMin, Is.EqualTo(viewport));
            Assert.That(reticleRect.anchorMax, Is.EqualTo(viewport));
            Assert.That(reticle.GetComponent<Canvas>(), Is.Not.Null);

            Transform threatTextTransform = FindDescendantByName(hud.transform, "ThreatText");
            Text threatText = threatTextTransform == null
                ? null
                : threatTextTransform.GetComponent<Text>();
            Assert.That(threatText, Is.Not.Null,
                "The formal HUD must retain a compact enemy-danger readout rather than a long RUNNING label.");
            Assert.That(threatText.isActiveAndEnabled, Is.True);
        }

        private static void AssertFixedFrontalD0Composition(BattleSceneContext context)
        {
            D0ThreeCProfile threeCProfile = RequireAuthoredThreeCProfile(context);
            CombatLabPlayerController playerController =
                context.PlayerAnchor.GetComponent<CombatLabPlayerController>();
            Assert.That(playerController, Is.Not.Null);
            Assert.That(playerController.UsesTwoPointFiveDPresentation, Is.True);
            Assert.That(playerController.PlanarMovementEnabled, Is.False);

            Transform cameraPivot = playerController.CameraPivot;
            Assert.That(cameraPivot, Is.Not.Null);
            Assert.That(context.MainCamera.transform.parent, Is.SameAs(cameraPivot));
            AssertCameraInstallationMatchesThreeCProfile(context, threeCProfile);

            Actor2DPresenter enemyPresenter = context.ActiveD0EnemyActorPresenter;
            Assert.That(enemyPresenter, Is.Not.Null,
                "The fixed frontal composition requires the D0 Burstbug presenter, not the disabled legacy greybox mesh.");
            GameObject enemyVisual = enemyPresenter.gameObject;

            Renderer enemyRenderer = FindFirstEnabledRenderer(enemyVisual);
            Assert.That(enemyRenderer, Is.Not.Null, "The D0 Burstbug must be renderable for the fixed frontal composition check.");
            Vector3 enemyViewportPoint = context.MainCamera.WorldToViewportPoint(enemyRenderer.bounds.center);
            Assert.That(enemyViewportPoint.z, Is.GreaterThan(context.MainCamera.nearClipPlane));
            D0EnemyBehaviorController behavior = RequireD0EnemyBehavior(context);
            Assert.That(behavior, Is.Not.Null,
                "The fixed camera composition must use the authored Burstbug behavior bridge.");
            Assert.That(behavior.TryValidate(out string behaviorError), Is.True, behaviorError);
            Assert.That(behavior.VisualRoot, Is.Not.SameAs(behavior.GameplayAnchor),
                "Burstbug's visual root and spatial hitbox anchor must remain separate but synchronized.");
        }

        private static void AssertPlayerImpactFeedbackEscapesAvatarOcclusion(BattleSceneContext context)
        {
            Actor2DPresenter playerPresenter = context.D0PlayerActorPresenter;
            Assert.That(playerPresenter, Is.Not.Null,
                "Player impact feedback must be evaluated against the D0 Fei presenter, not the disabled legacy greybox mesh.");
            GameObject playerVisual = playerPresenter.gameObject;
            Renderer playerRenderer = FindFirstEnabledRenderer(playerVisual);
            Assert.That(playerRenderer, Is.Not.Null);

            ImpactView[] impactViews = context.ImpactViewRoot.GetComponentsInChildren<ImpactView>(true);
            int activeImpactCount = 0;
            for (int index = 0; index < impactViews.Length; index++)
            {
                ImpactView impactView = impactViews[index];
                if (impactView == null || !impactView.IsActive)
                {
                    continue;
                }

                activeImpactCount++;
                Assert.That(
                    playerRenderer.bounds.Contains(impactView.transform.position),
                    Is.False,
                    "Player-targeted impact feedback must be lifted outside the visible avatar bounds instead of being hidden inside the shoulder-camera greybox.");
            }

            Assert.That(activeImpactCount, Is.GreaterThan(0));
        }

        private static Renderer FindFirstEnabledRenderer(GameObject gameObject)
        {
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    return renderer;
                }
            }

            return null;
        }

        private static void AssertRenderersDisabled(GameObject gameObject, string contractPath)
        {
            Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
            Assert.That(
                renderers,
                Is.Not.Empty,
                $"{contractPath} must retain renderer components so the legacy greybox can be restored outside D0.");
            for (int index = 0; index < renderers.Length; index++)
            {
                Assert.That(
                    renderers[index].enabled,
                    Is.False,
                    $"{contractPath} renderer must be disabled in the 2.5D D0 composition; the D0 weakpoint FX owns player-visible feedback.");
            }
        }

        private static void AssertHiddenGameplayBlockerContract(
            GameObject cover,
            int blockerLayer,
            string contractPath)
        {
            Collider collider = cover.GetComponent<Collider>();
            Assert.That(collider, Is.Not.Null, $"{contractPath} must retain its physical blocker collider.");
            Assert.That(collider.isTrigger, Is.False);
            Assert.That(cover.layer, Is.EqualTo(blockerLayer));

            Renderer[] renderers = cover.GetComponentsInChildren<Renderer>(true);
            Assert.That(
                renderers,
                Is.Not.Empty,
                $"{contractPath} must retain renderer components so the legacy view can be restored outside D0.");
            for (int index = 0; index < renderers.Length; index++)
            {
                Assert.That(
                    renderers[index].enabled,
                    Is.False,
                    $"{contractPath} renderer must be disabled in the 2.5D D0 composition; forest layers own visible cover framing.");
            }
        }

        private static void AssertViewportAimSelectsHitbox(
            BattleSceneContext context,
            BattleSessionHost host,
            Collider expectedCollider,
            HitPart expectedHitPart,
            GeometryId expectedGeometryId,
            long requestId)
        {
            Assert.That(expectedCollider, Is.Not.Null);
            Vector3 viewport3 = context.MainCamera.WorldToViewportPoint(expectedCollider.bounds.center);
            Assert.That(viewport3.z, Is.GreaterThan(0f),
                "The visual hitbox target must remain in front of the fixed D0 camera.");
            Vector2 viewport = new Vector2(viewport3.x, viewport3.y);
            context.CombatAimReticle.SetViewport(viewport);

            Ray cameraRay = context.MainCamera.ViewportPointToRay(
                new Vector3(viewport.x, viewport.y, 0f));
            RaycastHit[] hits = new RaycastHit[8];
            int hitCount = Physics.RaycastNonAlloc(
                cameraRay,
                hits,
                context.ScenarioConfig.AttackQuerySettings.MaxDistance,
                context.ScenarioConfig.AttackQuerySettings.PhysicsLayerMask,
                QueryTriggerInteraction.Collide);
            RaycastHit selectedHit = default(RaycastHit);
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = hits[index];
                Collider collider = hit.collider;
                if (collider == null
                    || (context.PlayerAnchor != null
                        && (collider.transform == context.PlayerAnchor
                            || collider.transform.IsChildOf(context.PlayerAnchor)))
                    || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                selectedHit = hit;
            }

            Assert.That(selectedHit.collider, Is.SameAs(expectedCollider),
                "The nearest D0 viewport ray hit must match the intended visible gameplay hitbox.");

            UnityBattleInputSource input = new UnityBattleInputSource();
            input.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            input.CaptureAimPose(
                context.AimAnchor.position,
                selectedHit.point - context.AimAnchor.position,
                context.MainCamera.transform.up);

            TickIndex queryTick = new TickIndex(requestId);
            AttackSnapshot attack = new AttackSnapshot(
                new AttackId(requestId),
                new ShotId(requestId),
                1,
                host.Session.PlayerRuntimeId,
                Team.Player,
                queryTick,
                host.Session.Definition.PlayerWeapon.PrimaryDamage,
                QueryPolicy.PelletRays,
                payloadCount: 1,
                maxImpactCount: 1,
                ammoCost: 1,
                rngVersion: 1);
            PelletSample[] pellets =
            {
                new PelletSample(attack.ShotId, 0, 0x7FFFFF, 0x7FFFFF)
            };
            QueryCandidate[] candidates = new QueryCandidate[
                SpatialContract.AttackQueryCandidateCapacity];
            UnityAttackQueryPort queryPort = new UnityAttackQueryPort(
                context.HitboxRegistry,
                context.ScenarioConfig.AttackQuerySettings);

            DomainResult queried = queryPort.Query(
                new AttackQueryRequest(input.GetTickInput(queryTick), attack, pellets, pellets.Length),
                candidates,
                out AttackQueryResult result);

            Assert.That(queried.IsSuccess, Is.True, queried.ToString());
            Assert.That(
                ContainsCandidate(
                    candidates,
                    result.CandidateCount,
                    host.Session.EnemyRuntimeId,
                    expectedHitPart,
                    expectedGeometryId),
                Is.True,
                "The live Unity attack query must preserve the visible viewport selection through AimAnchor parallax correction.");
        }

        private static void AssertPresentationTreeHasNoPhysics(Transform root, string contractPath)
        {
            Assert.That(root, Is.Not.Null, $"{contractPath} must be wired in BattleSceneContext.");
            Assert.That(
                root.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                $"{contractPath} must remain presentation-only and cannot contain 3D colliders.");
            Assert.That(
                root.GetComponentsInChildren<Collider2D>(true),
                Is.Empty,
                $"{contractPath} must remain presentation-only and cannot contain 2D colliders.");
            Assert.That(
                root.GetComponentsInChildren<Rigidbody>(true),
                Is.Empty,
                $"{contractPath} must remain presentation-only and cannot contain 3D rigidbodies.");
            Assert.That(
                root.GetComponentsInChildren<Rigidbody2D>(true),
                Is.Empty,
                $"{contractPath} must remain presentation-only and cannot contain 2D rigidbodies.");
        }

        private static void AssertCombatVfxWorldMatchesAuthoredPresentation(
            BattleSceneContext context)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(context.ScenarioConfig, Is.Not.Null);
            Assert.That(context.ScenarioConfig.UsesAuthoredScenario, Is.True);

            D0CombatVfxWorld world = context.CombatVfxWorld;
            Assert.That(world, Is.Not.Null);
            Assert.That(world.IsPrepared, Is.True);
            Assert.That(world.HotPathInstantiateCount, Is.Zero);
            Assert.That(world.HotPathDestroyCount, Is.Zero);

            D0CombatScenarioDefinition scenario =
                context.ScenarioConfig.AuthoredScenario;
            D0WeaponDefinition weapon = scenario.Player.Weapon;
            AssertCombatVfxPool(
                world,
                weapon.PrimaryPresentation.MuzzleVfxKey,
                weapon.PrimaryPresentation.MuzzlePrewarmCapacity,
                null,
                "primary muzzle");
            AssertCombatVfxPool(
                world,
                weapon.PrimaryPresentation.TracerVfxKey,
                weapon.PrimaryPresentation.TracerPrewarmCapacity,
                null,
                "primary tracer");
            AssertCombatVfxPool(
                world,
                weapon.SecondaryPresentation.Shot.MuzzleVfxKey,
                weapon.SecondaryPresentation.Shot.MuzzlePrewarmCapacity,
                null,
                "secondary muzzle");
            AssertCombatVfxPool(
                world,
                weapon.SecondaryPresentation.Shot.TracerVfxKey,
                weapon.SecondaryPresentation.Shot.TracerPrewarmCapacity,
                null,
                "secondary tracer");
            AssertCombatVfxPool(
                world,
                weapon.SecondaryPresentation.ChargeVfxKey,
                weapon.SecondaryPresentation.ChargePrewarmCapacity,
                null,
                "secondary charge");
            AssertCombatVfxPool(
                world,
                weapon.SecondaryPresentation.TargetBurstVfxKey,
                weapon.SecondaryPresentation.TargetBurstPrewarmCapacity,
                null,
                "secondary target burst");

            D0EncounterDefinition encounter = scenario.Encounter;
            for (int attackIndex = 0;
                 attackIndex < encounter.AttackScheduleCount;
                 attackIndex++)
            {
                D0EnemyAttackDefinition attack =
                    encounter.GetAttackScheduleEntry(attackIndex).Attack;
                AssertCombatVfxPool(
                    world,
                    attack.EffectiveVisualEffectKey,
                    attack.VfxPrewarmCapacity,
                    attack.VisualEffectPrefab,
                    attack.AttackId);
            }

            for (int spawnIndex = 0;
                 spawnIndex < encounter.SpawnSlotCount;
                 spawnIndex++)
            {
                D0EnemyDefinition enemy =
                    encounter.GetSpawnSlot(spawnIndex).Enemy;
                Assert.That(enemy, Is.Not.Null);
                D0ActorPresentationDefinition actorState =
                    enemy.ActorPresentation;
                Assert.That(actorState, Is.Not.Null);
                if (!actorState.TryGetEnemyEffects(
                        out D0EnemyEffectPresentationDefinition effects))
                {
                    continue;
                }

                D0EnemyEffectSlot[] deathSlots =
                {
                    D0EnemyEffectSlot.DeathLayerF4,
                    D0EnemyEffectSlot.DeathLayerF3,
                    D0EnemyEffectSlot.DeathLayerF2,
                    D0EnemyEffectSlot.DeathLayerF1
                };
                for (int slotIndex = 0;
                     slotIndex < deathSlots.Length;
                     slotIndex++)
                {
                    if (!effects.TryGet(
                            deathSlots[slotIndex],
                            out D0EnemyEffectPoolDefinition pool))
                    {
                        continue;
                    }

                    AssertCombatVfxPool(
                        world,
                        "actor." + actorState.ActorId
                            + ".state." + deathSlots[slotIndex],
                        pool.PrewarmCapacity,
                        pool.VisualPrefab,
                        actorState.ActorId + " " + deathSlots[slotIndex]);
                }
            }
        }

        private static void AssertCombatVfxPool(
            D0CombatVfxWorld world,
            string key,
            int expectedCapacity,
            GameObject expectedPrefab,
            string label)
        {
            Assert.That(
                world.TryGetPool(key, out D0CombatVfxPoolDefinition pool),
                Is.True,
                "Missing prewarmed combat VFX pool for " + label + ".");
            Assert.That(
                pool.Capacity,
                Is.EqualTo(expectedCapacity),
                "Combat VFX pool capacity does not match " + label + ".");
            if (expectedPrefab != null)
            {
                Assert.That(
                    pool.Prefab,
                    Is.SameAs(expectedPrefab),
                    "Combat VFX pool prefab does not match " + label + ".");
            }
        }

        private static D0StageDefinition RequireAuthoredStage(BattleSceneContext context)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(context.ScenarioConfig, Is.Not.Null);
            Assert.That(context.ScenarioConfig.UsesAuthoredScenario, Is.True);
            D0CombatScenarioDefinition scenario = context.ScenarioConfig.AuthoredScenario;
            Assert.That(scenario, Is.Not.Null);
            D0StageDefinition stageDefinition = scenario.StageDefinition;
            Assert.That(stageDefinition, Is.Not.Null);
            Assert.That(stageDefinition.TryValidate(out string error), Is.True, error);
            return stageDefinition;
        }

        private static D0ThreeCProfile RequireAuthoredThreeCProfile(BattleSceneContext context)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(context.ScenarioConfig, Is.Not.Null);
            Assert.That(context.ScenarioConfig.UsesAuthoredScenario, Is.True);
            D0CombatScenarioDefinition scenario = context.ScenarioConfig.AuthoredScenario;
            Assert.That(scenario, Is.Not.Null);
            D0ThreeCProfile threeCProfile = scenario.ThreeCProfile;
            Assert.That(threeCProfile, Is.Not.Null);
            Assert.That(threeCProfile.TryValidate(out string error), Is.True, error);
            return threeCProfile;
        }

        private static void AssertCameraInstallationMatchesThreeCProfile(
            BattleSceneContext context,
            D0ThreeCProfile threeCProfile)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(threeCProfile, Is.Not.Null);
            Assert.That(context.MainCamera, Is.Not.Null);
            CombatLabPlayerController playerController = context.PlayerAnchor == null
                ? null
                : context.PlayerAnchor.GetComponent<CombatLabPlayerController>();
            Assert.That(playerController, Is.Not.Null);
            Transform cameraPivot = playerController.CameraPivot;
            Assert.That(cameraPivot, Is.Not.Null);

            Assert.That(
                (cameraPivot.localPosition - threeCProfile.CameraPivotLocalPosition).sqrMagnitude,
                Is.LessThan(0.000001f),
                "CameraPivot position must match the authored D0 3C profile.");
            Assert.That(
                Quaternion.Angle(
                    cameraPivot.localRotation,
                    Quaternion.Euler(threeCProfile.CameraPivotLocalEulerAngles)),
                Is.LessThan(0.01f),
                "CameraPivot rotation must match the authored D0 3C profile.");
            Assert.That(
                (context.MainCamera.transform.localPosition - threeCProfile.CameraLocalPosition)
                    .sqrMagnitude,
                Is.LessThan(0.000001f),
                "MainCamera local position must match the authored D0 3C profile.");
            Assert.That(
                Quaternion.Angle(
                    context.MainCamera.transform.localRotation,
                    Quaternion.Euler(threeCProfile.CameraLocalEulerAngles)),
                Is.LessThan(0.01f),
                "MainCamera local rotation must match the authored D0 3C profile.");
            Assert.That(
                context.MainCamera.fieldOfView,
                Is.EqualTo(threeCProfile.CameraFieldOfView).Within(0.01f));
            Assert.That(
                context.MainCamera.nearClipPlane,
                Is.EqualTo(threeCProfile.CameraNearClipPlane).Within(0.001f));
            Assert.That(
                context.MainCamera.farClipPlane,
                Is.EqualTo(threeCProfile.CameraFarClipPlane).Within(0.001f));
        }

        private static void ApplyFixedFrontalCameraConfiguration(
            BattleSceneContext context,
            D0ThreeCProfile threeCProfile)
        {
            Type installerType = FindLoadedType("FPG.Demo.Editor.FpgDemoD0StageInstaller");
            Assert.That(installerType, Is.Not.Null,
                "The D0 installer must be available in the Editor PlayMode Test Runner.");
            MethodInfo configureCamera = installerType.GetMethod(
                "ConfigureFixedFrontalCamera",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(configureCamera, Is.Not.Null,
                "The D0 installer must retain the centralized camera-configuration step.");

            try
            {
                configureCamera.Invoke(null, new object[] { context, threeCProfile });
            }
            catch (TargetInvocationException exception)
            {
                Exception innerException = exception.InnerException ?? exception;
                Assert.Fail($"D0 camera configuration failed: {innerException.Message}");
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            Assert.That(target, Is.Not.Null);
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private profile field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static Type FindLoadedType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void AssertSpawnedActorOwnershipMatchesAuthoredDefinitions(
            BattleSceneContext context,
            Transform d0SliceRoot,
            D0CombatScenarioDefinition scenario,
            D0StageDefinition stageDefinition)
        {
            Assert.That(scenario, Is.Not.Null);
            Assert.That(context.EncounterSpawnPoints.Count, Is.EqualTo(stageDefinition.SpawnPoints.Count));
            for (int index = 0; index < stageDefinition.SpawnPoints.Count; index++)
            {
                D0StageSpawnPointDefinition definition =
                    stageDefinition.SpawnPoints[index];
                Assert.That(
                    context.TryGetEncounterSpawnPoint(
                        definition.SpawnPointId,
                        out D0SpawnPoint spawnPoint),
                    Is.True,
                    definition.SpawnPointId);
                Assert.That(spawnPoint.transform.IsChildOf(context.ActorsRoot), Is.True);
                Assert.That(
                    Vector3.Distance(
                        spawnPoint.transform.position,
                        context.ActorsRoot.TransformPoint(
                            definition.LocalPosition)),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        spawnPoint.transform.rotation,
                        context.ActorsRoot.rotation * Quaternion.Euler(
                            definition.LocalEulerAngles)),
                    Is.LessThan(0.01f));
            }

            Assert.That(
                d0SliceRoot.Find("D0Actors"),
                Is.Null,
                "Character entities must not be duplicated under presentation.");

            D0PlayerEntityView playerEntity = context.PlayerEntity;
            Assert.That(playerEntity, Is.Not.Null);
            Assert.That(
                scenario.Player.EntityPrefab,
                Is.Not.Null,
                "The player definition must own its complete Entity Prefab.");
            Assert.That(
                context.TryGetEncounterSpawnPoint(
                    scenario.PlayerSpawnPointId,
                    out D0SpawnPoint playerSpawn),
                Is.True);
            Assert.That(
                Vector3.Distance(
                    playerEntity.transform.position,
                    playerSpawn.transform.position),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(
                    playerEntity.transform.rotation,
                    playerSpawn.transform.rotation),
                Is.LessThan(0.01f));

            D0PlayerEntityView playerPrefab = scenario.Player.EntityPrefab;
            Assert.That(
                (playerEntity.VisualRoot.localPosition
                    - playerPrefab.VisualRoot.localPosition).sqrMagnitude,
                Is.LessThan(0.000001f));
            Assert.That(
                Quaternion.Angle(
                    playerEntity.VisualRoot.localRotation,
                    playerPrefab.VisualRoot.localRotation),
                Is.LessThan(0.01f));
            Assert.That(
                (playerEntity.VisualRoot.localScale
                    - playerPrefab.VisualRoot.localScale).sqrMagnitude,
                Is.LessThan(0.000001f));

            D0EnemyEntityWorld entityWorld = context.EnemyEntityWorld;
            Assert.That(entityWorld, Is.Not.Null);
            Assert.That(entityWorld.IsPrepared, Is.True);
            D0EnemyEntityView activeEntity = entityWorld.ActiveEntity;
            D0EnemyDefinition activeDefinition =
                entityWorld.ActiveEnemyDefinition;
            Assert.That(activeEntity, Is.Not.Null);
            Assert.That(
                activeDefinition,
                Is.SameAs(scenario.Encounter.InitialSpawnSlot.Enemy));
            Assert.That(activeEntity.GameplayAnchor.parent, Is.SameAs(activeEntity.transform));
            Assert.That(
                activeEntity.BodyHitbox.transform.IsChildOf(
                    activeEntity.GameplayAnchor),
                Is.True);
            Assert.That(
                activeEntity.WeakpointHitbox.transform.IsChildOf(
                    activeEntity.WeakpointAnchor),
                Is.True);
            Assert.That(
                context.ActiveD0EnemyActorPresenter,
                Is.SameAs(activeEntity.ActorPresenter));

            D0EnemyEntityView enemyPrefab = activeDefinition.EntityPrefab;
            Assert.That(enemyPrefab, Is.Not.Null);
            Assert.That(
                (activeEntity.VisualRoot.localPosition
                    - enemyPrefab.VisualRoot.localPosition).sqrMagnitude,
                Is.LessThan(0.000001f));
            Assert.That(
                Quaternion.Angle(
                    activeEntity.VisualRoot.localRotation,
                    enemyPrefab.VisualRoot.localRotation),
                Is.LessThan(0.01f));
            Assert.That(
                (activeEntity.VisualRoot.localScale
                    - enemyPrefab.VisualRoot.localScale).sqrMagnitude,
                Is.LessThan(0.000001f));

            D0ActorSocketRegistry sockets = playerEntity.SocketRegistry;
            D0WeaponDefinition weapon = scenario.Player.Weapon;
            Assert.That(sockets, Is.Not.Null);
            Assert.That(
                sockets.TryResolve(
                    weapon.PrimaryPresentation.SocketId,
                    out Transform primaryMuzzle),
                Is.True);
            Assert.That(
                sockets.TryResolve(
                    weapon.SecondaryPresentation.Shot.SocketId,
                    out Transform secondaryMuzzle),
                Is.True);
            Assert.That(primaryMuzzle, Is.Not.SameAs(secondaryMuzzle));
            Assert.That(primaryMuzzle.IsChildOf(playerEntity.transform), Is.True);
            Assert.That(secondaryMuzzle.IsChildOf(playerEntity.transform), Is.True);
            Assert.That(
                FindDescendantByName(context.ActorsRoot, "D0SecondaryTargetProxy"),
                Is.Null);
        }

        private static bool ContainsCandidate(
            QueryCandidate[] candidates,
            int count,
            RuntimeId targetId,
            HitPart hitPart,
            GeometryId geometryId)
        {
            for (int index = 0; index < count; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (candidate.TargetId == targetId
                    && candidate.HitPart == hitPart
                    && candidate.GeometryId == geometryId)
                {
                    return true;
                }
            }

            return false;
        }

        private static long OneGameplayTickWallTime()
        {
            return (TimeSpan.TicksPerSecond + GameplayClock.DefaultTickRate - 1L)
                / GameplayClock.DefaultTickRate;
        }

        private static UnityBattleInputSource CreateForwardPrimaryInputSource(
            Transform aimAnchor)
        {
            UnityBattleInputSource source = new UnityBattleInputSource();
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: true,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            source.CaptureAimPose(aimAnchor);
            return source;
        }

        private static void CapturePrimaryInputAtPoint(
            UnityBattleInputSource source,
            BattleSceneContext context,
            Vector3 targetPoint)
        {
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: true,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            source.CaptureAimPose(
                context.AimAnchor.position,
                targetPoint - context.AimAnchor.position,
                context.MainCamera.transform.up);
        }

        private static void FireSecondaryAtPoint(
            UnityBattleInputSource source,
            BattleSceneContext context,
            BattleSession session,
            Vector3 targetPoint)
        {
            CaptureSecondaryInputAtPoint(source, context, targetPoint, true, false);
            Assert.That(
                session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    source,
                    out int chargeSteps).IsSuccess,
                Is.True);
            Assert.That(chargeSteps, Is.EqualTo(1));

            for (int elapsedTicks = 1;
                 elapsedTicks < session.Definition.PlayerWeapon.SecondaryMinimumCharge.Value;
                 elapsedTicks++)
            {
                CaptureSecondaryInputAtPoint(source, context, targetPoint, false, false);
                Assert.That(
                    session.PumpWithBattleInput(
                        OneGameplayTickWallTime(),
                        source,
                        out int holdSteps).IsSuccess,
                    Is.True);
                Assert.That(holdSteps, Is.EqualTo(1));
            }

            Physics.SyncTransforms();
            CaptureSecondaryInputAtPoint(source, context, targetPoint, false, true);
            Assert.That(
                session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    source,
                    out int releaseSteps).IsSuccess,
                Is.True);
            Assert.That(releaseSteps, Is.EqualTo(1));
        }

        private static void FireSecondaryAtCurrentWeakpoint(
            UnityBattleInputSource source,
            BattleSceneContext context,
            BattleSession session,
            SphereCollider weakpointCollider,
            D0EnemyBehaviorController behavior)
        {
            Assert.That(weakpointCollider, Is.Not.Null);
            CaptureSecondaryInputAtPoint(
                source,
                context,
                weakpointCollider.bounds.center,
                true,
                false);
            Assert.That(
                session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    source,
                    behavior,
                    out int chargeSteps).IsSuccess,
                Is.True);
            Assert.That(chargeSteps, Is.EqualTo(1));

            for (int elapsedTicks = 1;
                 elapsedTicks < session.Definition.PlayerWeapon.SecondaryMinimumCharge.Value;
                 elapsedTicks++)
            {
                CaptureSecondaryInputAtPoint(
                    source,
                    context,
                    weakpointCollider.bounds.center,
                    false,
                    false);
                Assert.That(
                    session.PumpWithBattleInput(
                        OneGameplayTickWallTime(),
                        source,
                        behavior,
                        out int holdSteps).IsSuccess,
                    Is.True);
                Assert.That(holdSteps, Is.EqualTo(1));
            }

            Physics.SyncTransforms();
            CaptureSecondaryInputAtPoint(
                source,
                context,
                weakpointCollider.bounds.center,
                false,
                true);
            Assert.That(
                session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    source,
                    behavior,
                    out int releaseSteps).IsSuccess,
                Is.True);
            Assert.That(releaseSteps, Is.EqualTo(1));
        }

        private static void CompleteD0VictoryThroughSecondaryWeakpointCasts(
            BattleSceneContext context,
            BattleSession session,
            SphereCollider weakpointCollider,
            D0EnemyBehaviorController behavior)
        {
            UnityBattleInputSource actionInput = new UnityBattleInputSource();
            UnityBattleInputSource idleInput = new UnityBattleInputSource();
            idleInput.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            idleInput.CaptureAimPose(context.AimAnchor);

            const int RequiredWeakpointCasts = 7;
            for (int castIndex = 0;
                 castIndex < RequiredWeakpointCasts && session.State == BattleSessionState.Running;
                 castIndex++)
            {
                FireSecondaryAtCurrentWeakpoint(
                    actionInput,
                    context,
                    session,
                    weakpointCollider,
                    behavior);
                if (session.State != BattleSessionState.Running)
                {
                    break;
                }

                PumpIdleTicks(
                    session,
                    idleInput,
                    session.Definition.PlayerWeapon.SecondaryRecovery.Value + 1,
                    behavior);
                if (session.State != BattleSessionState.Running)
                {
                    break;
                }

                if ((castIndex + 1) % 2 == 0)
                {
                    CaptureReloadInput(actionInput, context);
                    Assert.That(
                        session.PumpWithBattleInput(
                            OneGameplayTickWallTime(),
                            actionInput,
                            behavior,
                            out int reloadSteps).IsSuccess,
                        Is.True);
                    Assert.That(reloadSteps, Is.EqualTo(1));
                    PumpIdleTicks(
                        session,
                        idleInput,
                        session.Definition.PlayerWeapon.ReloadDuration.Value + 1,
                        behavior);
                }
            }

            Assert.That(session.State, Is.EqualTo(BattleSessionState.Completed),
                "Seven real weakpoint casts must complete the authored D0 encounter before the heavy threat resolves.");
            Assert.That(session.CompletionReason, Is.EqualTo(BattleCompletionReason.Victory));
            Assert.That(session.GetFinalSnapshot().EnemyLife, Is.Zero);
        }

        private static void CaptureSecondaryInputAtPoint(
            UnityBattleInputSource source,
            BattleSceneContext context,
            Vector3 targetPoint,
            bool secondaryPressed,
            bool secondaryReleased)
        {
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: secondaryPressed,
                secondaryReleased: secondaryReleased,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            source.CaptureAimPose(
                context.AimAnchor.position,
                targetPoint - context.AimAnchor.position,
                context.MainCamera.transform.up);
        }

        private static void CaptureReloadInput(
            UnityBattleInputSource source,
            BattleSceneContext context)
        {
            source.Capture(new UnityInputSnapshot(
                aimHeld: true,
                primaryHeld: false,
                secondaryPressed: false,
                secondaryReleased: false,
                reloadPressed: true,
                pausePressed: false,
                restartPressed: false));
            source.CaptureAimPose(context.AimAnchor);
        }

        private static void PumpIdleTicks(
            BattleSession session,
            UnityBattleInputSource input,
            int tickCount,
            IBattleTickObserver tickObserver = null)
        {
            for (int index = 0; index < tickCount; index++)
            {
                DomainResult pumped = session.PumpWithBattleInput(
                    OneGameplayTickWallTime(),
                    input,
                    tickObserver,
                    out int executedSteps);
                Assert.That(pumped.IsSuccess, Is.True, pumped.ToString());
                Assert.That(executedSteps, Is.EqualTo(1));
            }
        }

        private static D0EnemyEntityView RequireActiveEnemyEntity(
            BattleSceneContext context)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(context.EnemyEntityWorld, Is.Not.Null);
            Assert.That(context.EnemyEntityWorld.IsPrepared, Is.True);
            D0EnemyEntityView active = context.EnemyEntityWorld.ActiveEntity;
            Assert.That(active, Is.Not.Null);
            Assert.That(active.IsGameplayBound, Is.True);
            return active;
        }

        private static D0EnemyBehaviorController RequireD0EnemyBehavior(
            BattleSceneContext context)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(context.ScenarioConfig, Is.Not.Null);
            Assert.That(
                context.ScenarioConfig.UsesAuthoredScenario,
                Is.True,
                "This D0 test must use an authored scenario so runtime-binding preflight cannot no-op.");
            Assert.That(
                context.TryValidateD0RuntimeBindings(out string runtimeBindingError),
                Is.True,
                runtimeBindingError);

            D0EnemyBehaviorController behavior = context.D0EnemyBehaviorController;
            Assert.That(
                behavior,
                Is.Not.Null,
                "CombatLab must serialize its D0 enemy behavior controller on BattleSceneContext.");
            bool entityWorldPrepared = context.EnemyEntityWorld != null
                && context.EnemyEntityWorld.IsPrepared;
            Transform expectedGameplayAnchor;
            string anchorContract;
            if (entityWorldPrepared)
            {
                expectedGameplayAnchor = context.ActiveEnemyGameplayAnchor;
                anchorContract =
                    "Prepared enemy behavior must follow the active prefab-owned gameplay anchor.";
            }
            else
            {
                D0CombatScenarioDefinition scenario =
                    context.ScenarioConfig.AuthoredScenario;
                Assert.That(scenario, Is.Not.Null);
                Assert.That(scenario.Encounter, Is.Not.Null);
                D0EncounterSpawnSlot initialSpawnSlot =
                    scenario.Encounter.InitialSpawnSlot;
                Assert.That(initialSpawnSlot, Is.Not.Null);
                Assert.That(initialSpawnSlot.Enemy, Is.Not.Null);
                Assert.That(initialSpawnSlot.Enemy.EntityPrefab, Is.Not.Null);
                expectedGameplayAnchor =
                    initialSpawnSlot.Enemy.EntityPrefab.GameplayAnchor;
                anchorContract =
                    "Before EnemyEntityWorld is prepared, enemy behavior must use the initial EntityPrefab gameplay anchor.";
            }

            Assert.That(expectedGameplayAnchor, Is.Not.Null);
            Assert.That(
                behavior.GameplayAnchor,
                Is.SameAs(expectedGameplayAnchor),
                anchorContract);
            return behavior;
        }

        private static D0ShotCameraFeedbackController RequireD0ShotCameraFeedback(
            BattleSceneContext context)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(context.ScenarioConfig, Is.Not.Null);
            Assert.That(context.ScenarioConfig.AuthoredScenario, Is.Not.Null);

            D0ShotCameraFeedbackController feedback =
                context.D0ShotCameraFeedbackController;
            Assert.That(
                feedback,
                Is.Not.Null,
                "CombatLab must serialize its D0 shot camera feedback controller on BattleSceneContext.");
            Assert.That(
                feedback.gameObject,
                Is.SameAs(context.MainCamera.gameObject),
                "The D0 shot camera feedback controller must remain attached to MainCamera.");
            Assert.That(feedback.SessionHost, Is.SameAs(context.SessionHost));
            Assert.That(
                feedback.ThreeCProfile,
                Is.SameAs(context.ScenarioConfig.AuthoredScenario.ThreeCProfile));
            Assert.That(feedback.TargetCamera, Is.SameAs(context.MainCamera));
            Assert.That(
                feedback.TryValidate(out string feedbackError),
                Is.True,
                feedbackError);
            return feedback;
        }

        private static void AssertInvalidD0ShotCameraFeedbackBinding(
            BattleSceneContext context,
            string expectedError)
        {
            Assert.That(
                context.TryValidateD0RuntimeBindings(out string error),
                Is.False,
                "The authored D0 context must reject a missing or miswired shot camera feedback binding.");
            Assert.That(error, Does.Contain(expectedError));
        }

        private static void SetD0ShotCameraFeedbackBinding(
            BattleSceneContext context,
            D0ShotCameraFeedbackController feedback)
        {
            FieldInfo field = typeof(BattleSceneContext).GetField(
                "d0ShotCameraFeedbackController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(context, feedback);
        }

        private static void CaptureSecondaryInput(
            UnityBattleInputSource source,
            Transform aimAnchor,
            bool aimHeld,
            bool secondaryPressed,
            bool secondaryReleased)
        {
            source.Capture(new UnityInputSnapshot(
                aimHeld: aimHeld,
                primaryHeld: false,
                secondaryPressed: secondaryPressed,
                secondaryReleased: secondaryReleased,
                reloadPressed: false,
                pausePressed: false,
                restartPressed: false));
            source.CaptureAimPose(aimAnchor);
        }

        private static FpgRoomCombatLabBinding RequireDefaultCombatLabRoomBinding(
            BattleSceneContext context)
        {
            Assert.That(context, Is.Not.Null);
            Assert.That(context.ScenarioConfig, Is.Not.Null);
            FpgRoomCombatLabBinding binding = context.RoomBinding;
            Assert.That(binding, Is.Not.Null,
                "CombatLab must explicitly serialize its room/scenario composition binding.");
            Assert.That(binding.RoomDefinition, Is.Not.Null);
            Assert.That(binding.RoomDefinition.RoomId, Is.EqualTo("room-combatlab-forest"));
            Assert.That(binding.RoomInstance, Is.Not.Null);
            Assert.That(binding.ScenarioDefinition, Is.Not.Null);
            Assert.That(
                binding.ScenarioDefinition,
                Is.SameAs(context.ScenarioConfig.AuthoredScenario));
            Assert.That(
                binding.RoomDefinition.TryValidate(
                    out FpgRoomValidationResult roomValidation),
                Is.True,
                roomValidation.FirstError == null
                    ? "Default CombatLab room definition is invalid."
                    : roomValidation.FirstError.Message);
            Assert.That(
                binding.RoomDefinition.TryGetPlayerEntryPoint("player-main", out _),
                Is.True);
            Assert.That(
                binding.RoomDefinition.TryGetEnemySpawnPoint("enemy-main", out _),
                Is.True);
            Assert.That(
                binding.TryValidate(out string bindingError),
                Is.True,
                bindingError);
            return binding;
        }
    }
}
