using UnityEngine;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// Complete authored player entity root. Scene services inject session and
    /// room dependencies; the prefab owns the player hierarchy and hitbox.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class D0PlayerEntityView : D0ActorEntityView
    {
        [Header("Player gameplay components")]
        [SerializeField]
        private CharacterController characterController;

        [SerializeField]
        private CombatLabPlayerController controller;

        [SerializeField]
        private CombatLabPlayerBounds bounds;

        [SerializeField]
        private Transform aimAnchor;

        [SerializeField]
        private Transform groundAnchor;

        [SerializeField]
        private Transform cameraPivot;

        [SerializeField]
        private Collider bodyHitbox;

        [SerializeField]
        private D0PlayerBarrierPresentationController barrier;

        public CharacterController CharacterController => characterController != null
            ? characterController
            : GetComponent<CharacterController>();
        public CombatLabPlayerController Controller => controller != null
            ? controller
            : GetComponent<CombatLabPlayerController>();
        public CombatLabPlayerController PlayerController => Controller;
        public CombatLabPlayerBounds Bounds => bounds != null
            ? bounds
            : GetComponent<CombatLabPlayerBounds>();
        public CombatLabPlayerBounds PlayerBounds => Bounds;
        public Transform AimAnchor => aimAnchor;
        public Transform GroundAnchor => groundAnchor;
        public Transform CameraPivot => cameraPivot;
        public Collider BodyHitbox => bodyHitbox;
        public Collider BodyCollider => bodyHitbox;
        public D0PlayerBarrierPresentationController Barrier => barrier != null
            ? barrier
            : GetComponentInChildren<D0PlayerBarrierPresentationController>(true);
        public D0PlayerBarrierPresentationController BarrierPresentation => Barrier;

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

            if (Controller == null || Controller.transform != transform)
            {
                error = "Player entity requires CombatLabPlayerController on the entity root.";
                return false;
            }

            if (Bounds == null || Bounds.transform != transform)
            {
                error = "Player entity requires CombatLabPlayerBounds on the entity root.";
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
                error = "Player entity requires a D0PlayerBarrierPresentationController.";
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
