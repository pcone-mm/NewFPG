using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.LevelAuthoring
{
    public static class FpgCoverCameraProfileAuthoring
    {
        private const string ClipboardFormat =
            "FPG.CoverCameraProfile.Settings";
        private const int ClipboardVersion = 1;

        [Serializable]
        private sealed class CameraProfileClipboardEnvelope
        {
            public string format;
            public int version;
            public string profileJson;
        }

        public const string DefaultProfileRoot =
            "Assets/FPGDemo/Config/Level/CameraProfiles";
        public const string DefaultTemplatePath =
            DefaultProfileRoot + "/FPG_Default_CoverCamera.asset";

        public static int CountReferences(FpgCoverCameraProfile profile)
        {
            if (profile == null)
            {
                return 0;
            }

            int count = 0;
            List<ScriptableObject> rooms = FpgRoomAuthoringSchema.FindAllRooms();
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                SerializedObject room = new SerializedObject(rooms[roomIndex]);
                SerializedProperty covers = room.FindProperty("coverSlots");
                if (covers == null || !covers.isArray)
                {
                    continue;
                }

                for (int coverIndex = 0; coverIndex < covers.arraySize; coverIndex++)
                {
                    SerializedProperty cameraProfile = covers
                        .GetArrayElementAtIndex(coverIndex)
                        .FindPropertyRelative("cameraProfile");
                    if (cameraProfile != null
                        && cameraProfile.objectReferenceValue == profile)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public static bool TryCreateClipboardText(
            FpgCoverCameraProfile profile,
            out string clipboardText,
            out string error)
        {
            clipboardText = string.Empty;
            if (profile == null)
            {
                error = "A camera profile is required to copy settings.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                error = $"Camera profile '{profile.name}' is invalid: {error}";
                return false;
            }

            try
            {
                CameraProfileClipboardEnvelope envelope =
                    new CameraProfileClipboardEnvelope
                    {
                        format = ClipboardFormat,
                        version = ClipboardVersion,
                        profileJson = EditorJsonUtility.ToJson(profile)
                    };
                clipboardText = JsonUtility.ToJson(envelope, true);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not copy camera profile settings: "
                    + exception.GetBaseException().Message;
                return false;
            }
        }

        public static bool TryPasteClipboardText(
            string clipboardText,
            FpgCoverCameraProfile destination,
            out string error)
        {
            if (destination == null)
            {
                error = "A destination camera profile is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                error = "The clipboard does not contain camera profile settings.";
                return false;
            }

            CameraProfileClipboardEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<
                    CameraProfileClipboardEnvelope>(clipboardText);
            }
            catch (Exception exception)
            {
                error = "The clipboard camera profile data is invalid: "
                    + exception.GetBaseException().Message;
                return false;
            }

            if (envelope == null
                || !string.Equals(
                    envelope.format,
                    ClipboardFormat,
                    StringComparison.Ordinal)
                || envelope.version != ClipboardVersion
                || string.IsNullOrWhiteSpace(envelope.profileJson))
            {
                error = "The clipboard does not contain compatible camera profile settings.";
                return false;
            }

            FpgCoverCameraProfile source =
                ScriptableObject.CreateInstance<FpgCoverCameraProfile>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(
                    envelope.profileJson,
                    source);
                if (!source.TryValidate(out error))
                {
                    error = "The clipboard camera profile settings are invalid: "
                        + error;
                    return false;
                }

                return TryCopyValues(
                    source,
                    destination,
                    "Paste Cover Camera Profile Settings",
                    out error);
            }
            catch (Exception exception)
            {
                error = "Could not paste camera profile settings: "
                    + exception.GetBaseException().Message;
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        public static IReadOnlyList<FpgCoverCameraProfile> FindOrphanProfiles()
        {
            HashSet<FpgCoverCameraProfile> referenced =
                new HashSet<FpgCoverCameraProfile>();
            List<ScriptableObject> rooms = FpgRoomAuthoringSchema.FindAllRooms();
            for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
            {
                SerializedObject room = new SerializedObject(rooms[roomIndex]);
                SerializedProperty covers = room.FindProperty("coverSlots");
                if (covers == null || !covers.isArray)
                {
                    continue;
                }

                for (int coverIndex = 0; coverIndex < covers.arraySize; coverIndex++)
                {
                    FpgCoverCameraProfile profile = covers
                        .GetArrayElementAtIndex(coverIndex)
                        .FindPropertyRelative("cameraProfile")
                        ?.objectReferenceValue as FpgCoverCameraProfile;
                    if (profile != null)
                    {
                        referenced.Add(profile);
                    }
                }
            }

            return AssetDatabase.FindAssets("t:FpgCoverCameraProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.Equals(
                        path,
                        DefaultTemplatePath,
                        StringComparison.Ordinal))
                .Select(AssetDatabase.LoadAssetAtPath<FpgCoverCameraProfile>)
                .Where(profile => profile != null && !referenced.Contains(profile))
                .OrderBy(profile => AssetDatabase.GetAssetPath(profile),
                    StringComparer.Ordinal)
                .ToArray();
        }

        public static bool TryCloneForCover(
            FpgCoverCameraProfile source,
            FpgRoomDefinition room,
            string coverId,
            out FpgCoverCameraProfile clone,
            out string error)
        {
            string roomPath = room == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(room);
            return TryCloneProfile(
                source,
                ResolveProfileFolder(roomPath, room == null ? null : room.name),
                room == null ? "Room" : room.name,
                coverId,
                true,
                out clone,
                out _,
                out error);
        }

        public static bool TryMakeCoverProfileUnique(
            FpgRoomDefinition room,
            int coverIndex,
            out FpgCoverCameraProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (room == null)
            {
                error = "A room is required to make a cover camera profile unique.";
                return false;
            }

            SerializedObject roomData = new SerializedObject(room);
            SerializedProperty covers = roomData.FindProperty("coverSlots");
            if (covers == null || !covers.isArray
                || coverIndex < 0 || coverIndex >= covers.arraySize)
            {
                error = $"Cover index {coverIndex} is outside room '{room.name}'.";
                return false;
            }

            SerializedProperty cover = covers.GetArrayElementAtIndex(coverIndex);
            SerializedProperty profileProperty =
                cover.FindPropertyRelative("cameraProfile");
            FpgCoverCameraProfile source =
                profileProperty?.objectReferenceValue as FpgCoverCameraProfile;
            if (source == null)
            {
                error = "The selected cover has no camera profile to copy.";
                return false;
            }

            string coverId = cover.FindPropertyRelative("markerId")?.stringValue;
            if (!TryCloneForCover(source, room, coverId, out profile, out error))
            {
                return false;
            }

            Undo.RecordObject(room, "Make Cover Camera Profile Unique");
            roomData.Update();
            covers = roomData.FindProperty("coverSlots");
            profileProperty = covers.GetArrayElementAtIndex(coverIndex)
                .FindPropertyRelative("cameraProfile");
            profileProperty.objectReferenceValue = profile;
            roomData.ApplyModifiedProperties();
            EditorUtility.SetDirty(room);
            return true;
        }

        public static bool TryCopyValues(
            FpgCoverCameraProfile source,
            FpgCoverCameraProfile destination,
            string undoName,
            out string error)
        {
            if (source == null || destination == null)
            {
                error = "Both source and destination camera profiles are required.";
                return false;
            }

            if (!source.TryValidate(out error))
            {
                return false;
            }

            Undo.RecordObject(destination, string.IsNullOrWhiteSpace(undoName)
                ? "Copy Cover Camera Profile"
                : undoName);
            SerializedObject sourceData = new SerializedObject(source);
            SerializedObject destinationData = new SerializedObject(destination);
            string[] properties =
            {
                "designerNotes",
                "cameraRigLocalPosition",
                "cameraRigLocalEulerAngles",
                "cameraLocalPosition",
                "cameraLocalEulerAngles",
                "fieldOfView",
                "nearClipPlane",
                "farClipPlane",
                "playerViewportAnchor",
                "focusViewportAnchor"
            };
            for (int index = 0; index < properties.Length; index++)
            {
                SerializedProperty sourceProperty =
                    sourceData.FindProperty(properties[index]);
                SerializedProperty destinationProperty =
                    destinationData.FindProperty(properties[index]);
                if (sourceProperty == null || destinationProperty == null)
                {
                    error = $"Camera profile property '{properties[index]}' is unavailable.";
                    return false;
                }

                destinationProperty.serializedObject.CopyFromSerializedProperty(
                    sourceProperty);
            }

            destinationData.ApplyModifiedProperties();
            EditorUtility.SetDirty(destination);
            error = string.Empty;
            return true;
        }

        internal static bool TryCloneProfilesForRoomDuplicate(
            FpgRoomDefinition duplicateRoom,
            string duplicateRoomAssetPath,
            List<string> createdAssetPaths,
            out string error)
        {
            error = string.Empty;
            if (duplicateRoom == null || createdAssetPaths == null)
            {
                error = "Room duplication camera-profile inputs are invalid.";
                return false;
            }

            SerializedObject roomData = new SerializedObject(duplicateRoom);
            SerializedProperty covers = roomData.FindProperty("coverSlots");
            if (covers == null || !covers.isArray)
            {
                error = "Duplicate room does not expose cover slots.";
                return false;
            }

            string roomName = Path.GetFileNameWithoutExtension(
                duplicateRoomAssetPath);
            string targetFolder = ResolveProfileFolder(
                duplicateRoomAssetPath,
                roomName);
            Dictionary<FpgCoverCameraProfile, FpgCoverCameraProfile> clones =
                new Dictionary<FpgCoverCameraProfile, FpgCoverCameraProfile>();
            for (int coverIndex = 0; coverIndex < covers.arraySize; coverIndex++)
            {
                SerializedProperty cover = covers.GetArrayElementAtIndex(coverIndex);
                SerializedProperty profileProperty =
                    cover.FindPropertyRelative("cameraProfile");
                FpgCoverCameraProfile source =
                    profileProperty?.objectReferenceValue as FpgCoverCameraProfile;
                if (source == null)
                {
                    error = $"Cover {coverIndex} has no camera profile.";
                    return false;
                }

                if (!clones.TryGetValue(source, out FpgCoverCameraProfile clone))
                {
                    if (EditorUtility.IsDirty(source))
                    {
                        error = $"Save camera profile '{source.name}' before duplicating the room.";
                        return false;
                    }

                    string coverId = cover.FindPropertyRelative("markerId")?.stringValue;
                    if (!TryCloneProfile(
                            source,
                            targetFolder,
                            roomName,
                            coverId,
                            false,
                            out clone,
                            out string clonePath,
                            out error))
                    {
                        return false;
                    }

                    clones.Add(source, clone);
                    createdAssetPaths.Add(clonePath);
                }

                profileProperty.objectReferenceValue = clone;
            }

            roomData.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        internal static void DeleteCreatedProfiles(
            IReadOnlyList<string> createdAssetPaths)
        {
            if (createdAssetPaths == null)
            {
                return;
            }

            for (int index = createdAssetPaths.Count - 1; index >= 0; index--)
            {
                string path = createdAssetPaths[index];
                if (!string.IsNullOrWhiteSpace(path)
                    && AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static bool TryCloneProfile(
            FpgCoverCameraProfile source,
            string targetFolder,
            string roomName,
            string coverId,
            bool registerUndo,
            out FpgCoverCameraProfile clone,
            out string clonePath,
            out string error)
        {
            clone = null;
            clonePath = string.Empty;
            error = string.Empty;
            if (source == null)
            {
                error = "A cover camera template is required.";
                return false;
            }

            if (!source.TryValidate(out error))
            {
                error = $"Camera template '{source.name}' is invalid: {error}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(source)))
            {
                error = "The camera template must be a saved asset.";
                return false;
            }

            if (!TryEnsureFolder(targetFolder, out error))
            {
                return false;
            }

            string fileName = "CAM_" + SanitizeSegment(roomName)
                + "_" + SanitizeSegment(coverId) + ".asset";
            clonePath = AssetDatabase.GenerateUniqueAssetPath(
                targetFolder + "/" + fileName);
            try
            {
                clone = UnityEngine.Object.Instantiate(source);
                clone.name = Path.GetFileNameWithoutExtension(clonePath);
                AssetDatabase.CreateAsset(clone, clonePath);
                if (registerUndo)
                {
                    Undo.RegisterCreatedObjectUndo(
                        clone,
                        "Create Cover Camera Profile");
                }
                AssetDatabase.SaveAssetIfDirty(clone);
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not clone cover camera profile: "
                    + exception.GetBaseException().Message;
                if (!string.IsNullOrWhiteSpace(clonePath)
                    && AssetDatabase.LoadMainAssetAtPath(clonePath) != null)
                {
                    AssetDatabase.DeleteAsset(clonePath);
                }
                else if (clone != null && !AssetDatabase.Contains(clone))
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }

                clone = null;
                clonePath = string.Empty;
                return false;
            }
        }

        private static string ResolveProfileFolder(
            string roomAssetPath,
            string roomName)
        {
            string safeRoomName = SanitizeSegment(roomName);
            return DefaultProfileRoot + "/" + safeRoomName;
        }

        private static bool TryEnsureFolder(string folder, out string error)
        {
            string normalized = (folder ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = $"Camera profile folder '{normalized}' is not Assets-relative.";
                return false;
            }

            string[] segments = normalized.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(
                        current,
                        segments[index]);
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        error = $"Could not create camera profile folder '{next}'.";
                        return false;
                    }
                }

                current = next;
            }

            error = string.Empty;
            return true;
        }

        private static string SanitizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "cover";
            }

            char[] result = value.Trim().Select(character =>
                char.IsLetterOrDigit(character)
                    || character == '-'
                    || character == '_'
                    ? character
                    : '_').ToArray();
            return new string(result);
        }
    }
}
