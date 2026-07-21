using System;
using FPG.Demo.Core;

namespace FPG.Demo.Run
{
    public sealed class ObservingProjectileWorldPort : IProjectileWorldPort
    {
        private readonly IProjectileWorldPort inner;
        private readonly IProjectilePresentationFeedWriter feed;

        public ObservingProjectileWorldPort(
            IProjectileWorldPort inner,
            IProjectilePresentationFeedWriter feed)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.feed = feed ?? throw new ArgumentNullException(nameof(feed));
        }

        public IProjectilePresentationFeed Feed => feed;

        public int ObservationFaultCount { get; private set; }

        public DomainResult Register(in ProjectileSpawnRequest request, out ProjectilePathSnapshot path)
        {
            DomainResult result = inner.Register(request, out path);
            if (!result.IsSuccess)
            {
                return result;
            }

            try
            {
                if (!path.Matches(request) || !feed.TryRecordSpawn(request, path))
                {
                    ObservationFaultCount++;
                }
            }
            catch (Exception)
            {
                ObservationFaultCount++;
            }

            return result;
        }

        public DomainResult Sweep(in ProjectileSweepRequest request, out ProjectileSweepHit hit)
        {
            DomainResult result = inner.Sweep(request, out hit);
            if (!result.IsSuccess)
            {
                return result;
            }

            try
            {
                SpatialVectorKey lastPoint = hit.Kind == ProjectileSweepHitKind.None
                    ? request.To
                    : hit.Point;
                if (!hit.IsValid || !feed.TryUpdateLastPoint(request, lastPoint))
                {
                    ObservationFaultCount++;
                }
            }
            catch (Exception)
            {
                ObservationFaultCount++;
            }

            return result;
        }

        public DomainResult Release(in ProjectileReleaseRequest request)
        {
            DomainResult result = inner.Release(request);
            if (!result.IsSuccess)
            {
                return result;
            }

            try
            {
                if (!feed.TryRecordTerminal(request))
                {
                    ObservationFaultCount++;
                }
            }
            catch (Exception)
            {
                ObservationFaultCount++;
            }

            return result;
        }
    }
}
