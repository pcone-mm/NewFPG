using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ProBuilderDefaultMaterialRepair
{
    private const string DefaultMaterialPath =
        "Packages/com.unity.probuilder/Content/Resources/Materials/ProBuilderDefault.mat";
    private const string DefaultMaterialGuid = "c22777d6e868e4f2fb421913386b154e";
    private const string ProBuilderMeshTypeName =
        "UnityEngine.ProBuilder.ProBuilderMesh, Unity.ProBuilder";
    private const string ProBuilderShaderName = "ProBuilder6/Standard Vertex Color";
    private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
    private const double RepairIntervalSeconds = 1.0;

    private static bool repairQueued;
    private static bool loggedShaderPatch;
    private static bool warnedMissingMaterial;
    private static double nextRepairTime;

    static ProBuilderDefaultMaterialRepair()
    {
        EditorApplication.delayCall += RepairLoadedScenes;
        EditorApplication.hierarchyChanged += QueueRepair;
        EditorApplication.update += RepairPeriodically;
    }

    [MenuItem("Tools/ProBuilder/Repair Default Materials", false, 2000)]
    public static void RepairLoadedScenes()
    {
        repairQueued = false;

        Type proBuilderMeshType = Type.GetType(ProBuilderMeshTypeName);
        if (proBuilderMeshType == null)
        {
            return;
        }

        Material defaultMaterial = LoadDefaultMaterial();
        if (defaultMaterial == null)
        {
            if (!warnedMissingMaterial)
            {
                Debug.LogWarning(
                    $"[ProBuilder Default Material Repair] Could not load {DefaultMaterialPath}.");
                warnedMissingMaterial = true;
            }

            return;
        }

        warnedMissingMaterial = false;

        int repairedCount = 0;
        foreach (MeshRenderer renderer in Resources.FindObjectsOfTypeAll<MeshRenderer>())
        {
            if (!IsLoadedSceneObject(renderer) ||
                renderer.GetComponent(proBuilderMeshType) == null)
            {
                continue;
            }

            if (RepairRenderer(renderer, defaultMaterial))
            {
                repairedCount++;
            }
        }

        if (repairedCount > 0)
        {
            Debug.Log(
                $"[ProBuilder Default Material Repair] Restored {repairedCount} renderer(s) to ProBuilderDefault.mat.");
        }
    }

    private static void QueueRepair()
    {
        if (repairQueued)
        {
            return;
        }

        repairQueued = true;
        EditorApplication.delayCall += RepairLoadedScenes;
    }

    private static void RepairPeriodically()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now < nextRepairTime)
        {
            return;
        }

        nextRepairTime = now + RepairIntervalSeconds;
        RepairLoadedScenes();
    }

    private static Material LoadDefaultMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        if (material != null)
        {
            PatchDefaultMaterialForUrp(material);
            return material;
        }

        string guidPath = AssetDatabase.GUIDToAssetPath(DefaultMaterialGuid);
        material = string.IsNullOrEmpty(guidPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Material>(guidPath);
        PatchDefaultMaterialForUrp(material);
        return material;
    }

    private static void PatchDefaultMaterialForUrp(Material material)
    {
        if (material == null ||
            !IsUniversalRenderPipeline())
        {
            return;
        }

        bool usesProBuilderShader = material.shader != null &&
            material.shader.name == ProBuilderShaderName;
        if (usesProBuilderShader)
        {
            Shader urpLitShader = Shader.Find(UrpLitShaderName);
            if (urpLitShader == null)
            {
                return;
            }

            Texture mainTexture = material.HasProperty("_MainTex")
                ? material.GetTexture("_MainTex")
                : null;
            Color color = material.HasProperty("_Color")
                ? material.GetColor("_Color")
                : Color.white;

            material.shader = urpLitShader;

            if (material.HasProperty("_BaseMap") && mainTexture != null)
            {
                material.SetTexture("_BaseMap", mainTexture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (!loggedShaderPatch)
            {
                Debug.Log(
                    $"[ProBuilder Default Material Repair] Patched ProBuilderDefault.mat shader to {UrpLitShaderName} for URP.");
                loggedShaderPatch = true;
            }
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.35f);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.black);
        }

        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    }

    private static bool IsUniversalRenderPipeline()
    {
        RenderPipelineAsset currentPipeline = GraphicsSettings.currentRenderPipeline;
        return currentPipeline != null &&
            currentPipeline.GetType().FullName.Contains("Universal");
    }

    private static bool IsLoadedSceneObject(Component component)
    {
        GameObject gameObject = component.gameObject;
        Scene scene = gameObject.scene;
        return scene.IsValid() &&
            scene.isLoaded &&
            !EditorUtility.IsPersistent(gameObject);
    }

    private static bool RepairRenderer(MeshRenderer renderer, Material defaultMaterial)
    {
        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            renderer.sharedMaterials = new[] { defaultMaterial };
            MarkDirty(renderer);
            return true;
        }

        bool changed = false;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null)
            {
                materials[i] = defaultMaterial;
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        renderer.sharedMaterials = materials;
        MarkDirty(renderer);
        return true;
    }

    private static void MarkDirty(Component component)
    {
        EditorUtility.SetDirty(component);
        Scene scene = component.gameObject.scene;
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
