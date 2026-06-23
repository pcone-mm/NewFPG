using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrototypeFirstPersonWeaponViewPreviewTests
{
    private Scene previewScene;
    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();
    private readonly List<Camera> disabledCameras = new List<Camera>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            if (objectsToDestroy[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(objectsToDestroy[i]);
            }
        }

        objectsToDestroy.Clear();

        for (int i = 0; i < disabledCameras.Count; i++)
        {
            if (disabledCameras[i] != null)
            {
                disabledCameras[i].enabled = true;
            }
        }

        disabledCameras.Clear();

        if (previewScene.IsValid())
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    [Test]
    public void PreviewSceneInstanceCannotModifySceneObject()
    {
        Type viewType = Type.GetType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp", true);
        MethodInfo canModifySceneObject = viewType.GetMethod("CanModifySceneObject", BindingFlags.Instance | BindingFlags.NonPublic);

        previewScene = EditorSceneManager.NewPreviewScene();
        var gameObject = new GameObject("Preview Weapon View");
        SceneManager.MoveGameObjectToScene(gameObject, previewScene);
        Component view = gameObject.AddComponent(viewType);

        Assert.IsFalse((bool)canModifySceneObject.Invoke(view, null));
    }

    [Test]
    public void RebuildWeaponsCreatesPointerHitColliders()
    {
        Assert.That(
            LayerMask.NameToLayer("FirstPersonWeapon"),
            Is.GreaterThanOrEqualTo(0),
            "This test expects the FirstPersonWeapon layer to exist.");

        Type viewType = Type.GetType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp", true);
        GameObject cameraObject = CreateTrackedGameObject("World Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();

        GameObject viewObject = CreateTrackedGameObject("First Person Weapon View");
        Component view = viewObject.AddComponent(viewType);
        ApplyPresentations(
            viewType,
            view,
            new[] { "Left Blade", "Center Sword", "Right Brush" },
            CreateTestSprites(3, new Rect(0f, 0f, 4f, 4f)));

        viewType.GetMethod("RebuildWeapons", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(view, null);

        foreach (string weaponName in new[] { "Left Blade 1", "Center Sword 2", "Right Brush 3" })
        {
            Transform weapon = FindChildRecursive(viewObject.transform, weaponName);
            Transform hitbox = FindChildRecursive(viewObject.transform, weaponName + " Pointer Hitbox");

            Assert.IsNotNull(weapon, weaponName + " should be generated.");
            Assert.IsNotNull(hitbox, weaponName + " should create a stable pointer hitbox.");
            Assert.IsNotNull(hitbox.GetComponent<BoxCollider>(), weaponName + " should use a BoxCollider for pointer hit tests.");

            MeshFilter meshFilter = weapon.GetComponent<MeshFilter>();
            Assert.IsNotNull(meshFilter, weaponName + " should use an explicit camera-facing mesh.");
            Assert.That(meshFilter.sharedMesh.normals[0].z, Is.LessThan(-0.9f), weaponName + " should face the first-person camera.");

            Renderer renderer = weapon.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, weaponName + " should have a renderer.");
            Material material = renderer.sharedMaterial;
            Assert.IsNotNull(material, weaponName + " should have a material.");
            if (material.HasProperty("_BaseColor"))
            {
                Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white), weaponName + " should render without tint.");
            }

            if (material.HasProperty("_Color"))
            {
                Assert.That(material.GetColor("_Color"), Is.EqualTo(Color.white), weaponName + " should render without tint.");
            }
        }
    }

    [Test]
    public void WeaponDefinitionIconCreatesVisibleWeaponQuad()
    {
        Assert.That(
            LayerMask.NameToLayer("FirstPersonWeapon"),
            Is.GreaterThanOrEqualTo(0),
            "This test expects the FirstPersonWeapon layer to exist.");

        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Weapons/HUD/Xianxia_FlyingSword.png");
        Assert.IsNotNull(icon, "HUD debug weapon icon should be available.");
        Assert.That(icon.rect.width / icon.rect.height, Is.EqualTo(1f).Within(0.01f));

        Type viewType = Type.GetType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp", true);
        Type presentationType = viewType.GetNestedType("WeaponPresentation", BindingFlags.Public);

        GameObject cameraObject = CreateTrackedGameObject("World Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();

        GameObject viewObject = CreateTrackedGameObject("First Person Weapon View");
        Component view = viewObject.AddComponent(viewType);

        Array presentations = Array.CreateInstance(presentationType, 1);
        presentations.SetValue(Activator.CreateInstance(presentationType, "Flying Sword", icon), 0);
        viewType.GetMethod("SetWeaponPresentations", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(view, new object[] { presentations });

        Transform weapon = FindChildRecursive(viewObject.transform, "Flying Sword 1");
        Assert.IsNotNull(weapon, "WeaponDefinition icon should generate a first-person weapon quad.");
        Assert.That(weapon.localScale.y, Is.GreaterThan(weapon.localScale.x * 0.8f));
    }

    [Test]
    public void LongWeaponDefinitionIconKeepsConfiguredAspectRatio()
    {
        Assert.That(
            LayerMask.NameToLayer("FirstPersonWeapon"),
            Is.GreaterThanOrEqualTo(0),
            "This test expects the FirstPersonWeapon layer to exist.");

        Sprite icon = CreateTestSprites(1, new Rect(0f, 0f, 24f, 4f))[0];
        Assert.That(icon.rect.width / icon.rect.height, Is.GreaterThan(5f));

        Type viewType = Type.GetType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp", true);
        Type presentationType = viewType.GetNestedType("WeaponPresentation", BindingFlags.Public);

        GameObject cameraObject = CreateTrackedGameObject("World Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();

        GameObject viewObject = CreateTrackedGameObject("First Person Weapon View");
        Component view = viewObject.AddComponent(viewType);

        Array presentations = Array.CreateInstance(presentationType, 1);
        presentations.SetValue(Activator.CreateInstance(presentationType, "Long Sword", icon), 0);
        viewType.GetMethod("SetWeaponPresentations", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(view, new object[] { presentations });

        Transform weapon = FindChildRecursive(viewObject.transform, "Long Sword 1");
        Assert.IsNotNull(weapon, "WeaponDefinition long icon should generate a first-person weapon quad.");
        float expectedAspect = icon.textureRect.width / icon.textureRect.height;
        Assert.That(
            weapon.localScale.x / weapon.localScale.y,
            Is.EqualTo(expectedAspect).Within(0.01f),
            "WeaponDefinition icon art should keep the configured sprite aspect ratio.");
    }

    [Test]
    public void RefreshRuntimeViewRebuildsAfterCameraIsAssignedLate()
    {
        Assert.That(
            LayerMask.NameToLayer("FirstPersonWeapon"),
            Is.GreaterThanOrEqualTo(0),
            "This test expects the FirstPersonWeapon layer to exist.");

        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Weapons/HUD/Xianxia_FlyingSword.png");
        Assert.IsNotNull(icon, "HUD debug weapon icon should be available.");

        Type viewType = Type.GetType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp", true);
        Type presentationType = viewType.GetNestedType("WeaponPresentation", BindingFlags.Public);

        DisableExistingCameras();

        GameObject viewObject = CreateTrackedGameObject("First Person Weapon View");
        Component view = viewObject.AddComponent(viewType);

        Array presentations = Array.CreateInstance(presentationType, 1);
        presentations.SetValue(Activator.CreateInstance(presentationType, "Late Camera Sword", icon), 0);
        viewType.GetMethod("SetWeaponPresentations", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(view, new object[] { presentations });

        Assert.IsNull(
            FindChildRecursive(viewObject.transform, "Late Camera Sword 1"),
            "Without a camera, initial presentation binding should not be able to build the first-person view.");

        GameObject cameraObject = CreateTrackedGameObject("Late World Camera");
        Camera camera = cameraObject.AddComponent<Camera>();

        viewType.GetMethod("RefreshRuntimeView", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(view, new object[] { camera });

        Transform weapon = FindChildRecursive(viewObject.transform, "Late Camera Sword 1");
        Assert.IsNotNull(weapon, "RefreshRuntimeView should rebuild weapons once a valid camera is assigned.");
    }

    private void DisableExistingCameras()
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].enabled)
            {
                cameras[i].enabled = false;
                disabledCameras.Add(cameras[i]);
            }
        }
    }

    private GameObject CreateTrackedGameObject(string name)
    {
        var gameObject = new GameObject(name);
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private void ApplyPresentations(Type viewType, Component view, string[] names, Sprite[] sprites)
    {
        Type presentationType = viewType.GetNestedType("WeaponPresentation", BindingFlags.Public);
        Array presentations = Array.CreateInstance(presentationType, names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            presentations.SetValue(Activator.CreateInstance(presentationType, names[i], sprites[i]), i);
        }

        viewType.GetMethod("SetWeaponPresentations", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(view, new object[] { presentations });
    }

    private Sprite[] CreateTestSprites(int count, Rect rect)
    {
        var sprites = new Sprite[count];
        for (int i = 0; i < sprites.Length; i++)
        {
            var texture = new Texture2D(Mathf.CeilToInt(rect.width), Mathf.CeilToInt(rect.height));
            texture.name = "Test Weapon Texture " + i;
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "Test Weapon Sprite " + i;
            sprites[i] = sprite;
            objectsToDestroy.Add(texture);
            objectsToDestroy.Add(sprite);
        }

        return sprites;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
