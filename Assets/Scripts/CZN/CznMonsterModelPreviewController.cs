using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace NewFPG.CZN
{
    public sealed class CznMonsterModelPreviewController : MonoBehaviour
    {
        private const string IdleAnimation = "normal_idle";

        [SerializeField] private SkeletonAnimation[] monsters = Array.Empty<SkeletonAnimation>();
        [SerializeField] private string[] monsterLabels = Array.Empty<string>();
        [SerializeField] private int selectedMonsterIndex;
        [SerializeField] private int[] selectedAnimationIndices = Array.Empty<int>();

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;

        public void Configure(SkeletonAnimation[] bindings, string[] labels)
        {
            monsters = bindings ?? Array.Empty<SkeletonAnimation>();
            monsterLabels = labels ?? Array.Empty<string>();
            selectedMonsterIndex = 0;
            selectedAnimationIndices = new int[monsters.Length];
            for (int index = 0; index < monsters.Length; index++)
            {
                selectedAnimationIndices[index] = FindAnimationIndex(monsters[index], IdleAnimation);
            }
        }

        private void OnEnable()
        {
            EnsureState();
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                ReplaySelected();
            }
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || monsters == null || monsters.Length == 0)
            {
                return;
            }

            EnsureState();
            HandleKeyboard(UnityEngine.Event.current);
            EnsureStyles();

            string monsterLabel = SelectedMonsterLabel;
            string animationName = SelectedAnimationName;

            GUILayout.BeginArea(new Rect(12f, 12f, 520f, 164f), GUI.skin.box);
            GUILayout.Label("CZN 怪物动画预览", titleStyle);
            GUILayout.Label(
                $"怪物 {selectedMonsterIndex + 1}/{monsters.Length}: {monsterLabel}",
                bodyStyle);
            GUILayout.Label($"动画: {animationName}", bodyStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上一个怪物"))
            {
                SelectMonster(-1);
            }
            if (GUILayout.Button("下一个怪物"))
            {
                SelectMonster(1);
            }
            if (GUILayout.Button("上一个动作"))
            {
                SelectAnimation(-1);
            }
            if (GUILayout.Button("下一个动作"))
            {
                SelectAnimation(1);
            }
            if (GUILayout.Button("重播"))
            {
                ReplaySelected();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(
                "快捷键：1–8 选怪，←/→ 选怪，↑/↓ 切动作，空格重播。技能动作不循环，结束后自动接回 normal_idle。",
                bodyStyle);
            GUILayout.EndArea();

        }

        public void ReplaySelected()
        {
            SkeletonAnimation skeleton = SelectedMonster;
            SkeletonData data = GetSkeletonData(skeleton);
            if (skeleton == null || data == null || data.Animations.Count == 0)
            {
                return;
            }

            int animationIndex = WrapAnimationIndex(
                selectedMonsterIndex,
                selectedAnimationIndices[selectedMonsterIndex]);
            selectedAnimationIndices[selectedMonsterIndex] = animationIndex;
            string animationName = data.Animations.Items[animationIndex].Name;
            bool loop = IsIdleAnimation(animationName);

            skeleton.Initialize(false);
            skeleton.ClearState();
            if (skeleton.Skeleton != null)
            {
                skeleton.Skeleton.SetToSetupPose();
                skeleton.Skeleton.UpdateWorldTransform();
            }

            skeleton.timeScale = 1f;
            skeleton.loop = loop;
            skeleton.AnimationState.SetAnimation(0, animationName, loop);
            if (!loop && !string.Equals(animationName, IdleAnimation, StringComparison.OrdinalIgnoreCase) &&
                data.FindAnimation(IdleAnimation) != null)
            {
                skeleton.AnimationState.AddAnimation(0, IdleAnimation, true, 0f);
            }

            skeleton.Update(0f);
            skeleton.LateUpdate();
        }

        private void SelectMonster(int delta)
        {
            selectedMonsterIndex = WrapIndex(selectedMonsterIndex + delta, monsters.Length);
            ReplaySelected();
        }

        private void SelectAnimation(int delta)
        {
            SkeletonData data = GetSkeletonData(SelectedMonster);
            if (data == null || data.Animations.Count == 0)
            {
                return;
            }

            selectedAnimationIndices[selectedMonsterIndex] = WrapIndex(
                selectedAnimationIndices[selectedMonsterIndex] + delta,
                data.Animations.Count);
            ReplaySelected();
        }

        private void SelectMonsterDirect(int index)
        {
            if (index < 0 || index >= monsters.Length)
            {
                return;
            }

            selectedMonsterIndex = index;
            ReplaySelected();
        }

        private void HandleKeyboard(UnityEngine.Event current)
        {
            if (current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            bool handled = true;
            switch (current.keyCode)
            {
                case KeyCode.LeftArrow:
                    SelectMonster(-1);
                    break;
                case KeyCode.RightArrow:
                    SelectMonster(1);
                    break;
                case KeyCode.UpArrow:
                    SelectAnimation(-1);
                    break;
                case KeyCode.DownArrow:
                    SelectAnimation(1);
                    break;
                case KeyCode.Space:
                    ReplaySelected();
                    break;
                default:
                    handled = TryHandleNumberKey(current.keyCode);
                    break;
            }

            if (handled)
            {
                current.Use();
            }
        }

        private bool TryHandleNumberKey(KeyCode keyCode)
        {
            int number = -1;
            if (keyCode >= KeyCode.Alpha1 && keyCode <= KeyCode.Alpha8)
            {
                number = (int)keyCode - (int)KeyCode.Alpha1;
            }
            else if (keyCode >= KeyCode.Keypad1 && keyCode <= KeyCode.Keypad8)
            {
                number = (int)keyCode - (int)KeyCode.Keypad1;
            }

            if (number < 0 || number >= monsters.Length)
            {
                return false;
            }

            SelectMonsterDirect(number);
            return true;
        }

        private void EnsureState()
        {
            monsters ??= Array.Empty<SkeletonAnimation>();
            monsterLabels ??= Array.Empty<string>();
            if (selectedAnimationIndices == null || selectedAnimationIndices.Length != monsters.Length)
            {
                selectedAnimationIndices = new int[monsters.Length];
                for (int index = 0; index < monsters.Length; index++)
                {
                    selectedAnimationIndices[index] = FindAnimationIndex(monsters[index], IdleAnimation);
                }
            }

            selectedMonsterIndex = monsters.Length > 0
                ? WrapIndex(selectedMonsterIndex, monsters.Length)
                : 0;
        }

        private void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.86f, 0.93f, 1f, 1f) },
                };
            }

            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    wordWrap = true,
                    normal = { textColor = Color.white },
                };
            }
        }

        private int FindAnimationIndex(SkeletonAnimation skeleton, string animationName)
        {
            SkeletonData data = GetSkeletonData(skeleton);
            if (data == null)
            {
                return 0;
            }

            for (int index = 0; index < data.Animations.Count; index++)
            {
                if (string.Equals(
                        data.Animations.Items[index].Name,
                        animationName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return 0;
        }

        private int WrapAnimationIndex(int monsterIndex, int animationIndex)
        {
            SkeletonData data = monsterIndex >= 0 && monsterIndex < monsters.Length
                ? GetSkeletonData(monsters[monsterIndex])
                : null;
            return data != null && data.Animations.Count > 0
                ? WrapIndex(animationIndex, data.Animations.Count)
                : 0;
        }

        private static SkeletonData GetSkeletonData(SkeletonAnimation skeleton)
        {
            if (skeleton == null || skeleton.SkeletonDataAsset == null)
            {
                return null;
            }

            return skeleton.SkeletonDataAsset.GetSkeletonData(true);
        }

        private static bool IsIdleAnimation(string animationName)
        {
            return !string.IsNullOrEmpty(animationName) &&
                   animationName.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int WrapIndex(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int wrapped = value % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private SkeletonAnimation SelectedMonster =>
            monsters != null && selectedMonsterIndex >= 0 && selectedMonsterIndex < monsters.Length
                ? monsters[selectedMonsterIndex]
                : null;

        private string SelectedMonsterLabel =>
            monsterLabels != null && selectedMonsterIndex >= 0 && selectedMonsterIndex < monsterLabels.Length
                ? monsterLabels[selectedMonsterIndex]
                : "Monster " + (selectedMonsterIndex + 1);

        private string SelectedAnimationName
        {
            get
            {
                SkeletonData data = GetSkeletonData(SelectedMonster);
                if (data == null || data.Animations.Count == 0)
                {
                    return "<none>";
                }

                int index = WrapAnimationIndex(
                    selectedMonsterIndex,
                    selectedAnimationIndices[selectedMonsterIndex]);
                return data.Animations.Items[index].Name;
            }
        }
    }
}
