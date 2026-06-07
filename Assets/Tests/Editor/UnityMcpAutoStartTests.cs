using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;

public sealed class UnityMcpAutoStartTests
{
    private const string PackageAutoStartKey = "MCPForUnity.AutoStartOnLoad";
    private const string PackageUseHttpKey = "MCPForUnity.UseHttpTransport";
    private const string PackageHttpScopeKey = "MCPForUnity.HttpTransportScope";

    private Type autoStartType;
    private Type startupServicesType;
    private string projectEnabledKey;
    private bool hadProjectEnabledKey;
    private bool projectEnabledValue;
    private bool packageAutoStartValue;
    private bool packageUseHttpValue;
    private string packageHttpScopeValue;

    [SetUp]
    public void SetUp()
    {
        autoStartType = Type.GetType("UnityMcpAutoStart, Assembly-CSharp-Editor", true);
        startupServicesType = autoStartType.GetNestedType("IStartupServices");
        projectEnabledKey = (string)autoStartType
            .GetField("ProjectEnabledEditorPrefKey", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null);

        hadProjectEnabledKey = EditorPrefs.HasKey(projectEnabledKey);
        projectEnabledValue = EditorPrefs.GetBool(projectEnabledKey, false);
        packageAutoStartValue = EditorPrefs.GetBool(PackageAutoStartKey, false);
        packageUseHttpValue = EditorPrefs.GetBool(PackageUseHttpKey, false);
        packageHttpScopeValue = EditorPrefs.GetString(PackageHttpScopeKey, string.Empty);
    }

    [TearDown]
    public void TearDown()
    {
        if (hadProjectEnabledKey)
        {
            EditorPrefs.SetBool(projectEnabledKey, projectEnabledValue);
        }
        else
        {
            EditorPrefs.DeleteKey(projectEnabledKey);
        }

        EditorPrefs.SetBool(PackageAutoStartKey, packageAutoStartValue);
        EditorPrefs.SetBool(PackageUseHttpKey, packageUseHttpValue);
        EditorPrefs.SetString(PackageHttpScopeKey, packageHttpScopeValue);
    }

    [Test]
    public void ProjectAutoStartDefaultsToEnabled()
    {
        EditorPrefs.DeleteKey(projectEnabledKey);

        Assert.IsTrue(GetIsProjectAutoStartEnabled());
    }

    [Test]
    public void SetProjectAutoStartEnabledPersistsUserChoice()
    {
        InvokeSetProjectAutoStartEnabled(false);

        Assert.IsFalse(GetIsProjectAutoStartEnabled());
        Assert.IsFalse(EditorPrefs.GetBool(projectEnabledKey, true));
        Assert.IsFalse(EditorPrefs.GetBool(PackageAutoStartKey, true));
    }

    [Test]
    public void ConfigurePackageForLocalHttpAutoStartEnablesPackagePreferences()
    {
        InvokeConfigurePackageForLocalHttpAutoStart();

        Assert.IsTrue(EditorPrefs.GetBool(PackageAutoStartKey, false));
        Assert.IsTrue(EditorPrefs.GetBool(PackageUseHttpKey, false));
        Assert.AreEqual("local", EditorPrefs.GetString(PackageHttpScopeKey, string.Empty));
    }

    [Test]
    public async Task StartAsyncDoesNothingWhenHttpTransportAlreadyRunning()
    {
        var services = new FakeStartupServices
        {
            IsHttpTransportRunning = true,
            IsLocalServerReachable = false
        };

        await InvokeStartAsync(services);

        Assert.AreEqual(0, services.StartLocalHttpServerCalls);
        Assert.AreEqual(0, services.StartBridgeCalls);
    }

