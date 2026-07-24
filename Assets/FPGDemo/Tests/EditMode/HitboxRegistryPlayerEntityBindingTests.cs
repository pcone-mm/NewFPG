using System;
using System.Collections.Generic;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class HitboxRegistryPlayerEntityBindingTests
    {
        private static readonly RuntimeId PlayerRuntimeId = new RuntimeId(11);
        private static readonly RuntimeId EnemyRuntimeId = new RuntimeId(22);
        private readonly List<GameObject> createdObjects = new List<GameObject>();

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
        public void BindPlayerEntityReplacesLegacyPlayerAndPreservesOtherOwners()
        {
            HitboxRegistry registry = CreateRegistry(
                true,
                out Collider environment,
                out Collider legacyPlayer);
            Assert.That(
                registry.TryRegisterStaticBindings(
                    PlayerRuntimeId,
                    EnemyRuntimeId,
                    out string error),
                Is.True,
                error);

            Collider projectile = CreateCollider("PlayerProjectile");
            Assert.That(
                registry.Register(
                    new HitboxBinding(
                        projectile,
                        new RuntimeId(31),
                        QueryTargetKind.Projectile,
                        HitPart.Projectile,
                        new GeometryId(4001),
                        Team.Player)).IsSuccess,
                Is.True);

            FpgPlayerEntityView playerEntity = CreatePlayerEntity(
                "ActivePlayer",
                out Collider entityBody);
            Assert.That(
                registry.TryBindPlayerEntity(
                    PlayerRuntimeId,
                    playerEntity,
                    new GeometryId(1001),
                    out error),
                Is.True,
                error);

            Assert.That(registry.BoundPlayerEntity, Is.SameAs(playerEntity));
            Assert.That(registry.TryResolve(legacyPlayer, out _), Is.False);
            Assert.That(registry.TryResolve(environment, out RegisteredHitbox blocker), Is.True);
            Assert.That(blocker.TargetKind, Is.EqualTo(QueryTargetKind.EnvironmentBlocker));
            Assert.That(registry.TryResolve(projectile, out RegisteredHitbox shot), Is.True);
            Assert.That(shot.TargetKind, Is.EqualTo(QueryTargetKind.Projectile));
            Assert.That(registry.TryResolve(entityBody, out RegisteredHitbox playerBody), Is.True);
            Assert.That(playerBody.RuntimeId, Is.EqualTo(PlayerRuntimeId));
            Assert.That(playerBody.Team, Is.EqualTo(Team.Player));
            Assert.That(playerBody.HitPart, Is.EqualTo(HitPart.Body));
            Assert.That(registry.Count, Is.EqualTo(3));

            Assert.That(registry.TryUnbindPlayerEntity(playerEntity), Is.True);
            Assert.That(registry.TryUnbindPlayerEntity(playerEntity), Is.True);
            Assert.That(registry.BoundPlayerEntity, Is.Null);
            Assert.That(registry.TryResolve(entityBody, out _), Is.False);
            Assert.That(registry.TryResolve(environment, out _), Is.True);
            Assert.That(registry.TryResolve(projectile, out _), Is.True);
            Assert.That(registry.Count, Is.EqualTo(2));
        }

        [Test]
        public void RebindAndUnbindUseTheActivePlayerEntityAsOwner()
        {
            HitboxRegistry registry = CreateRegistry(
                false,
                out Collider environment,
                out _);
            Assert.That(
                registry.TryRegisterStaticBindings(
                    PlayerRuntimeId,
                    EnemyRuntimeId,
                    out string error),
                Is.True,
                error);

            FpgPlayerEntityView first = CreatePlayerEntity("FirstPlayer", out Collider firstBody);
            FpgPlayerEntityView replacement = CreatePlayerEntity(
                "ReplacementPlayer",
                out Collider replacementBody);
            Assert.That(
                registry.TryBindPlayerEntity(
                    PlayerRuntimeId,
                    first,
                    new GeometryId(1001),
                    out error),
                Is.True,
                error);
            Assert.That(
                registry.TryBindPlayerEntity(
                    PlayerRuntimeId,
                    replacement,
                    new GeometryId(1002),
                    out error),
                Is.True,
                error);

            Assert.That(registry.TryResolve(firstBody, out _), Is.False);
            Assert.That(registry.TryResolve(replacementBody, out _), Is.True);
            Assert.That(registry.TryUnbindPlayerEntity(first), Is.True);
            Assert.That(registry.TryResolve(replacementBody, out _), Is.True);
            Assert.That(registry.BoundPlayerEntity, Is.SameAs(replacement));

            Assert.That(registry.TryUnbindPlayerEntity(replacement), Is.True);
            Assert.That(registry.TryResolve(replacementBody, out _), Is.False);
            Assert.That(registry.TryResolve(environment, out _), Is.True);
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void BindPlayerEntityRejectsWrongSessionAndForeignGeometryWithoutMutation()
        {
            HitboxRegistry registry = CreateRegistry(
                true,
                out Collider environment,
                out Collider legacyPlayer);
            Assert.That(
                registry.TryRegisterStaticBindings(
                    PlayerRuntimeId,
                    EnemyRuntimeId,
                    out string error),
                Is.True,
                error);
            FpgPlayerEntityView playerEntity = CreatePlayerEntity(
                "ActivePlayer",
                out Collider entityBody);

            Assert.That(
                registry.TryBindPlayerEntity(
                    new RuntimeId(99),
                    playerEntity,
                    new GeometryId(1001),
                    out error),
                Is.False);
            Assert.That(error, Does.Contain("active registry session"));
            Assert.That(
                registry.TryBindPlayerEntity(
                    PlayerRuntimeId,
                    playerEntity,
                    new GeometryId(3001),
                    out error),
                Is.False);
            Assert.That(error, Does.Contain("already owned"));
            Assert.That(registry.BoundPlayerEntity, Is.Null);
            Assert.That(registry.TryResolve(entityBody, out _), Is.False);
            Assert.That(registry.TryResolve(environment, out _), Is.True);
            Assert.That(registry.TryResolve(legacyPlayer, out _), Is.True);
            Assert.That(registry.Count, Is.EqualTo(2));
        }

        [Test]
        public void ClearingSessionAlsoClearsPlayerEntityOwnership()
        {
            HitboxRegistry registry = CreateRegistry(
                false,
                out _,
                out _);
            Assert.That(
                registry.TryRegisterStaticBindings(
                    PlayerRuntimeId,
                    EnemyRuntimeId,
                    out string error),
                Is.True,
                error);
            FpgPlayerEntityView playerEntity = CreatePlayerEntity(
                "ActivePlayer",
                out Collider entityBody);
            Assert.That(
                registry.TryBindPlayerEntity(
                    PlayerRuntimeId,
                    playerEntity,
                    new GeometryId(1001),
                    out error),
                Is.True,
                error);

            registry.ClearDynamicAndStaticBindings();

            Assert.That(registry.BoundPlayerEntity, Is.Null);
            Assert.That(registry.TryResolve(entityBody, out _), Is.False);
            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.StaticBindingsRegistered, Is.False);
        }

        private HitboxRegistry CreateRegistry(
            bool includeLegacyPlayer,
            out Collider environment,
            out Collider legacyPlayer)
        {
            GameObject root = CreateObject("HitboxRegistry");
            HitboxRegistry registry = root.AddComponent<HitboxRegistry>();
            environment = CreateCollider("Environment");
            legacyPlayer = includeLegacyPlayer
                ? CreateCollider("LegacyPlayer")
                : null;

            List<HitboxBinding> bindings = new List<HitboxBinding>
            {
                new HitboxBinding(
                    environment,
                    HitboxTargetReference.Environment,
                    QueryTargetKind.EnvironmentBlocker,
                    HitPart.Body,
                    new GeometryId(3001))
            };
            if (legacyPlayer != null)
            {
                bindings.Insert(
                    0,
                    new HitboxBinding(
                        legacyPlayer,
                        HitboxTargetReference.Player,
                        QueryTargetKind.Combatant,
                        HitPart.Body,
                        new GeometryId(1001)));
            }

            SetField(registry, "staticBindings", bindings.ToArray());
            return registry;
        }

        private FpgPlayerEntityView CreatePlayerEntity(
            string name,
            out Collider bodyCollider)
        {
            GameObject root = CreateObject(name);
            FpgPlayerEntityView entity = root.AddComponent<FpgPlayerEntityView>();
            GameObject body = CreateObject(name + "Body");
            body.transform.SetParent(root.transform, false);
            bodyCollider = body.AddComponent<BoxCollider>();
            SetField(entity, "bodyHitbox", bodyCollider);
            return entity;
        }

        private Collider CreateCollider(string name)
        {
            return CreateObject(name).AddComponent<BoxCollider>();
        }

        private GameObject CreateObject(string name)
        {
            GameObject value = new GameObject(name);
            createdObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
