using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Combat.SkillIndicators
{
    public sealed class SkillIndicatorRendererPool
    {
        private readonly Transform root;
        private readonly Dictionary<string, Stack<GameObject>> pool = new Dictionary<string, Stack<GameObject>>();
        private GameObject activeInstance;
        private string activeResourceId;
        private Material fallbackValidMaterial;
        private Material fallbackInvalidMaterial;

        public SkillIndicatorRendererPool(Transform root)
        {
            this.root = root;
        }

        public GameObject Show(string resourceId, SkillIndicatorTemporaryArtIndex artIndex)
        {
            resourceId = string.IsNullOrWhiteSpace(resourceId) ? "PF_IND_GroundCircle" : resourceId;
            if (activeInstance != null && string.Equals(activeResourceId, resourceId, System.StringComparison.Ordinal))
            {
                activeInstance.SetActive(true);
                return activeInstance;
            }

            HideActive();
            activeResourceId = resourceId;
            activeInstance = GetOrCreate(resourceId, artIndex);
            activeInstance.SetActive(true);
            return activeInstance;
        }

        public void HideActive()
        {
            if (activeInstance == null)
            {
                activeResourceId = null;
                return;
            }

            activeInstance.SetActive(false);
            if (!pool.TryGetValue(activeResourceId, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                pool[activeResourceId] = stack;
            }

            stack.Push(activeInstance);
            activeInstance = null;
            activeResourceId = null;
        }

        public Material ResolveMaterial(SkillIndicatorTemporaryArtIndex artIndex, string resourceId, bool valid)
        {
            Material material = artIndex != null ? artIndex.GetMaterial(resourceId) : null;
            if (material != null)
            {
                return material;
            }

            return valid ? GetFallbackValidMaterial() : GetFallbackInvalidMaterial();
        }

        private GameObject GetOrCreate(string resourceId, SkillIndicatorTemporaryArtIndex artIndex)
        {
            if (pool.TryGetValue(resourceId, out Stack<GameObject> stack) && stack.Count > 0)
            {
                return stack.Pop();
            }

            GameObject prefab = artIndex != null ? artIndex.GetPrefab(resourceId) : null;
            GameObject instance = prefab != null ? Object.Instantiate(prefab, root) : CreateFallbackInstance(resourceId);
            instance.name = resourceId + "_Runtime";
            instance.transform.SetParent(root, false);
            instance.SetActive(false);
            return instance;
        }

        private GameObject CreateFallbackInstance(string resourceId)
        {
            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                DestroySafe(collider);
            }

            MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = GetFallbackValidMaterial();
            }

            return instance;
        }

        private Material GetFallbackValidMaterial()
        {
            if (fallbackValidMaterial == null)
            {
                fallbackValidMaterial = CreateFallbackMaterial(new Color(0.1f, 0.72f, 1f, 0.82f));
            }

            return fallbackValidMaterial;
        }

        private Material GetFallbackInvalidMaterial()
        {
            if (fallbackInvalidMaterial == null)
            {
                fallbackInvalidMaterial = CreateFallbackMaterial(new Color(1f, 0.12f, 0.08f, 0.84f));
            }

            return fallbackInvalidMaterial;
        }

        private static Material CreateFallbackMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Color");
            Material material = new Material(shader);
            material.color = color;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }

        private static void DestroySafe(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
