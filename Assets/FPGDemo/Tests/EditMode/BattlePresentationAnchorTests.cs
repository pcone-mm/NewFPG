using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class BattlePresentationAnchorTests
    {
        private const string FeiEntityPrefabPath =
            "Assets/FPGDemo/Presentation/Actors/Fei/PF_D0_FeiEntity.prefab";

        [Test]
        public void ActorSocketRegistryResolvesStableStringIdsToDistinctPrefabAnchors()
        {
            GameObject root = new GameObject("ActorSocketRegistryRoot");
            GameObject primary = new GameObject("PrimaryMuzzle");
            GameObject secondary = new GameObject("SecondaryMuzzle");
            try
            {
                primary.transform.SetParent(root.transform, false);
                secondary.transform.SetParent(root.transform, false);
                primary.transform.localPosition = new Vector3(0.72f, 0.42f, -0.06f);
                secondary.transform.localPosition = new Vector3(0.72f, 0.42f, -0.06f);
                D0ActorSocketRegistry sockets =
                    root.AddComponent<D0ActorSocketRegistry>();

                Assert.That(
                    sockets.TryRegister(
                        D0ActorSocketRegistry.PrimaryMuzzleId,
                        primary.transform,
                        out string primaryError),
                    Is.True,
                    primaryError);
                Assert.That(
                    sockets.TryRegister(
                        D0ActorSocketRegistry.SecondaryMuzzleId,
                        secondary.transform,
                        out string secondaryError),
                    Is.True,
                    secondaryError);
                Assert.That(sockets.TryValidate(out string error), Is.True, error);
                Assert.That(
                    sockets.TryResolve(
                        D0ActorSocketRegistry.PrimaryMuzzleId,
                        out Transform primaryMuzzle),
                    Is.True);
                Assert.That(
                    sockets.TryResolve(
                        D0ActorSocketRegistry.SecondaryMuzzleId,
                        out Transform secondaryMuzzle),
                    Is.True);
                Assert.That(primaryMuzzle.localPosition,
                    Is.EqualTo(new Vector3(0.72f, 0.42f, -0.06f)));
                Assert.That(secondaryMuzzle.localPosition,
                    Is.EqualTo(new Vector3(0.72f, 0.42f, -0.06f)));
                Assert.That(primaryMuzzle, Is.Not.SameAs(secondaryMuzzle));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ContextComputesPlayerAnchorFacadesFromTheCompleteEntity()
        {
            GameObject contextObject = new GameObject("EntityContext");
            GameObject entityObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(FeiEntityPrefabPath));
            try
            {
                BattleSceneContext context =
                    contextObject.AddComponent<BattleSceneContext>();
                D0PlayerEntityView entity =
                    entityObject.GetComponent<D0PlayerEntityView>();
                Assert.That(entity, Is.Not.Null);
                SetPrivateField(context, "playerEntity", entity);

                Assert.That(context.PlayerEntity, Is.SameAs(entity));
                Assert.That(context.PlayerAnchor, Is.SameAs(entity.transform));
                Assert.That(context.PlayerGroundAnchor, Is.SameAs(entity.GroundAnchor));
                Assert.That(context.AimAnchor, Is.SameAs(entity.AimAnchor));
                Assert.That(
                    context.D0PlayerActorPresenter,
                    Is.SameAs(entity.ActorPresenter));
            }
            finally
            {
                Object.DestroyImmediate(entityObject);
                Object.DestroyImmediate(contextObject);
            }
        }

        [Test]
        public void ContextDoesNotSerializeLegacyActorAnchorsOrHiddenPresenters()
        {
            string[] removedFields =
            {
                "playerAnchor",
                "playerGroundAnchor",
                "enemyAnchor",
                "enemyProjectileSpawnAnchor",
                "enemyWeakpointAnchor",
                "aimAnchor",
                "d0PlayerActorPresenter",
                "d0EnemyActorPresenter",
                "d0BurstbugCznFxPresenter",
                "luanHudiePresentationController"
            };

            for (int index = 0; index < removedFields.Length; index++)
            {
                Assert.That(
                    typeof(BattleSceneContext).GetField(
                        removedFields[index],
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    Is.Null,
                    removedFields[index] + " must not remain a serialized truth source.");
            }
        }

        [Test]
        public void CoordinatorUsesEntityGroundForWarningsAndEntityRootForFeedback()
        {
            GameObject contextObject = new GameObject("EntityAnchorCoordinator");
            GameObject hostObject = new GameObject("EntityAnchorHost");
            GameObject entityObject = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(FeiEntityPrefabPath));
            try
            {
                BattleSceneContext context =
                    contextObject.AddComponent<BattleSceneContext>();
                BattleSessionHost host =
                    hostObject.AddComponent<BattleSessionHost>();
                BattlePresentationCoordinator coordinator =
                    contextObject.AddComponent<BattlePresentationCoordinator>();
                D0PlayerEntityView entity =
                    entityObject.GetComponent<D0PlayerEntityView>();
                Assert.That(entity, Is.Not.Null);
                entity.transform.position = new Vector3(2f, 1.6f, -3f);

                SetPrivateField(context, "playerEntity", entity);
                SetPrivateField(coordinator, "sessionHost", host);
                SetHostContext(host, context);

                Assert.That(
                    InvokePosition(coordinator, "ResolvePlayerWarningPosition"),
                    Is.EqualTo(entity.GroundAnchor.position));
                Assert.That(
                    InvokePosition(coordinator, "ResolvePlayerCombatantPosition"),
                    Is.EqualTo(entity.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(entityObject);
                Object.DestroyImmediate(contextObject);
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void DiagnosticsOverlayCanBeHiddenWithoutDisablingDiagnosticsText()
        {
            GameObject root = new GameObject("DiagnosticsPresenterRoot");
            BattleSessionDiagnosticsPresenter presenter =
                root.AddComponent<BattleSessionDiagnosticsPresenter>();
            try
            {
                Assert.That(presenter.ShowOnGui, Is.True);
                presenter.ShowOnGui = false;

                Assert.That(presenter.ShowOnGui, Is.False);
                Assert.That(
                    presenter.RefreshText(),
                    Is.EqualTo("BattleSession unavailable"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void SetHostContext(
            BattleSessionHost host,
            BattleSceneContext context)
        {
            FieldInfo contextField = typeof(BattleSessionHost).GetField(
                "<Context>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(contextField, Is.Not.Null);
            contextField.SetValue(host, context);
        }

        private static Vector3 InvokePosition(
            BattlePresentationCoordinator coordinator,
            string methodName)
        {
            MethodInfo method = typeof(BattlePresentationCoordinator).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Vector3)method.Invoke(coordinator, null);
        }
    }
}