    [Test]
    public async Task StartAsyncStartsLocalServerBeforeBridgeWhenServerIsNotReachable()
    {
        var services = new FakeStartupServices
        {
            IsLocalServerReachable = false,
            StartLocalHttpServerResult = true,
            StartBridgeResult = true
        };

        await InvokeStartAsync(services, 1, 0);

        Assert.AreEqual(1, services.StartLocalHttpServerCalls);
        Assert.AreEqual(1, services.StartBridgeCalls);
    }

    [Test]
    public async Task StartAsyncConnectsBridgeWhenServerIsAlreadyReachable()
    {
        var services = new FakeStartupServices
        {
            IsLocalServerReachable = true,
            StartBridgeResult = true
        };

        await InvokeStartAsync(services, 1, 0);

        Assert.AreEqual(0, services.StartLocalHttpServerCalls);
        Assert.AreEqual(1, services.StartBridgeCalls);
    }

    private bool GetIsProjectAutoStartEnabled()
    {
        return (bool)autoStartType
            .GetProperty("IsProjectAutoStartEnabled", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null);
    }

    private void InvokeSetProjectAutoStartEnabled(bool enabled)
    {
        autoStartType
            .GetMethod("SetProjectAutoStartEnabled", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { enabled });
    }

    private void InvokeConfigurePackageForLocalHttpAutoStart()
    {
        autoStartType
            .GetMethod("ConfigurePackageForLocalHttpAutoStart", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, null);
    }

    private Task InvokeStartAsync(FakeStartupServices services)
    {
        var proxy = StartupServicesProxy.CreateFor(startupServicesType, services);
        return (Task)autoStartType
            .GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Static, null, new[] { startupServicesType }, null)
            .Invoke(null, new object[] { proxy });
    }

    private Task InvokeStartAsync(FakeStartupServices services, int maxAttempts, int delayMilliseconds)
    {
        var proxy = StartupServicesProxy.CreateFor(startupServicesType, services);
        return (Task)autoStartType
            .GetMethod(
                "StartAsync",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { startupServicesType, typeof(int), typeof(int) },
                null)
            .Invoke(null, new object[] { proxy, maxAttempts, delayMilliseconds });
    }

    public sealed class FakeStartupServices
    {
        public bool IsHttpTransportRunning { get; set; }
        public bool IsLocalServerReachable { get; set; }
        public bool StartLocalHttpServerResult { get; set; }
        public bool StartBridgeResult { get; set; }
        public int StartLocalHttpServerCalls { get; private set; }
        public int StartBridgeCalls { get; private set; }

        public bool HttpTransportRunning()
        {
            return IsHttpTransportRunning;
        }

        public bool LocalServerReachable()
        {
            return IsLocalServerReachable;
        }

        public bool StartLocalHttpServer()
        {
            StartLocalHttpServerCalls++;
            IsLocalServerReachable = StartLocalHttpServerResult;
            return StartLocalHttpServerResult;
        }

        public Task<bool> StartBridgeAsync()
        {
            StartBridgeCalls++;
            IsHttpTransportRunning = StartBridgeResult;
            return Task.FromResult(StartBridgeResult);
        }
    }

    public class StartupServicesProxy : DispatchProxy
    {
        private FakeStartupServices services;

        public StartupServicesProxy()
        {
        }

        public static object CreateFor(Type interfaceType, FakeStartupServices services)
        {
            var method = typeof(StartupServicesProxy)
                .GetMethod(nameof(CreateTyped), BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(interfaceType);
            return method.Invoke(null, new object[] { services });
        }

        private static T CreateTyped<T>(FakeStartupServices services)
        {
            object proxy = DispatchProxy.Create<T, StartupServicesProxy>();
            ((StartupServicesProxy)proxy).services = services;
            return (T)proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            switch (targetMethod.Name)
            {
                case "HttpTransportRunning":
                    return services.HttpTransportRunning();
                case "LocalServerReachable":
                    return services.LocalServerReachable();
                case "StartLocalHttpServer":
                    return services.StartLocalHttpServer();
                case "StartBridgeAsync":
                    return services.StartBridgeAsync();
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }
}
