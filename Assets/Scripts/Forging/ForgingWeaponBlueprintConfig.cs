using System;
using System.Collections.Generic;
using NewFPG.Combat;
using NewFPG.Combat.SkillIndicators;
using UnityEngine;

namespace NewFPG.Forging
{
    [CreateAssetMenu(fileName = "ForgingWeaponBlueprint", menuName = "NewFPG/Forging/Weapon Blueprint Config")]
    public sealed class ForgingWeaponBlueprintConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string blueprintId;
        [SerializeField] private string displayName;
        [SerializeField] private string skillLogicId;

        [Header("Grid Shape")]
        [SerializeField, Min(1)] private int width = 3;
        [SerializeField, Min(1)] private int height = 3;
        [SerializeField] private List<Vector2Int> cells = new List<Vector2Int>();

        [Header("Skill Scaling")]
        [SerializeField] private List<ForgingSkillScaling> skillScalings = new List<ForgingSkillScaling>();
        [SerializeField, TextArea] private string skillDescription;

        [Header("Runtime Binding")]
        [SerializeField] private WeaponDefinition weaponDefinitionAsset;
        [SerializeField] private string weaponDefinitionAssetPath;
        [SerializeField] private Sprite hudIcon;
        [SerializeField] private string hudIconPath;
#pragma warning disable CS0618
        [Obsolete("Legacy migration field. Weapon geometry is authored on WeaponDefinition/runtime binding.")]
        [SerializeField, HideInInspector] private string indicatorConfigPath;
#pragma warning restore CS0618
        [SerializeField] private GameObject releaseEffectPrefab;
        [SerializeField] private string releaseEffectPrefabPath;
        [SerializeField] private GameObject hitEffectPrefab;
        [SerializeField] private string hitEffectPrefabPath;
        [SerializeField, Min(0f)] private float resourceCost;
        [SerializeField, Min(0f)] private float baseDamage;
        [SerializeField, Min(0.05f)] private float cooldown = 0.4f;
        [SerializeField, Min(0.1f)] private float range = 8f;
        [SerializeField, Min(0.05f)] private float radius = 0.55f;
        [SerializeField] private SkillIndicatorShapeType shapeType = SkillIndicatorShapeType.GroundCircle;
        [SerializeField, Min(0.05f)] private float shapeWidth = 1.1f;
        [SerializeField, Min(0.1f)] private float shapeLength = 8f;
        [SerializeField, Range(1f, 360f)] private float angle = 90f;
        [SerializeField, Min(0f)] private float shapeHeight = 2f;
        [SerializeField, Min(0f)] private float groundOffset = 0.06f;
        [SerializeField] private SkillIndicatorInputMode inputMode = SkillIndicatorInputMode.HoldPreview;
        [SerializeField] private SkillIndicatorDefaultReleasePolicy tapPolicy = SkillIndicatorDefaultReleasePolicy.AutoSelectBestTarget;
        [SerializeField] private SkillIndicatorDefaultReleasePolicy holdPolicy = SkillIndicatorDefaultReleasePolicy.CastAtCrosshairHit;
        [SerializeField] private SkillIndicatorInvalidReleasePolicy invalidReleasePolicy = SkillIndicatorInvalidReleasePolicy.Cancel;
        [SerializeField] private SkillIndicatorAimSource aimSource = SkillIndicatorAimSource.CrosshairRay;
        [SerializeField] private bool requireSurfaceHit;
        [SerializeField] private bool clampToRange = true;
        [SerializeField] private SkillIndicatorPlacementMode placementMode = SkillIndicatorPlacementMode.GroundSurface;
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField, Min(0f)] private float tapMaxDuration = 0.16f;
        [SerializeField, Min(0f)] private float holdEnterDelay = 0.1f;
        [SerializeField, Min(0f)] private float castDelay;
        [SerializeField, Min(0f)] private float warningTime;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField, Min(0f)] private float fadeOut = 0.15f;
        [SerializeField] private string previewPrefabResourceId;
        [SerializeField] private string validMaterialResourceId = "M_IND_OwnerValid";
        [SerializeField] private string invalidMaterialResourceId = "M_IND_Invalid";
        [SerializeField] private string confirmAudioResourceId = "S_IND_ConfirmRelease";
        [SerializeField] private string invalidAudioResourceId = "S_IND_Invalid";
        [SerializeField] private bool debugDraw;

        public string BlueprintId => blueprintId;

        public ForgingWeaponBlueprintDefinition ToDefinition()
        {
#pragma warning disable CS0618
            string legacyIndicatorConfigPath = indicatorConfigPath;
#pragma warning restore CS0618
            ForgingWeaponRuntimeBinding runtime = new ForgingWeaponRuntimeBinding
            {
                weaponDefinitionAssetPath = ForgingAssetPathUtility.GetAssetPath(weaponDefinitionAsset, weaponDefinitionAssetPath),
                hudIconPath = ForgingAssetPathUtility.GetAssetPath(hudIcon, hudIconPath),
                releaseEffectPrefabPath = ForgingAssetPathUtility.GetAssetPath(releaseEffectPrefab, releaseEffectPrefabPath),
                hitEffectPrefabPath = ForgingAssetPathUtility.GetAssetPath(hitEffectPrefab, hitEffectPrefabPath),
                resourceCost = Mathf.Max(0f, resourceCost),
                baseDamage = Mathf.Max(0f, baseDamage),
                cooldown = Mathf.Max(0.05f, cooldown),
                range = Mathf.Max(0.1f, range),
                radius = Mathf.Max(0.05f, radius),
                shapeType = shapeType,
                width = shapeWidth,
                length = shapeLength,
                angle = angle,
                height = shapeHeight,
                groundOffset = groundOffset,
                inputMode = inputMode,
                tapPolicy = tapPolicy,
                holdPolicy = holdPolicy,
                invalidReleasePolicy = invalidReleasePolicy,
                aimSource = aimSource,
                requireSurfaceHit = requireSurfaceHit,
                clampToRange = clampToRange,
                placementMode = placementMode,
                surfaceMask = surfaceMask.value,
                collisionMask = collisionMask.value,
                tapMaxDuration = tapMaxDuration,
                holdEnterDelay = holdEnterDelay,
                castDelay = castDelay,
                warningTime = warningTime,
                duration = duration,
                fadeOut = fadeOut,
                previewPrefabResourceId = previewPrefabResourceId,
                validMaterialResourceId = validMaterialResourceId,
                invalidMaterialResourceId = invalidMaterialResourceId,
                confirmAudioResourceId = confirmAudioResourceId,
                invalidAudioResourceId = invalidAudioResourceId,
                debugDraw = debugDraw,
            };
#pragma warning disable CS0618
            runtime.indicatorConfigPath = legacyIndicatorConfigPath;
#pragma warning restore CS0618
            runtime.Normalize();

            return new ForgingWeaponBlueprintDefinition
            {
                blueprintId = blueprintId,
                displayName = displayName,
                skillLogicId = skillLogicId,
                width = Mathf.Max(1, width),
                height = Mathf.Max(1, height),
                cells = new List<Vector2Int>(cells ?? new List<Vector2Int>()),
                skillScalings = new List<ForgingSkillScaling>(skillScalings ?? new List<ForgingSkillScaling>()),
                runtime = runtime,
                skillDescription = skillDescription,
            };
        }

        public void ApplyDefinition(ForgingWeaponBlueprintDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            blueprintId = definition.blueprintId;
            displayName = definition.displayName;
            skillLogicId = definition.skillLogicId;
            width = Mathf.Max(1, definition.width);
            height = Mathf.Max(1, definition.height);
            cells = new List<Vector2Int>(definition.cells ?? new List<Vector2Int>());
            skillScalings = new List<ForgingSkillScaling>(definition.skillScalings ?? new List<ForgingSkillScaling>());
            skillDescription = definition.skillDescription;

            ForgingWeaponRuntimeBinding runtime = definition.runtime ?? new ForgingWeaponRuntimeBinding();
            runtime.Normalize();
            weaponDefinitionAssetPath = runtime.weaponDefinitionAssetPath;
            hudIconPath = runtime.hudIconPath;
#pragma warning disable CS0618
            indicatorConfigPath = runtime.indicatorConfigPath;
#pragma warning restore CS0618
            releaseEffectPrefabPath = runtime.releaseEffectPrefabPath;
            hitEffectPrefabPath = runtime.hitEffectPrefabPath;
            weaponDefinitionAsset = ForgingAssetPathUtility.LoadAssetAtPath<WeaponDefinition>(weaponDefinitionAssetPath);
            hudIcon = ForgingAssetPathUtility.LoadAssetAtPath<Sprite>(hudIconPath);
            releaseEffectPrefab = ForgingAssetPathUtility.LoadAssetAtPath<GameObject>(releaseEffectPrefabPath);
            hitEffectPrefab = ForgingAssetPathUtility.LoadAssetAtPath<GameObject>(hitEffectPrefabPath);
            resourceCost = Mathf.Max(0f, runtime.resourceCost);
            baseDamage = Mathf.Max(0f, runtime.baseDamage);
            cooldown = Mathf.Max(0.05f, runtime.cooldown);
            range = Mathf.Max(0.1f, runtime.range);
            radius = Mathf.Max(0.05f, runtime.radius);
            shapeType = runtime.shapeType;
            shapeWidth = runtime.width;
            shapeLength = runtime.length;
            angle = runtime.angle;
            shapeHeight = runtime.height;
            groundOffset = runtime.groundOffset;
            inputMode = runtime.inputMode;
            tapPolicy = runtime.tapPolicy;
            holdPolicy = runtime.holdPolicy;
            invalidReleasePolicy = runtime.invalidReleasePolicy;
            aimSource = runtime.aimSource;
            requireSurfaceHit = runtime.requireSurfaceHit;
            clampToRange = runtime.clampToRange;
            placementMode = runtime.placementMode;
            surfaceMask = new LayerMask { value = runtime.surfaceMask };
            collisionMask = new LayerMask { value = runtime.collisionMask };
            tapMaxDuration = runtime.tapMaxDuration;
            holdEnterDelay = runtime.holdEnterDelay;
            castDelay = runtime.castDelay;
            warningTime = runtime.warningTime;
            duration = runtime.duration;
            fadeOut = runtime.fadeOut;
            previewPrefabResourceId = runtime.previewPrefabResourceId;
            validMaterialResourceId = runtime.validMaterialResourceId;
            invalidMaterialResourceId = runtime.invalidMaterialResourceId;
            confirmAudioResourceId = runtime.confirmAudioResourceId;
            invalidAudioResourceId = runtime.invalidAudioResourceId;
            debugDraw = runtime.debugDraw;
            NormalizeShape();
        }

        private void OnValidate()
        {
            NormalizeShape();
        }

        private void NormalizeShape()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            ForgingShapeUtility.NormalizeCells(cells, width, height, false);
            resourceCost = Mathf.Max(0f, resourceCost);
            baseDamage = Mathf.Max(0f, baseDamage);
            cooldown = Mathf.Max(0.05f, cooldown);
            range = Mathf.Max(0.1f, range);
            radius = Mathf.Max(0.05f, radius);
            shapeWidth = shapeWidth > 0f ? Mathf.Max(0.05f, shapeWidth) : radius * 2f;
            shapeLength = shapeLength > 0f ? Mathf.Max(0.1f, shapeLength) : range;
            angle = Mathf.Clamp(angle, 1f, 360f);
            shapeHeight = Mathf.Max(0f, shapeHeight);
            groundOffset = Mathf.Max(0f, groundOffset);
            tapMaxDuration = Mathf.Max(0f, tapMaxDuration);
            holdEnterDelay = Mathf.Max(0f, holdEnterDelay);
            castDelay = Mathf.Max(0f, castDelay);
            warningTime = Mathf.Max(0f, warningTime);
            duration = Mathf.Max(0f, duration);
            fadeOut = Mathf.Max(0f, fadeOut);

            if (string.IsNullOrWhiteSpace(validMaterialResourceId))
            {
                validMaterialResourceId = "M_IND_OwnerValid";
            }

            if (string.IsNullOrWhiteSpace(invalidMaterialResourceId))
            {
                invalidMaterialResourceId = "M_IND_Invalid";
            }

            if (string.IsNullOrWhiteSpace(confirmAudioResourceId))
            {
                confirmAudioResourceId = "S_IND_ConfirmRelease";
            }

            if (string.IsNullOrWhiteSpace(invalidAudioResourceId))
            {
                invalidAudioResourceId = "S_IND_Invalid";
            }
        }
    }
}
