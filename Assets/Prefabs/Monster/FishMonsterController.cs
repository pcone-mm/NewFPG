using UnityEngine;

namespace NewFPG.Monsters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FishMonsterController : MonoBehaviour
    {
        public enum MovementState
        {
            Idle,
            Move,
        }

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float acceleration = 16f;
        [SerializeField, Min(0f)] private float deceleration = 20f;
        [SerializeField] private bool movementEnabled = true;

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private bool autoFindTargetByTag = true;
        [SerializeField] private string targetTag = "Player";
        [SerializeField, Min(0f)] private float detectionRadius = 7f;
        [SerializeField, Min(0f)] private float stoppingDistance = 1.2f;
        [SerializeField, Min(0.05f)] private float targetRefreshInterval = 0.25f;

        [Header("Patrol")]
        [SerializeField] private bool patrolWhenNoTarget = true;
        [SerializeField, Min(0f)] private float patrolRadius = 3f;
        [SerializeField, Min(0f)] private float patrolPointTolerance = 0.2f;
        [SerializeField, Min(0f)] private float patrolPauseDuration = 1f;

        [Header("Visuals")]
        [SerializeField] private bool flipSpriteWithHorizontalMovement = true;
        [SerializeField] private bool spriteFacesRightByDefault = true;

        [Header("Collider")]
        [SerializeField] private bool autoConfigureCollider = true;
        [SerializeField, Min(0.05f)] private float colliderWidthScale = 0.8f;
        [SerializeField, Min(0.05f)] private float colliderHeightScale = 0.75f;
        [SerializeField, Min(0.05f)] private float colliderDepth = 0.75f;

        [Header("References")]
        [SerializeField] private Rigidbody body;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider boxCollider;

        [Header("Animator Parameters")]
        [SerializeField] private string moveXParameter = "MoveX";
        [SerializeField] private string moveZParameter = "MoveZ";
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string movementStateParameter = "MovementState";

        private Transform autoTarget;
        private Vector2 velocity;
        private Vector2 desiredDirection;
        private Vector3 homePosition;
        private Vector3 patrolDestination;
        private bool hasPatrolDestination;
        private float patrolPauseRemaining;
        private float targetRefreshTimer;
        private MovementState movementState;
        private int moveXHash;
        private int moveZHash;
        private int speedHash;
        private int isMovingHash;
        private int movementStateHash;

        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        public float DetectionRadius
        {
            get => detectionRadius;
            set => detectionRadius = Mathf.Max(0f, value);
        }

        public Transform Target
        {
            get => target != null ? target : autoTarget;
            set => target = value;
        }

        public Vector2 DesiredDirection => desiredDirection;
        public Vector2 Velocity => velocity;
        public Vector3 WorldVelocity => new Vector3(velocity.x, 0f, velocity.y);
        public MovementState State => movementState;

        private void Reset()
        {
            CacheReferences();
            ConfigureRigidbody();
            ConfigureCollider();
        }

        private void Awake()
        {
            CacheReferences();
            CacheAnimatorHashes();
            ConfigureRigidbody();
            homePosition = body != null ? body.position : transform.position;
        }

        private void OnEnable()
        {
            targetRefreshTimer = 0f;
            hasPatrolDestination = false;
        }

        private void OnValidate()
        {
            CacheReferences();
            CacheAnimatorHashes();
            ConfigureRigidbody();
            ConfigureCollider();
        }

        private void Update()
        {
            targetRefreshTimer -= Time.deltaTime;
            if (targetRefreshTimer <= 0f)
            {
                targetRefreshTimer = targetRefreshInterval;
                RefreshAutoTarget();
            }

            UpdateSpriteFacing();
            UpdateAnimator();
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            desiredDirection = movementEnabled ? ResolveDesiredDirection() : Vector2.zero;
            float rate = desiredDirection.sqrMagnitude > 0.001f ? acceleration : deceleration;
            Vector2 targetVelocity = desiredDirection * moveSpeed;
            velocity = Vector2.MoveTowards(velocity, targetVelocity, rate * Time.fixedDeltaTime);
            body.MovePosition(body.position + WorldVelocity * Time.fixedDeltaTime);
            movementState = velocity.sqrMagnitude > 0.001f ? MovementState.Move : MovementState.Idle;
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;

            if (movementEnabled)
            {
                return;
            }

            desiredDirection = Vector2.zero;
            velocity = Vector2.zero;
            movementState = MovementState.Idle;
            UpdateAnimator();
        }

        private Vector2 ResolveDesiredDirection()
        {
            Transform currentTarget = Target;
            if (currentTarget != null)
            {
                Vector3 toTarget = currentTarget.position - body.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > stoppingDistance * stoppingDistance)
                {
                    return new Vector2(toTarget.x, toTarget.z).normalized;
                }

                return Vector2.zero;
            }

            return patrolWhenNoTarget ? ResolvePatrolDirection() : Vector2.zero;
        }

        private Vector2 ResolvePatrolDirection()
        {
            if (patrolRadius <= 0f)
            {
                return Vector2.zero;
            }

            if (patrolPauseRemaining > 0f)
            {
                patrolPauseRemaining -= Time.fixedDeltaTime;
                return Vector2.zero;
            }

            Vector3 currentPosition = body.position;
            Vector3 flatToDestination = patrolDestination - currentPosition;
            flatToDestination.y = 0f;

            if (!hasPatrolDestination || flatToDestination.sqrMagnitude <= patrolPointTolerance * patrolPointTolerance)
            {
                PickPatrolDestination();
                patrolPauseRemaining = patrolPauseDuration;
                return Vector2.zero;
            }

            return new Vector2(flatToDestination.x, flatToDestination.z).normalized;
        }

        private void PickPatrolDestination()
        {
            Vector2 offset = Random.insideUnitCircle * patrolRadius;
            patrolDestination = homePosition + new Vector3(offset.x, 0f, offset.y);
            hasPatrolDestination = true;
        }

        private void RefreshAutoTarget()
        {
            if (!autoFindTargetByTag || target != null || string.IsNullOrWhiteSpace(targetTag))
            {
                return;
            }

            GameObject targetObject = GameObject.FindGameObjectWithTag(targetTag);
            if (targetObject == null)
            {
                autoTarget = null;
                return;
            }

            Vector3 currentPosition = body != null ? body.position : transform.position;
            Vector3 toTarget = targetObject.transform.position - currentPosition;
            toTarget.y = 0f;

            if (detectionRadius <= 0f || toTarget.sqrMagnitude <= detectionRadius * detectionRadius)
            {
                autoTarget = targetObject.transform;
                return;
            }

            autoTarget = null;
        }

        private void UpdateSpriteFacing()
        {
            if (!flipSpriteWithHorizontalMovement || spriteRenderer == null || Mathf.Abs(velocity.x) <= 0.001f)
            {
                return;
            }

            bool movingRight = velocity.x > 0f;
            spriteRenderer.flipX = spriteFacesRightByDefault ? !movingRight : movingRight;
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            animator.SetFloat(moveXHash, desiredDirection.x);
            animator.SetFloat(moveZHash, desiredDirection.y);
            animator.SetFloat(speedHash, velocity.magnitude);
            animator.SetBool(isMovingHash, movementState == MovementState.Move);
            animator.SetInteger(movementStateHash, (int)movementState);
        }

        private void CacheReferences()
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

            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider>();
            }
        }

        private void CacheAnimatorHashes()
        {
            moveXHash = Animator.StringToHash(moveXParameter);
            moveZHash = Animator.StringToHash(moveZParameter);
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
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }

        private void ConfigureCollider()
        {
            if (!autoConfigureCollider || boxCollider == null || spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            Vector3 size = spriteRenderer.sprite.bounds.size;
            boxCollider.center = Vector3.zero;
            boxCollider.size = new Vector3(
                Mathf.Max(0.05f, size.x * colliderWidthScale),
                Mathf.Max(0.05f, size.y * colliderHeightScale),
                colliderDepth);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = body != null ? body.position : transform.position;

            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(center, detectionRadius);

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.3f);
            Gizmos.DrawWireSphere(homePosition == Vector3.zero ? center : homePosition, patrolRadius);
        }
    }
}
