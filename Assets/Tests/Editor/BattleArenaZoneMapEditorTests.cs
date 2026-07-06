using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleArenaZoneMapEditorTests
{
    private GameObject temporaryObject;

    [TearDown]
    public void TearDown()
    {
        if (temporaryObject != null)
        {
            UnityEngine.Object.DestroyImmediate(temporaryObject);
        }
    }

    [Test]
    public void ZoneMapBuildsExpectedThreeByThreeLocalRects()
    {
        Component zoneMap = CreateZoneMap(new Vector2(12f, 9f), Vector3.zero, 0f);

        Rect leftFront = GetZoneRect(zoneMap, "left_front");
        Assert.AreEqual(-6f, leftFront.xMin, 0.001f);
        Assert.AreEqual(-2f, leftFront.xMax, 0.001f);
        Assert.AreEqual(-4.5f, leftFront.yMin, 0.001f);
        Assert.AreEqual(-1.5f, leftFront.yMax, 0.001f);

        Rect centerMid = GetZoneRect(zoneMap, "center_mid");
        Assert.AreEqual(-2f, centerMid.xMin, 0.001f);
        Assert.AreEqual(2f, centerMid.xMax, 0.001f);
        Assert.AreEqual(-1.5f, centerMid.yMin, 0.001f);
        Assert.AreEqual(1.5f, centerMid.yMax, 0.001f);
    }

    [Test]
    public void ZoneMapUsesCustomDividerSplits()
    {
        Component zoneMap = CreateZoneMap(new Vector2(12f, 10f), Vector3.zero, 0f);
        SetField(zoneMap, "columnSplits", new Vector2(0.25f, 0.75f));
        SetField(zoneMap, "rowSplits", new Vector2(0.2f, 0.8f));

        Rect leftFront = GetZoneRect(zoneMap, "left_front");
        Assert.AreEqual(-6f, leftFront.xMin, 0.001f);
        Assert.AreEqual(-3f, leftFront.xMax, 0.001f);
        Assert.AreEqual(-5f, leftFront.yMin, 0.001f);
        Assert.AreEqual(-3f, leftFront.yMax, 0.001f);

        Rect centerMid = GetZoneRect(zoneMap, "center_mid");
        Assert.AreEqual(-3f, centerMid.xMin, 0.001f);
        Assert.AreEqual(3f, centerMid.xMax, 0.001f);
        Assert.AreEqual(-3f, centerMid.yMin, 0.001f);
        Assert.AreEqual(3f, centerMid.yMax, 0.001f);

        Rect rightBack = GetZoneRect(zoneMap, "right_back");
        Assert.AreEqual(3f, rightBack.xMin, 0.001f);
        Assert.AreEqual(6f, rightBack.xMax, 0.001f);
        Assert.AreEqual(3f, rightBack.yMin, 0.001f);
        Assert.AreEqual(5f, rightBack.yMax, 0.001f);
    }

    [Test]
    public void ZoneMapRejectsUnknownZoneIds()
    {
        Component zoneMap = CreateZoneMap(new Vector2(12f, 9f), Vector3.zero, 0f);
        object[] args = { "somewhere_else", new Rect() };

        Assert.IsFalse((bool)Invoke(zoneMap, "TryGetZoneRect", args));
    }

    [Test]
    public void ZoneMapConvertsZoneCentersToWorldSpace()
    {
        Component zoneMap = CreateZoneMap(new Vector2(12f, 9f), new Vector3(1f, 0f, -2f), 0f);
        temporaryObject.transform.position = new Vector3(10f, 0f, 20f);

        object[] args = { "right_front", Vector3.zero };
        Assert.IsTrue((bool)Invoke(zoneMap, "TryGetZoneCenter", args));

        Vector3 center = (Vector3)args[1];
        Assert.AreEqual(15f, center.x, 0.001f);
        Assert.AreEqual(0f, center.y, 0.001f);
        Assert.AreEqual(15f, center.z, 0.001f);
    }

    private Component CreateZoneMap(Vector2 arenaSize, Vector3 centerOffset, float padding)
    {
        Type zoneMapType = RequireType("NewFPG.Combat.BattleArenaZoneMap, Assembly-CSharp");
        temporaryObject = new GameObject("Battle Arena Zone Map Test");
        Component zoneMap = temporaryObject.AddComponent(zoneMapType);
        SetField(zoneMap, "arenaSize", arenaSize);
        SetField(zoneMap, "centerOffset", centerOffset);
        SetField(zoneMap, "zonePadding", padding);
        return zoneMap;
    }

    private static Rect GetZoneRect(Component zoneMap, string zoneId)
    {
        object[] args = { zoneId, new Rect() };
        Assert.IsTrue((bool)Invoke(zoneMap, "TryGetZoneRect", args));
        return (Rect)args[1];
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static object Invoke(object target, string methodName, object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(method, target.GetType().Name + "." + methodName + " should exist.");
        return method.Invoke(target, args);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }
}
