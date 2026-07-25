using System;
using System.Collections.Generic;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Fixed-capacity RuntimeId to presentation/gameplay anchor map used by
    /// the formal multi-enemy path. A dead actor can retain its last pose for
    /// a short lease so delayed effects never attach to a recycled entity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgCombatantAnchorMap : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int capacity = 32;

        private Dictionary<RuntimeId, AnchorRecord> records;
        private AnchorRecord[] leaseScratch;
        private bool initialized;

        public int Capacity => capacity;
        public int Count => records == null ? 0 : records.Count;
        public bool IsInitialized => initialized;

        private void Awake()
        {
            TryInitialize(out _);
        }

        public bool TryInitialize(out string error)
        {
            if (capacity <= 0)
            {
                error = "Formal combatant anchor capacity must be positive.";
                initialized = false;
                return false;
            }

            records = new Dictionary<RuntimeId, AnchorRecord>(capacity);
            leaseScratch = new AnchorRecord[capacity];
            initialized = true;
            error = string.Empty;
            return true;
        }

        public bool TryRegister(
            RuntimeId runtimeId,
            Transform gameplayAnchor,
            Transform projectileAnchor,
            Transform weakpointAnchor,
            GameObject actor,
            out string error)
        {
            return TryRegister(
                runtimeId,
                gameplayAnchor,
                projectileAnchor,
                weakpointAnchor,
                actor,
                null,
                out error);
        }

        public bool TryRegister(
            RuntimeId runtimeId,
            Transform gameplayAnchor,
            Transform projectileAnchor,
            Transform weakpointAnchor,
            GameObject actor,
            D0ActorSocketRegistry socketRegistry,
            out string error)
        {
            if (!initialized && !TryInitialize(out error))
            {
                return false;
            }

            if (!runtimeId.IsValid)
            {
                error = "Formal anchor registration requires a valid RuntimeId.";
                return false;
            }

            if (gameplayAnchor == null || actor == null)
            {
                error = "Formal anchor registration requires gameplay anchor and actor references.";
                return false;
            }

            if (!records.ContainsKey(runtimeId) && records.Count >= capacity)
            {
                error = "Formal combatant anchor capacity is exhausted.";
                return false;
            }

            AnchorRecord record = new AnchorRecord(
                runtimeId,
                gameplayAnchor,
                projectileAnchor,
                weakpointAnchor,
                actor,
                socketRegistry,
                gameplayAnchor,
                0);
            records[runtimeId] = record;
            error = string.Empty;
            return true;
        }

        public bool TryGet(RuntimeId runtimeId, out FpgCombatantAnchorSnapshot snapshot)
        {
            if (records != null && records.TryGetValue(runtimeId, out AnchorRecord record))
            {
                snapshot = record.ToSnapshot();
                return true;
            }

            snapshot = default(FpgCombatantAnchorSnapshot);
            return false;
        }

        public bool TryUpdatePose(RuntimeId runtimeId)
        {
            if (records == null || !records.TryGetValue(runtimeId, out AnchorRecord record))
            {
                return false;
            }

            Transform source = record.GameplayAnchor;
            if (source == null)
            {
                return false;
            }

            record.LastPose = new Pose(source.position, source.rotation);
            records[runtimeId] = record;
            return true;
        }

        public bool TryRetainPresentationLease(RuntimeId runtimeId, int leaseTicks)
        {
            if (leaseTicks < 0
                || records == null
                || !records.TryGetValue(runtimeId, out AnchorRecord record))
            {
                return false;
            }

            if (record.GameplayAnchor != null)
            {
                record.LastPose = new Pose(
                    record.GameplayAnchor.position,
                    record.GameplayAnchor.rotation);
            }

            record.GameplayAnchor = null;
            record.ProjectileAnchor = null;
            record.WeakpointAnchor = null;
            record.Actor = null;
            record.SocketRegistry = null;
            record.LeaseTicksRemaining = leaseTicks;
            records[runtimeId] = record;
            if (leaseTicks == 0)
            {
                records.Remove(runtimeId);
            }

            return true;
        }

        public bool TryUnregister(RuntimeId runtimeId, bool retainPresentation, int leaseTicks)
        {
            if (records == null || !records.ContainsKey(runtimeId))
            {
                return false;
            }

            if (retainPresentation)
            {
                return TryRetainPresentationLease(runtimeId, leaseTicks);
            }

            return records.Remove(runtimeId);
        }

        /// <summary>
        /// Advances presentation leases without allocating or changing pool
        /// capacity. Call once per formal battle tick after impacts.
        /// </summary>
        public void TickPresentationLeases()
        {
            if (records == null || records.Count == 0)
            {
                return;
            }

            int leasedCount = 0;
            foreach (KeyValuePair<RuntimeId, AnchorRecord> pair in records)
            {
                AnchorRecord record = pair.Value;
                if (record.LeaseTicksRemaining <= 0)
                {
                    continue;
                }

                record.LeaseTicksRemaining--;
                leaseScratch[leasedCount++] = record;
            }

            // Apply changes only after enumeration has completed. Both value
            // replacement and removal invalidate Dictionary enumerators.
            for (int index = 0; index < leasedCount; index++)
            {
                AnchorRecord record = leaseScratch[index];
                if (record.LeaseTicksRemaining == 0)
                {
                    records.Remove(record.RuntimeId);
                }
                else
                {
                    records[record.RuntimeId] = record;
                }

                leaseScratch[index] = default(AnchorRecord);
            }
        }

        public void Clear()
        {
            records?.Clear();
        }

        private void OnDestroy()
        {
            Clear();
            initialized = false;
        }

        private struct AnchorRecord
        {
            public AnchorRecord(
                RuntimeId runtimeId,
                Transform gameplayAnchor,
                Transform projectileAnchor,
                Transform weakpointAnchor,
                GameObject actor,
                D0ActorSocketRegistry socketRegistry,
                Transform source,
                int leaseTicksRemaining)
            {
                RuntimeId = runtimeId;
                GameplayAnchor = gameplayAnchor;
                ProjectileAnchor = projectileAnchor;
                WeakpointAnchor = weakpointAnchor;
                Actor = actor;
                SocketRegistry = socketRegistry;
                LastPose = source == null
                    ? default(Pose)
                    : new Pose(source.position, source.rotation);
                LeaseTicksRemaining = leaseTicksRemaining;
            }

            public RuntimeId RuntimeId;
            public Transform GameplayAnchor;
            public Transform ProjectileAnchor;
            public Transform WeakpointAnchor;
            public GameObject Actor;
            public D0ActorSocketRegistry SocketRegistry;
            public Pose LastPose;
            public int LeaseTicksRemaining;

            public FpgCombatantAnchorSnapshot ToSnapshot()
            {
                return new FpgCombatantAnchorSnapshot(
                    RuntimeId,
                    GameplayAnchor,
                    ProjectileAnchor,
                    WeakpointAnchor,
                    Actor,
                    SocketRegistry,
                    LastPose,
                    LeaseTicksRemaining);
            }
        }
    }

    public readonly struct FpgCombatantAnchorSnapshot
    {
        internal FpgCombatantAnchorSnapshot(
            RuntimeId runtimeId,
            Transform gameplayAnchor,
            Transform projectileAnchor,
            Transform weakpointAnchor,
            GameObject actor,
            D0ActorSocketRegistry socketRegistry,
            Pose lastPose,
            int leaseTicksRemaining)
        {
            RuntimeId = runtimeId;
            GameplayAnchor = gameplayAnchor;
            ProjectileAnchor = projectileAnchor;
            WeakpointAnchor = weakpointAnchor;
            Actor = actor;
            SocketRegistry = socketRegistry;
            LastPose = lastPose;
            LeaseTicksRemaining = leaseTicksRemaining;
        }

        public RuntimeId RuntimeId { get; }
        public Transform GameplayAnchor { get; }
        public Transform ProjectileAnchor { get; }
        public Transform WeakpointAnchor { get; }
        public GameObject Actor { get; }
        public D0ActorSocketRegistry SocketRegistry { get; }
        public Pose LastPose { get; }
        public int LeaseTicksRemaining { get; }
        public bool IsPresentationLeaseActive => LeaseTicksRemaining > 0;
    }
}

