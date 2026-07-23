using FPG.Demo.Combat;
using FPG.Demo.Core;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class TargetSelectorTests
    {
        [Test]
        public void ExistingAttackConstructorDefaultsToLegacyQueryMode()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.PelletRays, 1, 1);

            Assert.That(attack.QueryMode, Is.EqualTo(AttackQueryMode.Legacy));
            Assert.That(attack.AdditionalPenetrationCount, Is.Zero);
            Assert.That(attack.AreaCombatantLimit, Is.Zero);
            Assert.That(attack.AreaProjectileLimit, Is.Zero);
            Assert.That(
                attack.AllowedTargetKinds,
                Is.EqualTo(AttackTargetKinds.Combatant | AttackTargetKinds.Projectile));
            Assert.That(attack.IsQueryConfigurationValid, Is.True);
        }

        [Test]
        public void CandidateCountMustFitInputAndTailIsNotRead()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.DirectThenArea, 1, 2);
            QueryCandidate valid = Combatant(
                AttackQueryStage.Direct,
                -1,
                2,
                HitPart.Body,
                10,
                3,
                0);
            QueryCandidate[] candidates = { valid };
            QueryCandidate[] output = new QueryCandidate[2];

            DomainResult overflow = TargetSelector.Select(
                attack,
                candidates,
                new AttackQueryResult(2, 0),
                output,
                out int overflowCount);
            Assert.That(overflow.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(overflowCount, Is.Zero);

            QueryCandidate[] ignoredInvalidTail = { valid, default(QueryCandidate) };
            DomainResult accepted = TargetSelector.Select(
                attack,
                ignoredInvalidTail,
                new AttackQueryResult(1, 0),
                output,
                out int selectedCount);
            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(selectedCount, Is.EqualTo(1));
            Assert.That(output[0].GeometryId.Value, Is.EqualTo(10));
        }

        [Test]
        public void MalformedCandidateAndDroppedCandidateCountAreRejected()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.DirectThenArea, 1, 1);
            QueryCandidate[] output = new QueryCandidate[1];

            DomainResult malformed = TargetSelector.Select(
                attack,
                new[] { default(QueryCandidate) },
                new AttackQueryResult(1, 0),
                output,
                out int malformedCount);
            Assert.That(malformed.RejectReason, Is.EqualTo(RejectReason.InvalidState));
            Assert.That(malformedCount, Is.Zero);

            DomainResult dropped = TargetSelector.Select(
                attack,
                new[] { Combatant(AttackQueryStage.Direct, -1, 2, HitPart.Body, 1, 1, 0) },
                new AttackQueryResult(1, 1),
                output,
                out int droppedCount);
            Assert.That(dropped.RejectReason, Is.EqualTo(RejectReason.BufferCapacity));
            Assert.That(droppedCount, Is.Zero);
        }

        [Test]
        public void PrimaryUsesSampleLanesAndBlockerWinsAtEqualDistance()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.PelletRays, 2, 2);
            QueryCandidate[] candidates =
            {
                Blocker(AttackQueryStage.Pellet, 0, 101, 10, 0),
                Projectile(AttackQueryStage.Pellet, 0, 30, 102, 10, 1),
                Combatant(AttackQueryStage.Pellet, 0, 20, HitPart.Weakpoint, 103, 9, 2),
                Combatant(AttackQueryStage.Pellet, 0, 10, HitPart.Body, 104, 2, 3),
                Projectile(AttackQueryStage.Pellet, 1, 40, 105, 50, 4),
                Combatant(AttackQueryStage.Pellet, 1, 5, HitPart.Weakpoint, 106, 1, 5)
            };
            QueryCandidate[] output = new QueryCandidate[2];

            DomainResult result = TargetSelector.Select(
                attack,
                candidates,
                new AttackQueryResult(candidates.Length, 0),
                output,
                out int selectedCount);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(selectedCount, Is.EqualTo(2));
            Assert.That(output[0].SampleIndex, Is.Zero);
            Assert.That(output[0].TargetId.Value, Is.EqualTo(20));
            Assert.That(output[0].HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(output[1].SampleIndex, Is.EqualTo(1));
            Assert.That(output[1].TargetId.Value, Is.EqualTo(40));
            Assert.That(output[1].HitPart, Is.EqualTo(HitPart.Projectile));
        }

        [Test]
        public void PrimaryOutputIsIndependentOfCandidateInputOrder()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.PelletRays, 3, 3);
            QueryCandidate[] first =
            {
                Combatant(AttackQueryStage.Pellet, 2, 9, HitPart.Body, 20, 1, 0),
                Combatant(AttackQueryStage.Pellet, 0, 8, HitPart.Weakpoint, 21, 20, 1),
                Combatant(AttackQueryStage.Pellet, 1, 7, HitPart.Body, 22, 2, 2),
                Projectile(AttackQueryStage.Pellet, 0, 6, 23, 30, 3),
                Combatant(AttackQueryStage.Pellet, 1, 5, HitPart.Weakpoint, 24, 3, 4)
            };
            QueryCandidate[] second = Reverse(first);

            QueryCandidate[] firstOutput = Select(attack, first, 3);
            QueryCandidate[] secondOutput = Select(attack, second, 3);

            AssertSameCandidates(firstOutput, secondOutput);
            Assert.That(firstOutput[0].SampleIndex, Is.Zero);
            Assert.That(firstOutput[0].TargetId.Value, Is.EqualTo(6));
            Assert.That(firstOutput[1].SampleIndex, Is.EqualTo(1));
            Assert.That(firstOutput[1].TargetId.Value, Is.EqualTo(5));
            Assert.That(firstOutput[2].SampleIndex, Is.EqualTo(2));
        }

        [Test]
        public void FirstSurfacePenetrationUsesPerPelletBlockersAndSharedDistanceSlots()
        {
            AttackSnapshot attack = CreateAttack(
                QueryPolicy.PelletRays,
                2,
                4,
                AttackQueryMode.FirstSurfacePenetration,
                1,
                0,
                0);
            QueryCandidate[] first =
            {
                Combatant(AttackQueryStage.Pellet, 0, 20, HitPart.Body, 5, 4, 0),
                Projectile(AttackQueryStage.Pellet, 0, 30, 6, 3, 1),
                Combatant(AttackQueryStage.Pellet, 0, 10, HitPart.Weakpoint, 2, 8, 2),
                Blocker(AttackQueryStage.Pellet, 0, 9, 10, 3),
                Combatant(AttackQueryStage.Pellet, 0, 10, HitPart.Body, 1, 2, 4),
                Projectile(AttackQueryStage.Pellet, 0, 40, 7, 10, 5),
                Combatant(AttackQueryStage.Pellet, 1, 60, HitPart.Weakpoint, 12, 6, 6),
                Blocker(AttackQueryStage.Pellet, 1, 13, 5, 7),
                Projectile(AttackQueryStage.Pellet, 1, 70, 14, 5, 8),
                Combatant(AttackQueryStage.Pellet, 1, 60, HitPart.Body, 11, 4, 9)
            };

            QueryCandidate[] firstOutput = Select(attack, first, 3);
            QueryCandidate[] secondOutput = Select(attack, Reverse(first), 3);

            AssertSameCandidates(firstOutput, secondOutput);
            Assert.That(firstOutput[0].TargetId.Value, Is.EqualTo(10));
            Assert.That(firstOutput[0].HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(firstOutput[1].TargetId.Value, Is.EqualTo(30));
            Assert.That(firstOutput[1].TargetKind, Is.EqualTo(QueryTargetKind.Projectile));
            Assert.That(firstOutput[2].TargetId.Value, Is.EqualTo(60));
            Assert.That(firstOutput[2].HitPart, Is.EqualTo(HitPart.Body));
        }

        [Test]
        public void SecondaryUsesPriorityAndStableDistanceRuntimeGeometryOrder()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.DirectThenArea, 1, 8);
            QueryCandidate[] first =
            {
                Combatant(AttackQueryStage.Area, -1, 2, HitPart.Body, 100, 1, 0),
                Combatant(AttackQueryStage.Area, -1, 1, HitPart.Body, 90, 1, 1),
                Combatant(AttackQueryStage.Area, -1, 2, HitPart.Body, 50, 1, 2),
                Combatant(AttackQueryStage.Direct, -1, 30, HitPart.Weakpoint, 80, 50, 3),
                Projectile(AttackQueryStage.Direct, -1, 50, 70, 100, 4)
            };
            QueryCandidate[] second = Reverse(first);

            QueryCandidate[] firstOutput = Select(attack, first, 4);
            QueryCandidate[] secondOutput = Select(attack, second, 4);

            AssertSameCandidates(firstOutput, secondOutput);
            Assert.That(firstOutput[0].HitPart, Is.EqualTo(HitPart.Projectile));
            Assert.That(firstOutput[1].HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(firstOutput[2].TargetId.Value, Is.EqualTo(1));
            Assert.That(firstOutput[3].TargetId.Value, Is.EqualTo(2));
            Assert.That(firstOutput[3].GeometryId.Value, Is.EqualTo(50));
        }

        [Test]
        public void SecondaryBlockersAreStageIsolated()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.DirectThenArea, 1, 3);
            QueryCandidate[] candidates =
            {
                Blocker(AttackQueryStage.Direct, -1, 10, 5, 0),
                Projectile(AttackQueryStage.Direct, -1, 20, 11, 5, 1),
                Combatant(AttackQueryStage.Direct, -1, 30, HitPart.Weakpoint, 12, 4, 2),
                Combatant(AttackQueryStage.Area, -1, 40, HitPart.Body, 13, 100, 3)
            };

            QueryCandidate[] output = Select(attack, candidates, 2);

            Assert.That(output[0].TargetId.Value, Is.EqualTo(30));
            Assert.That(output[1].TargetId.Value, Is.EqualTo(40));
        }

        [Test]
        public void SecondaryDeduplicatesRuntimeBeforeMaxImpactCount()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.DirectThenArea, 1, 2);
            QueryCandidate[] candidates =
            {
                Combatant(AttackQueryStage.Area, -1, 20, HitPart.Body, 1, 1, 0),
                Combatant(AttackQueryStage.Direct, -1, 20, HitPart.Weakpoint, 2, 20, 1),
                Combatant(AttackQueryStage.Area, -1, 30, HitPart.Body, 3, 2, 2),
                Combatant(AttackQueryStage.Area, -1, 40, HitPart.Body, 4, 3, 3)
            };

            QueryCandidate[] output = Select(attack, candidates, 2);

            Assert.That(output[0].TargetId.Value, Is.EqualTo(20));
            Assert.That(output[0].HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(output[1].TargetId.Value, Is.EqualTo(30));
        }

        [Test]
        public void AreaAtFirstSurfaceIgnoresDirectAndBlockersAndKeepsIndependentLimits()
        {
            AttackSnapshot attack = CreateAttack(
                QueryPolicy.DirectThenArea,
                1,
                3,
                AttackQueryMode.AreaAtFirstSurface,
                0,
                2,
                1);
            QueryCandidate[] first =
            {
                Combatant(AttackQueryStage.Direct, -1, 999, HitPart.Weakpoint, 1, 1, 0),
                Blocker(AttackQueryStage.Area, -1, 2, 0, 1),
                Combatant(AttackQueryStage.Area, -1, 10, HitPart.Body, 3, 1, 2),
                Combatant(AttackQueryStage.Area, -1, 10, HitPart.Weakpoint, 4, 5, 3),
                Combatant(AttackQueryStage.Area, -1, 20, HitPart.Body, 5, 2, 4),
                Combatant(AttackQueryStage.Area, -1, 30, HitPart.Body, 6, 3, 5),
                Projectile(AttackQueryStage.Area, -1, 100, 7, 10, 6),
                Projectile(AttackQueryStage.Area, -1, 101, 8, 4, 7),
                Projectile(AttackQueryStage.Area, -1, 101, 9, 6, 8)
            };

            QueryCandidate[] firstOutput = Select(attack, first, 3);
            QueryCandidate[] secondOutput = Select(attack, Reverse(first), 3);

            AssertSameCandidates(firstOutput, secondOutput);
            Assert.That(firstOutput[0].TargetId.Value, Is.EqualTo(10));
            Assert.That(firstOutput[0].HitPart, Is.EqualTo(HitPart.Weakpoint));
            Assert.That(firstOutput[1].TargetId.Value, Is.EqualTo(20));
            Assert.That(firstOutput[2].TargetId.Value, Is.EqualTo(101));
            Assert.That(firstOutput[2].TargetKind, Is.EqualTo(QueryTargetKind.Projectile));
        }

        [Test]
        public void AllowedTargetKindsFilterEveryModeWhileBlockersStillTruncate()
        {
            AttackSnapshot legacy = CreateAttack(
                QueryPolicy.DirectThenArea,
                1,
                1,
                AttackQueryMode.Legacy,
                0,
                0,
                0,
                AttackTargetKinds.Combatant);
            QueryCandidate[] legacyOutput = Select(
                legacy,
                new[]
                {
                    Projectile(AttackQueryStage.Direct, -1, 10, 1, 1, 0),
                    Combatant(AttackQueryStage.Direct, -1, 20, HitPart.Body, 2, 2, 1)
                },
                1);
            Assert.That(legacyOutput[0].TargetId.Value, Is.EqualTo(20));

            AttackSnapshot penetration = CreateAttack(
                QueryPolicy.PelletRays,
                1,
                2,
                AttackQueryMode.FirstSurfacePenetration,
                1,
                0,
                0,
                AttackTargetKinds.Combatant);
            QueryCandidate[] penetrationOutput = Select(
                penetration,
                new[]
                {
                    Projectile(AttackQueryStage.Pellet, 0, 10, 3, 1, 0),
                    Combatant(AttackQueryStage.Pellet, 0, 20, HitPart.Body, 4, 2, 1),
                    Blocker(AttackQueryStage.Pellet, 0, 5, 3, 2),
                    Combatant(AttackQueryStage.Pellet, 0, 30, HitPart.Body, 6, 4, 3)
                },
                1);
            Assert.That(penetrationOutput[0].TargetId.Value, Is.EqualTo(20));

            AttackSnapshot area = CreateAttack(
                QueryPolicy.DirectThenArea,
                1,
                2,
                AttackQueryMode.AreaAtFirstSurface,
                0,
                1,
                1,
                AttackTargetKinds.Projectile);
            QueryCandidate[] areaOutput = Select(
                area,
                new[]
                {
                    Combatant(AttackQueryStage.Area, -1, 20, HitPart.Body, 7, 1, 0),
                    Projectile(AttackQueryStage.Area, -1, 10, 8, 2, 1),
                    Blocker(AttackQueryStage.Area, -1, 9, 3, 2)
                },
                1);
            Assert.That(areaOutput[0].TargetId.Value, Is.EqualTo(10));
            Assert.That(areaOutput[0].TargetKind, Is.EqualTo(QueryTargetKind.Projectile));
        }

        [Test]
        public void InvalidAllowedTargetConfigurationsAreRejected()
        {
            AttackSnapshot none = CreateAttack(
                QueryPolicy.PelletRays,
                1,
                1,
                AttackQueryMode.FirstSurfacePenetration,
                0,
                0,
                0,
                AttackTargetKinds.None);
            AttackSnapshot unknown = CreateAttack(
                QueryPolicy.PelletRays,
                1,
                1,
                AttackQueryMode.FirstSurfacePenetration,
                0,
                0,
                0,
                (AttackTargetKinds)(1 << 2));
            AttackSnapshot noAllowedAreaCapacity = CreateAttack(
                QueryPolicy.DirectThenArea,
                1,
                1,
                AttackQueryMode.AreaAtFirstSurface,
                0,
                1,
                0,
                AttackTargetKinds.Projectile);

            Assert.That(none.IsQueryConfigurationValid, Is.False);
            Assert.That(unknown.IsQueryConfigurationValid, Is.False);
            Assert.That(noAllowedAreaCapacity.IsQueryConfigurationValid, Is.False);
            Assert.That(TargetSelector.Select(
                none,
                new QueryCandidate[0],
                AttackQueryResult.Empty,
                new QueryCandidate[0],
                out int noneCount).RejectReason, Is.EqualTo(RejectReason.InvalidState));
            Assert.That(noneCount, Is.Zero);
            Assert.That(TargetSelector.Select(
                unknown,
                new QueryCandidate[0],
                AttackQueryResult.Empty,
                new QueryCandidate[0],
                out int unknownCount).RejectReason, Is.EqualTo(RejectReason.InvalidState));
            Assert.That(unknownCount, Is.Zero);
        }

        [Test]
        public void NewModesRejectImpactCapacityBelowTheirIndependentLimits()
        {
            AttackSnapshot penetrating = CreateAttack(
                QueryPolicy.PelletRays,
                2,
                3,
                AttackQueryMode.FirstSurfacePenetration,
                1,
                0,
                0);
            AttackSnapshot area = CreateAttack(
                QueryPolicy.DirectThenArea,
                1,
                2,
                AttackQueryMode.AreaAtFirstSurface,
                0,
                2,
                1);

            DomainResult penetratingResult = TargetSelector.Select(
                penetrating,
                new QueryCandidate[0],
                AttackQueryResult.Empty,
                new QueryCandidate[0],
                out int penetratingCount);
            DomainResult areaResult = TargetSelector.Select(
                area,
                new QueryCandidate[0],
                AttackQueryResult.Empty,
                new QueryCandidate[0],
                out int areaCount);

            Assert.That(penetratingResult.RejectReason, Is.EqualTo(RejectReason.InvalidState));
            Assert.That(penetratingCount, Is.Zero);
            Assert.That(areaResult.RejectReason, Is.EqualTo(RejectReason.InvalidState));
            Assert.That(areaCount, Is.Zero);
        }

        [Test]
        public void EmptyQuerySucceedsWithoutRequiringImpactCapacity()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.DirectThenArea, 1, 4);
            DomainResult result = TargetSelector.Select(
                attack,
                new QueryCandidate[0],
                AttackQueryResult.Empty,
                new QueryCandidate[0],
                out int selectedCount);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(selectedCount, Is.Zero);
        }

        [Test]
        public void MaximumDistanceCandidateSurvivesWhenLaneHasNoBlocker()
        {
            AttackSnapshot attack = CreateAttack(QueryPolicy.DirectThenArea, 1, 1);
            QueryCandidate candidate = Combatant(
                AttackQueryStage.Direct,
                -1,
                2,
                HitPart.Body,
                1,
                int.MaxValue,
                0);

            QueryCandidate[] output = Select(attack, new[] { candidate }, 1);

            Assert.That(output[0].DistanceKey, Is.EqualTo(int.MaxValue));
        }

        private static QueryCandidate[] Select(
            AttackSnapshot attack,
            QueryCandidate[] candidates,
            int expectedCount)
        {
            QueryCandidate[] output = new QueryCandidate[expectedCount];
            DomainResult result = TargetSelector.Select(
                attack,
                candidates,
                new AttackQueryResult(candidates.Length, 0),
                output,
                out int selectedCount);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(selectedCount, Is.EqualTo(expectedCount));
            return output;
        }

        private static AttackSnapshot CreateAttack(
            QueryPolicy policy,
            int payloadCount,
            int maxImpactCount)
        {
            return new AttackSnapshot(
                new AttackId(1),
                new ShotId(1),
                100,
                new RuntimeId(1),
                Team.Player,
                new TickIndex(0),
                new DamageSpec(10, 2),
                policy,
                payloadCount,
                maxImpactCount,
                1,
                1);
        }

        private static AttackSnapshot CreateAttack(
            QueryPolicy policy,
            int payloadCount,
            int maxImpactCount,
            AttackQueryMode queryMode,
            int additionalPenetrationCount,
            int areaCombatantLimit,
            int areaProjectileLimit,
            AttackTargetKinds allowedTargetKinds = AttackSnapshot.DefaultAllowedTargetKinds)
        {
            return new AttackSnapshot(
                new AttackId(1),
                new ShotId(1),
                100,
                new RuntimeId(1),
                Team.Player,
                new TickIndex(0),
                new DamageSpec(10, 2),
                policy,
                payloadCount,
                maxImpactCount,
                1,
                1,
                queryMode,
                additionalPenetrationCount,
                areaCombatantLimit,
                areaProjectileLimit,
                allowedTargetKinds);
        }

        private static QueryCandidate Combatant(
            AttackQueryStage stage,
            int sampleIndex,
            long targetId,
            HitPart hitPart,
            int geometryId,
            int distanceKey,
            int queryOrdinal)
        {
            return new QueryCandidate(
                stage,
                sampleIndex,
                new RuntimeId(targetId),
                QueryTargetKind.Combatant,
                hitPart,
                new GeometryId(geometryId),
                distanceKey,
                new SpatialVectorKey(geometryId, distanceKey, queryOrdinal),
                queryOrdinal);
        }

        private static QueryCandidate Projectile(
            AttackQueryStage stage,
            int sampleIndex,
            long targetId,
            int geometryId,
            int distanceKey,
            int queryOrdinal)
        {
            return new QueryCandidate(
                stage,
                sampleIndex,
                new RuntimeId(targetId),
                QueryTargetKind.Projectile,
                HitPart.Projectile,
                new GeometryId(geometryId),
                distanceKey,
                new SpatialVectorKey(geometryId, distanceKey, queryOrdinal),
                queryOrdinal);
        }

        private static QueryCandidate Blocker(
            AttackQueryStage stage,
            int sampleIndex,
            int geometryId,
            int distanceKey,
            int queryOrdinal)
        {
            return new QueryCandidate(
                stage,
                sampleIndex,
                RuntimeId.Invalid,
                QueryTargetKind.EnvironmentBlocker,
                HitPart.Body,
                new GeometryId(geometryId),
                distanceKey,
                new SpatialVectorKey(geometryId, distanceKey, queryOrdinal),
                queryOrdinal);
        }

        private static QueryCandidate[] Reverse(QueryCandidate[] source)
        {
            QueryCandidate[] reversed = new QueryCandidate[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                reversed[index] = source[source.Length - index - 1];
            }

            return reversed;
        }

        private static void AssertSameCandidates(
            QueryCandidate[] expected,
            QueryCandidate[] actual)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].QueryStage, Is.EqualTo(expected[index].QueryStage));
                Assert.That(actual[index].SampleIndex, Is.EqualTo(expected[index].SampleIndex));
                Assert.That(actual[index].TargetId, Is.EqualTo(expected[index].TargetId));
                Assert.That(actual[index].HitPart, Is.EqualTo(expected[index].HitPart));
                Assert.That(actual[index].GeometryId, Is.EqualTo(expected[index].GeometryId));
                Assert.That(actual[index].DistanceKey, Is.EqualTo(expected[index].DistanceKey));
            }
        }
    }
}
