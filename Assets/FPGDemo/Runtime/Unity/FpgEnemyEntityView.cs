using System;
using System.Collections.Generic;
using FPG.Demo.Combat;
using FPG.Demo.Core;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Formal entity view contract. It exposes explicit anchors and keeps all
    /// hit parts disabled until the encounter activation boundary.
    /// </summary>
    public sealed class FpgEnemyEntityView : MonoBehaviour, IFpgFormalEnemyEntityBinder
    {
        [SerializeField]
        private Transform gameplayAnchor;

        [SerializeField]
        private Transform projectileAnchor;

        [SerializeField]
        private Transform weakpointAnchor;

        [SerializeField]
        private Transform overheadHealthBarAnchor;

        [SerializeField]
        private Collider[] hitParts = Array.Empty<Collider>();

        [SerializeField]
        private HitPart[] hitPartKinds = Array.Empty<HitPart>();

        [NonSerialized]
        private string runtimeId;

        [NonSerialized]
        private int spawnSequence = -1;

        [NonSerialized]
        private bool gameplayEnabled;

        public Transform GameplayAnchor => gameplayAnchor == null ? transform : gameplayAnchor;
        public Transform ProjectileAnchor => projectileAnchor == null ? GameplayAnchor : projectileAnchor;
        public Transform WeakpointAnchor => weakpointAnchor == null ? GameplayAnchor : weakpointAnchor;
        public Transform OverheadHealthBarAnchor => overheadHealthBarAnchor == null
            ? GameplayAnchor
            : overheadHealthBarAnchor;
        public IReadOnlyList<Collider> HitParts => hitParts ?? Array.Empty<Collider>();
        public int HitPartCount => hitParts == null ? 0 : hitParts.Length;
        public string RuntimeId => runtimeId ?? string.Empty;
        public int SpawnSequence => spawnSequence;
        public bool GameplayEnabled => gameplayEnabled;

        public bool TryGetHitPart(
            int hitPartOrdinal,
            out Collider collider,
            out HitPart hitPart)
        {
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            if (hitPartOrdinal < 0 || hitPartOrdinal >= colliders.Length)
            {
                collider = null;
                hitPart = HitPart.Body;
                return false;
            }

            collider = colliders[hitPartOrdinal];
            HitPart[] kinds = hitPartKinds ?? Array.Empty<HitPart>();
            hitPart = kinds.Length == 0 ? HitPart.Body : kinds[hitPartOrdinal];
            return collider != null
                && Enum.IsDefined(typeof(HitPart), hitPart)
                && hitPart != HitPart.Projectile;
        }

        public void BindRuntime(string nextRuntimeId, int nextSpawnSequence)
        {
            if (string.IsNullOrWhiteSpace(nextRuntimeId))
            {
                throw new ArgumentException("Runtime id is required.", nameof(nextRuntimeId));
            }

            if (nextSpawnSequence < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nextSpawnSequence));
            }

            runtimeId = nextRuntimeId;
            spawnSequence = nextSpawnSequence;
        }

        public void ClearRuntimeBinding()
        {
            runtimeId = string.Empty;
            spawnSequence = -1;
            gameplayEnabled = false;
        }

        public bool TryBindFormalRuntime(
            RuntimeId nextRuntimeId,
            int nextSpawnSequence,
            FpgEnemyDefinition definition,
            out string error)
        {
            if (!nextRuntimeId.IsValid || definition == null || nextSpawnSequence < 0)
            {
                error = "Formal entity binding requires a valid runtime, sequence and definition.";
                return false;
            }

            if (!TryValidate(out error))
            {
                return false;
            }

            BindRuntime(nextRuntimeId.ToString(), nextSpawnSequence);
            SetFormalGameplayEnabled(false);
            error = string.Empty;
            return true;
        }

        public void SetFormalGameplayEnabled(bool enabled)
        {
            gameplayEnabled = enabled;
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = enabled;
                }
            }
        }

        public void UnbindFormalRuntime()
        {
            SetFormalGameplayEnabled(false);
            ClearRuntimeBinding();
        }

        public bool TryValidate(out string error)
        {
            Collider[] colliders = hitParts ?? Array.Empty<Collider>();
            HitPart[] kinds = hitPartKinds ?? Array.Empty<HitPart>();
            if (colliders.Length == 0)
            {
                error = "Formal enemy entity requires at least one hit part.";
                return false;
            }

            if (kinds.Length != 0 && kinds.Length != colliders.Length)
            {
                error = "Formal enemy entity hit-part kinds must be empty or parallel the Collider array.";
                return false;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] == null)
                {
                    error = $"Formal enemy entity hit part {index} is missing.";
                    return false;
                }

                HitPart kind = kinds.Length == 0 ? HitPart.Body : kinds[index];
                if (!Enum.IsDefined(typeof(HitPart), kind) || kind == HitPart.Projectile)
                {
                    error = $"Formal enemy entity hit part {index} has an invalid combatant kind.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static long DeriveGeometryId(int spawnSequence, int hitPartOrdinal)
        {
            return FpgFormalGeometryId.Derive(spawnSequence, hitPartOrdinal);
        }

        public static GeometryId DeriveCombatGeometryId(int spawnSequence, int hitPartOrdinal)
        {
            return FpgFormalGeometryId.DeriveCombatGeometryId(spawnSequence, hitPartOrdinal);
        }

        private void Awake()
        {
            SetFormalGameplayEnabled(false);
        }

        private void OnDisable()
        {
            SetFormalGameplayEnabled(false);
        }
    }
}
