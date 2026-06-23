using UnityEngine;
using UnityEngine.UI;

namespace NewFPG.Combat.SkillIndicators
{
    [DisallowMultipleComponent]
    public sealed class SkillIndicatorUiInputBlocker : MonoBehaviour
    {
        private const string CanvasName = "SkillIndicatorUiInputBlockerCanvas";
        private const int SortingOrder = 32000;

        private Canvas canvas;
        private Image blockerImage;
        private int blockCount;

        public bool IsBlocking => blockCount > 0 && canvas != null && canvas.gameObject.activeSelf;

        public void BeginBlock()
        {
            blockCount++;
            SetBlocking(true);
        }

        public void EndBlock()
        {
            blockCount = Mathf.Max(0, blockCount - 1);
            SetBlocking(blockCount > 0);
        }

        public void Clear()
        {
            blockCount = 0;
            SetBlocking(false);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject imageObject = new GameObject("InputBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            blockerImage = imageObject.GetComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            RectTransform rect = blockerImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            canvasObject.SetActive(false);
        }

        private void SetBlocking(bool blocking)
        {
            if (blocking)
            {
                EnsureCanvas();
            }

            if (canvas != null)
            {
                canvas.gameObject.SetActive(blocking);
            }
        }
    }
}
