using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerCharacterController : MonoBehaviour
    {
        public enum MovementState
        {
            Idle,
            Walk,
        }

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0f)] private float acceleration = 40f;
        [SerializeField, Min(0f)] private float deceleration = 48f;
        [SerializeField] private bool normalizeDiagonalInput = true;
        [SerializeField] private bool invertHorizontalInput;
        [SerializeField] private bool invertDepthInput;
        [SerializeField] private bool movementEnabled = true;

        [Header("Visuals")]
        [SerializeField] private bool flipSpriteWithHorizontalMovement = true;
        [SerializeField] private bool spriteFacesRightByDefault = true;

        [Header("References")]
        [SerializeField] private Rigidbody body;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Animator Parameters")]
        [SerializeField] private string moveXParameter = "MoveX";
        [SerializeField] private string moveYParameter = "MoveY";
        [SerializeField] private string lastMoveXParameter = "LastMoveX";
        [SerializeField] private string lastMoveYParameter = "LastMoveY";
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string movementStateParameter = "MovementState";

        private Vector2 moveInput;
        private Vector2 velocity;
        private Vector2 lastMoveDirection = Vector2.down;
        private MovementState movementState;
        private int moveXHash;
        private int moveYHash;
        private int lastMoveXHash;
        private int lastMoveYHash;
        private int speedHash;
        private int isMovingHash;
        private int movementStateHash;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        public float Acceleration
        {
            get => acceleration;
            set => acceleration = Mathf.Max(0f, value);
        }

        public float Deceleration
        {
            get => deceleration;
            set => deceleration = Mathf.Max(0f, value);
        }

        public Vector2 MoveInput => moveInput;
        public Vector2 Velocity => velocity;
        public Vector3 WorldVelocity => new Vector3(velocity.x, 0f, velocity.y);
        public Vector2 LastMoveDirection => lastMoveDirection;
        public MovementState State => movementState;
        public bool MovementEnabled => movementEnabled;

        private void Reset()
        {
            body = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            ConfigureRigidbody();
            ConfigureCollider();
        }

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            CacheAnimatorHashes();
            ConfigureRigidbody();
        }

        private void OnValidate()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            CacheAnimatorHashes();
            ConfigureRigidbody();
            ConfigureCollider();
        }

        private void Update()
        {
            moveInput = movementEnabled ? ReadMovementInput() : Vector2.zero;

            if (moveInput.sqrMagnitude > 0.001f)
            {
                lastMoveDirection = moveInput.normalized;
            }

            movementState = moveInput.sqrMagnitude > 0.001f ? MovementState.Walk : MovementState.Idle;
            UpdateSpriteFacing();
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            float rate = moveInput.sqrMagnitude > 0.001f ? acceleration : deceleration;
            Vector2 targetVelocity = moveInput * moveSpeed;
            velocity = Vector2.MoveTowards(velocity, targetVelocity, rate * Time.fixedDeltaTime);
            Vector3 delta = new Vector3(velocity.x, 0f, velocity.y) * Time.fixedDeltaTime;
            body.MovePosition(body.position + delta);
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;

            if (movementEnabled)
            {
                return;
            }

            moveInput = Vector2.zero;
            velocity = Vector2.zero;
            movementState = MovementState.Idle;
            UpdateSpriteFacing();
            UpdateAnimator();
        }

        public void SetMoveInput(Vector2 input)
        {
            moveInput = ClampInput(input);
        }

        private Vector2 ReadMovementInput()
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

            return ClampInput(input);
        }

        private Vector2 ClampInput(Vector2 input)
        {
            if (invertHorizontalInput)
            {
                input.x = -input.x;
            }

            if (invertDepthInput)
            {
                input.y = -input.y;
            }

            if (normalizeDiagonalInput && input.sqrMagnitude > 1f)
            {
                return input.normalized;
            }

            return Vector2.ClampMagnitude(input, 1f);
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(moveXHash, moveInput.x);
            animator.SetFloat(moveYHash, moveInput.y);
            animator.SetFloat(lastMoveXHash, lastMoveDirection.x);
            animator.SetFloat(lastMoveYHash, lastMoveDirection.y);
            animator.SetFloat(speedHash, velocity.magnitude);
            animator.SetBool(isMovingHash, movementState == MovementState.Walk);
            animator.SetInteger(movementStateHash, (int)movementState);
        }

        private void UpdateSpriteFacing()
        {
            if (!flipSpriteWithHorizontalMovement || spriteRenderer == null || Mathf.Abs(moveInput.x) <= 0.001f)
            {
                return;
            }

            bool movingRight = moveInput.x > 0f;
            spriteRenderer.flipX = spriteFacesRightByDefault ? !movingRight : movingRight;
        }

        private void CacheAnimatorHashes()
        {
            moveXHash = Animator.StringToHash(moveXParameter);
            moveYHash = Animator.StringToHash(moveYParameter);
            lastMoveXHash = Animator.StringToHash(lastMoveXParameter);
            lastMoveYHash = Animator.StringToHash(lastMoveYParameter);
            speedHash = Animator.StringToHash(speedParameter);
            isMovingHash = Animator.StringToHash(isMovingParameter);
            movementStateHash = Animator.StringToHash(movementStateParameter);
        }

        private void ConfigureRigidbody()
        {
            if (body == null)
            {
                return;
            }

            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void ConfigureCollider()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null || spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            Vector3 size = spriteRenderer.sprite.bounds.size;
            capsule.direction = 1;
            capsule.center = new Vector3(0f, size.y * 0.5f, 0f);
            capsule.radius = Mathf.Max(0.01f, size.x * 0.5f);
            capsule.height = Mathf.Max(size.y, capsule.radius * 2f);
        }
    }
}
