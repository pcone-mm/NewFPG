using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    public readonly struct FpgExitSelectionEvent
    {
        public FpgExitSelectionEvent(
            GeometryId geometryId,
            FpgExitOffer offer,
            TickIndex tick)
        {
            GeometryId = geometryId;
            Offer = offer ?? throw new ArgumentNullException(nameof(offer));
            Tick = tick;
        }

        public GeometryId GeometryId { get; }
        public FpgExitOffer Offer { get; }
        public TickIndex Tick { get; }
    }

    public sealed class FpgExitAttackRegistry
    {
        public const int GeometryIdStart = 95000;
        public const int GeometryIdEndExclusive = 96000;

        private readonly Dictionary<int, ExitBinding> byGeometry =
            new Dictionary<int, ExitBinding>();
        private readonly List<Collider> registeredColliders =
            new List<Collider>();
        private HitboxRegistry hitboxRegistry;

        public int Count => byGeometry.Count;

        public bool TryRegisterRuntime(
            FpgRoomExitRuntime runtime,
            HitboxRegistry registry,
            ref int nextGeometryValue,
            out string error)
        {
            if (runtime == null
                || runtime.State != FpgRoomExitRuntimeState.Available
                || runtime.Offer == null
                || registry == null)
            {
                error = "Exit attack registration requires an available exit and hitbox registry.";
                return false;
            }

            if (hitboxRegistry != null && !ReferenceEquals(hitboxRegistry, registry))
            {
                error = "Exit attacks cannot span multiple hitbox registries.";
                return false;
            }

            IReadOnlyList<Collider> colliders = runtime.AttackColliders;
            for (int index = 0; index < colliders.Count; index++)
            {
                Collider collider = colliders[index];
                if (collider == null)
                {
                    continue;
                }

                if (nextGeometryValue < GeometryIdStart
                    || nextGeometryValue >= GeometryIdEndExclusive)
                {
                    error = "Exit GeometryId capacity is exhausted.";
                    return false;
                }

                GeometryId geometryId = new GeometryId(nextGeometryValue++);
                DomainResult registered = registry.Register(
                    new HitboxBinding(
                        collider,
                        HitboxTargetReference.Environment,
                        QueryTargetKind.EnvironmentBlocker,
                        HitPart.Body,
                        geometryId));
                if (!registered.IsSuccess)
                {
                    error = "Exit collider registration failed.";
                    return false;
                }

                byGeometry.Add(geometryId.Value, new ExitBinding(runtime));
                registeredColliders.Add(collider);
                hitboxRegistry = registry;
            }

            error = string.Empty;
            return true;
        }

        public bool TryGetAvailableOffer(
            GeometryId geometryId,
            out FpgExitOffer offer)
        {
            offer = null;
            if (!geometryId.IsValid
                || !byGeometry.TryGetValue(geometryId.Value, out ExitBinding binding)
                || binding.Runtime == null
                || binding.Runtime.State != FpgRoomExitRuntimeState.Available)
            {
                return false;
            }

            offer = binding.Runtime.Offer;
            return offer != null && offer.IsValid;
        }

        public bool TryGetRuntime(
            GeometryId geometryId,
            out FpgRoomExitRuntime runtime)
        {
            runtime = null;
            if (!byGeometry.TryGetValue(geometryId.Value, out ExitBinding binding))
            {
                return false;
            }

            runtime = binding.Runtime;
            return runtime != null;
        }

        public bool TryFindFirstVisibleExit(
            QueryCandidate[] candidates,
            int candidateCount,
            out GeometryId geometryId)
        {
            geometryId = GeometryId.Invalid;
            if (candidates == null
                || candidateCount < 0
                || candidateCount > candidates.Length)
            {
                return false;
            }

            bool found = false;
            QueryCandidate selected = default(QueryCandidate);
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (!IsRaySurface(candidate)
                    || !TryGetAvailableOffer(candidate.GeometryId, out _)
                    || HasEarlierBlocker(candidates, candidateCount, candidate))
                {
                    continue;
                }

                if (!found || CompareVisibleExit(candidate, selected) < 0)
                {
                    selected = candidate;
                    found = true;
                }
            }

            if (found)
            {
                geometryId = selected.GeometryId;
            }

            return found;
        }

        public void Clear()
        {
            if (hitboxRegistry != null)
            {
                for (int index = 0; index < registeredColliders.Count; index++)
                {
                    Collider collider = registeredColliders[index];
                    if (collider != null)
                    {
                        hitboxRegistry.Unregister(collider);
                    }
                }
            }

            registeredColliders.Clear();
            byGeometry.Clear();
            hitboxRegistry = null;
        }

        private static bool HasEarlierBlocker(
            QueryCandidate[] candidates,
            int candidateCount,
            in QueryCandidate target)
        {
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCandidate candidate = candidates[index];
                if (!IsRaySurface(candidate)
                    || candidate.QueryStage != target.QueryStage
                    || candidate.SampleIndex != target.SampleIndex)
                {
                    continue;
                }

                if (CompareSurface(candidate, target) < 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRaySurface(in QueryCandidate candidate)
        {
            return candidate.IsValid
                && candidate.TargetKind == QueryTargetKind.EnvironmentBlocker
                && (candidate.QueryStage == AttackQueryStage.Pellet
                    || candidate.QueryStage == AttackQueryStage.Direct);
        }

        private static int CompareSurface(
            in QueryCandidate left,
            in QueryCandidate right)
        {
            int distance = left.DistanceKey.CompareTo(right.DistanceKey);
            if (distance != 0)
            {
                return distance;
            }

            int ordinal = left.QueryOrdinal.CompareTo(right.QueryOrdinal);
            return ordinal != 0
                ? ordinal
                : left.GeometryId.Value.CompareTo(right.GeometryId.Value);
        }

        private static int CompareVisibleExit(
            in QueryCandidate left,
            in QueryCandidate right)
        {
            int stage = left.QueryStage.CompareTo(right.QueryStage);
            if (stage != 0)
            {
                return stage;
            }

            int sample = left.SampleIndex.CompareTo(right.SampleIndex);
            return sample != 0 ? sample : CompareSurface(left, right);
        }

        private readonly struct ExitBinding
        {
            public ExitBinding(FpgRoomExitRuntime runtime)
            {
                Runtime = runtime;
            }

            public FpgRoomExitRuntime Runtime { get; }
        }
    }
}
