using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ForgingSystemEditorTests
{
    [Test]
    public void DefaultCatalogLoadsBlueprintsMaterialsAndRuntimeBindings()
    {
        object catalog = InvokeStatic(ForgingType("ForgingCatalogLoader"), "LoadDefault");
        object taomuSword = InvokeInstance(catalog, "FindBlueprint", "taomu_sword");
        object heartMirror = InvokeInstance(catalog, "FindBlueprint", "heart_mirror");
        object fan = InvokeInstance(catalog, "FindBlueprint", "fan");
        object stardust = InvokeInstance(catalog, "FindMaterial", "stardust_gold");

        Assert.IsNotNull(taomuSword);
        Assert.IsNotNull(heartMirror);
        Assert.IsNotNull(fan);
        Assert.IsNotNull(stardust);
        Assert.AreEqual(
            "Assets/Settings/Combat/HudDebug/IND_HUD_Debug_TargetLock.asset",
            GetField<string>(GetField(taomuSword, "runtime"), "indicatorConfigPath"));
        Assert.GreaterOrEqual(GetField<int>(stardust, "shapeWidth"), 1);
        Assert.GreaterOrEqual(GetField<int>(stardust, "shapeHeight"), 1);
        Assert.Greater(GetField<IList>(stardust, "cells").Count, 0);
    }

    [Test]
    public void SplitCatalogFilesLoadTogether()
    {
        object catalog = InvokeStatic(
            ForgingType("ForgingCatalogLoader"),
            "LoadFromProjectPaths",
            "Assets/Settings/Forging/weapon_blueprints.json",
            "Assets/Settings/Forging/materials.json");

        Assert.IsNotNull(InvokeInstance(catalog, "FindBlueprint", "taomu_sword"));
        Assert.IsNotNull(InvokeInstance(catalog, "FindMaterial", "stardust_gold"));
    }

    [Test]
    public void MaterialConfigExportsShapeDimensionsAndCells()
    {
        ScriptableObject config = ScriptableObject.CreateInstance(ForgingType("ForgingMaterialConfig"));
        object material = CreateMaterial("wide", "Wide", 2f, 0f, 0f, 0f, 0f);
        SetField(material, "shapeWidth", 2);
        SetField(material, "shapeHeight", 1);
        SetField(material, "cells", ListOfVector2(new[] { Vector2Int.zero, Vector2Int.right }));

        InvokeInstance(config, "ApplyDefinition", material);
        object exported = InvokeInstance(config, "ToDefinition");

        Assert.AreEqual(2, GetField<int>(exported, "shapeWidth"));
        Assert.AreEqual(1, GetField<int>(exported, "shapeHeight"));
        Assert.AreEqual(2, GetField<IList>(exported, "cells").Count);
        UnityEngine.Object.DestroyImmediate(config);
    }

    [Test]
    public void RotatedMaterialPlacementUsesRotatedCells()
    {
        object material = CreateMaterial("bar", "Bar", 1f, 0f, 0f, 0f, 0f);
        SetField(material, "shapeWidth", 2);
        SetField(material, "shapeHeight", 1);
        SetField(material, "cells", ListOfVector2(new[] { Vector2Int.zero, Vector2Int.right }));
        object blueprint = CreateBlueprint("test", "Test", new[] { Vector2Int.zero, Vector2Int.up });
        IList placements = ListOf("ForgingPlacedMaterial");
        placements.Add(CreatePlacement(material, Vector2Int.zero, 1));

        object result = InvokeStatic(ForgingType("ForgingCalculator"), "Evaluate", blueprint, placements);

        Assert.IsTrue(GetField<bool>(result, "isValid"));
        Assert.IsTrue(GetField<bool>(result, "isComplete"));
    }

    [Test]
    public void StardustGoldHalvesAdjacentWoodMaterial()
    {
        object stardust = CreateWoodHalvingMaterial();
        object wood = CreateMaterial("wood", "Wood", 0f, 0f, 20f, 0f, 0f);
        object blueprint = CreateBlueprint("test", "Test", new[] { Vector2Int.zero, Vector2Int.right });
        IList placements = ListOf("ForgingPlacedMaterial");
        placements.Add(CreatePlacement(stardust, Vector2Int.zero));
        placements.Add(CreatePlacement(wood, Vector2Int.right));

        object result = InvokeStatic(ForgingType("ForgingCalculator"), "Evaluate", blueprint, placements);
        object attributes = GetField(result, "finalAttributes");

        Assert.AreEqual(10f, GetField<float>(attributes, "metal"));
        Assert.AreEqual(10f, GetField<float>(attributes, "wood"));
        Assert.AreEqual(5f, GetField<float>(attributes, "fire"));
        Assert.AreEqual(5f, GetField<float>(attributes, "earth"));
    }

    [Test]
    public void WeaponBonusReadsFinalWeaponAttributes()
    {
        object stardust = CreateMetalBonusMaterial();
        object metal = CreateMaterial("metal", "Metal", 30f, 0f, 0f, 0f, 0f);
        object blueprint = CreateBlueprint("test", "Test", new[] { Vector2Int.zero, Vector2Int.right });
        IList placements = ListOf("ForgingPlacedMaterial");
        placements.Add(CreatePlacement(stardust, Vector2Int.zero));
        placements.Add(CreatePlacement(metal, Vector2Int.right));

        object result = InvokeStatic(ForgingType("ForgingCalculator"), "Evaluate", blueprint, placements);
        IList bonuses = GetField<IList>(result, "bonuses");
        object runtimeStats = InvokeInstance(result, "ToRuntimeStats");

        Assert.AreEqual(1, bonuses.Count);
        Assert.AreEqual(0.4f, GetField<float>(bonuses[0], "minValue"), 0.0001f);
        Assert.AreEqual(2f, GetField<float>(bonuses[0], "maxValue"), 0.0001f);
        Assert.AreEqual(1.2f, GetProperty<float>(runtimeStats, "BonusDamageAverage"), 0.0001f);
    }

    [Test]
    public void TaomuSwordDamageScalesFromFinalWoodAttribute()
    {
        object catalog = InvokeStatic(ForgingType("ForgingCatalogLoader"), "LoadDefault");
        object taomuSword = InvokeInstance(catalog, "FindBlueprint", "taomu_sword");
        object wood = CreateMaterial("wood", "Wood", 0f, 0f, 10f, 0f, 0f);
        IList placements = ListOf("ForgingPlacedMaterial");
        IList cells = GetField<IList>(taomuSword, "cells");
        for (int i = 0; i < cells.Count; i++)
        {
            placements.Add(CreatePlacement(wood, (Vector2Int)cells[i]));
        }

        object result = InvokeStatic(ForgingType("ForgingCalculator"), "Evaluate", taomuSword, placements);

        Assert.IsTrue(GetField<bool>(result, "isComplete"));
        Assert.AreEqual(cells.Count * 10f, GetField<float>(result, "damage"), 0.0001f);
    }

    [Test]
    public void ForgedWeaponFactoryAppliesHudAndIndicatorConfigToWeaponDefinition()
    {
        object catalog = InvokeStatic(ForgingType("ForgingCatalogLoader"), "LoadDefault");
        object taomuSword = InvokeInstance(catalog, "FindBlueprint", "taomu_sword");
        object wood = CreateMaterial("wood", "Wood", 0f, 0f, 10f, 0f, 0f);
        IList placements = ListOf("ForgingPlacedMaterial");
        IList cells = GetField<IList>(taomuSword, "cells");
        for (int i = 0; i < cells.Count; i++)
        {
            placements.Add(CreatePlacement(wood, (Vector2Int)cells[i]));
        }

        object result = InvokeStatic(ForgingType("ForgingCalculator"), "Evaluate", taomuSword, placements);
        ScriptableObject weapon = (ScriptableObject)InvokeStatic(ForgingType("ForgingWeaponFactory"), "CreateRuntimeWeapon", taomuSword, result);

        Assert.IsNotNull(weapon);
        Assert.AreEqual("桃木剑", GetProperty<string>(weapon, "DisplayName"));
        Assert.IsNotNull(GetProperty<Sprite>(weapon, "Icon"));
        object indicatorConfig = GetProperty(weapon, "IndicatorConfig");
        Assert.IsNotNull(indicatorConfig);
        Assert.AreEqual("TargetReticle", GetProperty(indicatorConfig, "ShapeType").ToString());
        Assert.AreEqual(cells.Count * 10f, GetProperty<float>(weapon, "RuntimeTotalDamage"), 0.0001f);
        UnityEngine.Object.DestroyImmediate(weapon);
    }

    [Test]
    public void TryPlaceMaterialSupportsRotation()
    {
        object material = CreateMaterial("bar", "Bar", 1f, 0f, 0f, 0f, 0f);
        SetField(material, "shapeWidth", 2);
        SetField(material, "shapeHeight", 1);
        SetField(material, "cells", ListOfVector2(new[] { Vector2Int.zero, Vector2Int.right }));
        object blueprint = CreateBlueprint("test", "Test", new[] { Vector2Int.zero, Vector2Int.up });

        object[] args = { blueprint, ListOf("ForgingPlacedMaterial"), material, Vector2Int.zero, 1, null };
        object success = InvokeStatic(ForgingType("ForgingCalculator"), "TryPlaceMaterial", args);

        Assert.IsTrue((bool)success);
    }

    private static object CreateMaterial(string id, string name, float metal, float water, float wood, float fire, float earth)
    {
        object material = Activator.CreateInstance(ForgingType("ForgingMaterialDefinition"));
        SetField(material, "materialId", id);
        SetField(material, "displayName", name);
        SetField(material, "cells", ListOfVector2(Vector2Int.zero));
        SetField(material, "attributes", Activator.CreateInstance(
            ForgingType("ForgingElementAttributes"),
            metal,
            water,
            wood,
            fire,
            earth));
        return material;
    }

    private static object CreateWoodHalvingMaterial()
    {
        object material = CreateMaterial("wood_halver", "Wood Halver", 10f, 0f, 0f, 5f, 5f);
        object rule = Activator.CreateInstance(ForgingType("ForgingNeighborRule"));
        SetField(rule, "ruleType", Enum.Parse(ForgingType("ForgingNeighborRuleType"), "MultiplyElement"));
        SetField(rule, "targetElement", Enum.Parse(ForgingType("ForgingElement"), "Wood"));
        SetField(rule, "multiplier", 0.5f);
        IList rules = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ForgingType("ForgingNeighborRule")));
        rules.Add(rule);
        SetField(material, "neighborRules", rules);
        SetField(material, "neighborRule", rule);
        return material;
    }

    private static object CreateMetalBonusMaterial()
    {
        object material = CreateMaterial("metal_bonus", "Metal Bonus", 10f, 0f, 0f, 5f, 5f);
        object bonus = Activator.CreateInstance(ForgingType("ForgingWeaponBonus"));
        SetField(bonus, "bonusType", Enum.Parse(ForgingType("ForgingWeaponBonusType"), "ElementDamagePercentRange"));
        SetField(bonus, "element", Enum.Parse(ForgingType("ForgingElement"), "Metal"));
        SetField(bonus, "minPercent", 0.01f);
        SetField(bonus, "maxPercent", 0.05f);
        IList bonuses = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ForgingType("ForgingWeaponBonus")));
        bonuses.Add(bonus);
        SetField(material, "weaponBonuses", bonuses);
        SetField(material, "weaponBonus", bonus);
        return material;
    }

    private static object CreateBlueprint(string id, string name, IEnumerable<Vector2Int> cells)
    {
        object blueprint = Activator.CreateInstance(ForgingType("ForgingWeaponBlueprintDefinition"));
        SetField(blueprint, "blueprintId", id);
        SetField(blueprint, "displayName", name);
        SetField(blueprint, "width", 4);
        SetField(blueprint, "height", 4);
        SetField(blueprint, "cells", ListOfVector2(cells));
        return blueprint;
    }

    private static object CreatePlacement(object material, Vector2Int origin)
    {
        return Activator.CreateInstance(ForgingType("ForgingPlacedMaterial"), material, origin);
    }

    private static object CreatePlacement(object material, Vector2Int origin, int rotationSteps)
    {
        return Activator.CreateInstance(ForgingType("ForgingPlacedMaterial"), material, origin, rotationSteps);
    }

    private static IList ListOf(string typeName)
    {
        return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(ForgingType(typeName)));
    }

    private static IList ListOfVector2(IEnumerable<Vector2Int> values)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        list.AddRange(values);
        return list;
    }

    private static IList ListOfVector2(Vector2Int value)
    {
        return new List<Vector2Int> { value };
    }

    private static Type ForgingType(string typeName)
    {
        Type type = Type.GetType("NewFPG.Forging." + typeName + ", Assembly-CSharp", true);
        Assert.IsNotNull(type, typeName + " should resolve.");
        return type;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
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
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
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
}
