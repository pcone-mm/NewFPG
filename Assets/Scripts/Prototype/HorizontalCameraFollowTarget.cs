using UnityEngine;

namespace NewFPG.Prototype
{
    public sealed class HorizontalCameraFollowTarget : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 anchoredPosition;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 position = anchoredPosition;
            position.x = target.position.x;
            transform.position = position;
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                transform.position = anchoredPosition;
                return;
            }

            Vector3 position = anchoredPosition;
            position.x = target.position.x;
            transform.position = position;
        }
    }
}
