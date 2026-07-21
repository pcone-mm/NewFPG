using System.Collections.Generic;
using FPG.Demo.Core;
using FPG.Demo.Run;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Owns the prefab-backed enemy entities for one authored encounter. The
    /// encounter selects definitions, spawn points and lifecycle ticks; each
    /// enemy definition supplies its complete entity prefab.
    /// </summary>
    [DefaultExecutionOrder(1005)]
    [DisallowMultipleComponent]
    public sealed class D0EnemyEntityWorld : MonoBehaviour
    {
        [SerializeField]
        private BattleSessionHost sessionHost;

        [SerializeField]
        private HitboxRegistry hitboxRegistry;

        [SerializeField]
        private Transform entityRoot;

        private readonly List<PreparedEntitySlot> preparedSlots =
            new List<PreparedEntitySlot>();
        private D0CombatScenarioDefinition preparedScenario;
        private BattleSceneContext preparedContext;
        private PreparedEntitySlot activeSlot;

        public BattleSessionHost SessionHost => sessionHost;
        public HitboxRegistry HitboxRegistry => hitboxRegistry;
        public Transform EntityRoot => entityRoot;
        public int EntityCount => preparedSlots.Count;
        public D0EnemyEntityView LuanEntity
        {
            get { return FindEntityByEnemyId("luan"); }
        }
        public D0EnemyEntityView HudieEntity
        {
            get { return FindEntityByEnemyId("hudie"); }
        }
        public D0EnemyEntityView ActiveEntity => activeSlot == null
            ? null
            : activeSlot.View;
        public D0EnemyDefinition ActiveEnemyDefinition => activeSlot == null
            ? null
            : activeSlot.Definition.Enemy;
        public Actor2DPresenter ActiveActorPresenter => ActiveEntity == null
            ? null
            : ActiveEntity.ActorPresenter;
        public Transform ActiveGameplayAnchor => ActiveEntity == null
            ? null
            : ActiveEntity.GameplayAnchor;
        public Transform ActiveProjectileSpawnAnchor => ActiveEntity == null
            ? null
            : ActiveEntity.ProjectileSpawnAnchor;
        public Transform ActiveWeakpointAnchor => ActiveEntity == null
            ? null
            : ActiveEntity.WeakpointAnchor;
        public bool IsPrepared => preparedScenario != null && activeSlot != null;

        public bool TryValidate(out string error)
        {
            if (sessionHost == null || hitboxRegistry == null)
            {
                error = "Enemy entity world requires a BattleSessionHost and HitboxRegistry.";
                return false;
            }

            if (entityRoot == null)
            {
                error = "Enemy entity world requires an entity root.";
                return false;
            }

            if (entityRoot == transform || !entityRoot.IsChildOf(transform))
            {
                error = "Enemy entity root must be a dedicated child of the entity world.";
                return false;
            }

            if (entityRoot.localPosition.sqrMagnitude > 0.000001f
                || Quaternion.Angle(entityRoot.localRotation, Quaternion.identity) > 0.01f
                || (entityRoot.localScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                error = "Enemy entity root must use an identity local pose.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryPrepareScenario(
            D0CombatScenarioDefinition authoredScenario,
            BattleSceneContext context,
            out string error)
        {
            error = string.Empty;
            if (authoredScenario == null || context == null)
            {
                error = "Enemy entity world requires an authored scenario and scene context.";
                return false;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            bool roomMode = context.RoomBinding != null;
            bool scenarioValid = roomMode
                ? authoredScenario.TryValidateForRoom(out error)
                : authoredScenario.TryValidate(out error);
            if (!scenarioValid)
            {
                return false;
            }

            if (preparedScenario == authoredScenario && preparedSlots.Count > 0)
            {
                return TryResetForSession(out error);
            }

            ClearPreparedEntities();
            D0EncounterDefinition encounter = authoredScenario.Encounter;
            for (int index = 0; index < encounter.SpawnSlotCount; index++)
            {
                D0EncounterSpawnSlot definition = encounter.GetSpawnSlot(index);
                if (!context.TryGetEncounterSpawnPoint(
                        definition.SpawnPointId,
                        out D0SpawnPoint spawnPoint))
                {
                    error = $"Enemy spawn point '{definition.SpawnPointId}' is not bound in BattleSceneContext.";
                    ClearPreparedEntities();
                    return false;
                }

                D0EnemyEntityView view = InstantiateEntity(
                    definition.Enemy,
                    definition.DefinitionId,
                    context,
                    out error);
                if (view == null)
                {
                    ClearPreparedEntities();
                    return false;
                }

                PreparedEntitySlot prepared = new PreparedEntitySlot(
                    definition,
                    spawnPoint,
                    view);
                preparedSlots.Add(prepared);
                ResetPreparedPose(prepared);
                view.SetGameplayCollidersEnabled(false);
                view.gameObject.SetActive(false);
            }

            if (preparedSlots.Count == 0)
            {
                error = "Enemy entity world requires at least one prepared spawn slot.";
                ClearPreparedEntities();
                return false;
            }

            preparedScenario = authoredScenario;
            preparedContext = context;
            activeSlot = preparedSlots[0];
            activeSlot.View.gameObject.SetActive(true);
            activeSlot.View.SetGameplayCollidersEnabled(false);
            ConfigureRuntimeBindingsForActiveEntity();
            return true;
        }

        public bool TryPrepareInitialEntities(
            D0CombatScenarioDefinition authoredScenario,
            out string error)
        {
            BattleSceneContext context = sessionHost == null ? null : sessionHost.Context;
            return TryPrepareScenario(authoredScenario, context, out error);
        }

        public bool TryBindInitialRuntime(
            RuntimeId playerRuntimeId,
            RuntimeId enemyRuntimeId,
            out string error)
        {
            if (!IsPrepared)
            {
                error = "Enemy entity world must prepare its initial entity before binding a runtime.";
                return false;
            }

            return ActiveEntity.TryBindGameplay(
                hitboxRegistry,
                playerRuntimeId,
                enemyRuntimeId,
                out error);
        }

        public void RefreshRuntimeBindings()
        {
            ConfigureRuntimeBindingsForActiveEntity();
        }

        public bool TryApplyLifecycleChange(
            EnemyLifecycleChange change,
            RuntimeId playerRuntimeId,
            out string error)
        {
            error = string.Empty;
            if (!IsPrepared)
            {
                error = "Enemy entity world is not prepared.";
                return false;
            }

            if (change.DefinitionId == activeSlot.Definition.DefinitionId
                && change.CurrentRuntimeId == ActiveEntity.RuntimeId)
            {
                ConfigureRuntimeBindingsForActiveEntity();
                return true;
            }

            PreparedEntitySlot nextSlot = ResolveSlot(change.DefinitionId);
            if (nextSlot == null)
            {
                error = $"No entity prefab is configured for enemy definition {change.DefinitionId}.";
                return false;
            }

            PreparedEntitySlot previousSlot = activeSlot;
            D0EnemyEntityView previousEntity = previousSlot.View;
            RuntimeId previousRuntimeId = previousEntity.RuntimeId;
            if (!previousRuntimeId.IsValid)
            {
                error =
                    "The active enemy Entity must be runtime-bound before replacement.";
                return false;
            }

            Transform previousGameplayAnchor = previousEntity.GameplayAnchor;
            Vector3 inheritedPosition = previousGameplayAnchor == null
                ? previousEntity.transform.position
                : previousGameplayAnchor.position;
            Quaternion inheritedRotation = previousGameplayAnchor == null
                ? previousEntity.transform.rotation
                : previousGameplayAnchor.rotation;

            if (nextSlot != previousSlot)
            {
                if (nextSlot.Definition.PosePolicy
                    == D0EncounterSpawnPosePolicy.InheritPreviousGameplayPose)
                {
                    nextSlot.RestoreAuthoredLocalPose();
                    nextSlot.View.SetWorldPose(inheritedPosition, inheritedRotation);
                }
                else
                {
                    ResetPreparedPose(nextSlot);
                }

                nextSlot.View.gameObject.SetActive(true);
                nextSlot.View.SetGameplayCollidersEnabled(false);
            }

            if (!nextSlot.View.TryBindGameplay(
                    hitboxRegistry,
                    playerRuntimeId,
                    change.CurrentRuntimeId,
                    out error))
            {
                string bindingError = error;
                bool rollbackSucceeded = previousEntity.TryBindGameplay(
                    hitboxRegistry,
                    playerRuntimeId,
                    previousRuntimeId,
                    out string rollbackError);
                if (nextSlot != previousSlot)
                {
                    nextSlot.View.UnbindGameplay();
                    nextSlot.View.SetGameplayCollidersEnabled(false);
                    ResetPreparedPose(nextSlot);
                    nextSlot.View.gameObject.SetActive(false);
                }

                error = rollbackSucceeded
                    ? bindingError
                    : bindingError + " Rollback failed: " + rollbackError;
                return false;
            }

            if (nextSlot != previousSlot)
            {
                previousEntity.UnbindGameplay();
                previousEntity.SetGameplayCollidersEnabled(false);
                previousEntity.gameObject.SetActive(false);
            }

            activeSlot = nextSlot;
            ConfigureRuntimeBindingsForActiveEntity();
            return true;
        }

        public bool TryResetForSession(out string error)
        {
            if (preparedScenario == null || preparedSlots.Count == 0)
            {
                error = "Enemy entity world has no prepared scenario to reset.";
                return false;
            }

            for (int index = 0; index < preparedSlots.Count; index++)
            {
                PreparedEntitySlot prepared = preparedSlots[index];
                prepared.View.UnbindGameplay();
                prepared.View.SetGameplayCollidersEnabled(false);
                ResetPreparedPose(prepared);
                prepared.View.gameObject.SetActive(index == 0);
            }

            activeSlot = preparedSlots[0];
            ConfigureRuntimeBindingsForActiveEntity();
            error = string.Empty;
            return true;
        }

        public void ResetForSession()
        {
            TryResetForSession(out _);
        }

        public void UnbindAndDeactivateAll()
        {
            for (int index = 0; index < preparedSlots.Count; index++)
            {
                D0EnemyEntityView view = preparedSlots[index].View;
                if (view == null)
                {
                    continue;
                }

                view.UnbindGameplay();
                view.SetGameplayCollidersEnabled(false);
                view.RestoreAuthoredLocalPose();
                view.gameObject.SetActive(false);
            }

            activeSlot = null;
        }

        private D0EnemyEntityView InstantiateEntity(
            D0EnemyDefinition enemy,
            int definitionId,
            BattleSceneContext context,
            out string error)
        {
            error = string.Empty;
            if (enemy == null || enemy.EntityPrefab == null)
            {
                error = $"Enemy definition {definitionId} has no entity prefab.";
                return null;
            }

            D0EnemyEntityView view = Instantiate(enemy.EntityPrefab, entityRoot);
            view.name = $"EnemyEntity_{definitionId}_{enemy.EnemyId}";
            view.CaptureAuthoredLocalPose();
            view.gameObject.SetActive(true);
            view.SetGameplayCollidersEnabled(false);
            // Identity comes from D0EnemyDefinition; the Entity Prefab carries
            // no second stable-id source. Its authored VisualRoot pose is
            // intentionally left untouched by runtime configuration.
            if (!view.TryValidate(out error)
                || !enemy.ActorPresentation.TryGetEnemy(
                    out EnemyActorPresentationDefinition presentation)
                || presentation == null)
            {
                if (string.IsNullOrEmpty(error))
                {
                    error = $"Enemy '{enemy.EnemyId}' has no authored state presentation data.";
                }

                DestroyEntity(view);
                return null;
            }

            CombatPresentationProfile profile = context.D0PresentationProfile;
            if (!view.TryConfigureActorPresenter(
                    profile,
                    enemy.ActorPresentation,
                    out error))
            {
                DestroyEntity(view);
                return null;
            }

            ConfigureActorRenderers(
                view.gameObject,
                profile == null ? 10 : profile.Sorting.ActorOrder);
            return view;
        }

        private PreparedEntitySlot ResolveSlot(int definitionId)
        {
            for (int index = 0; index < preparedSlots.Count; index++)
            {
                PreparedEntitySlot candidate = preparedSlots[index];
                if (candidate.Definition.DefinitionId == definitionId)
                {
                    return candidate;
                }
            }

            return null;
        }

        private D0EnemyEntityView FindEntityByEnemyId(string enemyId)
        {
            for (int index = 0; index < preparedSlots.Count; index++)
            {
                PreparedEntitySlot candidate = preparedSlots[index];
                D0EnemyDefinition definition = candidate.Definition.Enemy;
                if (candidate.View != null
                    && definition != null
                    && definition.EnemyId == enemyId)
                {
                    return candidate.View;
                }
            }

            return null;
        }

        private static void ResetPreparedPose(PreparedEntitySlot prepared)
        {
            prepared.RestoreAuthoredLocalPose();
            prepared.View.SetWorldPose(
                prepared.SpawnPoint.transform.position,
                prepared.SpawnPoint.transform.rotation);
        }

        private void ConfigureRuntimeBindingsForActiveEntity()
        {
            if (ActiveEntity == null)
            {
                return;
            }

            BattleSceneContext context = preparedContext != null
                ? preparedContext
                : sessionHost == null ? null : sessionHost.Context;
            D0EnemyBehaviorController behavior = context == null
                ? null
                : context.D0EnemyBehaviorController;
            behavior?.NotifyEnemyEntityChanged(ActiveEntity);

            D0WeakpointPresentationController weakpoint = context == null
                ? null
                : context.D0WeakpointPresentationController;
            weakpoint?.RebindEnemyEntity(
                ActiveEntity.WeakpointAnchor,
                ActiveEntity.WeakpointHitbox as SphereCollider,
                ActiveEntity.ActorPresenter,
                ActiveEntity.HasWeakpoint);

            ThreatTelegraph2DPresenter telegraph = context == null
                ? null
                : context.D0ThreatTelegraphPresenter;
            telegraph?.RebindEnemyEntity(
                ActiveEntity.GameplayAnchor,
                ActiveEntity.WeakpointAnchor,
                ActiveEntity.ActorPresenter,
                preparedScenario == null ? null : preparedScenario.Encounter);
            telegraph?.RebindPlayerEntity(
                context == null
                    ? null
                    : context.PlayerGroundAnchor ?? context.PlayerAnchor);
        }

        private static void ConfigureActorRenderers(
            GameObject entity,
            int sortingOrder)
        {
            Renderer[] renderers = entity.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                renderer.sortingOrder = sortingOrder;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void ClearPreparedEntities()
        {
            for (int index = 0; index < preparedSlots.Count; index++)
            {
                D0EnemyEntityView view = preparedSlots[index].View;
                if (view != null)
                {
                    view.UnbindGameplay();
                    view.SetGameplayCollidersEnabled(false);
                }

                DestroyEntity(view);
            }

            preparedSlots.Clear();
            preparedScenario = null;
            preparedContext = null;
            activeSlot = null;
        }

        private static void DestroyEntity(D0EnemyEntityView entity)
        {
            if (entity == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(entity.gameObject);
            }
            else
            {
                DestroyImmediate(entity.gameObject);
            }
        }

        private void OnDestroy()
        {
            ClearPreparedEntities();
        }

        private sealed class PreparedEntitySlot
        {
            public PreparedEntitySlot(
                D0EncounterSpawnSlot definition,
                D0SpawnPoint spawnPoint,
                D0EnemyEntityView view)
            {
                Definition = definition;
                SpawnPoint = spawnPoint;
                View = view;
                if (!view.HasCapturedAuthoredLocalPose)
                {
                    view.CaptureAuthoredLocalPose();
                }
            }

            public D0EncounterSpawnSlot Definition { get; }
            public D0SpawnPoint SpawnPoint { get; }
            public D0EnemyEntityView View { get; }

            public void RestoreAuthoredLocalPose()
            {
                View.RestoreAuthoredLocalPose();
            }
        }
    }
}
