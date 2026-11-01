using System;
using System.Linq;
using FPG.Demo.Prototypes.ShinvSpriteSequenceTest;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPG.Demo.Prototypes.ShinvSpriteSequenceTest.Editor
{
    [InitializeOnLoad]
    internal static class BuildShinvSpriteSequencePreview
    {
        private const string Root = "Assets/FPGDemo/Prototypes/ShinvSpriteSequenceTest";
        private const string Art = Root + "/Art";
        private const string Animation = Root + "/Animation";
        private const string Prefabs = Root + "/Prefabs";
        private const string Scenes = Root + "/Scenes";
        private const string WalkTexture = Art + "/shinv_walk.png";
        private const string HitTexture = Art + "/shinv_hit.png";
        private const string WalkClip = Animation + "/Shinv_Walk.anim";
        private const string HitClip = Animation + "/Shinv_Hit.anim";
        private const string ControllerPath = Animation + "/Shinv_SpritePreview.controller";
        private const string PrefabPath = Prefabs + "/PF_ShInvSpriteSequencePreview.prefab";
        private const string ScenePath = Scenes + "/SC_ShInvSpriteSequencePreview.unity";
        private const float FrameRate = 12f;
        private const float SpritePixelsPerUnit = 100f;
        private const float PreviewScale = 0.15f;

        static BuildShinvSpriteSequencePreview()
        {
            EditorApplication.delayCall += TryBuildAfterImport;
        }

        [MenuItem("Tools/FPG/Build Shinv Sprite Sequence Preview")]
        private static void BuildFromMenu()
        {
            Build();
        }

        private static void TryBuildAfterImport()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryBuildAfterImport;
                return;
            }

            Sprite[] walkSprites = LoadSprites(WalkTexture);
            Sprite[] hitSprites = LoadSprites(HitTexture);
            if (walkSprites.Length == 0 || hitSprites.Length == 0)
            {
                return;
            }

            Build();
        }

        private static void Build()
        {
            EnsureFolder(Root + "/Editor");
            EnsureFolder(Animation);
            EnsureFolder(Prefabs);
            EnsureFolder(Scenes);

            ConfigureTexture(WalkTexture);
            ConfigureTexture(HitTexture);

            Sprite[] walkSprites = LoadSprites(WalkTexture);
            Sprite[] hitSprites = LoadSprites(HitTexture);
            if (walkSprites.Length != 19 || hitSprites.Length != 21)
            {
                Debug.LogError($"Shinv preview expected 19 walk sprites and 21 hit sprites; found {walkSprites.Length} and {hitSprites.Length}.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.LogWarning("Shinv sprite preview already exists. Remove the preview output assets before rebuilding.");
                return;
            }

            AnimationClip walkClip = CreateClip(WalkClip, walkSprites, true);
            AnimationClip hitClip = CreateClip(HitClip, hitSprites, false);
            AnimatorController controller = CreateController(walkClip, hitClip);
            GameObject prefab = CreatePrefab(controller, walkSprites[0]);
            CreatePreviewScene(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built Shinv sprite preview: {ScenePath}");
        }

        private static void ConfigureTexture(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"Missing texture importer for {assetPath}.");
                return;
            }

            bool changed = false;
            changed |= SetIfDifferent(importer.textureType, TextureImporterType.Sprite, value => importer.textureType = value);
            changed |= SetIfDifferent(importer.spriteImportMode, SpriteImportMode.Multiple, value => importer.spriteImportMode = value);
            changed |= SetIfDifferent(importer.spritePixelsPerUnit, SpritePixelsPerUnit, value => importer.spritePixelsPerUnit = value);
            changed |= SetIfDifferent(importer.maxTextureSize, 4096, value => importer.maxTextureSize = value);
            // Keep compression user-configurable in the Inspector and per-platform overrides.
            changed |= SetIfDifferent(importer.filterMode, FilterMode.Bilinear, value => importer.filterMode = value);
            changed |= SetIfDifferent(importer.mipmapEnabled, false, value => importer.mipmapEnabled = value);
            changed |= SetIfDifferent(importer.alphaIsTransparency, true, value => importer.alphaIsTransparency = value);

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static bool SetIfDifferent<T>(T current, T desired, Action<T> setValue)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(current, desired))
            {
                return false;
            }

            setValue(desired);
            return true;
        }

        private static Sprite[] LoadSprites(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => FrameNumber(sprite.name))
                .ToArray();
        }

        private static int FrameNumber(string spriteName)
        {
            string digits = new string(spriteName.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int frame) ? frame : int.MaxValue;
        }

        private static AnimationClip CreateClip(string path, Sprite[] sprites, bool loop)
        {
            var clip = new AnimationClip
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
                frameRate = FrameRate
            };

            var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / FrameRate,
                    value = sprites[Math.Min(i, sprites.Length - 1)]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"),
                keys);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static AnimatorController CreateController(AnimationClip walkClip, AnimationClip hitClip)
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState walkState = stateMachine.AddState("Walk");
            walkState.motion = walkClip;
            walkState.writeDefaultValues = false;
            stateMachine.defaultState = walkState;

            AnimatorState hitState = stateMachine.AddState("Hit");
            hitState.motion = hitClip;
            hitState.writeDefaultValues = false;

            AnimatorStateTransition toHit = stateMachine.AddAnyStateTransition(hitState);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            toHit.hasExitTime = false;
            toHit.duration = 0f;
            toHit.canTransitionToSelf = false;

            AnimatorStateTransition backToWalk = hitState.AddTransition(walkState);
            backToWalk.hasExitTime = true;
            backToWalk.exitTime = 1f;
            backToWalk.duration = 0f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject CreatePrefab(AnimatorController controller, Sprite firstSprite)
        {
            var instance = new GameObject("Shinv Sprite Sequence Preview");
            instance.transform.localScale = Vector3.one * PreviewScale;

            SpriteRenderer spriteRenderer = instance.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = firstSprite;
            spriteRenderer.sortingOrder = 10;

            Animator animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            ShinvSpriteSequencePreviewControls controls = instance.AddComponent<ShinvSpriteSequencePreviewControls>();
            controls.Configure(animator, "Hit");

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void CreatePreviewScene(GameObject prefab)
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);

                var cameraObject = new GameObject("Preview Camera");
                cameraObject.transform.position = new Vector3(0f, 0.55f, -10f);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 0.85f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.14f, 0.16f, 1f);

                PrefabUtility.InstantiatePrefab(prefab);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
