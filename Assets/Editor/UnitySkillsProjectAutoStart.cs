#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[InitializeOnLoad]
internal static class UnitySkillsProjectAutoStart
{
    private const string PackageName = "com.besty.unity-skills";
    private const string LatestReleaseUrl =
        "https://github.com/Besty0728/Unity-Skills/releases/latest";
    private const string UpdateCheckSessionKey =
        "NewFPG.UnitySkillsProjectAutoStart.UpdateChecked";

    private static UnityWebRequest updateRequest;

    static UnitySkillsProjectAutoStart()
    {
        EditorApplication.delayCall += Initialize;
    }

    private static void Initialize()
    {
        EnableAutoStart();

        if (!SessionState.GetBool(UpdateCheckSessionKey, false))
        {
            SessionState.SetBool(UpdateCheckSessionKey, true);
            CheckForUpdates(false);
        }
    }

    private static void EnableAutoStart()
    {
        Type serverType = FindType("UnitySkills.SkillsHttpServer");
        if (serverType == null)
        {
            return;
        }

        SetBooleanProperty(serverType, "AutoStart", true);
        SetBooleanProperty(serverType, "StartOnEditorLaunch", true);
    }

    [MenuItem("Tools/UnitySkills Project/Check for Updates")]
    private static void CheckForUpdatesFromMenu()
    {
        CheckForUpdates(true);
    }

    private static void CheckForUpdates(bool reportUpToDate)
    {
        if (updateRequest != null)
        {
            return;
        }

        updateRequest = UnityWebRequest.Head(LatestReleaseUrl);
        updateRequest.SetRequestHeader("User-Agent", "NewFPG-UnitySkills-UpdateCheck");
        updateRequest.SendWebRequest();
        EditorApplication.update += PollUpdateRequest;

        void PollUpdateRequest()
        {
            if (updateRequest == null || !updateRequest.isDone)
            {
                return;
            }

            EditorApplication.update -= PollUpdateRequest;
            UnityWebRequest completedRequest = updateRequest;
            updateRequest = null;

            try
            {
                if (completedRequest.result != UnityWebRequest.Result.Success)
                {
                    if (reportUpToDate)
                    {
                        Debug.LogWarning(
                            $"UnitySkills update check failed: {completedRequest.error}");
                    }

                    return;
                }

                string installedVersion = GetInstalledVersion();
                string releaseUrl = completedRequest.url;
                string latestVersion = NormalizeVersion(
                    releaseUrl.Substring(releaseUrl.LastIndexOf('/') + 1));

                if (string.IsNullOrEmpty(installedVersion) ||
                    string.IsNullOrEmpty(latestVersion))
                {
                    return;
                }

                if (IsNewerVersion(latestVersion, installedVersion))
                {
                    bool openRelease = EditorUtility.DisplayDialog(
                        "UnitySkills Update Available",
                        $"Installed: v{installedVersion}\nLatest: v{latestVersion}",
                        "Open Release Page",
                        "Later");

                    if (openRelease)
                    {
                        Application.OpenURL(releaseUrl);
                    }
                }
                else if (reportUpToDate)
                {
                    EditorUtility.DisplayDialog(
                        "UnitySkills Update Check",
                        $"UnitySkills v{installedVersion} is up to date.",
                        "OK");
                }
            }
            finally
            {
                completedRequest.Dispose();
            }
        }
    }

    private static string GetInstalledVersion()
    {
        foreach (UnityEditor.PackageManager.PackageInfo package in
                 UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
        {
            if (package.name == PackageName)
            {
                return NormalizeVersion(package.version);
            }
        }

        return null;
    }

    private static string NormalizeVersion(string version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? null
            : version.Trim().TrimStart('v', 'V').Split('-')[0];
    }

    private static bool IsNewerVersion(string candidate, string installed)
    {
        return Version.TryParse(candidate, out Version candidateVersion) &&
               Version.TryParse(installed, out Version installedVersion) &&
               candidateVersion > installedVersion;
    }

    private static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void SetBooleanProperty(Type type, string propertyName, bool value)
    {
        PropertyInfo property = type.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Static);

        if (property?.PropertyType == typeof(bool) && property.CanWrite)
        {
            property.SetValue(null, value);
        }
    }

}
#endif
