using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

public sealed class LevelEncounterResolverEditorTests
{
    private GameObject fishPrefab;
    private GameObject wolfPrefab;

    [SetUp]
    public void SetUp()
    {
        fishPrefab = new GameObject("Resolver Fish Prefab");
        wolfPrefab = new GameObject("Resolver Wolf Prefab");
    }

    [TearDown]
    public void TearDown()
    {
        if (fishPrefab != null)
        {
            UnityEngine.Object.DestroyImmediate(fishPrefab);
        }

        if (wolfPrefab != null)
        {
            UnityEngine.Object.DestroyImmediate(wolfPrefab);
        }
    }

    [Test]
    public void PresetGroupRandomReturnsOnlySelectedGroupEntries()
    {
        object wave = Wave("preset_wave", "PresetGroupRandom");
        AddListItem(wave, "presetGroups", Group("disabled", 0f, Entry("fish", fishPrefab, 3, 1f)));
        AddListItem(wave, "presetGroups", Group("selected", 1f, Entry("wolf", wolfPrefab, 2, 1f)));

        Array requests = Resolve(wave, new Random(12));

        Assert.AreEqual(2, requests.Length);
        Assert.AreEqual("wolf", GetRequestField(requests.GetValue(0), "MonsterId"));
        Assert.AreSame(wolfPrefab, GetRequestField(requests.GetValue(0), "MonsterPrefab"));
        Assert.AreEqual("wolf", GetRequestField(requests.GetValue(1), "MonsterId"));
    }

    [Test]
    public void RandomPoolSpawnCountStaysInsideConfiguredRange()
    {
        object wave = RandomPoolWave(2, 4);
        for (int seed = 0; seed < 20; seed++)
        {
            Array requests = Resolve(wave, new Random(seed));
            Assert.GreaterOrEqual(requests.Length, 2);
            Assert.LessOrEqual(requests.Length, 4);
        }
    }

    [Test]
    public void RandomPoolOnlyUsesWeightedCandidates()
    {
        object wave = RandomPoolWave(5, 5);
        object randomPool = GetField(wave, "randomPool");
        ((IList)GetField(randomPool, "candidates")).Clear();
        AddListItem(randomPool, "candidates", Entry("fish", fishPrefab, 1, 0f));
        AddListItem(randomPool, "candidates", Entry("wolf", wolfPrefab, 1, 1f));

        Array requests = Resolve(wave, new Random(7));

        Assert.AreEqual(5, requests.Length);
        for (int i = 0; i < requests.Length; i++)
        {
            object request = requests.GetValue(i);
            Assert.AreEqual("wolf", GetRequestField(request, "MonsterId"));
            Assert.AreSame(wolfPrefab, GetRequestField(request, "MonsterPrefab"));
        }
    }

    [Test]
    public void ResolverIsStableForFixedSeed()
    {
        object wave = RandomPoolWave(6, 6);
        AddListItem(GetField(wave, "randomPool"), "candidates", Entry("wolf", wolfPrefab, 1, 2f));

        Array first = Resolve(wave, new Random(33));
        Array second = Resolve(wave, new Random(33));

        Assert.AreEqual(first.Length, second.Length);
        for (int i = 0; i < first.Length; i++)
        {
            object firstRequest = first.GetValue(i);
            object secondRequest = second.GetValue(i);
            Assert.AreEqual(GetRequestField(firstRequest, "MonsterId"), GetRequestField(secondRequest, "MonsterId"));
            Assert.AreSame(GetRequestField(firstRequest, "MonsterPrefab"), GetRequestField(secondRequest, "MonsterPrefab"));
        }
    }

    [Test]
    public void EncounterKeepsWaveOrderForDirectorConsumption()
    {
        object encounter = Activator.CreateInstance(RequireType("NewFPG.Level.LevelEncounterDefinition, Assembly-CSharp"));
        SetPublicField(encounter, "encounterId", "ordered_encounter");
        AddListItem(encounter, "waves", Wave("wave_1", "PresetGroupRandom"));
        AddListItem(encounter, "waves", Wave("wave_2", "RandomPool"));

        IList waves = (IList)GetField(encounter, "waves");

        Assert.AreEqual("wave_1", GetField(waves[0], "waveId"));
        Assert.AreEqual("wave_2", GetField(waves[1], "waveId"));
    }

    private object RandomPoolWave(int minCount, int maxCount)
    {
        object wave = Wave("random_wave", "RandomPool");
        object randomPool = GetField(wave, "randomPool");
        SetPublicField(randomPool, "minCount", minCount);
        SetPublicField(randomPool, "maxCount", maxCount);
        AddListItem(randomPool, "candidates", Entry("fish", fishPrefab, 1, 1f));
        return wave;
    }

    private static object Wave(string waveId, string selectionMode)
    {
        object wave = Activator.CreateInstance(RequireType("NewFPG.Level.LevelEncounterWave, Assembly-CSharp"));
        SetPublicField(wave, "waveId", waveId);
        SetPublicField(
            wave,
            "selectionMode",
            Enum.Parse(RequireType("NewFPG.Level.LevelSpawnSelectionMode, Assembly-CSharp"), selectionMode));
        return wave;
    }

    private static object Group(string groupId, float weight, params object[] entries)
    {
        object group = Activator.CreateInstance(RequireType("NewFPG.Level.LevelSpawnGroup, Assembly-CSharp"));
        SetPublicField(group, "groupId", groupId);
        SetPublicField(group, "weight", weight);
        for (int i = 0; i < entries.Length; i++)
        {
            AddListItem(group, "entries", entries[i]);
        }

        return group;
    }

    private static object Entry(string monsterId, GameObject prefab, int count, float weight)
    {
        object entry = Activator.CreateInstance(RequireType("NewFPG.Level.LevelSpawnEntry, Assembly-CSharp"));
        SetPublicField(entry, "monsterId", monsterId);
        SetPublicField(entry, "monsterPrefab", prefab);
        SetPublicField(entry, "count", count);
        SetPublicField(entry, "weight", weight);
        return entry;
    }

    private static Array Resolve(object wave, Random random)
    {
        Type resolverType = RequireType("NewFPG.Level.LevelEncounterResolver, Assembly-CSharp");
        Type waveType = RequireType("NewFPG.Level.LevelEncounterWave, Assembly-CSharp");
        MethodInfo method = resolverType.GetMethod("Resolve", BindingFlags.Static | BindingFlags.Public, null, new[] { waveType, typeof(Random) }, null);
        Assert.IsNotNull(method, "LevelEncounterResolver.Resolve(LevelEncounterWave, Random) should exist.");
        return (Array)method.Invoke(null, new object[] { wave, random });
    }

    private static void AddListItem(object target, string fieldName, object item)
    {
        ((IList)GetField(target, fieldName)).Add(item);
    }

    private static object GetRequestField(object request, string fieldName)
    {
        return request.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public).GetValue(request);
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        return field.GetValue(target);
    }

    private static void SetPublicField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }
}
