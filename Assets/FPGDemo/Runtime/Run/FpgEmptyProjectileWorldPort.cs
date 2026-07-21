using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    /// <summary>
    /// Explicit non-spatial projectile world for pure tests. Production code
    /// must pass its real IProjectileWorldPort; unlike NullProjectileWorldPort,
    /// this test port owns a valid frozen path and acknowledges release.
    /// </summary>
    public sealed class FpgEmptyProjectileWorldPort : IProjectileWorldPort
    {
        public DomainResult Register(
            in ProjectileSpawnRequest request,
            out ProjectilePathSnapshot path)
        {
            path = new ProjectilePathSnapshot(
                request.ProjectileId,
                request.RuntimeId,
                request.Tick,
                request.ArrivalTick,
                default(SpatialVectorKey),
                default(SpatialVectorKey));
            return DomainResult.Success;
        }

        public DomainResult Sweep(
            in ProjectileSweepRequest request,
            out ProjectileSweepHit hit)
        {
            hit = ProjectileSweepHit.None;
            return DomainResult.Success;
        }

        public DomainResult Release(in ProjectileReleaseRequest request)
        {
            return DomainResult.Success;
        }
    }
}
