using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Core;
using FPG.Demo.Unity;
using NUnit.Framework;
using Spine.Unity;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0EnemyEntityWorldTests
    {
        private readonly List<UnityEngine.Object> createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void PreparedEntitiesResolveIdentityOnlyFromDefinitions()
        {
            D0EnemyEntityWorld world =
                CreateObject("EnemyWorld").AddComponent<D0EnemyEntityWorld>();
            D0EnemyEntityView luanView = CreateEntity("LuanEntity");
            D0EnemyEntityView hudieView = CreateEntity("HudieEntity");
            D0EnemyDefinition luan = CreateEnemyDefinition("luan");
            D0EnemyDefinition hudie = CreateEnemyDefinition("hudie");

            AddPreparedSlot(
                world,
                CreateSpawnSlot(1, luan),
                CreateSpawnPoint("LuanSpawn"),
                luanView);
            AddPreparedSlot(
                world,
                CreateSpawnSlot(2, hudie),
                CreateSpawnPoint("HudieSpawn"),
                hudieView);

            Assert.That(
                typeof(D0EnemyEntityView).GetField(
                    "legacyStableEnemyId",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(world.LuanEntity, Is.SameAs(luanView));
            Assert.That(world.HudieEntity, Is.SameAs(hudieView));
        }

        [Test]
        public void ResetRestoresCompleteAuthoredHierarchyBeforeSpawnPlacement()
        {
            D0EnemyEntityWorld world =
                CreateObject("EnemyWorld").AddComponent<D0EnemyEntityWorld>();
            D0EnemyEntityView view = CreateEntity("EnemyEntity");
            Transform socket = CreateChild(view.transform, "AttackSocket");
            Transform hitboxNode = CreateChild(view.GameplayAnchor, "ExtraHitboxNode");
            socket.localPosition = new Vector3(0.7f, 0.4f, -0.06f);
            socket.localRotation = Quaternion.Euler(0f, 0f, 13f);
            socket.localScale = new Vector3(1.1f, 0.9f, 1f);
            hitboxNode.localPosition = new Vector3(-0.25f, 0.8f, 0f);
            view.transform.localScale = new Vector3(2.2f, 2.2f, 2.2f);
            view.CaptureAuthoredLocalPose();

            D0SpawnPoint spawnPoint = CreateSpawnPoint("EnemySpawn");
            spawnPoint.transform.SetPositionAndRotation(
                new Vector3(8f, 3f, -1f),
                Quaternion.Euler(0f, 20f, 0f));
            AddPreparedSlot(
                world,
                CreateSpawnSlot(1, CreateEnemyDefinition("burstbug")),
                spawnPoint,
                view);
            SetField(
                world,
                "preparedScenario",
                CreateAsset<D0CombatScenarioDefinition>());

            view.transform.localScale = Vector3.one * 9f;
            view.VisualRoot.localPosition = Vector3.one * 4f;
            view.GameplayAnchor.localRotation = Quaternion.Euler(30f, 40f, 50f);
            socket.localPosition = Vector3.one * -5f;
            socket.localRotation = Quaternion.identity;
            socket.localScale = Vector3.one * 3f;
            hitboxNode.localPosition = Vector3.one * 6f;

            Assert.That(world.TryResetForSession(out string error), Is.True, error);
            Assert.That(world.ActiveEntity, Is.SameAs(view));
            Assert.That(view.transform.position, Is.EqualTo(spawnPoint.transform.position));
            Assert.That(
                Quaternion.Angle(view.transform.rotation, spawnPoint.transform.rotation),
                Is.LessThan(0.001f));
            Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one * 2.2f));
            Assert.That(view.VisualRoot.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(view.GameplayAnchor.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(socket.localPosition, Is.EqualTo(new Vector3(0.7f, 0.4f, -0.06f)));
            Assert.That(
                Quaternion.Angle(socket.localRotation, Quaternion.Euler(0f, 0f, 13f)),
                Is.LessThan(0.001f));
            Assert.That(socket.localScale, Is.EqualTo(new Vector3(1.1f, 0.9f, 1f)));
            Assert.That(hitboxNode.localPosition, Is.EqualTo(new Vector3(-0.25f, 0.8f, 0f)));
        }

        [Test]
        public void ClearPreparedEntitiesExplicitlyUnbindsDynamicHitboxes()
        {
            RuntimeId playerRuntimeId = new RuntimeId(101);
            RuntimeId enemyRuntimeId = new RuntimeId(202);
            HitboxRegistry registry =
                CreateObject("HitboxRegistry").AddComponent<HitboxRegistry>();
            Assert.That(
                registry.TryRegisterStaticBindings(
                    playerRuntimeId,
                    enemyRuntimeId,
                    out string registryError),
                Is.True,
                registryError);

            D0EnemyEntityView view = CreateEntity("EnemyEntity");
            Assert.That(
                view.TryBindGameplay(
                    registry,
                    playerRuntimeId,
                    enemyRuntimeId,
                    out string bindError),
                Is.True,
                bindError);
            Assert.That(view.IsGameplayBound, Is.True);
            Assert.That(registry.TryResolve(view.BodyHitbox, out _), Is.True);
            Assert.That(registry.TryResolve(view.WeakpointHitbox, out _), Is.True);

            D0EnemyEntityWorld world =
                CreateObject("EnemyWorld").AddComponent<D0EnemyEntityWorld>();
            AddPreparedSlot(
                world,
                CreateSpawnSlot(1, CreateEnemyDefinition("burstbug")),
                CreateSpawnPoint("EnemySpawn"),
                view);

            InvokePrivate(world, "ClearPreparedEntities");

            Assert.That(registry.Count, Is.Zero);
            Assert.That(world.EntityCount, Is.Zero);
        }

        private D0EnemyEntityView CreateEntity(string name)
        {
            GameObject root = CreateObject(name);
            D0EnemyEntityView view = root.AddComponent<D0EnemyEntityView>();
            Transform gameplay = CreateChild(root.transform, "GameplayRoot");
            Transform visual = CreateChild(root.transform, "VisualRoot");
            Transform socketsRoot = CreateChild(root.transform, "Sockets");
            D0ActorSocketRegistry sockets =
                socketsRoot.gameObject.AddComponent<D0ActorSocketRegistry>();
            Actor2DPresenter presenter = root.AddComponent<Actor2DPresenter>();
            SkeletonAnimation skeleton =
                visual.gameObject.AddComponent<SkeletonAnimation>();
            Transform projectile = CreateChild(gameplay, "ProjectileSpawn");
            Transform weakpoint = CreateChild(gameplay, "Weakpoint");
            BoxCollider body =
                CreateChild(gameplay, "BodyHitbox").gameObject.AddComponent<BoxCollider>();
            SphereCollider weakpointHitbox =
                CreateChild(weakpoint, "WeakpointHitbox")
                    .gameObject.AddComponent<SphereCollider>();

            SetField(view, "gameplayAnchor", gameplay);
            SetField(view, "visualRoot", visual);
            SetField(view, "socketRegistry", sockets);
            SetField(view, "actorPresenter", presenter);
            SetField(view, "skeletonAnimation", skeleton);
            SetField(view, "projectileSpawnAnchor", projectile);
            SetField(view, "weakpointAnchor", weakpoint);
            SetField(view, "bodyHitbox", body);
            SetField(view, "weakpointHitbox", weakpointHitbox);
            return view;
        }

        private D0EnemyDefinition CreateEnemyDefinition(string enemyId)
        {
            D0EnemyDefinition definition = CreateAsset<D0EnemyDefinition>();
            SetField(definition, "enemyId", enemyId);
            return definition;
        }

        private D0EncounterSpawnSlot CreateSpawnSlot(
            int definitionId,
            D0EnemyDefinition enemy)
        {
            D0EncounterSpawnSlot slot = new D0EncounterSpawnSlot();
            SetField(slot, "definitionId", definitionId);
            SetField(slot, "enemy", enemy);
            return slot;
        }

        private D0SpawnPoint CreateSpawnPoint(string name)
        {
            D0SpawnPoint spawnPoint =
                CreateObject(name).AddComponent<D0SpawnPoint>();
            spawnPoint.Configure(name);
            return spawnPoint;
        }

        private static void AddPreparedSlot(
            D0EnemyEntityWorld world,
            D0EncounterSpawnSlot definition,
            D0SpawnPoint spawnPoint,
            D0EnemyEntityView view)
        {
            Type slotType = typeof(D0EnemyEntityWorld).GetNestedType(
                "PreparedEntitySlot",
                BindingFlags.NonPublic);
            Assert.That(slotType, Is.Not.Null);
            object preparedSlot = Activator.CreateInstance(
                slotType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { definition, spawnPoint, view },
                null);
            IList slots = (IList)GetField(world, "preparedSlots").GetValue(world);
            slots.Add(preparedSlot);
        }

        private GameObject CreateObject(string name)
        {
            GameObject value = new GameObject(name);
            createdObjects.Add(value);
            return value;
        }

        private Transform CreateChild(Transform parent, string name)
        {
            GameObject child = CreateObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T CreateAsset<T>()
            where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(value);
            return value;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = GetField(target, fieldName);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static FieldInfo GetField(object target, string fieldName)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
