using UnityEngine;

namespace NewFPG.Prototype
{
    public sealed class PrototypeDirectionalSpriteAnimator : MonoBehaviour
    {
        private enum FacingDirection
        {
            Down,
            Left,
            Right,
            Up,
        }

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PrototypePlayerMover mover;
        [SerializeField] private float idleFramesPerSecond = 4f;
        [SerializeField] private float walkFramesPerSecond = 8f;
        [SerializeField] private Sprite[] idleDown;
        [SerializeField] private Sprite[] idleLeft;
        [SerializeField] private Sprite[] idleRight;
        [SerializeField] private Sprite[] idleUp;
        [SerializeField] private Sprite[] walkDown;
        [SerializeField] private Sprite[] walkLeft;
        [SerializeField] private Sprite[] walkRight;
        [SerializeField] private Sprite[] walkUp;

        private FacingDirection facing = FacingDirection.Down;
        private bool wasMoving;
        private int frameIndex;
        private float frameTimer;

        private void Reset()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            mover = GetComponentInParent<PrototypePlayerMover>();
        }

        private void Update()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Vector2 input = mover != null ? mover.MoveInput : Vector2.zero;
            bool isMoving = input.sqrMagnitude > 0.001f;
            FacingDirection nextFacing = isMoving ? ResolveFacing(input) : facing;

            if (nextFacing != facing || isMoving != wasMoving)
            {
                facing = nextFacing;
                wasMoving = isMoving;
                frameIndex = 0;
                frameTimer = 0f;
            }

            Sprite[] frames = GetFrames(facing, isMoving);
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            float fps = isMoving ? walkFramesPerSecond : idleFramesPerSecond;
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / fps)
            {
                frameTimer -= 1f / fps;
                frameIndex = (frameIndex + 1) % frames.Length;
            }

            spriteRenderer.sprite = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];
        }

        private static FacingDirection ResolveFacing(Vector2 input)
        {
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                return input.x < 0f ? FacingDirection.Left : FacingDirection.Right;
            }

            return input.y < 0f ? FacingDirection.Down : FacingDirection.Up;
        }

        private Sprite[] GetFrames(FacingDirection direction, bool isMoving)
        {
            switch (direction)
            {
                case FacingDirection.Left:
                    return isMoving ? walkLeft : idleLeft;
                case FacingDirection.Right:
                    return isMoving ? walkRight : idleRight;
                case FacingDirection.Up:
                    return isMoving ? walkUp : idleUp;
                default:
                    return isMoving ? walkDown : idleDown;
            }
        }
    }
}
