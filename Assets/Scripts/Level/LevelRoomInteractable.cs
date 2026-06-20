using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Level
{
    [DisallowMultipleComponent]
    public sealed class LevelRoomInteractable : MonoBehaviour
    {
        [SerializeField] private LevelFlowDirector flowDirector;
        [SerializeField] private LevelRoomDefinition room;
        [SerializeField] private Transform player;
        [SerializeField, Min(0.25f)] private float interactDistance = 2.4f;
        [SerializeField] private string prompt = "按 E 互动";

        private bool consumed;

        public LevelRoomDefinition Room => room;
        public string Prompt => prompt;
        public bool IsPlayerInRange => player != null
            && Vector3.Distance(FlatPosition(player.position), FlatPosition(transform.position)) <= interactDistance;

        public void Initialize(LevelFlowDirector director, LevelRoomDefinition roomDefinition, Transform playerTransform, string promptText)
        {
            flowDirector = director;
            room = roomDefinition;
            player = playerTransform;
            prompt = promptText;
            consumed = false;
            EnsurePhysics();
        }

        private void Reset()
        {
            EnsurePhysics();
        }

        private void Awake()
        {
            EnsurePhysics();
        }

        private void Update()
        {
            if (consumed || flowDirector == null || room == null)
            {
                return;
            }

            if (IsPlayerInRange && InteractPressedThisFrame())
            {
                Interact();
            }
        }

        private void OnMouseDown()
        {
            if (consumed)
            {
                return;
            }

            Interact();
        }

        public bool Interact()
        {
            if (consumed || flowDirector == null)
            {
                return false;
            }

            consumed = flowDirector.TryBeginRoomInteraction(this);
            return consumed;
        }

        private void EnsurePhysics()
        {
            Collider objectCollider = GetComponent<Collider>();
            if (objectCollider == null)
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = 0.65f;
                sphere.isTrigger = true;
            }

            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.useGravity = false;
            body.isKinematic = true;
        }

        private static Vector3 FlatPosition(Vector3 position)
        {
            position.y = 0f;
            return position;
        }

        private static bool InteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.25f, 0.32f);
            Gizmos.DrawWireSphere(transform.position, interactDistance);
        }
    }
}
