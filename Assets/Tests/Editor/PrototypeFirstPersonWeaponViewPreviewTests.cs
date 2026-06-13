using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrototypeFirstPersonWeaponViewPreviewTests
{
    private Scene previewScene;
    private readonly List<UnityEngine.Object> objectsToDestroy = new List<UnityEngine.Object>();

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
        viewType.GetField("weaponTextures", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(view, CreateTestTextures(7));

        viewType.GetMethod("RebuildWeapons", BindingFlags.Instance | BindingFlags.Public)
            .Invoke(view, null);

        foreach (string weaponName in new[] { "Left Blade", "Center Sword", "Right Brush" })
        {
            Transform weapon = FindChildRecursive(viewObject.transform, weaponName);
            Transform hitbox = FindChildRecursive(viewObject.transform, weaponName + " Pointer Hitbox");

            Assert.IsNotNull(weapon, weaponName + " should be generated.");
            Assert.IsNotNull(hitbox, weaponName + " should create a stable pointer hitbox.");
            Assert.IsNotNull(hitbox.GetComponent<BoxCollider>(), weaponName + " should use a BoxCollider for pointer hit tests.");
        }
    }

    private GameObject CreateTrackedGameObject(string name)
    {
        var gameObject = new GameObject(name);
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private Texture2D[] CreateTestTextures(int count)
    {
        var textures = new Texture2D[count];
        for (int i = 0; i < textures.Length; i++)
        {
            textures[i] = new Texture2D(4, 4);
            textures[i].name = "Test Weapon Texture " + i;
            objectsToDestroy.Add(textures[i]);
        }

        return textures;
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
