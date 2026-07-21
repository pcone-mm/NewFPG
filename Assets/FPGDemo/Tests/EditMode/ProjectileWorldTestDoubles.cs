using System;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using FPG.Demo.Run;

namespace FPG.Demo.Tests.EditMode
{
    internal enum ScriptedProjectileSweepMode
    {
        None = 0,
        TargetAtArrival,
        TargetAtFirstSweep,
        EnvironmentAtFirstSweep
    }

    internal sealed class ScriptedProjectileWorldPort : IProjectileWorldPort
    {
        private const int Capacity = 128;

        private readonly ProjectileSpawnRequest[] registrations = new ProjectileSpawnRequest[Capacity];
        private readonly bool[] activeRegistrations = new bool[Capacity];
        private readonly ProjectileSpawnRequest[] registerCalls = new ProjectileSpawnRequest[Capacity];
        private readonly ProjectileSweepRequest[] sweepCalls = new ProjectileSweepRequest[Capacity];
        private readonly ProjectileReleaseRequest[] releaseCalls = new ProjectileReleaseRequest[Capacity];

        public ScriptedProjectileWorldPort(
            ScriptedProjectileSweepMode sweepMode = ScriptedProjectileSweepMode.TargetAtArrival)
        {
            SweepMode = sweepMode;
        }

        public ScriptedProjectileSweepMode SweepMode { get; set; }
        public int FailRegisterCall { get; set; } = -1;
        public int FailReleaseCall { get; set; } = -1;
        public bool ReturnMismatchedPath { get; set; }
        public int RegisterCount { get; private set; }
        public int SweepCount { get; private set; }
        public int ReleaseCount { get; private set; }

        public int ActiveRegistrationCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < activeRegistrations.Length; index++)
                {
                    if (activeRegistrations[index])
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public ProjectileSpawnRequest GetRegisterCall(int index) => registerCalls[index];
        public ProjectileSweepRequest GetSweepCall(int index) => sweepCalls[index];
        public ProjectileReleaseRequest GetReleaseCall(int index) => releaseCalls[index];

        public DomainResult Register(
            in ProjectileSpawnRequest request,
            out ProjectilePathSnapshot path)
        {
            if (RegisterCount >= registerCalls.Length)
            {
                path = default(ProjectilePathSnapshot);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            registerCalls[RegisterCount++] = request;
            if (RegisterCount == FailRegisterCall)
            {
                path = default(ProjectilePathSnapshot);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            int slot = FindRegistration(request.RuntimeId);
            if (slot >= 0)
            {
                path = default(ProjectilePathSnapshot);
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            slot = FindFreeRegistration();
            if (slot < 0)
            {
                path = default(ProjectilePathSnapshot);
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            registrations[slot] = request;
            activeRegistrations[slot] = true;
            long duration = request.ArrivalTick - request.Tick;
            int endZ = checked((int)Math.Min(int.MaxValue, Math.Max(1L, duration) * 1000L));
            path = new ProjectilePathSnapshot(
                request.ProjectileId,
                request.RuntimeId,
                request.Tick,
                request.ArrivalTick,
                SpatialVectorKey.Zero,
                new SpatialVectorKey(0, 0, endZ));
            if (ReturnMismatchedPath)
            {
                path = new ProjectilePathSnapshot(
                    request.ProjectileId,
                    new RuntimeId(request.RuntimeId.Value + 1000L),
                    request.Tick,
                    request.ArrivalTick,
                    SpatialVectorKey.Zero,
                    new SpatialVectorKey(0, 0, endZ));
            }

            return DomainResult.Success;
        }

        public DomainResult Sweep(
            in ProjectileSweepRequest request,
            out ProjectileSweepHit hit)
        {
            if (SweepCount >= sweepCalls.Length)
            {
                hit = ProjectileSweepHit.None;
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            sweepCalls[SweepCount++] = request;
            int slot = FindRegistration(request.RuntimeId);
            if (slot < 0
                || registrations[slot].ProjectileId != request.ProjectileId
                || registrations[slot].SweepRadiusKey != request.SweepRadiusKey)
            {
                hit = ProjectileSweepHit.None;
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            bool firstSweep = CountSweepsFor(request.RuntimeId) == 1;
            switch (SweepMode)
            {
                case ScriptedProjectileSweepMode.TargetAtArrival:
                    if (request.Tick >= registrations[slot].ArrivalTick)
                    {
                        hit = CreateTargetHit(slot, request.To);
                        return DomainResult.Success;
                    }
                    break;

                case ScriptedProjectileSweepMode.TargetAtFirstSweep:
                    if (firstSweep)
                    {
                        hit = CreateTargetHit(slot, request.To);
                        return DomainResult.Success;
                    }
                    break;

                case ScriptedProjectileSweepMode.EnvironmentAtFirstSweep:
                    if (firstSweep)
                    {
                        hit = ProjectileSweepHit.EnvironmentBlocked(
                            new GeometryId(slot + 1),
                            1,
                            request.To);
                        return DomainResult.Success;
                    }
                    break;
            }

            hit = ProjectileSweepHit.None;
            return DomainResult.Success;
        }

        public DomainResult Release(in ProjectileReleaseRequest request)
        {
            if (ReleaseCount >= releaseCalls.Length)
            {
                return DomainResult.Rejected(RejectReason.BufferCapacity);
            }

            releaseCalls[ReleaseCount++] = request;
            int slot = FindRegistration(request.RuntimeId);
            if (slot < 0 || registrations[slot].ProjectileId != request.ProjectileId)
            {
                return DomainResult.Rejected(RejectReason.InvalidTarget);
            }

            if (ReleaseCount == FailReleaseCall)
            {
                return DomainResult.Rejected(RejectReason.InvalidState);
            }

            activeRegistrations[slot] = false;
            return DomainResult.Success;
        }

        private ProjectileSweepHit CreateTargetHit(int registrationSlot, SpatialVectorKey point)
        {
            return ProjectileSweepHit.Target(
                registrations[registrationSlot].TargetId,
                HitPart.Body,
                new GeometryId(registrationSlot + 1),
                1,
                point);
        }

        private int CountSweepsFor(RuntimeId runtimeId)
        {
            int count = 0;
            for (int index = 0; index < SweepCount; index++)
            {
                if (sweepCalls[index].RuntimeId == runtimeId)
                {
                    count++;
                }
            }

            return count;
        }

        private int FindRegistration(RuntimeId runtimeId)
        {
            for (int index = 0; index < registrations.Length; index++)
            {
                if (activeRegistrations[index] && registrations[index].RuntimeId == runtimeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeRegistration()
        {
            for (int index = 0; index < activeRegistrations.Length; index++)
            {
                if (!activeRegistrations[index])
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
