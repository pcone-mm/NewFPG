using UnityEngine;

namespace FPG.Demo.Prototypes.ShinvSpriteSequenceTest
{
    public sealed class ShinvSpriteSequencePreviewControls : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string hitTriggerName = "Hit";

        public void Configure(Animator targetAnimator, string triggerName)
        {
            animator = targetAnimator;
            hitTriggerName = triggerName;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || animator == null)
            {
                return;
            }

            if (GUI.Button(new Rect(12f, 12f, 132f, 36f), "Play Hit"))
            {
                animator.SetTrigger(hitTriggerName);
            }
        }
    }
}
