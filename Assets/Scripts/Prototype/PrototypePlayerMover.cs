using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Prototype
{
    public sealed class PrototypePlayerMover : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private bool mirrorVisualByHorizontalInput;
        [SerializeField] private bool movementEnabled = true;
        [SerializeField] private bool clampToLevelBounds;
        [SerializeField] private Vector2 xBounds = new Vector2(-23.5f, 23.5f);
        [SerializeField] private Vector2 zBounds = new Vector2(-8.5f, 8.5f);

        private Vector2 lastFacing = Vector2.down;

        public Vector2 MoveInput { get; private set; }

        private void Reset()
        {
            visualRoot = transform.Find("Visual");
            animator = GetComponentInChildren<Animator>();
            mirrorVisualByHorizontalInput = false;
        }

        private void Update()
        {
            if (!movementEnabled)
            {
                MoveInput = Vector2.zero;
                UpdateAnimator();
                return;
            }

            MoveInput = ReadMovement();
            Vector3 delta = new Vector3(MoveInput.x, 0f, MoveInput.y) * (moveSpeed * Time.deltaTime);
            transform.position += delta;
            ClampToLevelBounds();

            if (MoveInput.sqrMagnitude > 0.001f)
            {
                lastFacing = MoveInput.normalized;
            }

            UpdateVisuals();
            UpdateAnimator();
        }

        private static Vector2 ReadMovement()
        {
            Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    input.x -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    input.x += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    input.y -= 1f;
                }

                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    input.y += 1f;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            input.x = Input.GetAxisRaw("Horizontal");
            input.y = Input.GetAxisRaw("Vertical");
#endif

            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (movementEnabled)
            {
                return;
            }

            MoveInput = Vector2.zero;
            UpdateAnimator();
        }

        private void ClampToLevelBounds()
        {
            if (!clampToLevelBounds)
            {
                return;
            }

            Vector3 position = transform.position;
            position.x = Mathf.Clamp(position.x, Mathf.Min(xBounds.x, xBounds.y), Mathf.Max(xBounds.x, xBounds.y));
            position.z = Mathf.Clamp(position.z, Mathf.Min(zBounds.x, zBounds.y), Mathf.Max(zBounds.x, zBounds.y));
            transform.position = position;
        }

        private void UpdateVisuals()
        {
            if (!mirrorVisualByHorizontalInput || visualRoot == null || MoveInput.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (lastFacing.x < -0.01f ? -1f : 1f);
            visualRoot.localScale = scale;
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat("MoveX", MoveInput.x);
            animator.SetFloat("MoveY", MoveInput.y);
            animator.SetFloat("LastX", lastFacing.x);
            animator.SetFloat("LastY", lastFacing.y);
            animator.SetFloat("Speed", MoveInput.magnitude);
        }
    }
}
