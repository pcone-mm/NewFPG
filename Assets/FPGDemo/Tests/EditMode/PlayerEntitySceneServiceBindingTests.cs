using System.Reflection;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class PlayerEntitySceneServiceBindingTests
    {
        private const BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void PlayerControllersBindAndReleaseOnlySceneOwnedServices()
        {
            GameObject playerRoot = new GameObject("PlayerSceneServices");
            GameObject barrierRoot = new GameObject("BarrierSceneServices");
            GameObject weaponRoot = new GameObject("WeaponSceneServices");
            GameObject hostRoot = new GameObject("BattleSessionHost");
            GameObject cameraRoot = new GameObject("PresentationCamera");
            try
            {
                CombatLabPlayerController player =
                    playerRoot.AddComponent<CombatLabPlayerController>();
                D0PlayerBarrierPresentationController barrier =
                    barrierRoot.AddComponent<D0PlayerBarrierPresentationController>();
                PlayerWeaponPresentationController weapon =
                    weaponRoot.AddComponent<PlayerWeaponPresentationController>();
                BattleSessionHost host = hostRoot.AddComponent<BattleSessionHost>();
                Camera camera = cameraRoot.AddComponent<Camera>();

                Assert.That(
                    player.TryBindSceneServices(null, out string playerError),
                    Is.False);
                Assert.That(playerError, Does.Contain("BattleSessionHost"));
                Assert.That(
                    barrier.TryBindSceneServices(null, out string barrierError),
                    Is.False);
                Assert.That(barrierError, Does.Contain("BattleSessionHost"));
                Assert.That(
                    weapon.TryBindSceneServices(host, null, out string weaponError),
                    Is.False);
                Assert.That(weaponError, Does.Contain("Camera"));

                Assert.That(
                    player.TryBindSceneServices(host, out playerError),
                    Is.True,
                    playerError);
                Assert.That(
                    barrier.TryBindSceneServices(host, out barrierError),
                    Is.True,
                    barrierError);
                Assert.That(
                    weapon.TryBindSceneServices(host, camera, out weaponError),
                    Is.True,
                    weaponError);

                Assert.That(player.SessionHost, Is.SameAs(host));
                Assert.That(player.IsSceneServicesBound, Is.True);
                Assert.That(barrier.SessionHost, Is.SameAs(host));
                Assert.That(barrier.IsSceneServicesBound, Is.True);
                Assert.That(weapon.SessionHost, Is.SameAs(host));
                Assert.That(weapon.PresentationCamera, Is.SameAs(camera));
                Assert.That(weapon.IsSceneServicesBound, Is.True);

                player.UnbindSceneServices();
                barrier.UnbindSceneServices();
                weapon.UnbindSceneServices();

                Assert.That(player.SessionHost, Is.Null);
                Assert.That(player.IsSceneServicesBound, Is.False);
                Assert.That(barrier.SessionHost, Is.Null);
                Assert.That(barrier.IsSceneServicesBound, Is.False);
                Assert.That(weapon.SessionHost, Is.Null);
                Assert.That(weapon.PresentationCamera, Is.Null);
                Assert.That(weapon.IsSceneServicesBound, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(playerRoot);
                Object.DestroyImmediate(barrierRoot);
                Object.DestroyImmediate(weaponRoot);
                Object.DestroyImmediate(hostRoot);
                Object.DestroyImmediate(cameraRoot);
            }
        }

        [Test]
        public void SceneServiceFieldsAreRuntimeOnlyAndNeverSerializedOnEntityPrefabs()
        {
            AssertRuntimeOnlyField<CombatLabPlayerController>("sessionHost");
            AssertRuntimeOnlyField<D0PlayerBarrierPresentationController>("sessionHost");
            AssertRuntimeOnlyField<PlayerWeaponPresentationController>("sessionHost");
            AssertRuntimeOnlyField<PlayerWeaponPresentationController>("presentationCamera");
        }

        private static void AssertRuntimeOnlyField<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(fieldName, InstancePrivate);
            Assert.That(field, Is.Not.Null, typeof(T).Name + "." + fieldName);
            Assert.That(
                field.GetCustomAttribute<SerializeField>(),
                Is.Null,
                "Scene services are runtime injections and must not be serialized.");
        }
    }
}
