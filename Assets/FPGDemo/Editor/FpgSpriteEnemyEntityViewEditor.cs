using System;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor
{
    [CustomEditor(typeof(FpgSpriteEnemyEntityView))]
    [CanEditMultipleObjects]
    internal sealed class FpgSpriteEnemyEntityViewEditor : UnityEditor.Editor
    {
        private GameObject previewRoot;
        private Scene previewScene;
        private FpgSpriteEnemyEntityView previewView;
        private int animationIndex;
        private float previewTime;
        private bool previewPlaying;
        private bool previewLoop = true;
        private double lastPreviewUpdate;
        private Bounds previewBounds;
        private string previewError;

        private void OnEnable()
        {
            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += HandlePlayModeChange;
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            EditorApplication.playModeStateChanged -= HandlePlayModeChange;
            Undo.undoRedoPerformed -= HandleUndoRedo;
            DisposePreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("m_Script"));
            }

            DrawPropertiesExcluding(serializedObject, "m_Script");
            if (serializedObject.ApplyModifiedProperties())
            {
                DisposePreview();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("动画预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "在 Animations 中逐项调整 Playback Speed：Walk = 1，Hit = 2.5。"
                + "预览使用正式采样和倍率；动画倍率不会改变技能事件的触发时间。",
                MessageType.Info);

            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox(
                    "请选择单个 Sprite Enemy View 预览动画。",
                    MessageType.None);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox(
                    "退出 Play Mode 后可在此预览，不会修改场景或 Prefab 的姿态。",
                    MessageType.None);
                return;
            }

            DrawAnimationPreview((FpgSpriteEnemyEntityView)target);
        }

        private void DrawAnimationPreview(FpgSpriteEnemyEntityView source)
        {
            int count = source.AnimationCount;
            if (count == 0)
            {
                EditorGUILayout.HelpBox("尚未配置动画。", MessageType.None);
                DisposePreview();
                return;
            }

            animationIndex = Mathf.Clamp(animationIndex, 0, count - 1);
            var names = new string[count];
            for (int index = 0; index < count; index++)
            {
                string name = source.GetAnimationName(index);
                names[index] = string.IsNullOrEmpty(name)
                    ? $"<动画 {index + 1} 未命名>"
                    : name;
            }

            int nextIndex = EditorGUILayout.Popup("动画", animationIndex, names);
            if (nextIndex != animationIndex)
            {
                animationIndex = nextIndex;
                previewTime = 0f;
                DisposePreview();
            }

            string animationName = source.GetAnimationName(animationIndex);
            if (!source.TryGetAnimationPlaybackInfo(
                    animationName,
                    out float duration,
                    out float frameRate,
                    out int frameCount))
            {
                EditorGUILayout.HelpBox(
                    "此动画无法采样，请检查 Clip、帧资源和 Playback Speed。",
                    MessageType.Warning);
                DisposePreview();
                return;
            }

            if (!EnsurePreview(source, animationName, frameRate, frameCount))
            {
                EditorGUILayout.HelpBox(previewError, MessageType.Warning);
                return;
            }

            previewTime = Mathf.Clamp(previewTime, 0f, duration);
            EditorGUILayout.LabelField(
                $"{frameCount} 帧  ·  {frameRate:0.###} 帧/秒  ·  {duration:0.###} 秒",
                EditorStyles.miniLabel);
            DrawPlaybackControls(duration, frameRate);
            float nextTime = EditorGUILayout.Slider(
                "时间（秒）", previewTime, 0f, duration);
            if (nextTime != previewTime)
            {
                previewTime = nextTime;
                previewPlaying = false;
            }

            if (!previewView.TrySampleAnimationPreview(animationName, previewTime))
            {
                EditorGUILayout.HelpBox("动画采样失败。", MessageType.Warning);
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(280f),
                GUILayout.ExpandWidth(true));
            DrawSprite(rect, previewView.AnimationRenderer);
        }

        private void DrawPlaybackControls(float duration, float frameRate)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("上一帧"))
                {
                    StepFrame(-1, frameRate, duration);
                }

                if (GUILayout.Button(previewPlaying ? "暂停" : "播放"))
                {
                    previewPlaying = !previewPlaying;
                    if (previewPlaying && previewTime >= duration)
                    {
                        previewTime = 0f;
                    }

                    lastPreviewUpdate = EditorApplication.timeSinceStartup;
                }

                if (GUILayout.Button("下一帧"))
                {
                    StepFrame(1, frameRate, duration);
                }

                if (GUILayout.Button("复位"))
                {
                    previewTime = 0f;
                    previewPlaying = false;
                }
            }

            previewLoop = EditorGUILayout.Toggle("循环预览", previewLoop);
        }

        private void StepFrame(int direction, float frameRate, float duration)
        {
            previewPlaying = false;
            float frame = Mathf.Round(previewTime * frameRate) + direction;
            previewTime = Mathf.Clamp(frame / frameRate, 0f, duration);
        }

        private bool EnsurePreview(
            FpgSpriteEnemyEntityView source,
            string animationName,
            float frameRate,
            int frameCount)
        {
            if (previewView != null)
            {
                return true;
            }

            try
            {
                // The inactive parent prevents gameplay lifecycle callbacks
                // while preserving internal references in the isolated clone.
                previewScene = EditorSceneManager.NewPreviewScene();
                previewRoot = new GameObject("Sprite Enemy Animation Preview")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                previewRoot.SetActive(false);
                SceneManager.MoveGameObjectToScene(previewRoot, previewScene);
                GameObject clone = Instantiate(
                    source.gameObject,
                    previewRoot.transform,
                    false);
                foreach (Transform child in clone.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }

                previewView = clone.GetComponent<FpgSpriteEnemyEntityView>();
                bool hasBounds = false;
                for (int index = 0; index < Mathf.Max(1, frameCount); index++)
                {
                    if (!previewView.TrySampleAnimationPreview(
                            animationName,
                            index / frameRate))
                    {
                        continue;
                    }

                    SpriteRenderer renderer = previewView.AnimationRenderer;
                    if (renderer == null || renderer.sprite == null)
                    {
                        continue;
                    }

                    Bounds bounds = GetSpriteCanvasBounds(renderer);
                    if (!hasBounds)
                    {
                        previewBounds = bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        previewBounds.Encapsulate(bounds);
                    }
                }

                if (!hasBounds)
                {
                    DisposePreview();
                    previewError = "没有可预览的 Sprite 帧。";
                    return false;
                }

                previewError = null;
                return true;
            }
            catch (Exception exception)
            {
                DisposePreview();
                previewError = $"无法建立隔离预览：{exception.Message}";
                return false;
            }
        }

        private static Bounds GetSpriteCanvasBounds(SpriteRenderer renderer)
        {
            Sprite sprite = renderer.sprite;
            Vector2 size = sprite.rect.size / sprite.pixelsPerUnit;
            Vector2 center = (sprite.rect.size * 0.5f - sprite.pivot)
                / sprite.pixelsPerUnit;
            GetPreviewFlips(renderer, out bool flipX, out bool flipY);
            if (flipX)
            {
                center.x = -center.x;
            }

            if (flipY)
            {
                center.y = -center.y;
            }

            return new Bounds(center, size);
        }

        private static void GetPreviewFlips(
            SpriteRenderer renderer,
            out bool flipX,
            out bool flipY)
        {
            // Match the authored billboard's reflection in the world XY
            // plane, including a VisualRoot rotated by 180 degrees around Y.
            flipX = renderer.flipX
                ^ (renderer.transform.TransformVector(Vector3.right).x < 0f);
            flipY = renderer.flipY
                ^ (renderer.transform.TransformVector(Vector3.up).y < 0f);
        }

        private void DrawSprite(Rect rect, SpriteRenderer renderer)
        {
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f, 1f));
            if (Event.current.type != EventType.Repaint
                || renderer == null
                || renderer.sprite == null)
            {
                return;
            }

            Sprite sprite = renderer.sprite;
            try
            {
                Rect textureRect = sprite.textureRect;
                Vector2 trimOffset = sprite.textureRectOffset;
                float pixelsPerUnit = sprite.pixelsPerUnit;
                Vector2 size = textureRect.size / pixelsPerUnit;
                Vector2 lowerLeft = (trimOffset - sprite.pivot) / pixelsPerUnit;
                GetPreviewFlips(renderer, out bool flipX, out bool flipY);
                if (flipX)
                {
                    lowerLeft.x = -lowerLeft.x - size.x;
                }

                if (flipY)
                {
                    lowerLeft.y = -lowerLeft.y - size.y;
                }

                float scale = Mathf.Min(
                    (rect.width - 24f) / Mathf.Max(0.001f, previewBounds.size.x),
                    (rect.height - 24f) / Mathf.Max(0.001f, previewBounds.size.y));
                Vector2 canvasCenter = previewBounds.center;
                var drawRect = new Rect(
                    rect.center.x + (lowerLeft.x - canvasCenter.x) * scale,
                    rect.center.y - (lowerLeft.y + size.y - canvasCenter.y) * scale,
                    size.x * scale,
                    size.y * scale);
                Texture2D texture = sprite.texture;
                var uv = new Rect(
                    textureRect.x / texture.width,
                    textureRect.y / texture.height,
                    textureRect.width / texture.width,
                    textureRect.height / texture.height);
                if (flipX)
                {
                    uv.x += uv.width;
                    uv.width = -uv.width;
                }

                if (flipY)
                {
                    uv.y += uv.height;
                    uv.height = -uv.height;
                }

                Color previousColor = GUI.color;
                try
                {
                    GUI.color = renderer.color;
                    GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
                }
                finally
                {
                    GUI.color = previousColor;
                }
            }
            catch (UnityException)
            {
                GUI.Label(
                    rect,
                    "此帧使用紧密打包图集，无法直接显示矩形预览。",
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void UpdatePreview()
        {
            if (!previewPlaying
                || previewView == null
                || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling)
            {
                return;
            }

            FpgSpriteEnemyEntityView source = target as FpgSpriteEnemyEntityView;
            if (source == null
                || !source.TryGetAnimationPlaybackInfo(
                    source.GetAnimationName(animationIndex),
                    out float duration,
                    out _,
                    out _))
            {
                previewPlaying = false;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            previewTime += (float)Math.Min(0.1d, now - lastPreviewUpdate);
            lastPreviewUpdate = now;
            if (previewTime >= duration)
            {
                if (previewLoop)
                {
                    previewTime = Mathf.Repeat(previewTime, duration);
                }
                else
                {
                    previewTime = duration;
                    previewPlaying = false;
                }
            }

            Repaint();
        }

        private void HandlePlayModeChange(PlayModeStateChange state)
        {
            DisposePreview();
            Repaint();
        }

        private void HandleUndoRedo()
        {
            DisposePreview();
            Repaint();
        }

        private void DisposePreview()
        {
            previewPlaying = false;
            previewView = null;
            if (previewRoot != null)
            {
                DestroyImmediate(previewRoot);
                previewRoot = null;
            }

            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
                previewScene = default;
            }
        }
    }
}
