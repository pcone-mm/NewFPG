using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// How an authored socket gets its transform pose at runtime.
    /// </summary>
    public enum D0ActorSocketFollowMode
    {
        AuthoredTransform = 0,
        Transform = 0,
        SpineBone = 1
    }

    /// <summary>
    /// One stable, string-addressed actor socket. The Transform remains an
    /// authored prefab object; optional Spine metadata only controls following.
    /// </summary>
    [Serializable]
    public sealed class D0ActorSocketBinding
    {
        [SerializeField]
        private string socketId;

        [SerializeField]
        private Transform anchor;

        [SerializeField]
        private D0ActorSocketFollowMode followMode;

        [SerializeField]
        private string boneName;

        public D0ActorSocketBinding()
        {
        }

        public D0ActorSocketBinding(
            string socketId,
            Transform anchor,
            D0ActorSocketFollowMode followMode = D0ActorSocketFollowMode.AuthoredTransform,
            string boneName = null)
        {
            this.socketId = socketId;
            this.anchor = anchor;
            this.followMode = followMode;
            this.boneName = boneName;
        }

        public string SocketId => socketId;
        public string Id => socketId;
        public Transform Anchor => anchor;
        public Transform Transform => anchor;
        public D0ActorSocketFollowMode FollowMode => followMode;
        public string BoneName => boneName;
        public string SpineBoneName => boneName;
        public bool FollowsSpineBone => followMode == D0ActorSocketFollowMode.SpineBone;

        internal D0ActorSocketBinding Clone()
        {
            return new D0ActorSocketBinding(socketId, anchor, followMode, boneName);
        }
    }

    /// <summary>
    /// Stable string socket registry owned by an actor Entity Prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0ActorSocketRegistry : MonoBehaviour
    {
        public const string PrimaryMuzzleId = "weapon.primary.muzzle";
        public const string SecondaryMuzzleId = "weapon.secondary.muzzle";
        public const string DefaultAttackOriginId = "attack.default.origin";
        public const string PrimaryMuzzle = PrimaryMuzzleId;
        public const string SecondaryMuzzle = SecondaryMuzzleId;
        public const string DefaultAttackOrigin = DefaultAttackOriginId;

        [SerializeField]
        private List<D0ActorSocketBinding> bindings =
            new List<D0ActorSocketBinding>();

        public IReadOnlyList<D0ActorSocketBinding> Bindings => bindings;
        public IReadOnlyList<D0ActorSocketBinding> SocketBindings => bindings;
        public int Count => bindings == null ? 0 : bindings.Count;

        public bool TryResolve(string socketId, out Transform anchor)
        {
            if (TryGetBinding(socketId, out D0ActorSocketBinding binding))
            {
                anchor = binding.Anchor;
                return anchor != null;
            }

            anchor = null;
            return false;
        }

        public bool TryResolve(
            string socketId,
            out Transform anchor,
            out D0ActorSocketBinding binding)
        {
            if (TryGetBinding(socketId, out binding))
            {
                anchor = binding.Anchor;
                return anchor != null;
            }

            anchor = null;
            binding = null;
            return false;
        }

        public bool TryGetBinding(
            string socketId,
            out D0ActorSocketBinding binding)
        {
            List<D0ActorSocketBinding> source = bindings;
            if (source != null && !string.IsNullOrEmpty(socketId))
            {
                for (int index = 0; index < source.Count; index++)
                {
                    D0ActorSocketBinding candidate = source[index];
                    if (candidate != null
                        && string.Equals(
                            candidate.SocketId,
                            socketId,
                            StringComparison.Ordinal))
                    {
                        binding = candidate;
                        return true;
                    }
                }
            }

            binding = null;
            return false;
        }

        public bool TryRegister(
            string socketId,
            Transform anchor,
            out string error)
        {
            return TryRegister(
                new D0ActorSocketBinding(socketId, anchor),
                out error);
        }

        public bool TryRegister(
            string socketId,
            Transform anchor,
            D0ActorSocketFollowMode followMode,
            string boneName,
            out string error)
        {
            return TryRegister(
                new D0ActorSocketBinding(socketId, anchor, followMode, boneName),
                out error);
        }

        public bool TryRegister(
            D0ActorSocketBinding binding,
            out string error)
        {
            if (!TryValidateBinding(binding, out error))
            {
                return false;
            }

            if (TryGetBinding(binding.SocketId, out _))
            {
                error = $"Actor socket '{binding.SocketId}' is duplicated.";
                return false;
            }

            List<D0ActorSocketBinding> target = bindings
                ?? (bindings = new List<D0ActorSocketBinding>());
            for (int index = 0; index < target.Count; index++)
            {
                if (target[index] != null && target[index].Anchor == binding.Anchor)
                {
                    error = $"Actor socket Transform '{binding.Anchor.name}' is already registered.";
                    return false;
                }
            }

            target.Add(binding);
            error = string.Empty;
            return true;
        }

        public bool TryUnregister(string socketId)
        {
            if (bindings == null || string.IsNullOrEmpty(socketId))
            {
                return false;
            }

            for (int index = 0; index < bindings.Count; index++)
            {
                D0ActorSocketBinding binding = bindings[index];
                if (binding != null
                    && string.Equals(binding.SocketId, socketId, StringComparison.Ordinal))
                {
                    bindings.RemoveAt(index);
                    return true;
                }
            }

            return false;
        }

        public bool TryValidate(out string error)
        {
            if (bindings == null)
            {
                error = "Actor socket registry bindings cannot be null.";
                return false;
            }

            HashSet<string> socketIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<Transform> anchors = new HashSet<Transform>();
            for (int index = 0; index < bindings.Count; index++)
            {
                D0ActorSocketBinding binding = bindings[index];
                if (!TryValidateBinding(binding, out error))
                {
                    return false;
                }

                if (!socketIds.Add(binding.SocketId))
                {
                    error = $"Actor socket '{binding.SocketId}' is duplicated.";
                    return false;
                }

                if (!anchors.Add(binding.Anchor))
                {
                    error = $"Actor socket Transform '{binding.Anchor.name}' is duplicated.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryReplaceBindings(
            IEnumerable<D0ActorSocketBinding> source,
            out string error)
        {
            List<D0ActorSocketBinding> replacement =
                new List<D0ActorSocketBinding>();
            if (source != null)
            {
                foreach (D0ActorSocketBinding binding in source)
                {
                    replacement.Add(binding == null ? null : binding.Clone());
                }
            }

            List<D0ActorSocketBinding> previous = bindings;
            bindings = replacement;
            if (!TryValidate(out error))
            {
                bindings = previous;
                return false;
            }

            return true;
        }

        private bool TryValidateBinding(
            D0ActorSocketBinding binding,
            out string error)
        {
            if (binding == null)
            {
                error = "Actor socket registry cannot contain a null binding.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(binding.SocketId)
                || !string.Equals(binding.SocketId, binding.SocketId.Trim(), StringComparison.Ordinal))
            {
                error = "Actor socket id must be non-empty and must not contain leading or trailing whitespace.";
                return false;
            }

            if (binding.Anchor == null)
            {
                error = $"Actor socket '{binding.SocketId}' has no Transform anchor.";
                return false;
            }

            if (binding.Anchor == transform || !binding.Anchor.IsChildOf(transform))
            {
                error = $"Actor socket '{binding.SocketId}' anchor must be below the registry root.";
                return false;
            }

            if (binding.FollowMode != D0ActorSocketFollowMode.AuthoredTransform
                && binding.FollowMode != D0ActorSocketFollowMode.SpineBone)
            {
                error = "Actor socket follow mode is unsupported.";
                return false;
            }

            if (binding.FollowMode == D0ActorSocketFollowMode.SpineBone
                && string.IsNullOrWhiteSpace(binding.BoneName))
            {
                error = $"Actor socket '{binding.SocketId}' requires a Spine bone name when SpineBone follow mode is selected.";
                return false;
            }

            if (binding.FollowMode != D0ActorSocketFollowMode.SpineBone
                && !string.IsNullOrWhiteSpace(binding.BoneName))
            {
                error = $"Actor socket '{binding.SocketId}' has bone metadata but does not use SpineBone follow mode.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
