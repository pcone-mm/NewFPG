using System.Collections;
using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FPG.Demo.Tests.PlayMode
{
    public sealed class PlayerWeaponPresentationLifecyclePlayModeTests
    {
        private static readonly BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator DestroyingTheEnabledControllerReleasesItsStandaloneViews()
        {
            GameObject root = new GameObject("PlayerWeaponPresentationLifecycleRoot");
            GameObject hostObject = new GameObject("PlayerWeaponPresentationLifecycleHost");
            Material material = null;
            CombatPresentationProfile profile = null;
            D0WeaponDefinition weapon = null;
            try
            {
                BattleSessionHost host = hostObject.AddComponent<BattleSessionHost>();
                GameObject viewRootObject = new GameObject("PlayerShotViews");
                viewRootObject.transform.SetParent(root.transform, false);

                Shader shader = Shader.Find("Sprites/Default");
                Assert.That(shader, Is.Not.Null, "The PlayMode test requires Unity's built-in sprite shader.");
                material = new Material(shader);

                PlayerWeaponPresentationController controller =
                    root.AddComponent<PlayerWeaponPresentationController>();
                profile = ConfigureController(
                    controller,
                    host,
                    viewRootObject.transform,
                    material,
                    out weapon);
                Assert.That(controller.TryInitialize(out string error), Is.True, error);
                Assert.That(
                    viewRootObject.GetComponentsInChildren<PlayerMuzzleFlashView>(true),
                    Has.Length.EqualTo(1));

                Object.Destroy(controller);
                yield return null;
                yield return null;

                Assert.That(
                    viewRootObject.GetComponentsInChildren<PlayerShotTracerView>(true),
                    Is.Empty,
                    "Controller teardown must release all standalone tracer views.");
                Assert.That(
                    viewRootObject.GetComponentsInChildren<PlayerShotTargetBurstView>(true),
                    Is.Empty,
                    "Controller teardown must release all standalone target-burst views.");
                Assert.That(
                    viewRootObject.GetComponentsInChildren<PlayerMuzzleFlashView>(true),
                    Is.Empty,
                    "Controller teardown must release its standalone muzzle view.");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(hostObject);
                if (material != null)
                {
                    Object.Destroy(material);
                }

                if (profile != null)
                {
                    Object.Destroy(profile);
                }

                if (weapon != null)
                {
                    Object.Destroy(weapon);
                }
            }
        }

        [UnityTest]
        public IEnumerator DestroyingTheEnabledControllerReleasesItsSecondaryChargeViewAndChildren()
        {
            GameObject root = new GameObject("PlayerWeaponSecondaryLifecycleRoot");
            GameObject hostObject = new GameObject("PlayerWeaponSecondaryLifecycleHost");
            Material material = null;
            CombatPresentationProfile profile = null;
            D0WeaponDefinition weapon = null;
            try
            {
                BattleSessionHost host = hostObject.AddComponent<BattleSessionHost>();
                GameObject viewRootObject = new GameObject("PlayerShotViews");
                viewRootObject.transform.SetParent(root.transform, false);

                Shader shader = Shader.Find("Sprites/Default");
                Assert.That(shader, Is.Not.Null, "The PlayMode test requires Unity's built-in sprite shader.");
                material = new Material(shader);

                PlayerWeaponPresentationController controller =
                    root.AddComponent<PlayerWeaponPresentationController>();
                profile = ConfigureController(
                    controller,
                    host,
                    viewRootObject.transform,
                    material,
                    out weapon);
                Assert.That(controller.TryInitialize(out string error), Is.True, error);

                D0SecondaryChargeView secondaryChargeView = GetReference<D0SecondaryChargeView>(
                    controller,
                    "secondaryChargeView");
                Assert.That(secondaryChargeView.IsPrepared, Is.True);
                Assert.That(
                    controller.SocketRegistry.TryResolve(
                        controller.WeaponDefinition.SecondaryPresentation.Shot.SocketId,
                        out Transform skillSource),
                    Is.True);
                secondaryChargeView.BeginCharge(
                    skillSource.position,
                    skillSource.position + Vector3.forward * 8f,
                    Color.cyan,
                    0.2f);
                Assert.That(secondaryChargeView.IsActive, Is.True);

                LineRenderer[] childViews =
                    secondaryChargeView.GetComponentsInChildren<LineRenderer>(true);
                Assert.That(
                    childViews,
                    Is.Not.Empty,
                    "The activated standalone secondary visual must own child render views.");
                GameObject secondaryChargeObject = secondaryChargeView.gameObject;

                Object.Destroy(controller);
                yield return null;
                yield return null;

                Assert.That(
                    secondaryChargeObject == null,
                    Is.True,
                    "Controller teardown must destroy its standalone secondary visual.");
                foreach (LineRenderer childView in childViews)
                {
                    Assert.That(
                        childView == null,
                        Is.True,
                        "Controller teardown must destroy every child view of the secondary visual.");
                }

                Assert.That(
                    viewRootObject.GetComponentsInChildren<D0SecondaryChargeView>(true),
                    Is.Empty,
                    "No standalone secondary visual may remain below the player-shot root.");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(hostObject);
                if (material != null)
                {
                    Object.Destroy(material);
                }

                if (profile != null)
                {
                    Object.Destroy(profile);
                }

                if (weapon != null)
                {
                    Object.Destroy(weapon);
                }
            }
        }

        private static CombatPresentationProfile ConfigureController(
            PlayerWeaponPresentationController controller,
            BattleSessionHost host,
            Transform shotViewRoot,
            Material shotMaterial,
            out D0WeaponDefinition weapon)
        {
            CombatPresentationProfile profile =
                ScriptableObject.CreateInstance<CombatPresentationProfile>();
            weapon = ScriptableObject.CreateInstance<D0WeaponDefinition>();

            GameObject entityObject = new GameObject("TestPlayerEntity");
            entityObject.transform.SetParent(host.transform, false);
            D0PlayerEntityView playerEntity =
                entityObject.AddComponent<D0PlayerEntityView>();
            Actor2DPresenter actorPresenter =
                entityObject.AddComponent<Actor2DPresenter>();
            SetObjectField(actorPresenter, "playerActor", true);

            D0ActorSocketRegistry sockets =
                entityObject.AddComponent<D0ActorSocketRegistry>();
            GameObject primarySocket = new GameObject("PrimaryMuzzle");
            GameObject secondarySocket = new GameObject("SecondaryMuzzle");
            primarySocket.transform.SetParent(entityObject.transform, false);
            secondarySocket.transform.SetParent(entityObject.transform, false);
            Assert.That(
                sockets.TryRegister(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    primarySocket.transform,
                    out string primaryError),
                Is.True,
                primaryError);
            Assert.That(
                sockets.TryRegister(
                    D0ActorSocketRegistry.SecondaryMuzzleId,
                    secondarySocket.transform,
                    out string secondaryError),
                Is.True,
                secondaryError);

            GameObject cameraObject = new GameObject("PresentationCamera");
            cameraObject.transform.SetParent(host.transform, false);
            Camera presentationCamera = cameraObject.AddComponent<Camera>();

            SetReference(controller, "shotViewRoot", shotViewRoot);
            SetReference(controller, "presentationProfile", profile);
            SetReference(controller, "shotMaterial", shotMaterial);
            SetInteger(controller, "tracerCapacity", 1);
            SetInteger(controller, "areaCapacity", 1);

            Assert.That(
                controller.TryBindPlayerEntity(
                    playerEntity,
                    weapon,
                    out string entityError),
                Is.True,
                entityError);
            Assert.That(
                controller.TryBindSceneServices(
                    host,
                    presentationCamera,
                    out string serviceError),
                Is.True,
                serviceError);
            return profile;
        }

        private static void SetObjectField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
            Assert.That(field, Is.Not.Null, $"Missing field: {target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static T GetReference<T>(
            PlayerWeaponPresentationController controller,
            string fieldName)
            where T : Object
        {
            FieldInfo field = typeof(PlayerWeaponPresentationController).GetField(
                fieldName,
                InstancePrivate);
            Assert.That(field, Is.Not.Null, $"Missing controller field: {fieldName}");
            T value = field.GetValue(controller) as T;
            Assert.That(value, Is.Not.Null, $"Controller field was not initialized: {fieldName}");
            return value;
        }

        private static void SetReference(
            PlayerWeaponPresentationController controller,
            string fieldName,
            Object value)
        {
            FieldInfo field = typeof(PlayerWeaponPresentationController).GetField(
                fieldName,
                InstancePrivate);
            Assert.That(field, Is.Not.Null, $"Missing controller field: {fieldName}");
            field.SetValue(controller, value);
        }

        private static void SetInteger(
            PlayerWeaponPresentationController controller,
            string fieldName,
            int value)
        {
            FieldInfo field = typeof(PlayerWeaponPresentationController).GetField(
                fieldName,
                InstancePrivate);
            Assert.That(field, Is.Not.Null, $"Missing controller field: {fieldName}");
            field.SetValue(controller, value);
        }
    }
}
