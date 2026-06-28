using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Forging
{
    public enum ForgingLayoutDrawerStateCapture
    {
        Auto,
        Open,
        Closed,
    }

    [CreateAssetMenu(fileName = "ForgingUILayoutPreset", menuName = "NewFPG/Forging/UI Layout Preset")]
    public sealed class ForgingUILayoutPreset : ScriptableObject
    {
        [SerializeField] private Vector2 drawerOpenAnchoredPosition = new Vector2(-36f, 0f);
        [SerializeField] private Vector2 drawerClosedAnchoredPosition = new Vector2(722f, 0f);
        [SerializeField] private Vector2 drawerToggleOpenAnchoredPosition = new Vector2(-716f, -4f);
        [SerializeField] private Vector2 drawerToggleClosedAnchoredPosition = new Vector2(-28f, -4f);
        [SerializeField] private List<ForgingUILayoutEntry> entries = new List<ForgingUILayoutEntry>();

        public Vector2 DrawerOpenAnchoredPosition => drawerOpenAnchoredPosition;
        public Vector2 DrawerClosedAnchoredPosition => drawerClosedAnchoredPosition;
        public Vector2 DrawerToggleOpenAnchoredPosition => drawerToggleOpenAnchoredPosition;
        public Vector2 DrawerToggleClosedAnchoredPosition => drawerToggleClosedAnchoredPosition;
        public IReadOnlyList<ForgingUILayoutEntry> Entries => entries;

        public void CaptureFrom(RectTransform runtimeRoot, bool includeGeneratedContent)
        {
            entries.Clear();
            if (runtimeRoot == null)
            {
                return;
            }

            CaptureChildren(runtimeRoot, runtimeRoot, includeGeneratedContent);
        }

        public void CaptureDrawerState(
            ForgingLayoutDrawerStateCapture captureMode,
            RectTransform drawer,
            RectTransform drawerToggle,
            bool drawerIsOpen)
        {
            if (captureMode == ForgingLayoutDrawerStateCapture.Auto)
            {
                captureMode = drawerIsOpen ? ForgingLayoutDrawerStateCapture.Open : ForgingLayoutDrawerStateCapture.Closed;
            }

            if (drawer != null)
            {
                if (captureMode == ForgingLayoutDrawerStateCapture.Open)
                {
                    drawerOpenAnchoredPosition = drawer.anchoredPosition;
                }
                else
                {
                    drawerClosedAnchoredPosition = drawer.anchoredPosition;
                }
            }

            if (drawerToggle != null)
            {
                if (captureMode == ForgingLayoutDrawerStateCapture.Open)
                {
                    drawerToggleOpenAnchoredPosition = drawerToggle.anchoredPosition;
                }
                else
                {
                    drawerToggleClosedAnchoredPosition = drawerToggle.anchoredPosition;
                }
            }
        }

        public void ApplyTo(RectTransform runtimeRoot)
        {
            if (runtimeRoot == null || entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ForgingUILayoutEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.path))
                {
                    continue;
                }

                Transform target = runtimeRoot.Find(entry.path);
                if (target == null)
                {
                    continue;
                }

                RectTransform rect = target as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                entry.ApplyTo(rect);
            }
        }

        private void CaptureChildren(RectTransform runtimeRoot, Transform parent, bool includeGeneratedContent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                RectTransform rect = child as RectTransform;
                if (rect != null)
                {
                    string path = GetRelativePath(runtimeRoot, rect);
                    if (includeGeneratedContent || ShouldCapturePath(path))
                    {
                        entries.Add(ForgingUILayoutEntry.FromRect(path, rect));
                    }
                }

                CaptureChildren(runtimeRoot, child, includeGeneratedContent);
            }
        }

        private static bool ShouldCapturePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string name = path;
            int slash = path.LastIndexOf('/');
            if (slash >= 0 && slash < path.Length - 1)
            {
                name = path.Substring(slash + 1);
            }

            if (path.StartsWith("BlueprintGrid/Cell_", StringComparison.Ordinal)
                || path.StartsWith("BlueprintGrid/PlacementPreview", StringComparison.Ordinal)
                || path.Contains("/MaterialImage")
                || path.Contains("/PreviewCell_")
                || path.Contains("/PreviewFrame")
                || name.StartsWith("Cell_", StringComparison.Ordinal)
                || name.StartsWith("Dash", StringComparison.Ordinal)
                || name.StartsWith("Corner_", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            Stack<string> parts = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts.ToArray());
        }
    }

    [Serializable]
    public sealed class ForgingUILayoutEntry
    {
        public string path;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector3 anchoredPosition3D;
        public Vector2 sizeDelta;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public Vector3 localScale = Vector3.one;
        public Vector3 localEulerAngles;

        public static ForgingUILayoutEntry FromRect(string path, RectTransform rect)
        {
            return new ForgingUILayoutEntry
            {
                path = path,
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                anchoredPosition = rect.anchoredPosition,
                anchoredPosition3D = rect.anchoredPosition3D,
                sizeDelta = rect.sizeDelta,
                offsetMin = rect.offsetMin,
                offsetMax = rect.offsetMax,
                localScale = rect.localScale,
                localEulerAngles = rect.localEulerAngles,
            };
        }

        public void ApplyTo(RectTransform rect)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.anchoredPosition3D = anchoredPosition3D;
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = localScale;
            rect.localEulerAngles = localEulerAngles;
        }
    }
}
