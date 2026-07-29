#if UNITY_6000_3_OR_NEWER
using System;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Editor.LevelAuthoring
{
    [InitializeOnLoad]
    internal static class FpgPlayModeStartSceneToolbar
    {
        private const string ToolbarPath =
            "FPG Demo/Play Mode Start Scene";
        private const string PreferenceKey =
            "FPG.Demo.PlayModeStartSceneToolbar.StartOption";
        private const string MainToolbarWindowTypeName =
            "UnityEditor.MainToolbarWindow";
        private const int MaxToolbarDisplayAttempts = 10;

        private static StartOption selectedOption;
        private static int toolbarDisplayAttempts;

        static FpgPlayModeStartSceneToolbar()
        {
            selectedOption = ReadSelectedOption();
            EditorApplication.delayCall += ApplySelectedOption;
            EditorApplication.delayCall += EnsureToolbarDisplayed;
            EditorSceneManager.activeSceneChangedInEditMode +=
                OnActiveSceneChangedInEditMode;
            EditorApplication.projectChanged += RefreshToolbar;
        }

        private enum StartOption
        {
            Boot = 0,
            CurrentScene = 1
        }

        [MainToolbarElement(
            ToolbarPath,
            defaultDockPosition = MainToolbarDockPosition.Middle,
            defaultDockIndex = 1)]
        private static MainToolbarDropdown CreateDropdown()
        {
            GUIContent sceneIcon = EditorGUIUtility.IconContent(
                "SceneAsset Icon");
            MainToolbarContent content = new MainToolbarContent(
                GetToolbarText(),
                sceneIcon.image as Texture2D,
                GetTooltip());
            return new MainToolbarDropdown(content, ShowOptions);
        }

        private static void ShowOptions(Rect dropdownRect)
        {
            GenericMenu menu = new GenericMenu();
            if (LoadBootScene() != null)
            {
                menu.AddItem(
                    new GUIContent("Boot"),
                    selectedOption == StartOption.Boot,
                    () => Select(StartOption.Boot));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Boot (Missing)"));
            }

            menu.AddItem(
                new GUIContent("Current Scene"),
                selectedOption == StartOption.CurrentScene,
                () => Select(StartOption.CurrentScene));
            menu.DropDown(dropdownRect);
        }

        private static void Select(StartOption option)
        {
            selectedOption = option;
            EditorPrefs.SetInt(PreferenceKey, (int)option);
            ApplySelectedOption();
            RefreshToolbar();
        }

        private static StartOption ReadSelectedOption()
        {
            int storedValue = EditorPrefs.GetInt(
                PreferenceKey,
                (int)StartOption.Boot);
            return Enum.IsDefined(typeof(StartOption), storedValue)
                ? (StartOption)storedValue
                : StartOption.Boot;
        }

        private static void ApplySelectedOption()
        {
            if (selectedOption == StartOption.CurrentScene)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            SceneAsset bootScene = LoadBootScene();
            EditorSceneManager.playModeStartScene = bootScene;
            if (bootScene == null)
            {
                Debug.LogError(
                    "Play Mode could not use the Boot start scene because "
                    + $"'{FpgProductionSceneList.BootScenePath}' is missing.");
            }
        }

        private static SceneAsset LoadBootScene()
        {
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(
                FpgProductionSceneList.BootScenePath);
        }

        private static string GetToolbarText()
        {
            return selectedOption == StartOption.Boot
                ? "Boot"
                : "Current Scene";
        }

        private static string GetTooltip()
        {
            if (selectedOption == StartOption.Boot)
            {
                return "Play Mode start scene: Boot";
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.IsValid()
                ? activeScene.name
                : "Untitled";
            return $"Play Mode start scene: current scene ({sceneName})";
        }

        private static void OnActiveSceneChangedInEditMode(
            Scene previousScene,
            Scene newScene)
        {
            RefreshToolbar();
        }

        private static void RefreshToolbar()
        {
            MainToolbar.Refresh(ToolbarPath);
        }

        private static void EnsureToolbarDisplayed()
        {
            if (TryShowToolbarOverlay())
            {
                return;
            }

            // MainToolbarWindow can be created after the first editor delay call.
            // Retry briefly so a saved layout cannot leave the control invisible.
            if (++toolbarDisplayAttempts < MaxToolbarDisplayAttempts)
            {
                EditorApplication.delayCall += EnsureToolbarDisplayed;
            }
        }

        private static bool TryShowToolbarOverlay()
        {
            EditorWindow[] windows =
                Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (EditorWindow window in windows)
            {
                if (window == null || window.GetType().FullName !=
                    MainToolbarWindowTypeName)
                {
                    continue;
                }

                OverlayCanvas overlayCanvas = window.overlayCanvas;
                if (overlayCanvas == null)
                {
                    continue;
                }

                foreach (Overlay overlay in overlayCanvas.overlays)
                {
                    if (overlay == null || overlay.id != ToolbarPath)
                    {
                        continue;
                    }

                    if (!overlay.displayed)
                    {
                        overlay.displayed = true;
                    }

                    RefreshToolbar();
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
