using NewFPG.Characters;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.Level
{
    [DisallowMultipleComponent]
    public sealed class SceneInteractablePlaceholder : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Min(0.25f)] private float interactDistance = 2.4f;
        [SerializeField] private string interactionId;
        [SerializeField] private string displayName;
        [SerializeField] private string prompt = "Interact";
        [SerializeField, TextArea] private string note;

        public string InteractionId => interactionId;
        public string DisplayName => displayName;
        public string Prompt => prompt;
        public string Note => note;
        public bool IsPlayerInRange => player != null
            && Vector3.Distance(FlatPosition(player.position), FlatPosition(transform.position)) <= interactDistance;

        public void Initialize(
            Transform playerTransform,
            string newInteractionId,
            string newDisplayName,
            string newPrompt,
            string newNote,
            float newInteractDistance = 2.4f)
        {
            player = playerTransform;
            interactionId = newInteractionId;
            displayName = newDisplayName;
            prompt = newPrompt;
            note = newNote;
            interactDistance = Mathf.Max(0.25f, newInteractDistance);
            EnsurePhysics();
        }

        private void Reset()
        {
            player = FindFirstObjectByType<PlayerCharacterController>()?.transform;
            EnsurePhysics();
        }

        private void Awake()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCharacterController>()?.transform;
            }

            EnsurePhysics();
        }

        private void Update()
        {
            if (IsPlayerInRange && InteractPressedThisFrame())
            {
                Interact();
            }
        }

        private void OnMouseDown()
        {
            Interact();
        }

        public bool Interact()
        {
            Debug.Log(
                $"Scene interactable placeholder triggered: {displayName} ({interactionId}). {note}",
                this);
            return true;
        }

        private void EnsurePhysics()
        {
            Collider objectCollider = GetComponent<Collider>();
            if (objectCollider == null)
            {
                SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
                sphere.radius = 0.75f;
                objectCollider = sphere;
            }

            objectCollider.isTrigger = true;

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
            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.32f);
            Gizmos.DrawWireSphere(transform.position, interactDistance);
        }
    }
}
