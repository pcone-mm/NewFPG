using System;
using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Complete authored player entity root. The prefab owns the player hierarchy,
    /// presentation anchors and hitbox.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpgPlayerEntityView : D0ActorEntityView
    {
        [Header("Player gameplay components")]
        [SerializeField]
        private CharacterController characterController;

        [SerializeField]
        private FpgPlayerBounds bounds;

        [SerializeField]
        private Transform aimAnchor;

        [SerializeField]
        [Tooltip("Primary muzzle socket shared by authoritative shots and trajectory presentation. It may follow a Spine bone at runtime.")]
        private Transform shotOrigin;

        [SerializeField]
        private Transform groundAnchor;

        [SerializeField]
        private Transform cameraPivot;

        [SerializeField]
        private Collider bodyHitbox;

        [SerializeField]
        private FpgPlayerBarrierPresentationController barrier;

        [SerializeField]
        private FpgPlayerFacingController facingController;

        public CharacterController CharacterController => characterController != null
            ? characterController
            : GetComponent<CharacterController>();

        public FpgPlayerBounds Bounds => bounds != null
            ? bounds
            : GetComponent<FpgPlayerBounds>();
        public FpgPlayerBounds PlayerBounds => Bounds;
        public Transform AimAnchor => aimAnchor;
        public Transform ShotOrigin => shotOrigin;
        public Transform GroundAnchor => groundAnchor;
        public Transform CameraPivot => cameraPivot;
        public Collider BodyHitbox => bodyHitbox;
        public Collider BodyCollider => bodyHitbox;
        public FpgPlayerBarrierPresentationController Barrier => barrier != null
            ? barrier
            : GetComponentInChildren<FpgPlayerBarrierPresentationController>(true);
        public FpgPlayerBarrierPresentationController BarrierPresentation => Barrier;
        public FpgPlayerFacingController FacingController => facingController != null
            ? facingController
            : GetComponent<FpgPlayerFacingController>();
        public Transform FacingRoot => FacingController == null
            ? null
            : FacingController.FacingRoot;

        public bool TryResolvePresentationSocket(
            string socketId,
            out Transform anchor)
        {
            if (ShotOrigin != null
                && (string.IsNullOrEmpty(socketId)
                    || string.Equals(
                        socketId,
                        D0ActorSocketRegistry.PrimaryMuzzleId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        socketId,
                        D0ActorSocketRegistry.SecondaryMuzzleId,
                        StringComparison.Ordinal)))
            {
                anchor = ShotOrigin;
                return true;
            }

            FpgPlayerBarrierPresentationController cover = Barrier;
            if (cover != null
                && cover.TryResolvePresentationSocket(socketId, out anchor))
            {
                return true;
            }

            return TryResolveSocket(socketId, out anchor);
        }

        public override bool TryValidate(out string error)
        {
            if (!base.TryValidate(out error))
            {
                return false;
            }

            if (CharacterController == null)
            {
                error = "Player entity requires a CharacterController.";
                return false;
            }

            if (CharacterController.transform != transform)
            {
                error = "Player entity CharacterController must be attached to the entity root.";
                return false;
            }
            if (Bounds == null || Bounds.transform != transform)
            {
                error = "Player entity requires FpgPlayerBounds on the entity root.";
                return false;
            }

            if (AimAnchor == null || !AimAnchor.IsChildOf(transform))
            {
                error = "Player entity requires an AimAnchor below the entity root.";
                return false;
            }

            if (ShotOrigin == null || !ShotOrigin.IsChildOf(transform))
            {
                error = "Player entity requires a ShotOrigin below the entity root.";
                return false;
            }

            if (!SocketRegistry.TryResolve(
                    D0ActorSocketRegistry.PrimaryMuzzleId,
                    out Transform primaryMuzzle,
                    out D0ActorSocketBinding primaryMuzzleBinding)
                || primaryMuzzle != ShotOrigin)
            {
                error = "Player ShotOrigin must use the registered primary muzzle socket.";
                return false;
            }

            if (!primaryMuzzleBinding.FollowsSpineBone)
            {
                error = "Player primary muzzle ShotOrigin must follow a Spine bone.";
                return false;
            }

            if (GroundAnchor == null || !GroundAnchor.IsChildOf(transform))
            {
                error = "Player entity requires a GroundAnchor below the entity root.";
                return false;
            }

            if (CameraPivot == null || CameraPivot.parent != transform)
            {
                error = "Player entity CameraPivot must be a direct child of the entity root.";
                return false;
            }

            if (BodyHitbox == null || !BodyHitbox.transform.IsChildOf(GameplayAnchor))
            {
                error = "Player entity requires a body hitbox below GameplayAnchor.";
                return false;
            }

            if (Barrier == null)
            {
                error = "Player entity requires a FpgPlayerBarrierPresentationController.";
                return false;
            }

            if (!Barrier.TryValidate(out error))
            {
                return false;
            }

            FpgPlayerFacingController facing = FacingController;
            if (facing == null || facing.transform != transform
                || !facing.TryValidate(out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Player entity requires a valid FpgPlayerFacingController on the entity root."
                    : error;
                return false;
            }

            Transform peekRoot = Barrier.PeekRoot;
            Transform facingRoot = facing.FacingRoot;
            Transform presentationSockets =
                Barrier.PrimaryPresentationMuzzle == null
                    ? null
                    : Barrier.PrimaryPresentationMuzzle.parent;
            if (facingRoot == null || facingRoot.parent != peekRoot
                || facingRoot.localPosition != Vector3.zero
                || facingRoot.localRotation != Quaternion.identity
                || facingRoot.localScale != Vector3.one)
            {
                error = "Player FacingRoot must be a direct child of PeekRoot with an identity authored pose.";
                return false;
            }

            if (VisualRoot == null || VisualRoot.parent != facingRoot
                || presentationSockets == null
                || presentationSockets.parent != facingRoot
                || Barrier.SecondaryPresentationMuzzle == null
                || Barrier.SecondaryPresentationMuzzle.parent
                    != presentationSockets
                || facingRoot.childCount != 2
                || (facingRoot.GetChild(0) != VisualRoot
                    && facingRoot.GetChild(1) != VisualRoot)
                || (facingRoot.GetChild(0) != presentationSockets
                    && facingRoot.GetChild(1) != presentationSockets))
            {
                error = "Player FacingRoot must contain only VisualRoot and PresentationSockets as direct children.";
                return false;
            }
            if (VisualRoot == peekRoot || !VisualRoot.IsChildOf(peekRoot))
            {
                error = "Player entity VisualRoot must be below the cover PeekRoot.";
                return false;
            }

            if (GameplayAnchor.IsChildOf(facingRoot)
                || AimAnchor.IsChildOf(facingRoot)
                || ShotOrigin.IsChildOf(facingRoot)
                || GroundAnchor.IsChildOf(facingRoot)
                || CameraPivot.IsChildOf(facingRoot)
                || SocketRegistry.transform.IsChildOf(facingRoot))
            {
                error = "Player gameplay anchors and authoritative sockets must remain outside FacingRoot.";
                return false;
            }

            if (AimAnchor == ShotOrigin || AimAnchor == GroundAnchor
                || AimAnchor == CameraPivot || ShotOrigin == GroundAnchor
                || ShotOrigin == CameraPivot || GroundAnchor == CameraPivot)
            {
                error = "Player entity AimAnchor, ShotOrigin, GroundAnchor and CameraPivot must be distinct Transforms.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void SetGameplayCollidersEnabled(bool enabled)
        {
            if (bodyHitbox != null)
            {
                bodyHitbox.enabled = enabled;
            }
        }
    }
}
