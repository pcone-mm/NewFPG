using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPG.Demo.Unity
{
    [Serializable]
    public sealed class D0StageSpawnPointDefinition
    {
        [D0PlannerField("出生点 ID", "舞台内稳定且唯一的出生点标识。场景与遭遇通过该 ID 选择位置，不通过对象名称或数组下标查找。")]
        [SerializeField]
        private string spawnPointId;

        [D0PlannerField("局部位置", "出生点相对 BattleSceneContext.ActorsRoot 的位置。只描述在哪里生成，不指定角色、敌人、预制体或生成 Tick。")]
        [SerializeField]
        private Vector3 localPosition;

        [D0PlannerField("局部旋转（度）", "出生点相对 BattleSceneContext.ActorsRoot 的欧拉角。只描述生成朝向，不包含视觉缩放。")]
        [SerializeField]
        private Vector3 localEulerAngles;

        public string SpawnPointId => spawnPointId;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(spawnPointId))
            {
                error = "Stage spawn point requires a stable id.";
                return false;
            }

            if (!D0StageDefinitionValidation.IsFinite(localPosition)
                || !D0StageDefinitionValidation.IsFinite(localEulerAngles))
            {
                error = $"Stage spawn point '{spawnPointId}' requires finite pose values.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// One planner-authored visual layer of the D0 single-encounter stage.
    /// The installer instantiates only presentation components from it.
    /// </summary>
    [Serializable]
    public sealed class D0StageForestLayerDefinition
    {
        [D0PlannerField("图层 ID", "背景图层的稳定唯一节点键。安装器以它更新对应视觉节点；改名会创建新节点并清理旧节点，同一舞台内不能重复。")]
        [SerializeField]
        private string layerId;

        [D0PlannerField("图层精灵资源", "该背景层直接引用的 Sprite 资源。更换后需执行 D0 安装器；不要改为运行时字符串路径加载。")]
        [SerializeField]
        private Sprite sprite;

        [D0PlannerField("图层基础局部位置", "背景层在未叠加视口偏移时的局部位置。只影响舞台视觉构图。")]
        [SerializeField]
        private Vector3 baseLocalPosition;

        [D0PlannerField("目标世界宽度（世界单位）", "按精灵原始宽度等比缩放后的目标世界宽度。必须大于等于 0.01；不裁切精灵内容。")]
        [SerializeField, Min(0.01f)]
        private float desiredWorldWidth = 1f;

        [D0PlannerField("图层渲染排序值", "SpriteRenderer 的排序值。数值越大越靠前；只改变视觉遮挡顺序。")]
        [SerializeField]
        private int sortingOrder;

        [D0PlannerField("视口偏移系数", "读取准星位置后，该背景层跟随移动的系数。仅用于背景视差，不会回写准星、相机或攻击查询。")]
        [SerializeField]
        private Vector2 viewportOffsetMultiplier;

        [D0PlannerField("水平翻转", "是否将该背景层沿 X 轴水平镜像。只影响视觉。")]
        [SerializeField]
        private bool flipX;

        [D0PlannerField("图层颜色与透明度", "叠加到背景精灵的颜色和 Alpha。只影响渲染，不改变资源文件。")]
        [SerializeField]
        private Color color = Color.white;

        public string LayerId => layerId;
        public Sprite Sprite => sprite;
        public Vector3 BaseLocalPosition => baseLocalPosition;
        public float DesiredWorldWidth => desiredWorldWidth;
        public int SortingOrder => sortingOrder;
        public Vector2 ViewportOffsetMultiplier => viewportOffsetMultiplier;
        public bool FlipX => flipX;
        public Color Color => color;

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(layerId))
            {
                error = "Stage forest layer requires a stable layer ID.";
                return false;
            }

            if (sprite == null)
            {
                error = $"Stage forest layer '{layerId}' requires a Sprite reference.";
                return false;
            }

            if (!D0StageDefinitionValidation.IsFinite(baseLocalPosition)
                || !D0StageDefinitionValidation.IsFinite(viewportOffsetMultiplier)
                || !D0StageDefinitionValidation.IsFinite(color))
            {
                error = $"Stage forest layer '{layerId}' requires finite presentation values.";
                return false;
            }

            if (float.IsNaN(desiredWorldWidth)
                || float.IsInfinity(desiredWorldWidth)
                || desiredWorldWidth < 0.01f)
            {
                error = $"Stage forest layer '{layerId}' requires a finite positive world width.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Planner-owned environment and reusable spawn-point composition. Actor
    /// visuals, skills, entity prefabs, hitboxes, waves and spawn timing belong
    /// to their character, enemy, encounter or scenario definitions.
    /// </summary>
    [CreateAssetMenu(
        fileName = "D0StageDefinition",
        menuName = "FPG Demo/Config/D0 Stage Definition")]
    public sealed class D0StageDefinition : ScriptableObject
    {
        private static readonly D0StageForestLayerDefinition[] EmptyLayers =
            Array.Empty<D0StageForestLayerDefinition>();

        [D0PlannerSection("基础信息")]
        [D0PlannerField("舞台内部标识", "用于资产识别和校验的稳定字符串。遭遇通过对本舞台资产的直接引用关联，不通过此 ID 查找；保持非空且稳定。")]
        [SerializeField]
        private string stageId = "combatlab-forest";

        [D0PlannerField("显示名称", "供策划、验证日志和编辑器识别的舞台名称，不参与战斗计算。")]
        [SerializeField]
        private string displayName = "CombatLab Forest";

        [TextArea]
        [D0PlannerField("策划说明", "记录关卡构图、资源替换和验证备注；运行时不会读取此文本。")]
        [SerializeField]
        private string designerNotes;

        [D0PlannerSection("遭遇出生点")]
        [D0PlannerField("出生点列表", "舞台只定义可复用的具名 SpawnPoint 位置与朝向。玩家选择哪个点由场景配置决定，敌人和生成 Tick 由遭遇 SpawnSlot 决定。")]
        [SerializeField]
        private D0StageSpawnPointDefinition[] spawnPoints =
            Array.Empty<D0StageSpawnPointDefinition>();

        [D0PlannerSection("森林背景图层")]
        [D0PlannerField("森林背景图层列表", "单遭遇舞台使用的背景层。每层直接引用 Sprite，并配置构图、排序和视差；修改后需执行 D0 安装器。")]
        [SerializeField]
        private D0StageForestLayerDefinition[] forestLayers = EmptyLayers;

        public string StageId => stageId;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;
        public IReadOnlyList<D0StageSpawnPointDefinition> SpawnPoints =>
            spawnPoints ?? Array.Empty<D0StageSpawnPointDefinition>();
        public IReadOnlyList<D0StageForestLayerDefinition> ForestLayers =>
            forestLayers ?? EmptyLayers;

        public bool TryGetSpawnPoint(
            string spawnPointId,
            out D0StageSpawnPointDefinition definition)
        {
            D0StageSpawnPointDefinition[] points = spawnPoints
                ?? Array.Empty<D0StageSpawnPointDefinition>();
            for (int index = 0; index < points.Length; index++)
            {
                D0StageSpawnPointDefinition candidate = points[index];
                if (candidate != null
                    && string.Equals(
                        candidate.SpawnPointId,
                        spawnPointId,
                        StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(stageId) || string.IsNullOrWhiteSpace(displayName))
            {
                error = "Stage definition requires stable ID and display name values.";
                return false;
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                error = "Stage definition requires at least one encounter spawn point.";
                return false;
            }

            HashSet<string> spawnPointIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                D0StageSpawnPointDefinition spawnPoint = spawnPoints[index];
                if (spawnPoint == null || !spawnPoint.TryValidate(out error))
                {
                    if (string.IsNullOrEmpty(error))
                    {
                        error = $"Stage spawn point {index} is missing.";
                    }

                    return false;
                }

                if (!spawnPointIds.Add(spawnPoint.SpawnPointId))
                {
                    error = $"Stage spawn point id '{spawnPoint.SpawnPointId}' must be unique.";
                    return false;
                }
            }

            if (forestLayers == null || forestLayers.Length == 0)
            {
                error = "Stage definition requires at least one forest presentation layer.";
                return false;
            }

            HashSet<string> layerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < forestLayers.Length; index++)
            {
                D0StageForestLayerDefinition layer = forestLayers[index];
                if (layer == null)
                {
                    error = $"Stage forest layer {index} is missing.";
                    return false;
                }

                if (!layer.TryValidate(out error))
                {
                    return false;
                }

                if (!layerIds.Add(layer.LayerId))
                {
                    error = $"Stage forest layer ID '{layer.LayerId}' must be unique.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }

    internal static class D0StageDefinitionValidation
    {
        public static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static bool IsFinite(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g)
                   && IsFinite(value.b) && IsFinite(value.a);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
