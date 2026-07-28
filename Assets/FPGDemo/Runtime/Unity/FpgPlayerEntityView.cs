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
        private Transform groundAnchor;

        [SerializeField]
        private Transform cameraPivot;

        [SerializeField]
        private Collider bodyHitbox;

        [SerializeField]
        private FpgPlayerBarrierPresentationController barrier;

        public CharacterController CharacterController => characterController != null
            ? characterController
            : GetComponent<CharacterController>();

        public FpgPlayerBounds Bounds => bounds != null
            ? bounds
            : GetComponent<FpgPlayerBounds>();
        public FpgPlayerBounds PlayerBounds => Bounds;
        public Transform AimAnchor => aimAnchor;
        public Transform GroundAnchor => groundAnchor;
        public Transform CameraPivot => cameraPivot;
        public Collider BodyHitbox => bodyHitbox;
        public Collider BodyCollider => bodyHitbox;
        public FpgPlayerBarrierPresentationController Barrier => barrier != null
            ? barrier
            : GetComponentInChildren<FpgPlayerBarrierPresentationController>(true);
        public FpgPlayerBarrierPresentationController BarrierPresentation => Barrier;

        public bool TryResolvePresentationSocket(
            string socketId,
            out Transform anchor)
        {
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

            Transform peekRoot = Barrier.PeekRoot;
            if (VisualRoot == peekRoot || !VisualRoot.IsChildOf(peekRoot))
            {
                error = "Player entity VisualRoot must be below the cover PeekRoot.";
                return false;
            }

            if (GameplayAnchor.IsChildOf(peekRoot)
                || AimAnchor.IsChildOf(peekRoot)
                || GroundAnchor.IsChildOf(peekRoot)
                || CameraPivot.IsChildOf(peekRoot)
                || SocketRegistry.transform.IsChildOf(peekRoot))
            {
                error = "Player gameplay anchors and authoritative sockets must remain outside PeekRoot.";
                return false;
            }

            if (AimAnchor == GroundAnchor || AimAnchor == CameraPivot
                || GroundAnchor == CameraPivot)
            {
                error = "Player entity AimAnchor, GroundAnchor and CameraPivot must be distinct Transforms.";
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
