using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;
using NUnit.Framework;

namespace FPG.Demo.Tests.EditMode
{
    public sealed class PresentationCursorTests
    {
        [Test]
        public void CombatTraceCursorConsumesInitialSequenceZeroWithoutRepeatingCommittedEvents()
        {
            CombatTrace trace = new CombatTrace(2);
            CombatTraceCursor cursor = new CombatTraceCursor();
            CombatEvent first = RecordTraceEvent(trace, 0);
            CombatEvent[] output = new CombatEvent[1];

            Assert.That(cursor.LastSeenSequence, Is.EqualTo(-1));
            Assert.That(cursor.CopyUnread(trace, output, out bool hasGap), Is.EqualTo(1));
            Assert.That(hasGap, Is.False);
            Assert.That(output[0].Sequence, Is.EqualTo(0));

            cursor.Commit(output[0]);
            Assert.That(cursor.LastSeenSequence, Is.EqualTo(first.Sequence));
            Assert.That(cursor.CopyUnread(trace, output, out hasGap), Is.Zero);
            Assert.That(hasGap, Is.False);
        }

        [Test]
        public void CombatTraceCursorSignalsRingGapAndResumesAfterExplicitResolution()
        {
            CombatTrace trace = new CombatTrace(2);
            CombatTraceCursor cursor = new CombatTraceCursor();
            CombatEvent consumed = RecordTraceEvent(trace, 0);
            CombatEvent[] output = new CombatEvent[2];
            cursor.Commit(consumed);

            RecordTraceEvent(trace, 1);
            RecordTraceEvent(trace, 2);
            RecordTraceEvent(trace, 3);

            Assert.That(trace.GetOldest(0).Sequence, Is.EqualTo(2));
            Assert.That(cursor.CopyUnread(trace, output, out bool hasGap), Is.Zero);
            Assert.That(hasGap, Is.True);
            Assert.That(cursor.LastSeenSequence, Is.EqualTo(0));
            Assert.That(cursor.GapCount, Is.Zero);

            cursor.ResolveGap(trace);
            Assert.That(cursor.LastSeenSequence, Is.EqualTo(3));
            Assert.That(cursor.GapCount, Is.EqualTo(1));

            RecordTraceEvent(trace, 4);
            Assert.That(cursor.CopyUnread(trace, output, out hasGap), Is.EqualTo(1));
            Assert.That(hasGap, Is.False);
            Assert.That(output[0].Sequence, Is.EqualTo(4));
            cursor.Commit(output[0]);
            Assert.That(cursor.LastSeenSequence, Is.EqualTo(4));
        }

        [Test]
        public void SelectedAttackHitCursorDoesNotRepeatCommittedHitsAndResetReplaysTheCurrentBinding()
        {
            SelectedAttackHit first = CreateSelectedHit(1, 0);
            SelectedAttackHit second = CreateSelectedHit(2, 1);
            SelectedAttackHitStream stream = new SelectedAttackHitStream(4);
            SelectedAttackHitCursor cursor = new SelectedAttackHitCursor();
            SelectedAttackHit[] output = new SelectedAttackHit[2];

            Assert.That(stream.TryAppend(new[] { first }, 1).IsSuccess, Is.True);
            Assert.That(cursor.CopyUnread(stream, output), Is.EqualTo(1));
            Assert.That(output[0].ShotId, Is.EqualTo(first.ShotId));
            cursor.CommitOne();
            Assert.That(cursor.CopyUnread(stream, output), Is.Zero);

            Assert.That(stream.TryAppend(new[] { second }, 1).IsSuccess, Is.True);
            Assert.That(cursor.CopyUnread(stream, output), Is.EqualTo(1));
            Assert.That(output[0].ShotId, Is.EqualTo(second.ShotId));
            cursor.CommitOne();
            Assert.That(cursor.CopyUnread(stream, output), Is.Zero);

            cursor.Reset();
            Assert.That(cursor.ConsumedCount, Is.Zero);
            Assert.That(cursor.CopyUnread(stream, output), Is.EqualTo(2));
            Assert.That(output[0].ShotId, Is.EqualTo(first.ShotId));
            Assert.That(output[1].ShotId, Is.EqualTo(second.ShotId));
        }

        [Test]
        public void SelectedAttackHitCursorTreatsAShorterSourceAsASafeRebindReset()
        {
            SelectedAttackHitStream firstBinding = new SelectedAttackHitStream(4);
            SelectedAttackHitCursor cursor = new SelectedAttackHitCursor();
            SelectedAttackHit[] output = new SelectedAttackHit[2];

            Assert.That(firstBinding.TryAppend(new[]
            {
                CreateSelectedHit(1, 0),
                CreateSelectedHit(2, 1)
            }, 2).IsSuccess, Is.True);
            Assert.That(cursor.CopyUnread(firstBinding, output), Is.EqualTo(2));
            cursor.CommitOne();
            cursor.CommitOne();
            Assert.That(cursor.ConsumedCount, Is.EqualTo(2));

            SelectedAttackHit reboundHit = CreateSelectedHit(3, 0);
            SelectedAttackHitStream reboundBinding = new SelectedAttackHitStream(4);
            Assert.That(reboundBinding.TryAppend(new[] { reboundHit }, 1).IsSuccess, Is.True);

            Assert.That(cursor.CopyUnread(reboundBinding, output), Is.EqualTo(1));
            Assert.That(cursor.ConsumedCount, Is.Zero);
            Assert.That(output[0].ShotId, Is.EqualTo(reboundHit.ShotId));
            cursor.CommitOne();
            Assert.That(cursor.CopyUnread(reboundBinding, output), Is.Zero);
        }

        private static CombatEvent RecordTraceEvent(CombatTrace trace, int tick)
        {
            return trace.Record(
                new TickIndex(tick),
                CombatEventType.InputAccepted,
                RuntimeId.Invalid,
                RuntimeId.Invalid,
                AttackId.Invalid,
                ImpactId.Invalid,
                0,
                0);
        }

        private static SelectedAttackHit CreateSelectedHit(int shotValue, int impactOrdinal)
        {
            return new SelectedAttackHit(
                new AttackId(shotValue),
                new ShotId(shotValue),
                new TickIndex(0),
                impactOrdinal,
                AttackQueryStage.Direct,
                -1,
                new RuntimeId(2),
                QueryTargetKind.Combatant,
                HitPart.Body,
                new GeometryId(10 + impactOrdinal),
                new SpatialVectorKey(impactOrdinal, 0, 100));
        }
    }
}
