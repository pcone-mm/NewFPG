using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Player;
using FPG.Demo.Run;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Selects the small, presentation-only subset of a committed player-shot
    /// snapshot used by the D0 2.5D visual layer. It deliberately consumes the
    /// frozen query record only: no Physics query, battle-state mutation, or
    /// visual-object ownership belongs here.
    /// </summary>
    public static class PlayerShotVisualAggregation
    {
        /// <summary>
        /// Selects one representative primary trajectory for a multi-pellet
        /// shot. Damage and hit resolution remain independent for every pellet;
        /// this selection only prevents the presentation layer from emitting a
        /// full-screen trail per pellet.
        ///
        /// The ordering is Weakpoint, Projectile, Body, EnvironmentBlocker,
        /// then Miss. Equal categories use the lower frozen sample index so the
        /// result is deterministic regardless of presentation timing.
        /// </summary>
        public static bool TryGetPrimaryRepresentative(
            in PlayerShotPresentationSnapshot snapshot,
            out PlayerShotTrajectory trajectory)
        {
            trajectory = default(PlayerShotTrajectory);
            if (snapshot.ReleaseKind != WeaponReleaseKind.Primary
                || snapshot.TrajectoryCount <= 0)
            {
                return false;
            }

            int bestPriority = 0;
            int bestSampleIndex = int.MaxValue;
            for (int index = 0; index < snapshot.TrajectoryCount; index++)
            {
                PlayerShotTrajectory candidate = snapshot.GetTrajectory(index);
                int candidatePriority = GetPrimaryPriority(candidate);
                if (candidatePriority > bestPriority
                    || candidatePriority == bestPriority
                        && candidate.SampleIndex < bestSampleIndex)
                {
                    trajectory = candidate;
                    bestPriority = candidatePriority;
                    bestSampleIndex = candidate.SampleIndex;
                }
            }

            return bestPriority > 0;
        }

        /// <summary>
        /// Returns the visual anchor for the secondary release's target-local
        /// burst. A direct combatant or projectile terminal gives the burst an
        /// exact frozen target point; misses and world blockers intentionally
        /// fall back to the committed secondary-area center.
        /// </summary>
        public static bool TryGetSecondaryBurstAnchor(
            in PlayerShotPresentationSnapshot snapshot,
            out SpatialVectorKey anchor)
        {
            anchor = default(SpatialVectorKey);
            if (snapshot.ReleaseKind != WeaponReleaseKind.Secondary
                || snapshot.TrajectoryCount <= 0)
            {
                return false;
            }

            PlayerShotTrajectory directTrajectory = snapshot.GetTrajectory(0);
            anchor = directTrajectory.TerminalKind == PlayerShotTerminalKind.Combatant
                || directTrajectory.TerminalKind == PlayerShotTerminalKind.Projectile
                ? directTrajectory.TerminalPoint
                : snapshot.SecondaryAreaCenter;
            return true;
        }

        private static int GetPrimaryPriority(in PlayerShotTrajectory trajectory)
        {
            switch (trajectory.TerminalKind)
            {
                case PlayerShotTerminalKind.Combatant:
                    return trajectory.HitPart == HitPart.Weakpoint ? 5 : 3;
                case PlayerShotTerminalKind.Projectile:
                    return 4;
                case PlayerShotTerminalKind.EnvironmentBlocker:
                    return 2;
                case PlayerShotTerminalKind.Miss:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
