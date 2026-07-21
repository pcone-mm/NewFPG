using System;
using System.Collections.Generic;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FPG.Demo.Editor
{
    /// <summary>
    /// Owns the G1 scene-facing composition beneath D0Slice2D. It never calls
    /// the legacy greybox installer and mutates only D0-owned visual roots plus
    /// the explicit camera/reticle bindings required by the new slice contract.
    /// </summary>
    internal static class FpgDemoD0StageInstaller
    {
        private const string HitTipNormalSpritePath =
            "Assets/Art/HUD/Hit_tip/di_nomal&critical.png";
        private const string HitTipCriticalSpritePath =
            "Assets/Art/HUD/Hit_tip/di_elemental.png";
        private const string FeedbackMaterialPath =
            "Assets/FPGDemo/Presentation/M_FPG_Feedback.mat";
        public static void Ensure(
            Transform d0SliceRoot,
            BattleSceneContext context,
            CombatPresentationProfile profile,
            CombatAudioBank audioBank)
        {
            if (d0SliceRoot == null || context == null || profile == null || audioBank == null)
            {
                throw new ArgumentNullException(
                    d0SliceRoot == null
                        ? nameof(d0SliceRoot)
                        : context == null
                            ? nameof(context)
                            : profile == null
                                ? nameof(profile)
                                : nameof(audioBank));
            }

            Transform stageRoot = RequireDirectChild(d0SliceRoot, "D0Stage");
            Transform canvasRoot = RequireDirectChild(d0SliceRoot, "D0Canvas");
            D0StageDefinition stageDefinition = ResolveStageDefinition(context);
            EnsureEncounterSpawning(context, stageDefinition);
            D0ThreeCProfile threeCProfile = ResolveThreeCProfile(context);
            CombatAimReticle reticle = EnsureOverlayAndReticle(
                canvasRoot,
                context,
                profile,
                threeCProfile);
            EnsureForestStage(stageRoot, reticle, profile, stageDefinition);
            HideLegacyGreyboxWorldRenderer(context.WorldRoot, "CombatGround");
            HideLegacyGreyboxWorldRenderer(context.WorldRoot, "Blockers/SideBlocker");
            ConfigureFixedFrontalCamera(context, threeCProfile);
            D0ActorPresentationBindings actorBindings = EnsureActorPresentation(
                d0SliceRoot,
                context,
                profile,
                threeCProfile);
            D0ShotCameraFeedbackController shotCameraFeedback =
                EnsureShotCameraFeedback(context, threeCProfile);
            D0HitTipPresenter hitTipPresenter = EnsureHitTipPresentation(canvasRoot, profile);
            D0G3PresentationBindings g3Bindings = EnsureG3Presentation(
                d0SliceRoot,
                context,
                profile,
                audioBank,
                reticle);
            D0G4PresentationBindings g4Bindings = EnsureG4Presentation(
                d0SliceRoot,
                context,
                profile);
            BindContextReticle(context, reticle);
            BindContextD0Presentation(
                context,
                actorBindings,
                shotCameraFeedback,
                hitTipPresenter,
                g3Bindings,
                g4Bindings);
            DisableLegacyCrosshair(context);
            DisableLegacyHud(context);

            if (!reticle.TryValidate(out string reticleError))
            {
                throw new InvalidOperationException(
                    $"D0 aim reticle is invalid: {reticleError}");
            }

            D0ForestParallax parallax = stageRoot.GetComponent<D0ForestParallax>();
            if (parallax == null)
            {
                throw new InvalidOperationException(
                    "D0 forest stage is missing its D0ForestParallax component.");
            }

            if (!parallax.TryValidate(out string parallaxError))
            {
                throw new InvalidOperationException(
                    $"D0 forest stage is invalid: {parallaxError}");
            }
        }

        private static CombatAimReticle EnsureOverlayAndReticle(
            Transform canvasRoot,
            BattleSceneContext context,
            CombatPresentationProfile profile,
            D0ThreeCProfile threeCProfile)
        {
            RectTransform overlay = EnsureOverlayCanvas(canvasRoot, profile);
            MoveOrReplaceLegacyCanvasChild(canvasRoot, overlay, "D0Hud");
            MoveOrReplaceLegacyCanvasChild(canvasRoot, overlay, "D0AimReticle");

            RectTransform hud = EnsureRectTransformChild(overlay, "D0Hud");
            hud.anchorMin = Vector2.zero;
            hud.anchorMax = Vector2.one;
            hud.offsetMin = Vector2.zero;
            hud.offsetMax = Vector2.zero;

            RectTransform reticleRoot = EnsureRectTransformChild(overlay, "D0AimReticle");
            reticleRoot.pivot = new Vector2(0.5f, 0.5f);
            reticleRoot.sizeDelta = new Vector2(72f, 72f);
            CombatAimReticle reticle = GetOrAddComponent<CombatAimReticle>(reticleRoot.gameObject);
            ConfigureObjectReference(reticle, "sessionHost", context.SessionHost);
            if (!reticle.TrySetThreeCProfile(threeCProfile, out string reticleProfileError))
            {
                throw new InvalidOperationException(
                    $"D0 aim reticle could not apply the 3C profile: {reticleProfileError}");
            }

            EnsureReticleCanvas(reticleRoot, profile.Sorting.ReticleOrder);
            LayeredAimIndicatorGraphic indicatorGraphic =
                GetOrAddComponent<LayeredAimIndicatorGraphic>(reticleRoot.gameObject);
            PlayerAimIndicatorPresenter indicatorPresenter =
                GetOrAddComponent<PlayerAimIndicatorPresenter>(reticleRoot.gameObject);
            D0CombatScenarioDefinition scenario =
                context.ScenarioConfig == null
                    ? null
                    : context.ScenarioConfig.AuthoredScenario;
            D0WeaponDefinition weapon = scenario == null || scenario.Player == null
                ? null
                : scenario.Player.Weapon;
            if (weapon == null)
            {
                throw new InvalidOperationException(
                    "D0 aim indicator requires the active player's WeaponDefinition.");
            }

            SerializedObject serializedPresenter = new SerializedObject(indicatorPresenter);
            ConfigureSerializedReference(
                serializedPresenter,
                "sessionHost",
                context.SessionHost);
            ConfigureSerializedReference(
                serializedPresenter,
                "indicatorGraphic",
                indicatorGraphic);
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            indicatorPresenter.Configure(
                context.SessionHost,
                indicatorGraphic,
                weapon);
            indicatorGraphic.raycastTarget = false;
            EditorUtility.SetDirty(indicatorGraphic);
            EditorUtility.SetDirty(indicatorPresenter);

            DisableReticleLine(reticleRoot, "Horizontal");
            DisableReticleLine(reticleRoot, "Vertical");
            if (!indicatorGraphic.TryValidate(out string indicatorGraphicError))
            {
                throw new InvalidOperationException(
                    $"D0 layered aim indicator is invalid: {indicatorGraphicError}");
            }

            if (!indicatorPresenter.TryValidate(out string indicatorPresenterError))
            {
                throw new InvalidOperationException(
                    $"D0 aim indicator presenter is invalid: {indicatorPresenterError}");
            }

            return reticle;
        }

        private static RectTransform EnsureOverlayCanvas(
            Transform canvasRoot,
            CombatPresentationProfile profile)
        {
            RectTransform overlay = EnsureRectTransformChild(canvasRoot, "D0OverlayCanvas");
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;

            Canvas canvas = GetOrAddComponent<Canvas>(overlay.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = profile.Sorting.HudOrder;
            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(overlay.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return overlay;
        }

        private static void EnsureReticleCanvas(RectTransform reticleRoot, int sortingOrder)
        {
            Canvas canvas = GetOrAddComponent<Canvas>(reticleRoot.gameObject);
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }

        private static void DisableReticleLine(RectTransform parent, string name)
        {
            Transform line = parent.Find(name);
            if (line == null || line.parent != parent)
            {
                return;
            }

            line.gameObject.SetActive(false);
            EditorUtility.SetDirty(line.gameObject);
        }

        private static void EnsureForestStage(
            Transform stageRoot,
            CombatAimReticle reticle,
            CombatPresentationProfile profile,
            D0StageDefinition stageDefinition)
        {
            if (stageDefinition == null)
            {
                throw new InvalidOperationException(
                    "D0 forest stage requires a stage definition.");
            }

            if (!stageDefinition.TryValidate(out string stageError))
            {
                throw new InvalidOperationException(
                    $"D0 forest stage requires a valid stage definition: {stageError}");
            }

            D0ForestParallax parallax = GetOrAddComponent<D0ForestParallax>(stageRoot.gameObject);
            IReadOnlyList<D0StageForestLayerDefinition> definitions = stageDefinition.ForestLayers;
            D0ForestParallaxLayer[] layers = new D0ForestParallaxLayer[definitions.Count];
            for (int index = 0; index < definitions.Count; index++)
            {
                layers[index] = EnsureForestLayer(stageRoot, definitions[index], profile);
            }

            RemoveObsoleteForestLayers(stageRoot, definitions);
            parallax.Configure(reticle, layers);
            EditorUtility.SetDirty(parallax);
        }

        private static void RemoveObsoleteForestLayers(
            Transform stageRoot,
            IReadOnlyList<D0StageForestLayerDefinition> definitions)
        {
            HashSet<string> activeLayerNames = new HashSet<string>();
            for (int index = 0; index < definitions.Count; index++)
            {
                activeLayerNames.Add(definitions[index].LayerId);
            }

            List<GameObject> staleLayers = new List<GameObject>();
            for (int index = 0; index < stageRoot.childCount; index++)
            {
                Transform child = stageRoot.GetChild(index);
                if (child.GetComponent<D0ForestParallaxLayer>() != null
                    && !activeLayerNames.Contains(child.name))
                {
                    staleLayers.Add(child.gameObject);
                }
            }

            for (int index = 0; index < staleLayers.Count; index++)
            {
                UnityEngine.Object.DestroyImmediate(staleLayers[index]);
            }
        }

        private static D0ForestParallaxLayer EnsureForestLayer(
            Transform stageRoot,
            D0StageForestLayerDefinition definition,
            CombatPresentationProfile profile)
        {
            Transform existing = stageRoot.Find(definition.LayerId);
            GameObject layerObject;
            bool created = existing == null;
            if (created)
            {
                layerObject = new GameObject(definition.LayerId);
                layerObject.transform.SetParent(stageRoot, false);
            }
            else
            {
                if (existing.parent != stageRoot)
                {
                    throw new InvalidOperationException(
                        $"D0 forest layer '{definition.LayerId}' must be a direct D0Stage child.");
                }

                layerObject = existing.gameObject;
            }

            layerObject.SetActive(true);

            // G1 owns these visual-only objects. Clean a stale component only
            // inside this owned subtree so a prior type/file migration cannot
            // leave a dead SerializedReference in the stage array on reload.
            if (GameObjectUtility.RemoveMonoBehavioursWithMissingScript(layerObject) > 0)
            {
                EditorUtility.SetDirty(layerObject);
            }

            SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(layerObject);
            D0ForestParallaxLayer parallaxLayer = GetOrAddComponent<D0ForestParallaxLayer>(layerObject);
            Sprite sprite = definition.Sprite;

            // Apply the complete contract on every installation. This lets a
            // camera-composition correction update an already-authored stage
            // instead of requiring a scene YAML migration or a fresh project.
            renderer.sprite = sprite;
            renderer.sortingLayerName = profile.Sorting.SortingLayerName;
            renderer.sortingOrder = definition.SortingOrder;
            renderer.color = definition.Color;
            renderer.flipX = definition.FlipX;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            float spriteWidth = Mathf.Max(0.001f, sprite.bounds.size.x);
            float scale = definition.DesiredWorldWidth / spriteWidth;
            layerObject.transform.localScale = new Vector3(scale, scale, 1f);
            parallaxLayer.Configure(definition.BaseLocalPosition, definition.ViewportOffsetMultiplier);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(layerObject);

            return parallaxLayer;
        }

        private static D0StageDefinition ResolveStageDefinition(BattleSceneContext context)
        {
            BattleScenarioConfig scenarioConfig = context == null ? null : context.ScenarioConfig;
            if (scenarioConfig == null || !scenarioConfig.UsesAuthoredScenario)
            {
                throw new InvalidOperationException(
                    "D0 stage installation requires an authored D0 combat scenario.");
            }

            D0CombatScenarioDefinition scenario = scenarioConfig.AuthoredScenario;
            D0StageDefinition stageDefinition = scenario == null ? null : scenario.StageDefinition;
            if (stageDefinition == null)
            {
                throw new InvalidOperationException(
                    "D0 stage installation requires an authored stage definition.");
            }

            if (!stageDefinition.TryValidate(out string stageError))
            {
                throw new InvalidOperationException(
                    $"D0 stage installation requires a valid stage definition: {stageError}");
            }

            return stageDefinition;
        }

        private static void EnsureEncounterSpawning(
            BattleSceneContext context,
            D0StageDefinition stageDefinition)
        {
            if (context.ActorsRoot == null)
            {
                throw new InvalidOperationException(
                    "D0 encounter spawning requires BattleSceneContext.ActorsRoot.");
            }

            Transform spawnRoot = EnsureDirectChild(
                context.ActorsRoot,
                "EncounterSpawnPoints");
            spawnRoot.localPosition = Vector3.zero;
            spawnRoot.localRotation = Quaternion.identity;
            spawnRoot.localScale = Vector3.one;
            IReadOnlyList<D0StageSpawnPointDefinition> definitions =
                stageDefinition.SpawnPoints;
            D0SpawnPoint[] spawnPoints = new D0SpawnPoint[definitions.Count];
            HashSet<string> activeNames = new HashSet<string>();
            for (int index = 0; index < definitions.Count; index++)
            {
                D0StageSpawnPointDefinition definition = definitions[index];
                Transform pointTransform = EnsureDirectChild(
                    spawnRoot,
                    definition.SpawnPointId);
                pointTransform.localPosition = definition.LocalPosition;
                pointTransform.localRotation = Quaternion.Euler(
                    definition.LocalEulerAngles);
                pointTransform.localScale = Vector3.one;
                D0SpawnPoint point =
                    GetOrAddComponent<D0SpawnPoint>(pointTransform.gameObject);
                point.Configure(definition.SpawnPointId);
                spawnPoints[index] = point;
                activeNames.Add(definition.SpawnPointId);
                EditorUtility.SetDirty(point);
                EditorUtility.SetDirty(pointTransform);
            }

            for (int index = spawnRoot.childCount - 1; index >= 0; index--)
            {
                Transform child = spawnRoot.GetChild(index);
                if (child.GetComponent<D0SpawnPoint>() != null
                    && !activeNames.Contains(child.name))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            Transform worldTransform = context.EnemyEntityWorld == null
                ? EnsureDirectChild(context.ActorsRoot, "EnemyEntityWorld")
                : context.EnemyEntityWorld.transform;
            if (worldTransform.parent != context.ActorsRoot)
            {
                worldTransform.SetParent(context.ActorsRoot, false);
            }

            worldTransform.localPosition = Vector3.zero;
            worldTransform.localRotation = Quaternion.identity;
            worldTransform.localScale = Vector3.one;
            Transform entityRoot = EnsureDirectChild(
                worldTransform,
                "EnemyEntities");
            RemoveAllDirectChildren(entityRoot);
            entityRoot.localPosition = Vector3.zero;
            entityRoot.localRotation = Quaternion.identity;
            entityRoot.localScale = Vector3.one;
            D0EnemyEntityWorld entityWorld =
                GetOrAddComponent<D0EnemyEntityWorld>(worldTransform.gameObject);
            SerializedObject serializedWorld = new SerializedObject(entityWorld);
            ConfigureSerializedReference(
                serializedWorld,
                "sessionHost",
                context.SessionHost);
            ConfigureSerializedReference(
                serializedWorld,
                "hitboxRegistry",
                context.HitboxRegistry);
            ConfigureSerializedReference(
                serializedWorld,
                "entityRoot",
                entityRoot);
            serializedWorld.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedContext = new SerializedObject(context);
            ConfigureSerializedReference(
                serializedContext,
                "enemyEntityWorld",
                entityWorld);
            SerializedProperty serializedSpawnPoints =
                serializedContext.FindProperty("encounterSpawnPoints");
            if (serializedSpawnPoints == null || !serializedSpawnPoints.isArray)
            {
                throw new InvalidOperationException(
                    "BattleSceneContext no longer exposes encounterSpawnPoints.");
            }

            serializedSpawnPoints.arraySize = spawnPoints.Length;
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                serializedSpawnPoints.GetArrayElementAtIndex(index)
                    .objectReferenceValue = spawnPoints[index];
            }

            serializedContext.ApplyModifiedPropertiesWithoutUndo();
            RemoveLegacyActorStaticBindings(context.HitboxRegistry);
            EditorUtility.SetDirty(entityWorld);
            EditorUtility.SetDirty(context);
        }

        private static void RemoveLegacyActorStaticBindings(
            HitboxRegistry registry)
        {
            if (registry == null)
            {
                throw new InvalidOperationException(
                    "D0 encounter spawning requires a HitboxRegistry.");
            }

            SerializedObject serializedRegistry =
                new SerializedObject(registry);
            SerializedProperty bindings =
                serializedRegistry.FindProperty("staticBindings");
            if (bindings == null || !bindings.isArray)
            {
                throw new InvalidOperationException(
                    "HitboxRegistry no longer exposes staticBindings.");
            }

            for (int index = bindings.arraySize - 1; index >= 0; index--)
            {
                SerializedProperty binding =
                    bindings.GetArrayElementAtIndex(index);
                SerializedProperty targetReference =
                    binding.FindPropertyRelative("targetReference");
                if (targetReference != null
                    && (targetReference.enumValueIndex
                            == (int)HitboxTargetReference.Player
                        || targetReference.enumValueIndex
                            == (int)HitboxTargetReference.Enemy))
                {
                    bindings.DeleteArrayElementAtIndex(index);
                }
            }

            serializedRegistry.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
        }

        private static D0ThreeCProfile ResolveThreeCProfile(BattleSceneContext context)
        {
            BattleScenarioConfig scenarioConfig = context == null ? null : context.ScenarioConfig;
            D0CombatScenarioDefinition scenario = scenarioConfig == null
                ? null
                : scenarioConfig.AuthoredScenario;
            D0ThreeCProfile threeCProfile = scenario == null ? null : scenario.ThreeCProfile;
            if (threeCProfile == null)
            {
                throw new InvalidOperationException(
                    "D0 stage installation requires an authored D0 3C profile.");
            }

            string error = string.Empty;
            if (!threeCProfile.TryValidate(out error))
            {
                throw new InvalidOperationException(
                    $"D0 stage installation requires a valid D0 3C profile: {error}");
            }

            return threeCProfile;
        }

        private static void ConfigureFixedFrontalCamera(
            BattleSceneContext context,
            D0ThreeCProfile threeCProfile)
        {
            Camera mainCamera = context.MainCamera;
            CombatLabPlayerController playerController = context.PlayerAnchor == null
                ? null
                : context.PlayerAnchor.GetComponent<CombatLabPlayerController>();
            if (mainCamera == null || playerController == null || playerController.CameraPivot == null)
            {
                throw new InvalidOperationException(
                    "CombatLab requires MainCamera and CombatLabPlayerController.CameraPivot for D0 composition.");
            }

            Transform pivot = playerController.CameraPivot;
            mainCamera.transform.SetParent(pivot, false);

            SerializedObject serializedController = new SerializedObject(playerController);
            serializedController.FindProperty("twoPointFiveDPresentationMode").boolValue = true;
            serializedController.FindProperty("planarMovementEnabled").boolValue = false;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            if (!playerController.TryApplyTwoPointFiveDCameraProfile(
                    threeCProfile,
                    mainCamera,
                    out string cameraProfileError))
            {
                throw new InvalidOperationException(
                    $"D0 camera profile could not be applied: {cameraProfileError}");
            }

            EditorUtility.SetDirty(playerController);
        }

        private static void BindContextReticle(
            BattleSceneContext context,
            CombatAimReticle reticle)
        {
            SerializedObject serializedContext = new SerializedObject(context);
            SerializedProperty property = serializedContext.FindProperty("combatAimReticle");
            if (property == null)
            {
                throw new InvalidOperationException(
                    "BattleSceneContext no longer exposes combatAimReticle.");
            }

            property.objectReferenceValue = reticle;
            serializedContext.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(context);
        }

        private static void BindContextD0Presentation(
            BattleSceneContext context,
            D0ActorPresentationBindings actorBindings,
            D0ShotCameraFeedbackController shotCameraFeedback,
            D0HitTipPresenter hitTipPresenter,
            D0G3PresentationBindings g3Bindings,
            D0G4PresentationBindings g4Bindings)
        {
            if (actorBindings.Player == null
                || shotCameraFeedback == null
                || hitTipPresenter == null
                || g3Bindings.ThreatTelegraph == null
                || g3Bindings.Weakpoint == null
                || g3Bindings.CombatVfxWorld == null
                || g3Bindings.Audio == null
                || g4Bindings.Hud == null
                || actorBindings.EnemyBehavior == null)
            {
                throw new ArgumentNullException(
                    actorBindings.Player == null
                        ? nameof(actorBindings)
                        : shotCameraFeedback == null
                            ? nameof(shotCameraFeedback)
                        : hitTipPresenter == null
                            ? nameof(hitTipPresenter)
                            : g3Bindings.ThreatTelegraph == null
                                ? "g3Bindings.ThreatTelegraph"
                                : g3Bindings.Weakpoint == null
                                    ? "g3Bindings.Weakpoint"
                                    : g3Bindings.CombatVfxWorld == null
                                        ? "g3Bindings.CombatVfxWorld"
                                        : g3Bindings.Audio == null
                                            ? "g3Bindings.Audio"
                                            : g4Bindings.Hud == null
                                                ? "g4Bindings.Hud"
                                                : "actorBindings.EnemyBehavior");
            }

            SerializedObject serializedContext = new SerializedObject(context);
            ConfigureContextReference(
                serializedContext,
                "d0EnemyBehaviorController",
                actorBindings.EnemyBehavior);
            ConfigureContextReference(
                serializedContext,
                "d0ShotCameraFeedbackController",
                shotCameraFeedback);
            ConfigureContextReference(
                serializedContext,
                "d0HitTipPresenter",
                hitTipPresenter);
            ConfigureContextReference(
                serializedContext,
                "d0ThreatTelegraphPresenter",
                g3Bindings.ThreatTelegraph);
            ConfigureContextReference(
                serializedContext,
                "d0WeakpointPresentationController",
                g3Bindings.Weakpoint);
            ConfigureContextReference(
                serializedContext,
                "combatVfxWorld",
                g3Bindings.CombatVfxWorld);
            ConfigureContextReference(
                serializedContext,
                "d0CombatAudioPresenter",
                g3Bindings.Audio);
            ConfigureContextReference(
                serializedContext,
                "d0CombatHud2DPresenter",
                g4Bindings.Hud);
            serializedContext.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(context);
        }

        /// <summary>
        /// Builds the G3-only read-model presentation roots. These objects are
        /// deliberately rooted under D0Slice2D and receive no collider,
        /// Rigidbody, hitbox or combat-state component. Runtime pools are
        /// prepared by their presenters after the BattleSession is bound.
        /// </summary>
        private static D0G3PresentationBindings EnsureG3Presentation(
            Transform d0SliceRoot,
            BattleSceneContext context,
            CombatPresentationProfile profile,
            CombatAudioBank audioBank,
            CombatAimReticle reticle)
        {
            Transform worldFxRoot = RequireDirectChild(d0SliceRoot, "D0WorldFx");
            RemoveDirectChildIfPresent(worldFxRoot, "D0ActorEffects");
            D0CombatVfxWorld combatVfxWorld =
                GetOrAddComponent<D0CombatVfxWorld>(worldFxRoot.gameObject);
            SerializedObject serializedVfxWorld =
                new SerializedObject(combatVfxWorld);
            ConfigureSerializedReference(
                serializedVfxWorld,
                "poolRoot",
                worldFxRoot);
            ConfigureSerializedBoolean(
                serializedVfxWorld,
                "prepareOnEnable",
                false);
            ConfigureSerializedBoolean(
                serializedVfxWorld,
                "automaticallyAdvance",
                true);
            serializedVfxWorld.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combatVfxWorld);
            if (!combatVfxWorld.TryValidate(out string combatVfxError))
            {
                throw new InvalidOperationException(
                    $"D0 combat VFX world is invalid: {combatVfxError}");
            }

            Transform audioRoot = RequireDirectChild(d0SliceRoot, "D0Audio");
            Material feedbackMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                FeedbackMaterialPath);
            if (feedbackMaterial == null)
            {
                throw new InvalidOperationException(
                    "D0 G3 presentation requires the existing feedback material.");
            }

            Transform weakpointRoot = EnsureDirectChild(worldFxRoot, "D0WeakpointFx");
            D0WeakpointPresentationController weakpoint =
                GetOrAddComponent<D0WeakpointPresentationController>(weakpointRoot.gameObject);
            weakpoint.Configure(
                profile,
                feedbackMaterial,
                context.MainCamera,
                null,
                reticle);
            EditorUtility.SetDirty(weakpoint);
            if (!weakpoint.TryValidateAuthoring(out string weakpointError))
            {
                throw new InvalidOperationException(
                    $"D0 weakpoint presentation is invalid: {weakpointError}");
            }

            Transform telegraphRoot = EnsureDirectChild(worldFxRoot, "D0ThreatTelegraphs");
            ThreatTelegraph2DPresenter threatTelegraph =
                GetOrAddComponent<ThreatTelegraph2DPresenter>(telegraphRoot.gameObject);
            threatTelegraph.Configure(
                profile,
                feedbackMaterial,
                context.MainCamera,
                null,
                null,
                null,
                telegraphRoot,
                null,
                weakpoint,
                profile.PoolCapacities.ThreatTelegraphCapacity);
            EditorUtility.SetDirty(threatTelegraph);
            if (!threatTelegraph.TryValidateAuthoring(out string threatError))
            {
                throw new InvalidOperationException(
                    $"D0 threat telegraph presentation is invalid: {threatError}");
            }

            CombatAudioPresenter audio = EnsureCombatAudioPresentation(
                audioRoot,
                context,
                profile,
                audioBank);
            threatTelegraph.SetAudioPresenter(audio);

            EnsurePresentationOnly(weakpointRoot);
            EnsurePresentationOnly(telegraphRoot);
            EnsurePresentationOnly(audio.transform);
            return new D0G3PresentationBindings(
                threatTelegraph,
                weakpoint,
                combatVfxWorld,
                audio);
        }

        /// <summary>
        /// Builds the D0-owned formal HUD, terminal result surface and the
        /// optional developer overlay. These views are all screen-space UI and
        /// only receive copied snapshots / committed trace events at runtime.
        /// They contain no hitboxes, colliders or combat-writing components.
        /// </summary>
        private static D0G4PresentationBindings EnsureG4Presentation(
            Transform d0SliceRoot,
            BattleSceneContext context,
            CombatPresentationProfile profile)
        {
            if (context == null || profile == null)
            {
                throw new ArgumentNullException(context == null ? nameof(context) : nameof(profile));
            }

            Transform canvasRoot = RequireDirectChild(d0SliceRoot, "D0Canvas");
            Transform screenFxRoot = RequireDirectChild(d0SliceRoot, "D0ScreenFx");
            Transform developmentRoot = RequireDirectChild(d0SliceRoot, "D0DevelopmentOverlay");
            D0TerminalScreenFxPresenter screenFx = EnsureTerminalScreenFx(
                screenFxRoot,
                profile);
            CombatHud2DPresenter hud = EnsureCombatHud(
                canvasRoot,
                developmentRoot,
                context,
                profile,
                screenFx);

            if (context.DiagnosticsPresenter != null)
            {
                context.DiagnosticsPresenter.ShowOnGui = false;
                EditorUtility.SetDirty(context.DiagnosticsPresenter);
            }

            EnsurePresentationOnly(canvasRoot);
            EnsurePresentationOnly(screenFxRoot);
            EnsurePresentationOnly(developmentRoot);
            return new D0G4PresentationBindings(hud, screenFx);
        }

        private static D0TerminalScreenFxPresenter EnsureTerminalScreenFx(
            Transform screenFxRoot,
            CombatPresentationProfile profile)
        {
            RectTransform canvasRoot = EnsureRectTransformChild(screenFxRoot, "D0ScreenFxCanvas");
            StretchToParent(canvasRoot);
            EnsureScreenSpaceCanvas(canvasRoot, profile.Sorting.ScreenEffectsOrder);
            CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(canvasRoot.gameObject);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            RectTransform dimming = EnsureRectTransformChild(canvasRoot, "D0TerminalDimming");
            StretchToParent(dimming);
            Image dimmingImage = GetOrAddComponent<Image>(dimming.gameObject);
            dimmingImage.raycastTarget = false;
            dimmingImage.color = Color.clear;

            D0TerminalScreenFxPresenter presenter =
                GetOrAddComponent<D0TerminalScreenFxPresenter>(canvasRoot.gameObject);
            SerializedObject serialized = new SerializedObject(presenter);
            ConfigureSerializedReference(serialized, "canvasGroup", canvasGroup);
            ConfigureSerializedReference(serialized, "dimmingImage", dimmingImage);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
            presenter.Clear();
            if (!presenter.TryValidate(out string error))
            {
                throw new InvalidOperationException($"D0 terminal screen FX is invalid: {error}");
            }

            return presenter;
        }

        private static CombatHud2DPresenter EnsureCombatHud(
            Transform canvasRoot,
            Transform developmentRoot,
            BattleSceneContext context,
            CombatPresentationProfile profile,
            D0TerminalScreenFxPresenter screenFx)
        {
            Transform overlayTransform = canvasRoot.Find("D0OverlayCanvas");
            if (!(overlayTransform is RectTransform overlay)
                || overlay.parent != canvasRoot)
            {
                throw new InvalidOperationException(
                    "D0 formal HUD requires D0OverlayCanvas as a direct RectTransform child.");
            }

            RectTransform hudRoot = EnsureRectTransformChild(overlay, "D0Hud");
            StretchToParent(hudRoot);
            RectTransform formalHudRoot = EnsureRectTransformChild(hudRoot, "D0FormalHud");
            StretchToParent(formalHudRoot);
            // Jumping hit tips must remain above stable HUD panels and never be
            // occluded by their opaque backgrounds.
            formalHudRoot.SetAsFirstSibling();
            Font font = GetBuiltinHudFont();

            Image enemyPanel = EnsureHudPanel(
                formalHudRoot,
                "D0EnemyReadout",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -34f),
                new Vector2(720f, 142f),
                new Color(0.025f, 0.045f, 0.09f, 0.84f));
            Text enemyNameText = EnsureHudText(
                enemyPanel.transform,
                "EnemyNameText",
                font,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -22f),
                new Vector2(600f, 30f),
                22,
                new Color(0.94f, 0.98f, 1f, 1f),
                "BURSTBUG",
                new Vector2(0.5f, 1f));
            Image enemyLifeFill = EnsureHudBar(
                enemyPanel.transform,
                "EnemyLifeBar",
                new Vector2(0f, -55f),
                new Vector2(610f, 13f),
                new Color(0.96f, 0.24f, 0.25f, 1f));
            Text enemyLifeText = EnsureHudText(
                enemyPanel.transform,
                "EnemyLifeText",
                font,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -73f),
                new Vector2(610f, 20f),
                14,
                new Color(1f, 0.72f, 0.74f, 1f),
                "HP --",
                new Vector2(0.5f, 1f));
            Image enemyBreakFill = EnsureHudBar(
                enemyPanel.transform,
                "EnemyBreakBar",
                new Vector2(0f, -98f),
                new Vector2(610f, 9f),
                new Color(1f, 0.78f, 0.23f, 1f));
            Text enemyBreakText = EnsureHudText(
                enemyPanel.transform,
                "EnemyBreakText",
                font,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -114f),
                new Vector2(610f, 18f),
                13,
                new Color(1f, 0.86f, 0.42f, 1f),
                "BREAK --",
                new Vector2(0.5f, 1f));
            Image threatIndicator = EnsureHudPanel(
                enemyPanel.transform,
                "ThreatIndicator",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-168f, 13f),
                new Vector2(13f, 13f),
                new Color(0.42f, 0.72f, 0.82f, 0.75f));
            Text threatText = EnsureHudText(
                enemyPanel.transform,
                "ThreatText",
                font,
                TextAnchor.MiddleCenter,
                new Vector2(18f, 13f),
                new Vector2(410f, 24f),
                14,
                new Color(0.66f, 0.88f, 0.94f, 0.92f),
                "THREAT | CLEAR",
                new Vector2(0.5f, 0f));

            Image playerPanel = EnsureHudPanel(
                formalHudRoot,
                "D0PlayerReadout",
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                new Vector2(40f, 44f),
                new Vector2(450f, 178f),
                new Color(0.02f, 0.06f, 0.08f, 0.86f));
            Text playerNameText = EnsureHudText(
                playerPanel.transform,
                "PlayerNameText",
                font,
                TextAnchor.MiddleLeft,
                new Vector2(18f, -22f),
                new Vector2(380f, 27f),
                20,
                new Color(0.68f, 1f, 0.85f, 1f),
                "FEI_30048",
                new Vector2(0f, 1f));
            Image playerLifeFill = EnsureHudBar(
                playerPanel.transform,
                "PlayerLifeBar",
                new Vector2(18f, -54f),
                new Vector2(395f, 14f),
                new Color(0.16f, 0.96f, 0.47f, 1f),
                new Vector2(0f, 1f));
            Text playerLifeText = EnsureHudText(
                playerPanel.transform,
                "PlayerLifeText",
                font,
                TextAnchor.MiddleLeft,
                new Vector2(18f, -73f),
                new Vector2(395f, 19f),
                13,
                new Color(0.72f, 1f, 0.84f, 1f),
                "LIFE --",
                new Vector2(0f, 1f));
            Image playerBarrierFill = EnsureHudBar(
                playerPanel.transform,
                "PlayerBarrierBar",
                new Vector2(18f, -101f),
                new Vector2(395f, 11f),
                new Color(0.33f, 0.72f, 1f, 1f),
                new Vector2(0f, 1f));
            Text playerBarrierText = EnsureHudText(
                playerPanel.transform,
                "PlayerBarrierText",
                font,
                TextAnchor.MiddleLeft,
                new Vector2(18f, -118f),
                new Vector2(395f, 18f),
                13,
                new Color(0.56f, 0.84f, 1f, 1f),
                "BARRIER --",
                new Vector2(0f, 1f));
            Image ammoFill = EnsureHudBar(
                playerPanel.transform,
                "AmmoBar",
                new Vector2(18f, -147f),
                new Vector2(395f, 8f),
                new Color(1f, 0.78f, 0.32f, 1f),
                new Vector2(0f, 1f));
            Text ammoText = EnsureHudText(
                playerPanel.transform,
                "AmmoText",
                font,
                TextAnchor.MiddleLeft,
                new Vector2(18f, -161f),
                new Vector2(395f, 16f),
                12,
                new Color(1f, 0.86f, 0.52f, 1f),
                "AMMO --",
                new Vector2(0f, 1f));

            Image actionPanel = EnsureHudPanel(
                formalHudRoot,
                "D0ActionReadout",
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-40f, 44f),
                new Vector2(340f, 108f),
                new Color(0.035f, 0.05f, 0.1f, 0.86f));
            Text actionCaption = EnsureHudText(
                actionPanel.transform,
                "ActionCaption",
                font,
                TextAnchor.MiddleRight,
                new Vector2(-18f, -23f),
                new Vector2(280f, 20f),
                12,
                new Color(0.68f, 0.78f, 0.98f, 0.92f),
                "FOCUS",
                new Vector2(1f, 1f));
            Image actionFill = EnsureHudBar(
                actionPanel.transform,
                "ActionBar",
                new Vector2(-18f, -49f),
                new Vector2(280f, 12f),
                new Color(0.64f, 0.9f, 1f, 1f),
                new Vector2(1f, 1f),
                true);
            Text actionText = EnsureHudText(
                actionPanel.transform,
                "ActionText",
                font,
                TextAnchor.MiddleRight,
                new Vector2(-18f, -75f),
                new Vector2(280f, 23f),
                15,
                new Color(0.68f, 1f, 0.85f, 1f),
                "READY",
                new Vector2(1f, 1f));

            RectTransform terminalPanel = EnsureRectTransformChild(formalHudRoot, "D0ResultPanel");
            ConfigureUiRect(
                terminalPanel,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -16f),
                new Vector2(650f, 220f));
            Image terminalBackground = GetOrAddComponent<Image>(terminalPanel.gameObject);
            terminalBackground.color = new Color(0.025f, 0.04f, 0.085f, 0.92f);
            terminalBackground.raycastTarget = false;
            CanvasGroup terminalCanvasGroup = GetOrAddComponent<CanvasGroup>(terminalPanel.gameObject);
            Text terminalTitleText = EnsureHudText(
                terminalPanel,
                "TerminalTitleText",
                font,
                TextAnchor.MiddleCenter,
                new Vector2(0f, -76f),
                new Vector2(570f, 72f),
                48,
                new Color(1f, 0.86f, 0.32f, 1f),
                string.Empty,
                new Vector2(0.5f, 1f));
            Text terminalPromptText = EnsureHudText(
                terminalPanel,
                "TerminalPromptText",
                font,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 42f),
                new Vector2(570f, 30f),
                16,
                new Color(0.88f, 0.94f, 1f, 0.96f),
                string.Empty,
                new Vector2(0.5f, 0f));

            GameObject developmentOverlay;
            Text developmentText;
            EnsureDevelopmentOverlay(
                developmentRoot,
                profile,
                font,
                out developmentOverlay,
                out developmentText);

            CombatHud2DPresenter presenter =
                GetOrAddComponent<CombatHud2DPresenter>(formalHudRoot.gameObject);
            SerializedObject serialized = new SerializedObject(presenter);
            ConfigureSerializedReference(serialized, "presentationProfile", profile);
            ConfigureSerializedReference(serialized, "enemyLifeFill", enemyLifeFill);
            ConfigureSerializedReference(serialized, "enemyBreakFill", enemyBreakFill);
            ConfigureSerializedReference(serialized, "threatIndicator", threatIndicator);
            ConfigureSerializedReference(serialized, "enemyNameText", enemyNameText);
            ConfigureSerializedReference(serialized, "enemyLifeText", enemyLifeText);
            ConfigureSerializedReference(serialized, "enemyBreakText", enemyBreakText);
            ConfigureSerializedReference(serialized, "threatText", threatText);
            ConfigureSerializedReference(serialized, "playerLifeFill", playerLifeFill);
            ConfigureSerializedReference(serialized, "playerBarrierFill", playerBarrierFill);
            ConfigureSerializedReference(serialized, "ammoFill", ammoFill);
            ConfigureSerializedReference(serialized, "actionFill", actionFill);
            ConfigureSerializedReference(serialized, "playerNameText", playerNameText);
            ConfigureSerializedReference(serialized, "playerLifeText", playerLifeText);
            ConfigureSerializedReference(serialized, "playerBarrierText", playerBarrierText);
            ConfigureSerializedReference(serialized, "ammoText", ammoText);
            ConfigureSerializedReference(serialized, "actionText", actionText);
            ConfigureSerializedReference(serialized, "terminalPanel", terminalPanel.gameObject);
            ConfigureSerializedReference(serialized, "terminalCanvasGroup", terminalCanvasGroup);
            ConfigureSerializedReference(serialized, "terminalTitleText", terminalTitleText);
            ConfigureSerializedReference(serialized, "terminalPromptText", terminalPromptText);
            ConfigureSerializedReference(serialized, "terminalScreenFx", screenFx);
            ConfigureSerializedReference(serialized, "developmentOverlay", developmentOverlay);
            ConfigureSerializedReference(serialized, "developmentText", developmentText);
            ConfigureSerializedReference(serialized, "diagnosticsPresenter", context.DiagnosticsPresenter);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
            presenter.Clear();
            if (!presenter.TryValidate(out string error))
            {
                throw new InvalidOperationException($"D0 combat HUD is invalid: {error}");
            }

            return presenter;
        }

        private static void EnsureDevelopmentOverlay(
            Transform developmentRoot,
            CombatPresentationProfile profile,
            Font font,
            out GameObject overlayObject,
            out Text developmentText)
        {
            RectTransform canvasRoot = EnsureRectTransformChild(
                developmentRoot,
                "D0DevelopmentOverlayCanvas");
            StretchToParent(canvasRoot);
            EnsureScreenSpaceCanvas(canvasRoot, profile.Sorting.DevelopmentOverlayOrder);
            Image panel = EnsureHudPanel(
                canvasRoot,
                "D0DevelopmentPanel",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(720f, 178f),
                new Color(0.01f, 0.025f, 0.065f, 0.94f));
            developmentText = EnsureHudText(
                panel.transform,
                "D0DevelopmentText",
                font,
                TextAnchor.UpperLeft,
                new Vector2(16f, -14f),
                new Vector2(686f, 148f),
                13,
                new Color(0.76f, 0.9f, 1f, 1f),
                "DEV OVERLAY",
                Vector2.zero);
            developmentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            developmentText.verticalOverflow = VerticalWrapMode.Overflow;
            overlayObject = canvasRoot.gameObject;
            overlayObject.SetActive(false);
        }

        private static CombatAudioPresenter EnsureCombatAudioPresentation(
            Transform audioRoot,
            BattleSceneContext context,
            CombatPresentationProfile profile,
            CombatAudioBank audioBank)
        {
            CombatAudioPresenter presenter = GetOrAddComponent<CombatAudioPresenter>(
                audioRoot.gameObject);
            SerializedObject serialized = new SerializedObject(presenter);
            ConfigureSerializedReference(serialized, "sessionHost", context.SessionHost);
            ConfigureSerializedReference(serialized, "audioBank", audioBank);
            ConfigureSerializedReference(serialized, "presentationProfile", profile);
            ConfigureSerializedReference(serialized, "audioSourceRoot", audioRoot);
            ConfigureSerializedInteger(
                serialized,
                "sourcePoolCapacity",
                profile.PoolCapacities.AudioSourceCapacity);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
            if (!presenter.TryValidate(out string error))
            {
                throw new InvalidOperationException(
                    $"D0 combat audio presentation is invalid: {error}");
            }

            return presenter;
        }

        private static void ConfigureSerializedReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized object no longer exposes '{propertyName}'.");
            }

            property.objectReferenceValue = value;
        }

        private static void ConfigureOptionalSerializedReference(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void ConfigureSerializedInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Integer)
            {
                throw new InvalidOperationException(
                    $"Serialized object no longer exposes integer '{propertyName}'.");
            }

            property.intValue = value;
        }

        private static void ConfigureSerializedBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
            {
                throw new InvalidOperationException(
                    $"Serialized object no longer exposes boolean '{propertyName}'.");
            }

            property.boolValue = value;
        }

        private static void ConfigureContextReference(
            SerializedObject serializedContext,
            string propertyName,
            UnityEngine.Object value)
        {
            ConfigureOptionalSerializedReference(
                serializedContext,
                propertyName,
                value);
        }

        private static void DisableLegacyCrosshair(BattleSceneContext context)
        {
            if (context.PresentationCanvas == null)
            {
                return;
            }

            Transform legacyCrosshair = context.PresentationCanvas.transform.Find("Crosshair");
            if (legacyCrosshair != null)
            {
                legacyCrosshair.gameObject.SetActive(false);
            }
        }

        private static void DisableLegacyHud(BattleSceneContext context)
        {
            if (context == null || context.BattleHudPresenter == null)
            {
                return;
            }

            // The compatibility presenter remains bound so non-D0 tests and
            // legacy scenes keep their contract. The installed D0 slice owns
            // the visible player HUD beneath D0Canvas instead.
            context.BattleHudPresenter.gameObject.SetActive(false);
            EditorUtility.SetDirty(context.BattleHudPresenter.gameObject);
        }

        private static void EnsureScreenSpaceCanvas(RectTransform root, int sortingOrder)
        {
            Canvas canvas = GetOrAddComponent<Canvas>(root.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(root.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(scaler);
        }

        private static Image EnsureHudPanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color color)
        {
            RectTransform rect = EnsureRectTransformChild(parent, name);
            ConfigureUiRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, size);
            Image image = GetOrAddComponent<Image>(rect.gameObject);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text EnsureHudText(
            Transform parent,
            string name,
            Font font,
            TextAnchor alignment,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            Color color,
            string initialText,
            Vector2 anchor)
        {
            RectTransform rect = EnsureRectTransformChild(parent, name);
            ConfigureUiRect(rect, anchor, anchor, anchor, anchoredPosition, size);
            Text text = GetOrAddComponent<Text>(rect.gameObject);
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = initialText;
            return text;
        }

        private static Image EnsureHudBar(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Color fillColor)
        {
            return EnsureHudBar(
                parent,
                name,
                anchoredPosition,
                size,
                fillColor,
                new Vector2(0.5f, 1f),
                false);
        }

        private static Image EnsureHudBar(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Color fillColor,
            Vector2 anchor,
            bool reverse = false)
        {
            Image background = EnsureHudPanel(
                parent,
                name,
                anchor,
                anchor,
                anchor,
                anchoredPosition,
                size,
                new Color(0.01f, 0.02f, 0.05f, 0.90f));
            background.raycastTarget = false;
            RectTransform fillRect = EnsureRectTransformChild(background.transform, "Fill");
            StretchToParent(fillRect);
            Image fill = GetOrAddComponent<Image>(fillRect.gameObject);
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)(reverse
                ? Image.OriginHorizontal.Right
                : Image.OriginHorizontal.Left);
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            return fill;
        }

        private static void ConfigureUiRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font GetBuiltinHudFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null
                ? font
                : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static D0ActorPresentationBindings EnsureActorPresentation(
            Transform d0SliceRoot,
            BattleSceneContext context,
            CombatPresentationProfile profile,
            D0ThreeCProfile threeCProfile)
        {
            D0CombatScenarioDefinition scenario =
                context == null || context.ScenarioConfig == null
                    ? null
                    : context.ScenarioConfig.AuthoredScenario;
            D0CharacterDefinition playerDefinition =
                scenario == null ? null : scenario.Player;
            D0EnemyDefinition enemyDefinition =
                scenario == null || scenario.Encounter == null
                    ? null
                    : scenario.Encounter.Enemy;
            if (playerDefinition == null
                || playerDefinition.EntityPrefab == null
                || enemyDefinition == null
                || enemyDefinition.EntityPrefab == null)
            {
                throw new InvalidOperationException(
                    "D0 actor installation requires player and enemy Entity Prefabs selected by the authored scenario.");
            }

            if (!playerDefinition.EntityPrefab.TryValidate(
                    out string playerEntityPrefabError))
            {
                throw new InvalidOperationException(
                    $"D0 player Entity Prefab is invalid: {playerEntityPrefabError}");
            }

            if (!enemyDefinition.EntityPrefab.TryValidate(
                    out string enemyEntityPrefabError))
            {
                throw new InvalidOperationException(
                    $"D0 enemy Entity Prefab is invalid: {enemyEntityPrefabError}");
            }

            if (!context.TryGetEncounterSpawnPoint(
                    scenario.PlayerSpawnPointId,
                    out D0SpawnPoint playerSpawnPoint))
            {
                throw new InvalidOperationException(
                    "D0 actor installation requires a bound player spawn point.");
            }

            D0PlayerEntityView playerEntity = EnsurePlayerEntityInstance(
                context,
                playerDefinition.EntityPrefab,
                playerSpawnPoint.transform);
            ConfigureActorRenderers(
                playerEntity.gameObject,
                profile.Sorting.ActorOrder + 1);
            Actor2DPresenter playerPresenter = ConfigurePlayerActorPresenter(
                playerEntity,
                profile,
                playerDefinition.ActorPresentation);
            ConfigurePlayerBarrierPresentation(
                playerEntity,
                profile.Sorting.ActorOrder + 3,
                threeCProfile);
            D0EnemyBehaviorController enemyBehavior = EnsureEnemyBehavior(
                context,
                enemyDefinition.EntityPrefab);
            ConfigurePlayerShotPresentation(
                context.PlayerWeaponPresentationController,
                profile,
                playerEntity,
                playerDefinition.Weapon);
            RemoveLegacyActorScaffolding(d0SliceRoot);

            return new D0ActorPresentationBindings(
                playerPresenter,
                enemyBehavior);
        }

        private static D0PlayerEntityView EnsurePlayerEntityInstance(
            BattleSceneContext context,
            D0PlayerEntityView sourcePrefab,
            Transform playerSpawnPoint)
        {
            if (context == null
                || context.ActorsRoot == null
                || sourcePrefab == null
                || playerSpawnPoint == null)
            {
                throw new ArgumentNullException(
                    context == null
                        ? nameof(context)
                        : context.ActorsRoot == null
                            ? "context.ActorsRoot"
                            : sourcePrefab == null
                                ? nameof(sourcePrefab)
                                : nameof(playerSpawnPoint));
            }

            SerializedObject serializedContext = new SerializedObject(context);
            D0PlayerEntityView playerEntity = context.PlayerEntity;
            Transform mainCameraTransform = context.MainCamera == null
                ? null
                : context.MainCamera.transform;
            if (mainCameraTransform != null)
            {
                mainCameraTransform.SetParent(context.transform, true);
            }

            if (playerEntity != null
                && !IsPrefabInstanceOf(playerEntity.gameObject, sourcePrefab.gameObject))
            {
                UnityEngine.Object.DestroyImmediate(playerEntity.gameObject);
                playerEntity = null;
            }

            D0PlayerEntityView[] sceneEntities =
                context.ActorsRoot.GetComponentsInChildren<D0PlayerEntityView>(true);
            for (int index = 0; index < sceneEntities.Length; index++)
            {
                D0PlayerEntityView candidate = sceneEntities[index];
                if (candidate == null || candidate == playerEntity)
                {
                    continue;
                }

                if (playerEntity == null
                    && IsPrefabInstanceOf(candidate.gameObject, sourcePrefab.gameObject))
                {
                    playerEntity = candidate;
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(candidate.gameObject);
            }

            if (playerEntity == null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    sourcePrefab.gameObject,
                    context.ActorsRoot) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        $"Unable to instantiate player Entity Prefab '{sourcePrefab.name}'.");
                }

                playerEntity = instance.GetComponent<D0PlayerEntityView>();
                if (playerEntity == null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    throw new InvalidOperationException(
                        $"Player Entity Prefab '{sourcePrefab.name}' has no D0PlayerEntityView.");
                }
            }

            if (playerEntity.transform.parent != context.ActorsRoot)
            {
                playerEntity.transform.SetParent(context.ActorsRoot, true);
            }

            if (PrefabUtility.IsPartOfPrefabInstance(playerEntity.gameObject))
            {
                PrefabUtility.RevertPrefabInstance(
                    playerEntity.gameObject,
                    InteractionMode.AutomatedAction);
            }

            playerEntity.name = sourcePrefab.name;
            playerEntity.transform.SetPositionAndRotation(
                playerSpawnPoint.position,
                playerSpawnPoint.rotation);
            playerEntity.gameObject.SetActive(true);
            if (!playerEntity.TryValidate(out string entityError))
            {
                throw new InvalidOperationException(
                    $"Installed player Entity Prefab is invalid: {entityError}");
            }

            if (mainCameraTransform != null && playerEntity.CameraPivot != null)
            {
                mainCameraTransform.SetParent(
                    playerEntity.CameraPivot,
                    false);
            }

            ConfigureSerializedReference(
                serializedContext,
                "playerEntity",
                playerEntity);
            serializedContext.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(context);
            EditorUtility.SetDirty(playerEntity.transform);

            return playerEntity;
        }

        private static bool IsPrefabInstanceOf(
            GameObject instance,
            GameObject sourcePrefab)
        {
            return instance != null
                && sourcePrefab != null
                && PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance)
                    == sourcePrefab;
        }

        private static Actor2DPresenter ConfigurePlayerActorPresenter(
            D0PlayerEntityView playerEntity,
            CombatPresentationProfile profile,
            D0ActorPresentationDefinition presentationDefinition)
        {
            Actor2DPresenter presenter =
                playerEntity == null ? null : playerEntity.ActorPresenter;
            if (presenter == null)
            {
                throw new InvalidOperationException(
                    "Player Entity Prefab requires its authored Actor2DPresenter.");
            }

            SerializedObject serializedPresenter =
                new SerializedObject(presenter);
            ConfigureSerializedReference(
                serializedPresenter,
                "presentationProfile",
                profile);
            ConfigureSerializedBoolean(
                serializedPresenter,
                "playerActor",
                true);
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);

            if (!presenter.TryValidateWithPresentation(
                    presentationDefinition,
                    out string presenterError))
            {
                throw new InvalidOperationException(
                    $"Player Entity Prefab presenter is invalid: {presenterError}");
            }

            return presenter;
        }

        private static void ConfigurePlayerBarrierPresentation(
            D0PlayerEntityView playerEntity,
            int sortingOrder,
            D0ThreeCProfile threeCProfile)
        {
            D0PlayerBarrierPresentationController barrier =
                playerEntity == null ? null : playerEntity.Barrier;
            if (barrier == null)
            {
                throw new InvalidOperationException(
                    "Player Entity Prefab requires its authored barrier presentation.");
            }

            LineRenderer lineRenderer = barrier.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.sortingOrder = sortingOrder;
                EditorUtility.SetDirty(lineRenderer);
            }

            if (!barrier.TryValidate(out string barrierError))
            {
                throw new InvalidOperationException(
                    $"D0 player barrier presentation is invalid: {barrierError}");
            }

            // The Entity Prefab remains the authored source. The active
            // Scenario applies 3C response values at runtime.
            EditorUtility.SetDirty(barrier);
        }

        private static void RemoveLegacyActorScaffolding(
            Transform d0SliceRoot)
        {
            RemoveDirectChildIfPresent(d0SliceRoot, "D0Actors");
        }

        private static D0HitTipPresenter EnsureHitTipPresentation(
            Transform canvasRoot,
            CombatPresentationProfile profile)
        {
            Transform overlay = canvasRoot.Find("D0OverlayCanvas");
            if (!(overlay is RectTransform overlayRect) || overlay.parent != canvasRoot)
            {
                throw new InvalidOperationException(
                    "D0 overlay canvas must be a direct RectTransform child before hit-tip installation.");
            }

            RectTransform hud = EnsureRectTransformChild(overlayRect, "D0Hud");
            RectTransform hitTipRoot = EnsureRectTransformChild(hud, "D0HitTips");
            hitTipRoot.anchorMin = Vector2.zero;
            hitTipRoot.anchorMax = Vector2.one;
            hitTipRoot.offsetMin = Vector2.zero;
            hitTipRoot.offsetMax = Vector2.zero;
            hitTipRoot.pivot = new Vector2(0.5f, 0.5f);

            Sprite normalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HitTipNormalSpritePath);
            Sprite criticalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HitTipCriticalSpritePath);
            if (normalSprite == null || criticalSprite == null)
            {
                throw new InvalidOperationException(
                    "D0 Hit_tip sprites are missing; expected the approved normal/critical source assets.");
            }

            D0HitTipPresenter presenter = GetOrAddComponent<D0HitTipPresenter>(hitTipRoot.gameObject);
            SerializedObject serialized = new SerializedObject(presenter);
            SerializedProperty poolRoot = serialized.FindProperty("poolRoot");
            SerializedProperty normalBackground = serialized.FindProperty("normalBackgroundSprite");
            SerializedProperty criticalBackground = serialized.FindProperty("criticalBackgroundSprite");
            SerializedProperty capacity = serialized.FindProperty("prewarmCapacity");
            if (poolRoot == null || normalBackground == null || criticalBackground == null || capacity == null)
            {
                throw new InvalidOperationException(
                    "D0HitTipPresenter no longer exposes its required serialized bindings.");
            }

            poolRoot.objectReferenceValue = hitTipRoot;
            normalBackground.objectReferenceValue = normalSprite;
            criticalBackground.objectReferenceValue = criticalSprite;
            capacity.intValue = profile.PoolCapacities.HitTipCapacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);

            // Do not call TryPrepare here. It creates pool objects and would
            // serialize editor-only prewarm children into CombatLab. Awake / the
            // coordinator prepares the fixed pool only in a running session.
            if (!presenter.TryValidate(out string error))
            {
                throw new InvalidOperationException($"D0 Hit_tip presenter is invalid: {error}");
            }

            return presenter;
        }

        private static void ConfigureActorRenderers(GameObject actor, int sortingOrder)
        {
            Renderer[] renderers = actor.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                renderer.sortingOrder = sortingOrder;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static D0EnemyBehaviorController EnsureEnemyBehavior(
            BattleSceneContext context,
            D0EnemyEntityView initialEntityPrefab)
        {
            if (context == null || context.EnemyEntityWorld == null
                || initialEntityPrefab == null)
            {
                throw new InvalidOperationException(
                    "D0 enemy behavior requires an empty EnemyEntityWorld and an initial enemy Entity Prefab.");
            }

            if (!initialEntityPrefab.TryValidate(out string entityError))
            {
                throw new InvalidOperationException(
                    $"Initial enemy Entity Prefab is invalid: {entityError}");
            }

            GameObject behaviorOwner = context.EnemyEntityWorld.gameObject;
            D0EnemyBehaviorController previousController =
                context.D0EnemyBehaviorController;
            if (previousController != null
                && previousController.gameObject != behaviorOwner)
            {
                UnityEngine.Object.DestroyImmediate(previousController);
            }

            D0EnemyBehaviorController controller =
                GetOrAddComponent<D0EnemyBehaviorController>(behaviorOwner);
            controller.Configure(
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static D0ShotCameraFeedbackController EnsureShotCameraFeedback(
            BattleSceneContext context,
            D0ThreeCProfile threeCProfile)
        {
            if (context == null || context.SessionHost == null || context.MainCamera == null)
            {
                throw new InvalidOperationException(
                    "D0 shot camera feedback requires a BattleSessionHost and MainCamera.");
            }

            D0ShotCameraFeedbackController controller =
                GetOrAddComponent<D0ShotCameraFeedbackController>(
                    context.MainCamera.gameObject);
            controller.Configure(
                context.SessionHost,
                threeCProfile,
                context.MainCamera);
            if (!controller.TryValidate(out string error))
            {
                throw new InvalidOperationException(
                    $"D0 shot camera feedback is invalid: {error}");
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigurePlayerShotPresentation(
            PlayerWeaponPresentationController controller,
            CombatPresentationProfile profile,
            D0PlayerEntityView playerEntity,
            D0WeaponDefinition weaponDefinition)
        {
            if (controller == null || playerEntity == null
                || weaponDefinition == null)
            {
                throw new InvalidOperationException(
                    "CombatLab requires a player weapon presenter, complete Player Entity and WeaponDefinition.");
            }

            if (!weaponDefinition.TryValidatePresentation(out string weaponError))
            {
                throw new InvalidOperationException(
                    $"WeaponDefinition presentation is invalid: {weaponError}");
            }

            if (!playerEntity.TryValidate(out string entityError))
            {
                throw new InvalidOperationException(
                    $"Player Entity Prefab is invalid: {entityError}");
            }

            if (!playerEntity.SocketRegistry.TryResolve(
                    weaponDefinition.PrimaryPresentation.SocketId,
                    out _)
                || !playerEntity.SocketRegistry.TryResolve(
                    weaponDefinition.SecondaryPresentation.Shot.SocketId,
                    out _))
            {
                throw new InvalidOperationException(
                    "Player Entity sockets do not satisfy the active WeaponDefinition.");
            }

            SerializedObject serializedController =
                new SerializedObject(controller);
            ConfigureSerializedReference(
                serializedController,
                "presentationProfile",
                profile);
            ConfigureSerializedInteger(
                serializedController,
                "tracerCapacity",
                profile.PoolCapacities.PlayerShotCapacity);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            if (!controller.TryValidate(out string error))
            {
                throw new InvalidOperationException(
                    $"Player weapon presentation infrastructure is invalid: {error}");
            }
        }

        private static void HideLegacyGreyboxWorldRenderer(Transform worldRoot, string relativePath)
        {
            Transform legacyVisual = worldRoot == null ? null : worldRoot.Find(relativePath);
            if (legacyVisual == null)
            {
                return;
            }

            Renderer[] renderers = legacyVisual.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = false;
                EditorUtility.SetDirty(renderers[index]);
            }
        }

        private static void RemoveDirectChildIfPresent(Transform parent, string name)
        {
            Transform child = parent == null ? null : parent.Find(name);
            if (child != null && child.parent == parent)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void RemoveAllDirectChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(
                    parent.GetChild(index).gameObject);
            }
        }

        private static Transform EnsureDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                if (child.parent != parent)
                {
                    throw new InvalidOperationException(
                        $"D0 child '{name}' must be direct under '{parent.name}'.");
                }

                return child;
            }

            GameObject childObject = new GameObject(name);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void EnsurePresentationOnly(Transform root)
        {
            if (root.GetComponentsInChildren<Collider>(true).Length > 0
                || root.GetComponentsInChildren<Collider2D>(true).Length > 0
                || root.GetComponentsInChildren<Rigidbody>(true).Length > 0
                || root.GetComponentsInChildren<Rigidbody2D>(true).Length > 0)
            {
                throw new InvalidOperationException(
                    $"D0 presentation root '{root.name}' must not contain Collider or Rigidbody components.");
            }
        }

        private static Transform RequireDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null || child.parent != parent)
            {
                throw new InvalidOperationException(
                    $"D0 slice is missing direct child '{name}' under '{parent.name}'.");
            }

            return child;
        }

        private static void MoveOrReplaceLegacyCanvasChild(
            Transform canvasRoot,
            RectTransform overlay,
            string childName)
        {
            Transform legacy = canvasRoot.Find(childName);
            if (legacy == null || legacy.parent != canvasRoot)
            {
                return;
            }

            if (legacy is RectTransform)
            {
                legacy.SetParent(overlay, false);
                return;
            }

            Component[] components = legacy.GetComponents<Component>();
            if (legacy.childCount != 0 || components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"D0-owned legacy canvas child '{childName}' cannot be safely upgraded to RectTransform.");
            }

            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }

        private static RectTransform EnsureRectTransformChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                if (existing.parent != parent || !(existing is RectTransform rectTransform))
                {
                    throw new InvalidOperationException(
                        $"D0 UI child '{name}' must be a direct RectTransform child of '{parent.name}'.");
                }

                return rectTransform;
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private static void ConfigureObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} no longer exposes '{propertyName}'.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private readonly struct D0ActorPresentationBindings
        {
            public D0ActorPresentationBindings(
                Actor2DPresenter player,
                D0EnemyBehaviorController enemyBehavior)
            {
                Player = player;
                EnemyBehavior = enemyBehavior;
            }

            public Actor2DPresenter Player { get; }
            public D0EnemyBehaviorController EnemyBehavior { get; }
        }

        private readonly struct D0G3PresentationBindings
        {
            public D0G3PresentationBindings(
                ThreatTelegraph2DPresenter threatTelegraph,
                D0WeakpointPresentationController weakpoint,
                D0CombatVfxWorld combatVfxWorld,
                CombatAudioPresenter audio)
            {
                ThreatTelegraph = threatTelegraph;
                Weakpoint = weakpoint;
                CombatVfxWorld = combatVfxWorld;
                Audio = audio;
            }

            public ThreatTelegraph2DPresenter ThreatTelegraph { get; }
            public D0WeakpointPresentationController Weakpoint { get; }
            public D0CombatVfxWorld CombatVfxWorld { get; }
            public CombatAudioPresenter Audio { get; }
        }

        private readonly struct D0G4PresentationBindings
        {
            public D0G4PresentationBindings(
                CombatHud2DPresenter hud,
                D0TerminalScreenFxPresenter screenFx)
            {
                Hud = hud;
                ScreenFx = screenFx;
            }

            public CombatHud2DPresenter Hud { get; }
            public D0TerminalScreenFxPresenter ScreenFx { get; }
        }
    }
}
