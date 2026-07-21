using System;
using System.Reflection;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using FPG.Demo.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class D0HitTipCombatAggregationTests
    {
        [Test]
        public void RepresentativeHitTipUsesExactAppliedDamageForItsOwnTarget()
        {
            GameObject root = new GameObject("D0HitTipAggregationRoot");
            GameObject presenterObject = new GameObject("D0HitTip", typeof(RectTransform));
            GameObject poolObject = new GameObject("Pool", typeof(RectTransform));
            GameObject cameraObject = new GameObject("PresentationCamera", typeof(Camera));
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            BattleSession session = CombatLabHarness.CreateSession();
            try
            {
                presenterObject.transform.SetParent(root.transform, false);
                poolObject.transform.SetParent(presenterObject.transform, false);
                cameraObject.transform.SetParent(root.transform, false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = Vector3.zero;
                camera.transform.rotation = Quaternion.identity;

                D0HitTipPresenter presenter = presenterObject.AddComponent<D0HitTipPresenter>();
                Assert.That(
                    presenter.TryPrepare(
                        poolObject.GetComponent<RectTransform>(),
                        sprite,
                        sprite,
                        null,
                        4,
                        out string prepareError),
                    Is.True,
                    prepareError);

                BattlePresentationCoordinator coordinator =
                    root.AddComponent<BattlePresentationCoordinator>();
                ConfigureCoordinatorForD0Tip(coordinator, session, presenter, camera, 4);

                AttackId attackId = new AttackId(71L);
                InvokePrivate(
                    coordinator,
                    "RecordD0AppliedDamage",
                    CreateDamageEvent(
                        session,
                        attackId,
                        session.EnemyRuntimeId,
                        100,
                        68,
                        new ImpactId(1L)));
                // Same attack, but a different target. Its 20 actual damage
                // must not be mixed into the enemy's representative hit tip.
                InvokePrivate(
                    coordinator,
                    "RecordD0AppliedDamage",
                    CreateDamageEvent(
                        session,
                        attackId,
                        new RuntimeId(991L),
                        100,
                        80,
                        new ImpactId(2L)));

                InvokePrivate(
                    coordinator,
                    "PresentD0HitTip",
                    CreateRepresentativeHit(attackId, session.EnemyRuntimeId, HitPart.Weakpoint));

                Assert.That(presenter.ActiveCount, Is.EqualTo(1));
                Text valueText = poolObject.GetComponentInChildren<Text>(true);
                Assert.That(valueText, Is.Not.Null);
                Assert.That(valueText.text, Is.EqualTo("32"));
            }
            finally
            {
                session.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MissingAppliedDamageDoesNotInventANumber()
        {
            GameObject root = new GameObject("D0HitTipMissingDamageRoot");
            GameObject presenterObject = new GameObject("D0HitTip", typeof(RectTransform));
            GameObject poolObject = new GameObject("Pool", typeof(RectTransform));
            GameObject cameraObject = new GameObject("PresentationCamera", typeof(Camera));
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            BattleSession session = CombatLabHarness.CreateSession();
            try
            {
                presenterObject.transform.SetParent(root.transform, false);
                poolObject.transform.SetParent(presenterObject.transform, false);
                cameraObject.transform.SetParent(root.transform, false);
                D0HitTipPresenter presenter = presenterObject.AddComponent<D0HitTipPresenter>();
                Assert.That(
                    presenter.TryPrepare(
                        poolObject.GetComponent<RectTransform>(),
                        sprite,
                        sprite,
                        null,
                        1,
                        out string prepareError),
                    Is.True,
                    prepareError);

                BattlePresentationCoordinator coordinator =
                    root.AddComponent<BattlePresentationCoordinator>();
                ConfigureCoordinatorForD0Tip(
                    coordinator,
                    session,
                    presenter,
                    cameraObject.GetComponent<Camera>(),
                    1);

                InvokePrivate(
                    coordinator,
                    "PresentD0HitTip",
                    CreateRepresentativeHit(
                        new AttackId(72L),
                        session.EnemyRuntimeId,
                        HitPart.Body));

                Assert.That(presenter.ActiveCount, Is.Zero);
            }
            finally
            {
                session.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ConfigureCoordinatorForD0Tip(
            BattlePresentationCoordinator coordinator,
            BattleSession session,
            D0HitTipPresenter presenter,
            Camera camera,
            int aggregateCapacity)
        {
            SetPrivateField(coordinator, "session", session);
            SetPrivateField(coordinator, "d0HitTipPresenter", presenter);
            SetPrivateField(coordinator, "presentationCamera", camera);
            FieldInfo aggregateField = typeof(BattlePresentationCoordinator).GetField(
                "d0AttackDamageAggregates",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(aggregateField, Is.Not.Null);
            Array aggregates = Array.CreateInstance(
                aggregateField.FieldType.GetElementType(),
                aggregateCapacity);
            aggregateField.SetValue(coordinator, aggregates);
            SetPrivateField(coordinator, "d0AttackDamageAggregateCount", 0);
        }

        private static CombatEvent CreateDamageEvent(
            BattleSession session,
            AttackId attackId,
            RuntimeId targetId,
            int valueBefore,
            int valueAfter,
            ImpactId impactId)
        {
            return new CombatEvent(
                1L,
                new TickIndex(1L),
                CombatEventType.DamageApplied,
                session.PlayerRuntimeId,
                targetId,
                attackId,
                impactId,
                valueBefore,
                valueAfter,
                RejectReason.None,
                0UL,
                DamageChannel.Life,
                0,
                false);
        }

        private static SelectedAttackHit CreateRepresentativeHit(
            AttackId attackId,
            RuntimeId targetId,
            HitPart hitPart)
        {
            return new SelectedAttackHit(
                attackId,
                new ShotId(4L),
                new TickIndex(1L),
                0,
                AttackQueryStage.Pellet,
                0,
                targetId,
                QueryTargetKind.Combatant,
                hitPart,
                new GeometryId(1001),
                new SpatialVectorKey(0, 0, 5000));
        }

        private static void InvokePrivate(
            BattlePresentationCoordinator coordinator,
            string methodName,
            object argument)
        {
            MethodInfo method = typeof(BattlePresentationCoordinator).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(coordinator, new[] { argument });
        }

        private static void SetPrivateField(
            BattlePresentationCoordinator coordinator,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(BattlePresentationCoordinator).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(coordinator, value);
        }
    }
}
