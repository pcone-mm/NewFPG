using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class SkillIndicatorSystemEditorTests
{
    [Test]
    public void RuntimeApiExposesSkillIndicatorInputPipeline()
    {
        Type weaponViewType = RequireType("NewFPG.Prototype.PrototypeFirstPersonWeaponView, Assembly-CSharp");
        Type weaponCasterType = RequireType("NewFPG.Combat.PlayerWeaponCaster, Assembly-CSharp");
        Type weaponDefinitionType = RequireType("NewFPG.Combat.WeaponDefinition, Assembly-CSharp");
        Type castCommandType = RequireType("NewFPG.Combat.SkillIndicators.CastCommandData, Assembly-CSharp");
        Type configType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorConfig, Assembly-CSharp");
        Type inputControllerType = RequireType("NewFPG.Combat.SkillIndicators.AbilityInputController, Assembly-CSharp");
        Type previewRuntimeType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPreviewRuntime, Assembly-CSharp");
        Type aimSolverType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorAimSolver, Assembly-CSharp");
        Type placementModeType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPlacementMode, Assembly-CSharp");

        Assert.IsNotNull(weaponViewType.GetEvent("WeaponPointerPressed", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(weaponViewType.GetEvent("WeaponPointerHeld", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(weaponViewType.GetEvent("WeaponPointerReleased", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(weaponViewType.GetEvent("WeaponPointerCancelled", BindingFlags.Instance | BindingFlags.Public));
        AssertPublicMethod(weaponViewType, "PlayWeaponAttack", typeof(int));
        Type weaponPresentationType = weaponViewType.GetNestedType("WeaponPresentation", BindingFlags.Public);
        Assert.IsNotNull(weaponPresentationType, "Weapon HUD should expose WeaponPresentation for direct WeaponDefinition-driven icons.");
        AssertPublicMethod(weaponViewType, "SetWeaponPresentations", weaponPresentationType.MakeArrayType());

        AssertPublicMethod(weaponCasterType, "TryCast", typeof(int), castCommandType);
        Assert.IsNotNull(weaponDefinitionType.GetProperty("IndicatorConfig", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(weaponDefinitionType.GetProperty("Icon", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(configType.GetCustomAttribute<CreateAssetMenuAttribute>());
        AssertPublicMethod(inputControllerType, "SetInputEnabled", typeof(bool));
        AssertPublicMethod(
            inputControllerType,
            "Bind",
            weaponViewType,
            weaponCasterType,
            typeof(Camera),
            RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorTemporaryArtIndex, Assembly-CSharp"));
        AssertPublicMethod(previewRuntimeType, "HidePreview");
        AssertPublicStaticMethod(aimSolverType, "ResolveSceneSurfaceMask", typeof(LayerMask));
        AssertPublicMethod(weaponCasterType, "SetRuntimeCastOriginOverride", typeof(Transform));
        Assert.IsNotNull(configType.GetProperty("PlacementMode", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(configType.GetProperty("GroundOffset", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(configType.GetProperty("StickToGround", BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNotNull(placementModeType.GetField("GroundSurface", BindingFlags.Static | BindingFlags.Public));
        Assert.IsNotNull(placementModeType.GetField("AttachToCastOrigin", BindingFlags.Static | BindingFlags.Public));
    }

    [Test]
    public void CastCommandDataContainsSharedShapeFields()
    {
        Type castCommandType = RequireType("NewFPG.Combat.SkillIndicators.CastCommandData, Assembly-CSharp");

        AssertField(castCommandType, "Origin", typeof(Vector3));
        AssertField(castCommandType, "SceneOrigin", typeof(Vector3));
        AssertField(castCommandType, "Direction", typeof(Vector3));
        AssertField(castCommandType, "TargetPoint", typeof(Vector3));
        AssertField(castCommandType, "PlacementMode", RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPlacementMode, Assembly-CSharp"));
        AssertField(castCommandType, "Radius", typeof(float));
        AssertField(castCommandType, "Width", typeof(float));
        AssertField(castCommandType, "Length", typeof(float));
        AssertField(castCommandType, "Angle", typeof(float));
        AssertField(castCommandType, "GroundOffset", typeof(float));
        AssertField(castCommandType, "HoldDuration", typeof(float));
        AssertField(castCommandType, "IsValid", typeof(bool));
    }

    [Test]
    public void SceneSurfaceMaskExcludesFirstPersonWeaponLayer()
    {
        int firstPersonWeaponLayer = LayerMask.NameToLayer("FirstPersonWeapon");
        Assert.That(firstPersonWeaponLayer, Is.GreaterThanOrEqualTo(0), "FirstPersonWeapon layer should exist.");

        Type aimSolverType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorAimSolver, Assembly-CSharp");
        MethodInfo resolveMask = aimSolverType.GetMethod("ResolveSceneSurfaceMask", BindingFlags.Static | BindingFlags.Public);
        var configuredMask = new LayerMask { value = ~0 };
        var resolvedMask = (LayerMask)resolveMask.Invoke(null, new object[] { configuredMask });

        Assert.That(
            resolvedMask.value & (1 << firstPersonWeaponLayer),
            Is.EqualTo(0),
            "Skill indicator scene raycasts should not hit the first-person HUD weapon layer.");
    }

    [Test]
    public void AimSolverKeepsHudOriginAndProjectsSceneOriginToSurface()
    {
        Type resolvedConfigType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorResolvedConfig, Assembly-CSharp");
        Type aimSolverType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorAimSolver, Assembly-CSharp");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        GameObject castOrigin = new GameObject("HUD Cast Origin");
        try
        {
            ground.name = "Skill Indicator Test Ground";
            ground.transform.position = Vector3.zero;
            castOrigin.transform.position = new Vector3(1.25f, 2.4f, -1.5f);
            Physics.SyncTransforms();

            object config = Activator.CreateInstance(resolvedConfigType);
            SetField(config, "abilityId", "test");
            SetEnumField(config, "aimSource", "Self");
            SetEnumField(config, "holdPolicy", "CastOnSelf");
            SetEnumField(config, "shapeType", "GroundCircle");
            SetField(config, "surfaceMask", new LayerMask { value = ~0 });
            SetField(config, "range", 6f);
            SetField(config, "radius", 1f);
            SetField(config, "width", 1f);
            SetField(config, "length", 6f);
            SetField(config, "height", 2f);
            SetField(config, "clampToRange", true);

            MethodInfo resolve = aimSolverType.GetMethod("Resolve", BindingFlags.Static | BindingFlags.Public);
            object frame = resolve.Invoke(
                null,
                new object[] { config, castOrigin.transform, castOrigin.transform, null, Vector2.zero, false, 0.25f, 7 });
            object command = GetField(frame, "Command");
            Vector3 origin = (Vector3)GetField(command, "Origin");
            Vector3 sceneOrigin = (Vector3)GetField(command, "SceneOrigin");
            Vector3 targetPoint = (Vector3)GetField(command, "TargetPoint");

            Assert.That(origin, Is.EqualTo(castOrigin.transform.position));
            Assert.That(sceneOrigin.x, Is.EqualTo(castOrigin.transform.position.x).Within(0.001f));
            Assert.That(sceneOrigin.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(sceneOrigin.z, Is.EqualTo(castOrigin.transform.position.z).Within(0.001f));
            Assert.That(targetPoint, Is.EqualTo(sceneOrigin));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(castOrigin);
            UnityEngine.Object.DestroyImmediate(ground);
        }
    }

    [Test]
    public void GroundAimIgnoresDamageableColliderAndStaysOnGround()
    {
        Type resolvedConfigType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorResolvedConfig, Assembly-CSharp");
        Type aimSolverType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorAimSolver, Assembly-CSharp");
        Type combatVitalsType = RequireType("NewFPG.Combat.CombatVitals, Assembly-CSharp");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        GameObject targetBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject castOrigin = new GameObject("HUD Cast Origin");
        GameObject cameraObject = new GameObject("Ground Aim Camera");
        try
        {
            ground.name = "Ground Aim Test Surface";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            targetBody.name = "Damageable Target Body";
            targetBody.transform.position = new Vector3(0f, 1f, 0f);
            targetBody.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
            targetBody.AddComponent(combatVitalsType);
            castOrigin.transform.position = new Vector3(0f, 1.2f, -3f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 3f, -5f);
            camera.transform.LookAt(new Vector3(0f, 0.4f, 0f));
            Physics.SyncTransforms();

            object config = CreateGroundResolvedConfig(resolvedConfigType, requireSurfaceHit: true, range: 8f);
            MethodInfo resolve = aimSolverType.GetMethod("Resolve", BindingFlags.Static | BindingFlags.Public);
            object frame = resolve.Invoke(
                null,
                new object[] { config, castOrigin.transform, castOrigin.transform, camera, Vector2.zero, false, 0.25f, 11 });
            object command = GetField(frame, "Command");

            Vector3 targetPoint = (Vector3)GetField(command, "TargetPoint");
            Vector3 surfaceNormal = (Vector3)GetField(command, "SurfaceNormal");
            Vector3 direction = (Vector3)GetField(command, "Direction");

            Assert.That(targetPoint.y, Is.EqualTo(0f).Within(0.001f), "Ground indicators should not move up onto damageable bodies.");
            Assert.That(surfaceNormal, Is.EqualTo(Vector3.up));
            Assert.That(Mathf.Abs(direction.y), Is.LessThan(0.001f), "Ground indicator aim direction should stay horizontal.");
            Assert.That((bool)GetField(command, "IsValid"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(castOrigin);
            UnityEngine.Object.DestroyImmediate(targetBody);
            UnityEngine.Object.DestroyImmediate(ground);
        }
    }

    [Test]
    public void GroundAimRejectsSteepSurfaceAndFallsBackToHorizontalPlane()
    {
        Type resolvedConfigType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorResolvedConfig, Assembly-CSharp");
        Type aimSolverType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorAimSolver, Assembly-CSharp");
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject castOrigin = new GameObject("HUD Cast Origin");
        GameObject cameraObject = new GameObject("Ground Aim Wall Camera");
        try
        {
            wall.name = "Steep Aim Wall";
            wall.transform.position = new Vector3(0f, 1f, 4f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.2f);
            castOrigin.transform.position = new Vector3(0f, 1.2f, 0f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 2f, -4f);
            camera.transform.LookAt(new Vector3(0f, 1f, 4f));
            Physics.SyncTransforms();

            object config = CreateGroundResolvedConfig(resolvedConfigType, requireSurfaceHit: false, range: 3f);
            MethodInfo resolve = aimSolverType.GetMethod("Resolve", BindingFlags.Static | BindingFlags.Public);
            object frame = resolve.Invoke(
                null,
                new object[] { config, castOrigin.transform, castOrigin.transform, camera, Vector2.zero, false, 0.25f, 12 });
            object command = GetField(frame, "Command");

            Vector3 targetPoint = (Vector3)GetField(command, "TargetPoint");
            Vector3 surfaceNormal = (Vector3)GetField(command, "SurfaceNormal");
            Vector3 sceneOrigin = (Vector3)GetField(command, "SceneOrigin");

            Assert.That(targetPoint.y, Is.EqualTo(sceneOrigin.y).Within(0.001f));
            Assert.That((targetPoint - sceneOrigin).magnitude, Is.EqualTo(3f).Within(0.01f));
            Assert.That(surfaceNormal, Is.EqualTo(Vector3.up), "Steep hits should not rotate ground indicators vertically.");
            Assert.That((bool)GetField(command, "IsValid"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(castOrigin);
            UnityEngine.Object.DestroyImmediate(wall);
        }
    }

    [Test]
    public void PreviewRuntimeKeepsGroundCircleHorizontalEvenWithBadSurfaceNormal()
    {
        Type previewRuntimeType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPreviewRuntime, Assembly-CSharp");
        Type previewFrameType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPreviewFrame, Assembly-CSharp");
        Type commandType = RequireType("NewFPG.Combat.SkillIndicators.CastCommandData, Assembly-CSharp");
        Type resolvedConfigType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorResolvedConfig, Assembly-CSharp");
        Type validationType = RequireType("NewFPG.Combat.SkillIndicators.IndicatorValidationResult, Assembly-CSharp");
        GameObject hostObject = new GameObject("Preview Rotation Host");
        GameObject previewInstance = GameObject.CreatePrimitive(PrimitiveType.Quad);
        try
        {
            Component runtime = hostObject.AddComponent(previewRuntimeType);
            MethodInfo showPreview = previewRuntimeType.GetMethod("ShowPreview", BindingFlags.Instance | BindingFlags.Public);
            showPreview.Invoke(runtime, new object[] { null, null, hostObject.transform, hostObject.transform, Vector2.zero, false, 0f });

            object command = Activator.CreateInstance(commandType);
            SetField(command, "SceneOrigin", Vector3.zero);
            SetField(command, "TargetPoint", new Vector3(2f, 0f, 3f));
            SetField(command, "Direction", Vector3.forward);
            SetField(command, "SurfaceNormal", Vector3.right);
            SetField(command, "GroundOffset", 0.06f);
            SetField(command, "IsValid", true);

            object config = CreateGroundResolvedConfig(resolvedConfigType, requireSurfaceHit: false, range: 6f);
            SetEnumField(config, "shapeType", "GroundCircle");
            SetField(config, "radius", 1.2f);

            object frame = Activator.CreateInstance(previewFrameType);
            SetField(frame, "Command", command);
            SetField(frame, "Config", config);
            MethodInfo validFactory = validationType.GetMethod("Valid", BindingFlags.Static | BindingFlags.Public);
            SetField(frame, "Validation", validFactory.Invoke(null, Array.Empty<object>()));

            MethodInfo applyFrame = previewRuntimeType.GetMethod("ApplyFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            applyFrame.Invoke(runtime, new object[] { previewInstance, frame, hostObject.transform });

            Assert.That(Quaternion.Angle(previewInstance.transform.rotation, Quaternion.identity), Is.LessThan(0.01f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(previewInstance);
            UnityEngine.Object.DestroyImmediate(hostObject);
            GameObject sceneRoot = GameObject.Find("SkillIndicatorScenePreviewRoot");
            if (sceneRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(sceneRoot);
            }
        }
    }

    [Test]
    public void PreviewRuntimePlacesPreviewObjectsOnSceneLayerOutsideHudHierarchy()
    {
        Type previewRuntimeType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPreviewRuntime, Assembly-CSharp");
        GameObject existingRoot = GameObject.Find("SkillIndicatorScenePreviewRoot");
        if (existingRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(existingRoot);
        }

        int firstPersonWeaponLayer = LayerMask.NameToLayer("FirstPersonWeapon");
        Assert.That(firstPersonWeaponLayer, Is.GreaterThanOrEqualTo(0), "FirstPersonWeapon layer should exist.");

        GameObject hudObject = new GameObject("HUD Preview Runtime Host");
        GameObject staleRoot = new GameObject("SkillIndicatorScenePreviewRoot");
        GameObject staleChild = new GameObject("Stale Preview Child");
        try
        {
            hudObject.layer = firstPersonWeaponLayer;
            staleRoot.layer = firstPersonWeaponLayer;
            staleChild.layer = firstPersonWeaponLayer;
            staleRoot.transform.SetParent(hudObject.transform, false);
            staleRoot.transform.localPosition = new Vector3(0.25f, -0.5f, 1.25f);
            staleChild.transform.SetParent(staleRoot.transform, false);

            Component runtime = hudObject.AddComponent(previewRuntimeType);
            MethodInfo showPreview = previewRuntimeType.GetMethod("ShowPreview", BindingFlags.Instance | BindingFlags.Public);
            object frame = showPreview.Invoke(
                runtime,
                new object[] { null, null, hudObject.transform, hudObject.transform, Vector2.zero, false, 0f });
            object command = GetField(frame, "Command");

            Assert.That((Vector3)GetField(command, "Origin"), Is.EqualTo(hudObject.transform.position));
            Assert.That(staleRoot.transform.parent, Is.Null, "Preview root should live in the scene, not under the HUD weapon view.");
            Assert.That(staleRoot.transform.position, Is.EqualTo(Vector3.zero), "Preview root should not inherit HUD weapon view transforms.");
            Assert.That(staleRoot.layer, Is.EqualTo(0), "Preview root should render on the scene/default layer.");
            Assert.That(staleChild.layer, Is.EqualTo(0), "Existing preview root children should be corrected to the scene/default layer.");

            Transform previewInstance = staleRoot.transform.Find("PF_IND_GroundCircle_Runtime");
            Assert.That(previewInstance, Is.Not.Null, "Preview instance should be spawned under the scene preview root.");
            Assert.That(previewInstance.gameObject.layer, Is.EqualTo(0), "Preview instance should render on the scene/default layer.");
            Assert.That(previewInstance.position.y, Is.GreaterThanOrEqualTo(0.059f), "Ground previews should be lifted above the surface to avoid z-fighting.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hudObject);
            UnityEngine.Object.DestroyImmediate(staleRoot);
        }
    }

    [Test]
    public void NonGroundPreviewCanAttachToCastOrigin()
    {
        Type previewRuntimeType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPreviewRuntime, Assembly-CSharp");
        Type configType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorConfig, Assembly-CSharp");
        GameObject existingSceneRoot = GameObject.Find("SkillIndicatorScenePreviewRoot");
        if (existingSceneRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(existingSceneRoot);
        }

        GameObject existingWorldRoot = GameObject.Find("SkillIndicatorWorldPreviewRoot");
        if (existingWorldRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(existingWorldRoot);
        }

        GameObject hostObject = new GameObject("Non Ground Preview Host");
        GameObject castOrigin = new GameObject("Attached Cast Origin");
        try
        {
            castOrigin.transform.position = new Vector3(2f, 1.5f, -3f);
            UnityEngine.Object config = ScriptableObject.CreateInstance(configType);
            SetPrivateField(config, "placementMode", Enum.Parse(RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorPlacementMode, Assembly-CSharp"), "AttachToCastOrigin"));

            Component runtime = hostObject.AddComponent(previewRuntimeType);
            MethodInfo showPreview = previewRuntimeType.GetMethod("ShowPreview", BindingFlags.Instance | BindingFlags.Public);
            showPreview.Invoke(
                runtime,
                new object[] { config, null, castOrigin.transform, castOrigin.transform, Vector2.zero, false, 0f });

            Transform previewInstance = castOrigin.transform.Find("PF_IND_GroundCircle_Runtime");
            Assert.That(previewInstance, Is.Not.Null, "AttachToCastOrigin previews should live under the cast origin.");
            Assert.That(previewInstance.position, Is.EqualTo(castOrigin.transform.position));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostObject);
            UnityEngine.Object.DestroyImmediate(castOrigin);
        }
    }

    [Test]
    public void UiInputBlockerBlocksAndRestoresRaycasts()
    {
        Type blockerType = RequireType("NewFPG.Combat.SkillIndicators.SkillIndicatorUiInputBlocker, Assembly-CSharp");
        GameObject hostObject = new GameObject("UI Input Blocker Host");
        try
        {
            Component blocker = hostObject.AddComponent(blockerType);
            MethodInfo beginBlock = blockerType.GetMethod("BeginBlock", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo endBlock = blockerType.GetMethod("EndBlock", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo clear = blockerType.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo isBlocking = blockerType.GetProperty("IsBlocking", BindingFlags.Instance | BindingFlags.Public);

            beginBlock.Invoke(blocker, Array.Empty<object>());
            Assert.That((bool)isBlocking.GetValue(blocker), Is.True);

            Canvas blockerCanvas = hostObject.GetComponentInChildren<Canvas>(true);
            Assert.That(blockerCanvas, Is.Not.Null);
            Assert.That(blockerCanvas.sortingOrder, Is.GreaterThanOrEqualTo(32000));
            Image blockerImage = hostObject.GetComponentInChildren<Image>(true);
            Assert.That(blockerImage, Is.Not.Null);
            Assert.That(blockerImage.raycastTarget, Is.True);

            endBlock.Invoke(blocker, Array.Empty<object>());
            Assert.That((bool)isBlocking.GetValue(blocker), Is.False);

            beginBlock.Invoke(blocker, Array.Empty<object>());
            clear.Invoke(blocker, Array.Empty<object>());
            Assert.That((bool)isBlocking.GetValue(blocker), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostObject);
        }
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        return field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, value);
    }

    private static void SetEnumField(object target, string fieldName, string valueName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, target.GetType().Name + "." + fieldName + " should exist.");
        field.SetValue(target, Enum.Parse(field.FieldType, valueName));
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName, true);
        Assert.IsNotNull(type, assemblyQualifiedName + " should resolve.");
        return type;
    }

    private static void AssertPublicMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        Assert.IsNotNull(
            type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null),
            type.Name + "." + methodName + " should be public.");
    }

    private static void AssertPublicStaticMethod(Type type, string methodName, params Type[] parameterTypes)
    {
        Assert.IsNotNull(
            type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public, null, parameterTypes, null),
            type.Name + "." + methodName + " should be public static.");
    }

    private static object CreateGroundResolvedConfig(Type resolvedConfigType, bool requireSurfaceHit, float range)
    {
        object config = Activator.CreateInstance(resolvedConfigType);
        SetField(config, "abilityId", "ground-test");
        SetEnumField(config, "aimSource", "CrosshairRay");
        SetEnumField(config, "holdPolicy", "CastAtCrosshairHit");
        SetEnumField(config, "placementMode", "GroundSurface");
        SetEnumField(config, "shapeType", "GroundCircle");
        SetField(config, "surfaceMask", new LayerMask { value = ~0 });
        SetField(config, "range", range);
        SetField(config, "radius", 1f);
        SetField(config, "width", 1f);
        SetField(config, "length", range);
        SetField(config, "height", 2f);
        SetField(config, "groundOffset", 0.06f);
        SetField(config, "requireSurfaceHit", requireSurfaceHit);
        SetField(config, "clampToRange", true);
        return config;
    }

    private static void AssertField(Type type, string fieldName, Type fieldType)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(field, type.Name + "." + fieldName + " should exist.");
        Assert.AreEqual(fieldType, field.FieldType);
    }
}
