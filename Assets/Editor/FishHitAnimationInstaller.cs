using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewFPG.EditorTools
{
    public static class FishHitAnimationInstaller
    {
        private const string ControllerPath = "Assets/Prefabs/Monster/FishAssets/Fish.controller";
        private const string HitClipPath = "Assets/Prefabs/Monster/FishAssets/Animations/Fish_Hit.anim";
        private const string HitSpritePath = "Assets/Prefabs/Monster/FishAssets/Hit/fish_hit.png";
        private const string HitParameter = "Hit";
        private const string HitStateName = "Hit";

        [MenuItem("NewFPG/Level/Install Fish Hit Animation")]
        public static void InstallFishHitAnimation()
        {
            Sprite hitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HitSpritePath);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (hitSprite == null || controller == null)
            {
                Debug.LogError("Fish hit animation install failed. Missing sprite or controller.");
                return;
            }

            AnimationClip hitClip = CreateOrUpdateHitClip(hitSprite);
            EnsureHitParameter(controller);
            EnsureHitState(controller, hitClip);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("Installed Fish Hit animation trigger on controller: " + ControllerPath);
        }

        private static AnimationClip CreateOrUpdateHitClip(Sprite hitSprite)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(HitClipPath);
            if (clip == null)
            {
                clip = new AnimationClip
                {
                    frameRate = 12f,
                    name = "Fish_Hit",
                };
                AssetDatabase.CreateAsset(clip, HitClipPath);
            }

            clip.wrapMode = WrapMode.Once;
            EditorCurveBinding binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite",
            };
            ObjectReferenceKeyframe[] frames =
            {
                new ObjectReferenceKeyframe { time = 0f, value = hitSprite },
                new ObjectReferenceKeyframe { time = 0.18f, value = hitSprite },
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void EnsureHitParameter(AnimatorController controller)
        {
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                if (controller.parameters[i].name == HitParameter)
                {
                    return;
                }
            }

            controller.AddParameter(HitParameter, AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureHitState(AnimatorController controller, Motion hitClip)
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState hitState = null;
            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                if (stateMachine.states[i].state.name == HitStateName)
                {
                    hitState = stateMachine.states[i].state;
                    break;
                }
            }

            if (hitState == null)
            {
                hitState = stateMachine.AddState(HitStateName, new Vector3(570f, 150f, 0f));
            }

            hitState.motion = hitClip;
            hitState.writeDefaultValues = true;

            bool hasAnyStateTransition = false;
            for (int i = 0; i < stateMachine.anyStateTransitions.Length; i++)
            {
                AnimatorStateTransition transition = stateMachine.anyStateTransitions[i];
                if (transition.destinationState == hitState)
                {
                    hasAnyStateTransition = true;
                    ConfigureHitEntryTransition(transition);
                    break;
                }
            }

            if (!hasAnyStateTransition)
            {
                AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(hitState);
                ConfigureHitEntryTransition(transition);
            }

            bool hasExitTransition = false;
            for (int i = 0; i < hitState.transitions.Length; i++)
            {
                AnimatorStateTransition transition = hitState.transitions[i];
                if (transition.isExit)
                {
                    hasExitTransition = true;
                    ConfigureHitExitTransition(transition);
                    break;
                }
            }

            if (!hasExitTransition)
            {
                AnimatorStateTransition transition = hitState.AddExitTransition();
                ConfigureHitExitTransition(transition);
            }
        }

        private static void ConfigureHitEntryTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = true;
            transition.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = HitParameter,
                    threshold = 0f,
                },
            };
        }

        private static void ConfigureHitExitTransition(AnimatorStateTransition transition)
        {
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
            transition.duration = 0.05f;
            transition.conditions = System.Array.Empty<AnimatorCondition>();
        }
    }
}
