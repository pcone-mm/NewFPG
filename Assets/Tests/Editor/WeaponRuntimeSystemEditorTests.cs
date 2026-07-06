using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class WeaponRuntimeSystemEditorTests
{
    [Test]
    public void ResolverUsesLatestWeaponDefinitionValues()
    {
        ScriptableObject weapon = CreateWeapon("RuntimeSourceWeapon", "runtime_source", damage: 10f, cooldown: 0.4f, range: 5f, radius: 0.6f);
        try
        {
            object firstStats = ResolveWeapon(weapon);
            Assert.That(GetProperty<float>(firstStats, "Damage"), Is.EqualTo(10f).Within(0.0001f));
            Assert.That(GetProperty<float>(firstStats, "Cooldown"), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(GetProperty<float>(firstStats, "Range"), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(GetProperty<float>(firstStats, "Radius"), Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(GetProperty<float>(firstStats, "Width"), Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(GetProperty<float>(firstStats, "Length"), Is.EqualTo(5f).Within(0.0001f));

            SetField(weapon, "damage", 24f);
            SetField(weapon, "cooldown", 0.9f);
            SetField(weapon, "range", 11f);
            SetField(weapon, "radius", 1.8f);
            SetField(weapon, "shapeType", Enum.Parse(RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorShapeType, Assembly-CSharp"), "Cone"));
            SetField(weapon, "width", 3.6f);
            SetField(weapon, "length", 7.5f);
            SetField(weapon, "angle", 72f);

            object nextStats = ResolveWeapon(weapon);
            Assert.That(GetProperty<float>(nextStats, "Damage"), Is.EqualTo(24f).Within(0.0001f));
            Assert.That(GetProperty<float>(nextStats, "Cooldown"), Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(GetProperty<float>(nextStats, "Range"), Is.EqualTo(11f).Within(0.0001f));
            Assert.That(GetProperty<float>(nextStats, "Radius"), Is.EqualTo(1.8f).Within(0.0001f));
            Assert.That(GetProperty(nextStats, "ShapeType").ToString(), Is.EqualTo("Cone"));
            Assert.That(GetProperty<float>(nextStats, "Width"), Is.EqualTo(3.6f).Within(0.0001f));
            Assert.That(GetProperty<float>(nextStats, "Length"), Is.EqualTo(7.5f).Within(0.0001f));
            Assert.That(GetProperty<float>(nextStats, "Angle"), Is.EqualTo(72f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void MigrationCopiesLegacyIndicatorDisplayIntoWeaponDefinition()
    {
        Type configType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorConfig, Assembly-CSharp");
        Type shapeType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorShapeType, Assembly-CSharp");
        Type migrationType = RequireType("NewFPG.EditorTools.WeaponDefinitionGeometryMigrationUtility, Assembly-CSharp-Editor");
        ScriptableObject weapon = CreateWeapon("MigrationWeapon", "migration_weapon", damage: 10f, cooldown: 0.4f, range: 30f, radius: 0.5f);
        ScriptableObject config = ScriptableObject.CreateInstance(configType);
        try
        {
            SetField(config, "shapeType", Enum.Parse(shapeType, "TargetReticle"));
            SetField(config, "range", 10f);
            SetField(config, "radius", 0.75f);
            SetField(config, "width", 1.2f);
            SetField(config, "length", 10f);
            SetField(config, "angle", 90f);
            SetField(weapon, "indicatorConfig", config);

            object migrated = InvokeStatic(migrationType, "MigrateWeaponDefinition", weapon);

            Assert.That((bool)migrated, Is.True);
            Assert.That(GetProperty<float>(weapon, "Range"), Is.EqualTo(10f).Within(0.0001f));
            Assert.That(GetProperty<float>(weapon, "Radius"), Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(GetProperty(weapon, "ShapeType").ToString(), Is.EqualTo("TargetReticle"));
            Assert.That(GetProperty<float>(weapon, "Width"), Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(GetProperty<float>(weapon, "Length"), Is.EqualTo(10f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void MigrationWritesConcreteWeaponGeometryWhenLegacyIndicatorUsesZeroFallbacks()
    {
        Type configType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorConfig, Assembly-CSharp");
        Type shapeType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorShapeType, Assembly-CSharp");
        Type migrationType = RequireType("NewFPG.EditorTools.WeaponDefinitionGeometryMigrationUtility, Assembly-CSharp-Editor");
        ScriptableObject weapon = CreateWeapon("FallbackMigrationWeapon", "fallback_migration", damage: 10f, cooldown: 0.4f, range: 12f, radius: 2f);
        ScriptableObject config = ScriptableObject.CreateInstance(configType);
        try
        {
            SetField(config, "shapeType", Enum.Parse(shapeType, "Line"));
            SetField(config, "range", 0f);
            SetField(config, "radius", 0f);
            SetField(config, "width", 0f);
            SetField(config, "length", 0f);
            SetField(weapon, "indicatorConfig", config);

            object migrated = InvokeStatic(migrationType, "MigrateWeaponDefinition", weapon);

            Assert.That((bool)migrated, Is.True);
            Assert.That(GetProperty<float>(weapon, "Range"), Is.EqualTo(12f).Within(0.0001f));
            Assert.That(GetProperty<float>(weapon, "Radius"), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(GetProperty<float>(weapon, "Width"), Is.EqualTo(4f).Within(0.0001f));
            Assert.That(GetProperty<float>(weapon, "Length"), Is.EqualTo(12f).Within(0.0001f));
            Assert.That(GetProperty(weapon, "ShapeType").ToString(), Is.EqualTo("Line"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void TwoInstancesFromSameDefinitionResolveIndependently()
    {
        ScriptableObject weapon = CreateWeapon("SharedBaseWeapon", "shared_base", damage: 10f, cooldown: 0.4f, range: 6f, radius: 0.6f);
        try
        {
            object firstInstance = CreateInstance("first", "shared_base", damage: 25f);
            object secondInstance = CreateInstance("second", "shared_base", damage: 40f);

            object firstStats = ResolveWeapon(weapon, firstInstance, null);
            object secondStats = ResolveWeapon(weapon, secondInstance, null);

            Assert.That(GetProperty<float>(firstStats, "Damage"), Is.EqualTo(25f).Within(0.0001f));
            Assert.That(GetProperty<float>(secondStats, "Damage"), Is.EqualTo(40f).Within(0.0001f));
            Assert.That(GetProperty<float>(weapon, "RuntimeTotalDamage"), Is.EqualTo(10f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void ModifiersApplyByLayerAndActiveRemovalRestores()
    {
        Type modifierType = RequireType("NewFPG.Combat.WeaponModifier, Assembly-CSharp");
        ScriptableObject weapon = CreateWeapon("ModifiedWeapon", "modified_weapon", damage: 10f, cooldown: 0.4f, range: 6f, radius: 0.6f);
        try
        {
            object instance = CreateInstance("modified_instance", "modified_weapon", damage: 20f);
            IList permanentModifiers = ListOf(modifierType);
            permanentModifiers.Add(CreateDamageModifier("Add", 5f));
            permanentModifiers.Add(CreateDamageModifier("Multiply", 2f));
            SetField(instance, "permanentModifiers", permanentModifiers);

            IList activeModifiers = ListOf(modifierType);
            activeModifiers.Add(CreateDamageModifier("Add", 10f));
            activeModifiers.Add(CreateDamageModifier("Multiply", 0.5f));
            activeModifiers.Add(CreateDamageModifier("Override", 33f));

            object activeStats = ResolveWeapon(weapon, instance, activeModifiers);
            Assert.That(GetProperty<float>(activeStats, "Damage"), Is.EqualTo(33f).Within(0.0001f));

            object restoredStats = ResolveWeapon(weapon, instance, null);
            Assert.That(GetProperty<float>(restoredStats, "Damage"), Is.EqualTo(50f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void GeometryModifiersAffectNextRuntimeSnapshotAndCanBeRemoved()
    {
        Type modifierType = RequireType("NewFPG.Combat.WeaponModifier, Assembly-CSharp");
        ScriptableObject weapon = CreateWeapon("GeometryModifiedWeapon", "geometry_modified", damage: 10f, cooldown: 0.4f, range: 6f, radius: 0.6f);
        try
        {
            SetField(weapon, "width", 1.2f);
            SetField(weapon, "length", 6f);
            SetField(weapon, "angle", 60f);

            IList activeModifiers = ListOf(modifierType);
            object modifier = Activator.CreateInstance(modifierType);
            SetField(modifier, "operation", Enum.Parse(RequireType("NewFPG.Combat.WeaponModifierOperation, Assembly-CSharp"), "Add"));
            SetField(modifier, "modifyWidth", true);
            SetField(modifier, "width", 0.8f);
            SetField(modifier, "modifyLength", true);
            SetField(modifier, "length", 2f);
            SetField(modifier, "modifyAngle", true);
            SetField(modifier, "angle", 15f);
            activeModifiers.Add(modifier);

            object activeStats = ResolveWeapon(weapon, null, activeModifiers);
            Assert.That(GetProperty<float>(activeStats, "Width"), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(GetProperty<float>(activeStats, "Length"), Is.EqualTo(8f).Within(0.0001f));
            Assert.That(GetProperty<float>(activeStats, "Angle"), Is.EqualTo(75f).Within(0.0001f));

            object restoredStats = ResolveWeapon(weapon, null, null);
            Assert.That(GetProperty<float>(restoredStats, "Width"), Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(GetProperty<float>(restoredStats, "Length"), Is.EqualTo(6f).Within(0.0001f));
            Assert.That(GetProperty<float>(restoredStats, "Angle"), Is.EqualTo(60f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void NoPreviewCastCommandUsesWeaponRuntimeShape()
    {
        Type casterType = RequireType("NewFPG.Combat.PlayerWeaponCaster, Assembly-CSharp");
        Type shapeType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorShapeType, Assembly-CSharp");
        GameObject host = new GameObject("Runtime Shape Caster");
        ScriptableObject weapon = CreateWeapon("LineCommandWeapon", "line_command", damage: 10f, cooldown: 0.4f, range: 9f, radius: 0.45f);
        try
        {
            SetField(weapon, "shapeType", Enum.Parse(shapeType, "Line"));
            SetField(weapon, "width", 1.2f);
            SetField(weapon, "length", 9f);
            SetField(weapon, "angle", 90f);

            Component caster = host.AddComponent(casterType);
            object stats = ResolveWeapon(weapon);
            MethodInfo createCommand = casterType.GetMethod("CreateDefaultCastCommand", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(createCommand);
            object command = createCommand.Invoke(caster, new[] { stats });

            Assert.That(GetField(command, "ShapeType").ToString(), Is.EqualTo("Line"));
            Assert.That(GetField<float>(command, "Radius"), Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(GetField<float>(command, "Width"), Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(GetField<float>(command, "Length"), Is.EqualTo(9f).Within(0.0001f));
            Assert.That(GetField<float>(command, "Angle"), Is.EqualTo(90f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void GroundCircleHitsColliderEdgeInsteadOfOnlyAimPoint()
    {
        GameObject edgeTarget = CreateDamageableCube("Circle Edge Target", new Vector3(1.25f, 0.5f, 0f), new Vector3(0.6f, 1f, 0.6f), out _, out BoxCollider edgeCollider);
        GameObject outsideTarget = CreateDamageableCube("Circle Outside Target", new Vector3(1.5f, 0.5f, 0f), new Vector3(0.6f, 1f, 0.6f), out _, out BoxCollider outsideCollider);
        try
        {
            object command = CreateCommand("GroundCircle");
            SetField(command, "TargetPoint", Vector3.zero);
            SetField(command, "Radius", 1f);
            SetField(command, "Height", 2f);
            Physics.SyncTransforms();

            Assert.That(TryResolveHit(command, edgeCollider), Is.True);
            Assert.That(TryResolveHit(command, outsideCollider), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(edgeTarget);
            UnityEngine.Object.DestroyImmediate(outsideTarget);
        }
    }

    [Test]
    public void GroundCircleHeightFiltersTargetsAboveThePreviewVolume()
    {
        GameObject target = CreateDamageableCube("Circle High Target", new Vector3(0f, 3.5f, 0f), Vector3.one, out _, out BoxCollider collider);
        try
        {
            object command = CreateCommand("GroundCircle");
            SetField(command, "TargetPoint", Vector3.zero);
            SetField(command, "Radius", 2f);
            SetField(command, "Height", 1f);
            Physics.SyncTransforms();

            Assert.That(TryResolveHit(command, collider), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void HitResolverRejectsUntargetableDamageable()
    {
        GameObject target = CreateDamageableCube("Untargetable Cast Target", new Vector3(0f, 0.5f, 0f), Vector3.one, out Component vitals, out BoxCollider collider);
        Component state = target.AddComponent(MonsterStateType);
        try
        {
            SetField(state, "invisibleStacks", 1);
            SetField(vitals, "monsterState", state);

            object command = CreateCommand("GroundCircle");
            SetField(command, "TargetPoint", Vector3.zero);
            SetField(command, "Radius", 2f);
            SetField(command, "Height", 2f);
            Physics.SyncTransforms();

            Assert.That(GetProperty<bool>(vitals, "IsTargetable"), Is.False);
            Assert.That(TryResolveHit(command, collider), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void RectangleHitsColliderEdgeAndRejectsWidthOrLengthMisses()
    {
        GameObject edgeTarget = CreateDamageableCube("Rectangle Edge Target", new Vector3(0.75f, 0.5f, 2f), new Vector3(0.6f, 1f, 0.6f), out _, out BoxCollider edgeCollider);
        GameObject widthMissTarget = CreateDamageableCube("Rectangle Width Miss Target", new Vector3(0.9f, 0.5f, 2f), new Vector3(0.6f, 1f, 0.6f), out _, out BoxCollider widthMissCollider);
        GameObject lengthMissTarget = CreateDamageableCube("Rectangle Length Miss Target", new Vector3(0f, 0.5f, 4.4f), new Vector3(0.6f, 1f, 0.6f), out _, out BoxCollider lengthMissCollider);
        try
        {
            object command = CreateCommand("Rectangle");
            SetField(command, "Width", 1f);
            SetField(command, "Length", 4f);
            SetField(command, "Height", 2f);
            Physics.SyncTransforms();

            Assert.That(TryResolveHit(command, edgeCollider), Is.True);
            Assert.That(TryResolveHit(command, widthMissCollider), Is.False);
            Assert.That(TryResolveHit(command, lengthMissCollider), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(edgeTarget);
            UnityEngine.Object.DestroyImmediate(widthMissTarget);
            UnityEngine.Object.DestroyImmediate(lengthMissTarget);
        }
    }

    [Test]
    public void ConeUsesColliderProjectionAndCommandAngle()
    {
        GameObject edgeTarget = CreateDamageableCube("Cone Edge Target", new Vector3(1.72f, 0.5f, 2.46f), new Vector3(0.6f, 1f, 0.6f), out _, out BoxCollider edgeCollider);
        GameObject angleTarget = CreateDamageableCube("Cone Angle Target", new Vector3(2.1f, 0.5f, 2.2f), new Vector3(0.6f, 1f, 0.6f), out _, out BoxCollider angleCollider);
        try
        {
            object command = CreateCommand("Cone");
            SetField(command, "Length", 4f);
            SetField(command, "Angle", 60f);
            SetField(command, "Height", 2f);
            Physics.SyncTransforms();

            Assert.That(TryResolveHit(command, edgeCollider), Is.True);
            Assert.That(TryResolveHit(command, angleCollider), Is.False);

            SetField(command, "Angle", 90f);
            Assert.That(TryResolveHit(command, angleCollider), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(edgeTarget);
            UnityEngine.Object.DestroyImmediate(angleTarget);
        }
    }

    [Test]
    public void NoPreviewLineCastDamagesColliderEdge()
    {
        ScriptableObject weapon = CreateWeapon("NoPreviewLineHitWeapon", "no_preview_line_hit", damage: 10f, cooldown: 0.4f, range: 4f, radius: 0.4f);
        GameObject casterObject = CreateCaster(weapon, out Component caster);
        GameObject target = CreateDamageableCube("No Preview Line Edge Target", new Vector3(0.75f, 0.5f, 2f), new Vector3(0.6f, 1f, 0.6f), out Component vitals, out _);
        try
        {
            SetField(weapon, "shapeType", Enum.Parse(SkillIndicatorShapeTypeType, "Line"));
            SetField(weapon, "width", 1f);
            SetField(weapon, "length", 4f);
            SetField(weapon, "tapPolicy", Enum.Parse(SkillIndicatorDefaultReleasePolicyType, "CastForwardMaxRange"));
            Physics.SyncTransforms();

            Assert.That((bool)InvokeInstance(caster, "TryCast", 0), Is.True);
            Assert.That(GetProperty<float>(vitals, "CurrentHealth"), Is.EqualTo(90f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(casterObject);
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void NoPreviewGroundLineUsesGroundSceneOriginForHeightFiltering()
    {
        ScriptableObject weapon = CreateWeapon("NoPreviewGroundedLineWeapon", "no_preview_grounded_line", damage: 10f, cooldown: 0.4f, range: 4f, radius: 0.4f);
        GameObject casterObject = CreateCaster(weapon, out Component caster);
        GameObject target = CreateDamageableCube("Grounded Line Short Target", new Vector3(0f, 0.5f, 2f), Vector3.one, out Component vitals, out _);
        try
        {
            casterObject.transform.position = new Vector3(0f, 1.55f, 0f);
            SetField(weapon, "shapeType", Enum.Parse(SkillIndicatorShapeTypeType, "Line"));
            SetField(weapon, "width", 1f);
            SetField(weapon, "length", 4f);
            SetField(weapon, "height", 1f);
            SetField(weapon, "tapPolicy", Enum.Parse(SkillIndicatorDefaultReleasePolicyType, "CastForwardMaxRange"));
            Physics.SyncTransforms();

            object command = CreateDefaultCommand(caster, 0);
            Assert.That(GetField<Vector3>(command, "SceneOrigin").y, Is.EqualTo(0f).Within(0.001f));
            Assert.That((bool)InvokeInstance(caster, "TryCast", 0), Is.True);
            Assert.That(GetProperty<float>(vitals, "CurrentHealth"), Is.EqualTo(90f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(casterObject);
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void AutoSelectConeDoesNotAimAtTargetBeyondShapeLength()
    {
        ScriptableObject weapon = CreateWeapon("AutoSelectConeWeapon", "auto_select_cone", damage: 10f, cooldown: 0.4f, range: 20f, radius: 0.4f);
        GameObject casterObject = CreateCaster(weapon, out Component caster);
        GameObject farTarget = CreateDamageableCube("Auto Select Far Cone Target", new Vector3(0f, 0.5f, 8f), Vector3.one, out _, out _);
        try
        {
            SetField(weapon, "shapeType", Enum.Parse(SkillIndicatorShapeTypeType, "Cone"));
            SetField(weapon, "length", 4f);
            SetField(weapon, "angle", 72f);
            SetField(weapon, "tapPolicy", Enum.Parse(SkillIndicatorDefaultReleasePolicyType, "AutoSelectBestTarget"));
            Physics.SyncTransforms();

            object command = CreateDefaultCommand(caster, 0);
            Vector3 targetPoint = GetField<Vector3>(command, "TargetPoint");
            Assert.That(targetPoint.z, Is.LessThanOrEqualTo(4f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(farTarget);
            UnityEngine.Object.DestroyImmediate(casterObject);
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void CastDamagesMultiColliderTargetOnlyOnce()
    {
        ScriptableObject weapon = CreateWeapon("MultiColliderHitWeapon", "multi_collider_hit", damage: 10f, cooldown: 0.4f, range: 4f, radius: 2f);
        GameObject casterObject = CreateCaster(weapon, out Component caster);
        GameObject target = CreateDamageableCube("Multi Collider Target", new Vector3(0f, 0.5f, 1f), Vector3.one, out Component vitals, out _);
        try
        {
            target.AddComponent<CapsuleCollider>();
            Physics.SyncTransforms();

            Assert.That((bool)InvokeInstance(caster, "TryCast", 0), Is.True);
            Assert.That(GetProperty<float>(vitals, "CurrentHealth"), Is.EqualTo(90f).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(casterObject);
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void InventoryJsonRoundTripsAndSkipsMissingBaseWeaponIds()
    {
        Type saveDataType = RequireType("NewFPG.Combat.WeaponInventorySaveData, Assembly-CSharp");
        Type instanceType = RequireType("NewFPG.Combat.WeaponInstanceData, Assembly-CSharp");
        ScriptableObject weapon = CreateWeapon("ValidInventoryWeapon", "valid_weapon", damage: 10f, cooldown: 0.4f, range: 6f, radius: 0.6f);
        try
        {
            object saveData = Activator.CreateInstance(saveDataType);
            IList weapons = (IList)GetField(saveData, "weapons");
            weapons.Add(CreateInstance("valid_instance", "valid_weapon", damage: 18f));
            weapons.Add(CreateInstance("missing_instance", "missing_weapon", damage: 30f));

            string json = (string)InvokeInstance(saveData, "ToJson");
            Assert.That(json, Does.Contain("valid_weapon"));

            object loaded = InvokeStatic(saveDataType, "FromJson", json);
            IList definitions = ListOf(WeaponDefinitionType);
            definitions.Add(weapon);

            LogAssert.Expect(LogType.Warning, "Skipped weapon instance with missing base weapon id: missing_weapon");
            IList resolved = (IList)InvokeInstance(loaded, "ResolveValidInstances", definitions);
            Assert.That(resolved.Count, Is.EqualTo(1));
            Assert.That(GetField<string>(resolved[0], "baseWeaponId"), Is.EqualTo("valid_weapon"));
            Assert.That(resolved[0].GetType(), Is.EqualTo(instanceType));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void ForgingFactoryCreatesWeaponInstanceWithoutMutatingBaseDefinition()
    {
        Type factoryType = RequireType("NewFPG.Forging.ForgingWeaponFactory, Assembly-CSharp");
        Type blueprintType = RequireType("NewFPG.Forging.ForgingWeaponBlueprintDefinition, Assembly-CSharp");
        Type resultType = RequireType("NewFPG.Forging.ForgingResult, Assembly-CSharp");
        ScriptableObject weapon = CreateWeapon("ForgingBaseWeapon", "forging_base", damage: 12f, cooldown: 0.4f, range: 6f, radius: 0.6f);
        try
        {
            object blueprint = Activator.CreateInstance(blueprintType);
            SetField(blueprint, "blueprintId", "test_blueprint");
            SetField(blueprint, "displayName", "Forged Test");

            object result = Activator.CreateInstance(resultType);
            SetField(result, "blueprint", blueprint);
            SetField(result, "isValid", true);
            SetField(result, "damage", 77f);

            MethodInfo createInstance = factoryType.GetMethod(
                "CreateWeaponInstance",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { WeaponDefinitionType, blueprintType, resultType },
                null);
            Assert.IsNotNull(createInstance);

            object instance = createInstance.Invoke(null, new[] { weapon, blueprint, result });
            object forgedStats = GetField(instance, "forgedStats");

            Assert.That(GetField<string>(instance, "baseWeaponId"), Is.EqualTo("forging_base"));
            Assert.That(GetField<float>(forgedStats, "damage"), Is.EqualTo(77f).Within(0.0001f));
            Assert.That(GetProperty<float>(weapon, "RuntimeTotalDamage"), Is.EqualTo(12f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    [Test]
    public void HudDebugDamageWeaponsHavePositiveDamage()
    {
        AssertHudDebugWeaponDamage("Assets/Settings/Combat/HudDebug/HUD_Debug_FlyingSword.asset");
        AssertHudDebugWeaponDamage("Assets/Settings/Combat/HudDebug/HUD_Debug_MoonDao.asset");
        AssertHudDebugWeaponDamage("Assets/Settings/Combat/HudDebug/HUD_Debug_RitualDagger.asset");
    }

    [Test]
    public void HudDebugDamageWeaponsUseSameTapAndHoldReleasePolicy()
    {
        AssertHudDebugWeaponReleasePolicy("Assets/Settings/Combat/HudDebug/HUD_Debug_FlyingSword.asset");
        AssertHudDebugWeaponReleasePolicy("Assets/Settings/Combat/HudDebug/HUD_Debug_MoonDao.asset");
        AssertHudDebugWeaponReleasePolicy("Assets/Settings/Combat/HudDebug/HUD_Debug_RitualDagger.asset");
    }

    private static Type WeaponDefinitionType => RequireType("NewFPG.Combat.WeaponDefinition, Assembly-CSharp");
    private static Type PlayerWeaponCasterType => RequireType("NewFPG.Combat.PlayerWeaponCaster, Assembly-CSharp");
    private static Type CombatResourcePoolType => RequireType("NewFPG.Combat.CombatResourcePool, Assembly-CSharp");
    private static Type CombatVitalsType => RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp");
    private static Type CombatVitalsSettingsType => RequireType("NewFPG.Combat.CombatVitalsSettings, Assembly-CSharp");
    private static Type MonsterStateType => RequireType("NewFPG.Monsters.MonsterState, Assembly-CSharp");
    private static Type WeaponCastHitResolverType => RequireType("NewFPG.Combat.WeaponCastHitResolver, Assembly-CSharp");
    private static Type CastCommandDataType => RequireType("NewFPG.Combat.SkillIndicators.CastCommandData, Assembly-CSharp");
    private static Type SkillIndicatorShapeTypeType => RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorShapeType, Assembly-CSharp");
    private static Type SkillIndicatorPlacementModeType => RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPlacementMode, Assembly-CSharp");
    private static Type SkillIndicatorDefaultReleasePolicyType => RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorDefaultReleasePolicy, Assembly-CSharp");

    private static ScriptableObject CreateWeapon(
        string assetName,
        string weaponId,
        float damage,
        float cooldown,
        float range,
        float radius)
    {
        ScriptableObject weapon = ScriptableObject.CreateInstance(WeaponDefinitionType);
        weapon.name = assetName;
        SetField(weapon, "weaponId", weaponId);
        SetField(weapon, "displayName", assetName);
        SetField(weapon, "resourceCost", 0f);
        SetField(weapon, "damage", damage);
        SetField(weapon, "cooldown", cooldown);
        SetField(weapon, "range", range);
        SetField(weapon, "radius", radius);
        SetField(weapon, "shapeType", Enum.Parse(SkillIndicatorShapeTypeType, "GroundCircle"));
        SetField(weapon, "width", radius * 2f);
        SetField(weapon, "length", range);
        SetField(weapon, "angle", 90f);
        return weapon;
    }

    private static object CreateCommand(string shapeType)
    {
        object command = Activator.CreateInstance(CastCommandDataType);
        SetField(command, "AbilityId", "test");
        SetField(command, "Origin", Vector3.zero);
        SetField(command, "SceneOrigin", Vector3.zero);
        SetField(command, "Direction", Vector3.forward);
        SetField(command, "TargetPoint", Vector3.zero);
        SetField(command, "SurfaceNormal", Vector3.up);
        SetField(command, "PlacementMode", Enum.Parse(SkillIndicatorPlacementModeType, "GroundSurface"));
        SetField(command, "ShapeType", Enum.Parse(SkillIndicatorShapeTypeType, shapeType));
        SetField(command, "Radius", 1f);
        SetField(command, "Width", 1f);
        SetField(command, "Length", 4f);
        SetField(command, "Angle", 90f);
        SetField(command, "Height", 2f);
        SetField(command, "GroundOffset", 0.06f);
        SetField(command, "HasTargetPoint", true);
        SetField(command, "IsValid", true);
        return command;
    }

    private static GameObject CreateDamageableCube(
        string name,
        Vector3 position,
        Vector3 scale,
        out Component vitals,
        out BoxCollider collider)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = name;
        target.transform.position = position;
        target.transform.localScale = scale;
        collider = target.GetComponent<BoxCollider>();
        vitals = target.AddComponent(CombatVitalsType);
        object settings = Activator.CreateInstance(CombatVitalsSettingsType);
        SetField(settings, "maxHealth", 100f);
        SetField(settings, "startingHealth", 100f);
        InvokeInstance(vitals, "ApplySettings", settings, true);
        return target;
    }

    private static GameObject CreateCaster(ScriptableObject weapon, out Component caster)
    {
        GameObject casterObject = new GameObject("Weapon Range Test Caster");
        Component resourcePool = casterObject.AddComponent(CombatResourcePoolType);
        InvokeInstance(resourcePool, "SetCurrent", 10f);
        caster = casterObject.AddComponent(PlayerWeaponCasterType);
        Array weapons = Array.CreateInstance(WeaponDefinitionType, 1);
        weapons.SetValue(weapon, 0);
        SetField(caster, "weapons", weapons);
        SetField(caster, "resourcePool", resourcePool);
        SetField(caster, "castOrigin", casterObject.transform);
        SetField(caster, "targetMask", new LayerMask { value = ~0 });
        SetField(caster, "combatEnabled", true);
        return casterObject;
    }

    private static object CreateInstance(string instanceId, string baseWeaponId, float damage)
    {
        object instance = Activator.CreateInstance(RequireType("NewFPG.Combat.WeaponInstanceData, Assembly-CSharp"));
        SetField(instance, "instanceId", instanceId);
        SetField(instance, "baseWeaponId", baseWeaponId);
        SetField(instance, "forgedStats", CreateForgedStats(damage));
        return instance;
    }

    private static object CreateForgedStats(float damage)
    {
        object stats = Activator.CreateInstance(RequireType("NewFPG.Forging.ForgedWeaponRuntimeStats, Assembly-CSharp"));
        SetField(stats, "damage", damage);
        return stats;
    }

    private static object CreateDamageModifier(string operation, float damage)
    {
        Type modifierType = RequireType("NewFPG.Combat.WeaponModifier, Assembly-CSharp");
        object modifier = Activator.CreateInstance(modifierType);
        SetField(modifier, "operation", Enum.Parse(RequireType("NewFPG.Combat.WeaponModifierOperation, Assembly-CSharp"), operation));
        SetField(modifier, "modifyDamage", true);
        SetField(modifier, "damage", damage);
        return modifier;
    }

    private static object CreateDefaultCommand(Component caster, int weaponIndex)
    {
        object stats = InvokeInstance(caster, "GetRuntimeStats", weaponIndex);
        MethodInfo createCommand = FindMethod(caster.GetType(), "CreateDefaultCastCommand", BindingFlags.Instance | BindingFlags.NonPublic, 1);
        Assert.IsNotNull(createCommand);
        return createCommand.Invoke(caster, new[] { stats });
    }

    private static void AssertHudDebugWeaponDamage(string path)
    {
        UnityEngine.Object weapon = AssetDatabase.LoadAssetAtPath(path, WeaponDefinitionType);
        Assert.IsNotNull(weapon, path + " should exist.");
        Assert.That(GetProperty<float>(weapon, "Damage"), Is.GreaterThan(0f), path + " must deal positive damage so CombatVitals.Damaged and damage numbers fire.");
    }

    private static void AssertHudDebugWeaponReleasePolicy(string path)
    {
        UnityEngine.Object weapon = AssetDatabase.LoadAssetAtPath(path, WeaponDefinitionType);
        Assert.IsNotNull(weapon, path + " should exist.");
        Assert.That(GetProperty(weapon, "TapPolicy").ToString(), Is.EqualTo("CastAtCrosshairHit"), path + " tap should resolve the same crosshair command path as hold.");
        Assert.That(GetProperty(weapon, "HoldPolicy").ToString(), Is.EqualTo("CastAtCrosshairHit"), path + " hold should resolve the same crosshair command path as tap.");
    }

    private static bool TryResolveHit(object command, Collider collider)
    {
        MethodInfo method = FindMethod(WeaponCastHitResolverType, "TryResolveHit", BindingFlags.Static | BindingFlags.Public, 4);
        Assert.IsNotNull(method);

        object[] args = { command, collider, null, null };
        return (bool)method.Invoke(null, args);
    }

    private static object ResolveWeapon(ScriptableObject weapon)
    {
        Type resolverType = RequireType("NewFPG.Combat.WeaponRuntimeResolver, Assembly-CSharp");
        MethodInfo resolve = resolverType.GetMethod(
            "Resolve",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { WeaponDefinitionType },
            null);
        Assert.IsNotNull(resolve);
        return resolve.Invoke(null, new object[] { weapon });
    }

    private static object ResolveWeapon(ScriptableObject weapon, object instance, object activeModifiers)
    {
        Type resolverType = RequireType("NewFPG.Combat.WeaponRuntimeResolver, Assembly-CSharp");
        MethodInfo resolve = FindMethod(resolverType, "Resolve", BindingFlags.Static | BindingFlags.Public, 3);
        Assert.IsNotNull(resolve);
        return resolve.Invoke(null, new[] { weapon, instance, activeModifiers });
    }

    private static IList ListOf(Type elementType)
    {
        return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
    }

    private static object InvokeStatic(Type type, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(type, methodName, BindingFlags.Static | BindingFlags.Public, args != null ? args.Length : 0);
        Assert.IsNotNull(method, type.Name + "." + methodName + " should exist.");
        return method.Invoke(null, args);
    }

    private static object InvokeInstance(object target, string methodName, params object[] args)
    {
        MethodInfo method = FindMethod(target.GetType(), methodName, BindingFlags.Instance | BindingFlags.Public, args != null ? args.Length : 0);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static MethodInfo FindMethod(Type type, string methodName, BindingFlags flags, int argumentCount)
    {
        MethodInfo[] methods = type.GetMethods(flags);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name == methodName && method.GetParameters().Length == argumentCount)
            {
                return method;
            }
        }

        return null;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        return field.GetValue(target);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        return (T)GetField(target, fieldName);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, target.GetType().Name + "." + propertyName + " should exist.");
        return property.GetValue(target);
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        return (T)GetProperty(target, propertyName);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }
}
