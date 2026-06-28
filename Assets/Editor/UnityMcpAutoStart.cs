using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UnityMcpAutoStart
{
    public const string ProjectEnabledEditorPrefKey = "NewFPG.UnityMcpAutoStart.Enabled";

    private const string PackageAutoStartKey = "MCPForUnity.AutoStartOnLoad";
    private const string MenuAutoStartPath = "Tools/Unity MCP/Auto Start on Unity Launch";
    private const string MenuStartNowPath = "Tools/Unity MCP/Start Server Now";
    private const int DefaultMaxAttempts = 30;
    private const int DefaultDelayMilliseconds = 500;

    private static bool startInProgress;

    public interface IStartupServices
    {
        bool HttpTransportRunning();
        bool LocalServerReachable();
        bool StartLocalHttpServer();
        Task<bool> StartBridgeAsync();
    }

    public static bool IsProjectAutoStartEnabled =>
        EditorPrefs.GetBool(ProjectEnabledEditorPrefKey, true);

    static UnityMcpAutoStart()
    {
        if (!IsProjectAutoStartEnabled)
        {
            EditorPrefs.SetBool(PackageAutoStartKey, false);
            return;
        }

        ConfigurePackageForLocalHttpAutoStart();

        if (Application.isBatchMode &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UNITY_MCP_ALLOW_BATCH")))
        {
            return;
        }

        EditorApplication.delayCall += StartAfterEditorLoad;
    }

    public static void SetProjectAutoStartEnabled(bool enabled)
    {
        EditorPrefs.SetBool(ProjectEnabledEditorPrefKey, enabled);

        if (enabled)
        {
            ConfigurePackageForLocalHttpAutoStart();
            StartAfterEditorLoad();
            return;
        }

        EditorPrefs.SetBool(PackageAutoStartKey, false);
    }

    public static void ConfigurePackageForLocalHttpAutoStart()
    {
        EditorPrefs.SetBool(PackageAutoStartKey, true);
        EditorConfigurationCache.Instance.SetUseHttpTransport(true);
        EditorConfigurationCache.Instance.SetHttpTransportScope("local");
    }

    public static Task<bool> StartAsync(IStartupServices services)
    {
        return StartAsync(services, DefaultMaxAttempts, DefaultDelayMilliseconds);
    }

    public static async Task<bool> StartAsync(
        IStartupServices services,
        int maxAttempts,
        int delayMilliseconds)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (!IsProjectAutoStartEnabled)
        {
            return false;
        }

        ConfigurePackageForLocalHttpAutoStart();

        if (services.HttpTransportRunning())
        {
            return true;
        }

        if (!services.LocalServerReachable() && !services.StartLocalHttpServer())
        {
            Debug.LogWarning("[Unity MCP Auto Start] Failed to start the local MCP HTTP server.");
            return false;
        }

        int attempts = Math.Max(1, maxAttempts);
        int delay = Math.Max(0, delayMilliseconds);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (!IsProjectAutoStartEnabled)
            {
                return false;
            }

            if (services.HttpTransportRunning())
            {
                return true;
            }

            if (services.LocalServerReachable())
            {
                bool started = await services.StartBridgeAsync();
                if (started)
                {
                    Debug.Log("[Unity MCP Auto Start] MCP server and Unity bridge are running.");
                    return true;
                }
            }

            if (attempt < attempts - 1 && delay > 0)
            {
                await Task.Delay(delay);
            }
        }

        Debug.LogWarning("[Unity MCP Auto Start] MCP server did not become reachable in time.");
        return false;
    }

    [MenuItem(MenuAutoStartPath, false, 1000)]
    private static void ToggleProjectAutoStart()
    {
        SetProjectAutoStartEnabled(!IsProjectAutoStartEnabled);
    }

    [MenuItem(MenuAutoStartPath, true)]
    private static bool ValidateToggleProjectAutoStart()
    {
        Menu.SetChecked(MenuAutoStartPath, IsProjectAutoStartEnabled);
        return true;
    }

    [MenuItem(MenuStartNowPath, false, 1001)]
    private static void StartServerNow()
    {
        SetProjectAutoStartEnabled(true);
    }

    private static void StartAfterEditorLoad()
    {
        if (startInProgress)
        {
            return;
        }

        _ = StartAfterEditorLoadAsync();
    }

    private static async Task StartAfterEditorLoadAsync()
    {
        startInProgress = true;
        try
        {
            await StartAsync(new UnityStartupServices());
        }
        finally
        {
            startInProgress = false;
        }
    }

    private sealed class UnityStartupServices : IStartupServices
    {
        public bool HttpTransportRunning()
        {
            return MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http);
        }

        public bool LocalServerReachable()
        {
            return MCPServiceLocator.Server.IsLocalHttpServerReachable();
        }

        public bool StartLocalHttpServer()
        {
            return MCPServiceLocator.Server.StartLocalHttpServer(quiet: true);
        }

        public Task<bool> StartBridgeAsync()
        {
            return MCPServiceLocator.Bridge.StartAsync();
        }
    }
}
