using System;
using System.Reflection;
using UnityEditor;

namespace FPG.Demo.Editor.LevelAuthoring
{
    internal sealed class FpgGameViewAspectSession : IDisposable
    {
        private const int AspectWidth = 16;
        private const int AspectHeight = 9;

        private const BindingFlags InstanceMemberFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticMemberFlags =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly Type _gameViewType;
        private EditorWindow _gameView;
        private readonly PropertyInfo _selectedSizeIndexProperty;
        private readonly MethodInfo _sizeSelectionCallback;
        private readonly int _previousSelectedSizeIndex;
        private Type _gameViewSizesType;
        private string _previousSizeSignature = string.Empty;


        private bool _restoreCompleted;

        private FpgGameViewAspectSession(
            EditorWindow gameView,
            PropertyInfo selectedSizeIndexProperty,
            MethodInfo sizeSelectionCallback,
            int previousSelectedSizeIndex)
        {
            _gameView = gameView;
            _gameViewType = gameView.GetType();
            _selectedSizeIndexProperty = selectedSizeIndexProperty;
            _sizeSelectionCallback = sizeSelectionCallback;
            _previousSelectedSizeIndex = previousSelectedSizeIndex;
        }

        public string RestoreError { get; private set; } = string.Empty;

        public static bool TryBegin16By9(
            out FpgGameViewAspectSession session,
            out string error)
        {
            session = null;
            error = string.Empty;

            EditorWindow gameView = null;
            PropertyInfo selectedSizeIndexProperty = null;
            MethodInfo sizeSelectionCallback = null;
            int previousSelectedSizeIndex = -1;
            string previousSizeSignature = string.Empty;

            bool previousIndexCaptured = false;

            try
            {
                Assembly editorAssembly = typeof(EditorWindow).Assembly;
                Type gameViewType = editorAssembly.GetType("UnityEditor.GameView", false);
                Type gameViewSizesType = editorAssembly.GetType("UnityEditor.GameViewSizes", false);
                if (gameViewType == null || gameViewSizesType == null)
                {
                    error = "Unity's internal Game View types are unavailable in this editor version.";
                    return false;
                }

                selectedSizeIndexProperty = gameViewType.GetProperty(
                    "selectedSizeIndex",
                    InstanceMemberFlags);
                sizeSelectionCallback = gameViewType.GetMethod(
                    "SizeSelectionCallback",
                    InstanceMemberFlags,
                    null,
                    new[] { typeof(int), typeof(object) },
                    null);
                if (selectedSizeIndexProperty == null ||
                    !selectedSizeIndexProperty.CanRead ||
                    !selectedSizeIndexProperty.CanWrite ||
                    selectedSizeIndexProperty.PropertyType != typeof(int) ||
                    sizeSelectionCallback == null)
                {
                    error = "Unity's internal Game View size-selection API is unavailable.";
                    return false;
                }

                if (!TryFindExisting16By9Index(
                        gameViewSizesType,
                        out int targetSizeIndex,
                        out error))
                {
                    return false;
                }

                gameView = EditorWindow.GetWindow(gameViewType);
                if (gameView == null)
                {
                    error = "Unity could not open the Game View window.";
                    return false;
                }

                gameView.Show();
                gameView.Focus();

                previousSelectedSizeIndex = ReadSelectedSizeIndex(
                    gameView,
                    selectedSizeIndexProperty);
                previousIndexCaptured = true;

                if (!TryReadSizeSignature(
                        gameViewSizesType,
                        previousSelectedSizeIndex,
                        out previousSizeSignature,
                        out error))
                {
                    return false;
                }


                if (!TrySelectSize(
                        gameView,
                        selectedSizeIndexProperty,
                        sizeSelectionCallback,
                        targetSizeIndex,
                        out error))
                {
                    AppendRollbackError(
                        gameView,
                        selectedSizeIndexProperty,
                        sizeSelectionCallback,
                        previousSelectedSizeIndex,
                        ref error);
                    return false;
                }

                session = new FpgGameViewAspectSession(
                    gameView,
                    selectedSizeIndexProperty,
                    sizeSelectionCallback,
                    previousSelectedSizeIndex);
                session._gameViewSizesType = gameViewSizesType;
                session._previousSizeSignature = previousSizeSignature;
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not start the 16:9 Game View session: " +
                        GetExceptionMessage(exception);
                if (previousIndexCaptured)
                {
                    AppendRollbackError(
                        gameView,
                        selectedSizeIndexProperty,
                        sizeSelectionCallback,
                        previousSelectedSizeIndex,
                        ref error);
                }

                return false;
            }
        }

        public bool TryRestore(out string error)
        {
            if (_restoreCompleted)
            {
                error = string.Empty;
                return true;
            }

            EditorWindow gameView = _gameView;
            if (gameView == null)
            {
                try
                {
                    gameView = EditorWindow.GetWindow(_gameViewType);
                    if (gameView == null)
                    {
                        RestoreError =
                            "Unity could not reopen the Game View to restore its previous size.";
                        error = RestoreError;
                        return false;
                    }

                    gameView.Show();
                    _gameView = gameView;
                }
                catch (Exception exception)
                {
                    RestoreError = "Could not reopen the Game View: "
                        + GetExceptionMessage(exception);
                    error = RestoreError;
                    return false;
                }
            }

            if (!TryFindSizeIndexBySignature(
                    _gameViewSizesType,
                    _previousSelectedSizeIndex,
                    _previousSizeSignature,
                    out int restoreSizeIndex,
                    out string resolveError))
            {
                RestoreError = "Could not restore the previous Game View size: "
                    + resolveError;
                error = RestoreError;
                return false;
            }

            if (!TrySelectSize(
                    gameView,
                    _selectedSizeIndexProperty,
                    _sizeSelectionCallback,
                    restoreSizeIndex,
                    out string selectionError))
            {
                RestoreError = "Could not restore the previous Game View size: "
                    + selectionError;
                error = RestoreError;
                return false;
            }

            _restoreCompleted = true;
            RestoreError = string.Empty;
            error = string.Empty;
            return true;
        }

        public void Dispose()
        {
            TryRestore(out _);
        }

        private static bool TryFindExisting16By9Index(
            Type gameViewSizesType,
            out int sizeIndex,
            out string error)
        {
            sizeIndex = -1;
            error = string.Empty;

            PropertyInfo instanceProperty = FindPropertyInHierarchy(
                gameViewSizesType,
                "instance",
                StaticMemberFlags);
            PropertyInfo currentGroupProperty = gameViewSizesType.GetProperty(
                "currentGroup",
                InstanceMemberFlags);
            if (instanceProperty == null ||
                !instanceProperty.CanRead ||
                currentGroupProperty == null ||
                !currentGroupProperty.CanRead)
            {
                error = "Unity's internal Game View size registry is unavailable.";
                return false;
            }

            object gameViewSizes = instanceProperty.GetValue(null, null);
            object currentGroup = gameViewSizes == null
                ? null
                : currentGroupProperty.GetValue(gameViewSizes, null);
            if (currentGroup == null)
            {
                error = "Unity did not provide a Game View size group for the active build target.";
                return false;
            }

            Type groupType = currentGroup.GetType();
            MethodInfo getTotalCountMethod = groupType.GetMethod(
                "GetTotalCount",
                InstanceMemberFlags,
                null,
                Type.EmptyTypes,
                null);
            MethodInfo getGameViewSizeMethod = groupType.GetMethod(
                "GetGameViewSize",
                InstanceMemberFlags,
                null,
                new[] { typeof(int) },
                null);
            if (getTotalCountMethod == null || getGameViewSizeMethod == null)
            {
                error = "Unity's internal Game View size-group API is unavailable.";
                return false;
            }

            Type sizeType = getGameViewSizeMethod.ReturnType;
            PropertyInfo widthProperty = sizeType.GetProperty("width", InstanceMemberFlags);
            PropertyInfo heightProperty = sizeType.GetProperty("height", InstanceMemberFlags);
            PropertyInfo sizeTypeProperty = sizeType.GetProperty("sizeType", InstanceMemberFlags);
            if (widthProperty == null ||
                !widthProperty.CanRead ||
                heightProperty == null ||
                !heightProperty.CanRead)
            {
                error = "Unity's internal Game View size dimensions are unavailable.";
                return false;
            }

            int totalCount = Convert.ToInt32(
                getTotalCountMethod.Invoke(currentGroup, null));
            int fixedResolutionFallback = -1;
            for (int index = 0; index < totalCount; index++)
            {
                object gameViewSize = getGameViewSizeMethod.Invoke(
                    currentGroup,
                    new object[] { index });
                if (gameViewSize == null)
                {
                    continue;
                }

                int width = Convert.ToInt32(widthProperty.GetValue(gameViewSize, null));
                int height = Convert.ToInt32(heightProperty.GetValue(gameViewSize, null));
                if (width <= 0 ||
                    height <= 0 ||
                    (long)width * AspectHeight != (long)height * AspectWidth)
                {
                    continue;
                }

                if (fixedResolutionFallback < 0)
                {
                    fixedResolutionFallback = index;
                }

                object reflectedSizeType = sizeTypeProperty == null
                    ? null
                    : sizeTypeProperty.GetValue(gameViewSize, null);
                if (reflectedSizeType != null &&
                    string.Equals(
                        reflectedSizeType.ToString(),
                        "AspectRatio",
                        StringComparison.Ordinal))
                {
                    sizeIndex = index;
                    return true;
                }
            }

            if (fixedResolutionFallback >= 0)
            {
                sizeIndex = fixedResolutionFallback;
                return true;
            }

            error = "No existing 16:9 Game View size is configured for the active build target.";
            return false;
        }

        private static bool TryReadSizeSignature(
            Type gameViewSizesType,
            int sizeIndex,
            out string signature,
            out string error)
        {
            signature = string.Empty;
            if (!TryGetCurrentSizeGroupApi(
                    gameViewSizesType,
                    out object currentGroup,
                    out MethodInfo getTotalCountMethod,
                    out MethodInfo getGameViewSizeMethod,
                    out error))
            {
                return false;
            }

            int totalCount = Convert.ToInt32(
                getTotalCountMethod.Invoke(currentGroup, null));
            if (sizeIndex < 0 || sizeIndex >= totalCount)
            {
                error = "The selected Game View size index is no longer valid.";
                return false;
            }

            object gameViewSize = getGameViewSizeMethod.Invoke(
                currentGroup,
                new object[] { sizeIndex });
            return TryBuildSizeSignature(gameViewSize, out signature, out error);
        }

        private static bool TryFindSizeIndexBySignature(
            Type gameViewSizesType,
            int preferredIndex,
            string expectedSignature,
            out int sizeIndex,
            out string error)
        {
            sizeIndex = -1;
            error = string.Empty;
            if (gameViewSizesType == null || string.IsNullOrEmpty(expectedSignature))
            {
                error = "The previous Game View size identity was not captured.";
                return false;
            }

            if (!TryGetCurrentSizeGroupApi(
                    gameViewSizesType,
                    out object currentGroup,
                    out MethodInfo getTotalCountMethod,
                    out MethodInfo getGameViewSizeMethod,
                    out error))
            {
                return false;
            }

            int totalCount = Convert.ToInt32(
                getTotalCountMethod.Invoke(currentGroup, null));
            if (preferredIndex >= 0 && preferredIndex < totalCount)
            {
                object preferredSize = getGameViewSizeMethod.Invoke(
                    currentGroup,
                    new object[] { preferredIndex });
                if (TryBuildSizeSignature(
                        preferredSize,
                        out string preferredSignature,
                        out _) &&
                    string.Equals(
                        preferredSignature,
                        expectedSignature,
                        StringComparison.Ordinal))
                {
                    sizeIndex = preferredIndex;
                    return true;
                }
            }

            for (int index = 0; index < totalCount; index++)
            {
                object gameViewSize = getGameViewSizeMethod.Invoke(
                    currentGroup,
                    new object[] { index });
                if (TryBuildSizeSignature(
                        gameViewSize,
                        out string candidateSignature,
                        out _) &&
                    string.Equals(
                        candidateSignature,
                        expectedSignature,
                        StringComparison.Ordinal))
                {
                    sizeIndex = index;
                    return true;
                }
            }

            error = "The previous Game View size is not configured for the active build target.";
            return false;
        }

        private static bool TryGetCurrentSizeGroupApi(
            Type gameViewSizesType,
            out object currentGroup,
            out MethodInfo getTotalCountMethod,
            out MethodInfo getGameViewSizeMethod,
            out string error)
        {
            currentGroup = null;
            getTotalCountMethod = null;
            getGameViewSizeMethod = null;
            error = string.Empty;
            if (gameViewSizesType == null)
            {
                error = "Unity's internal Game View size registry is unavailable.";
                return false;
            }

            PropertyInfo instanceProperty = FindPropertyInHierarchy(
                gameViewSizesType,
                "instance",
                StaticMemberFlags);
            PropertyInfo currentGroupProperty = gameViewSizesType.GetProperty(
                "currentGroup",
                InstanceMemberFlags);
            if (instanceProperty == null ||
                !instanceProperty.CanRead ||
                currentGroupProperty == null ||
                !currentGroupProperty.CanRead)
            {
                error = "Unity's internal Game View size registry is unavailable.";
                return false;
            }

            object gameViewSizes = instanceProperty.GetValue(null, null);
            currentGroup = gameViewSizes == null
                ? null
                : currentGroupProperty.GetValue(gameViewSizes, null);
            if (currentGroup == null)
            {
                error = "Unity did not provide a Game View size group for the active build target.";
                return false;
            }

            Type groupType = currentGroup.GetType();
            getTotalCountMethod = groupType.GetMethod(
                "GetTotalCount",
                InstanceMemberFlags,
                null,
                Type.EmptyTypes,
                null);
            getGameViewSizeMethod = groupType.GetMethod(
                "GetGameViewSize",
                InstanceMemberFlags,
                null,
                new[] { typeof(int) },
                null);
            if (getTotalCountMethod == null || getGameViewSizeMethod == null)
            {
                error = "Unity's internal Game View size-group API is unavailable.";
                return false;
            }

            return true;
        }

        private static bool TryBuildSizeSignature(
            object gameViewSize,
            out string signature,
            out string error)
        {
            signature = string.Empty;
            error = string.Empty;
            if (gameViewSize == null)
            {
                error = "Unity returned an empty Game View size.";
                return false;
            }

            Type sizeType = gameViewSize.GetType();
            PropertyInfo reflectedSizeTypeProperty = sizeType.GetProperty(
                "sizeType",
                InstanceMemberFlags);
            PropertyInfo widthProperty = sizeType.GetProperty(
                "width",
                InstanceMemberFlags);
            PropertyInfo heightProperty = sizeType.GetProperty(
                "height",
                InstanceMemberFlags);
            PropertyInfo nameProperty = sizeType.GetProperty(
                "baseText",
                InstanceMemberFlags) ??
                sizeType.GetProperty("displayText", InstanceMemberFlags);
            if (reflectedSizeTypeProperty == null ||
                !reflectedSizeTypeProperty.CanRead ||
                widthProperty == null ||
                !widthProperty.CanRead ||
                heightProperty == null ||
                !heightProperty.CanRead ||
                nameProperty == null ||
                !nameProperty.CanRead)
            {
                error = "Unity's internal Game View size identity API is unavailable.";
                return false;
            }

            object reflectedSizeType = reflectedSizeTypeProperty.GetValue(
                gameViewSize,
                null);
            int width = Convert.ToInt32(widthProperty.GetValue(gameViewSize, null));
            int height = Convert.ToInt32(heightProperty.GetValue(gameViewSize, null));
            string name = Convert.ToString(nameProperty.GetValue(gameViewSize, null));
            signature = Convert.ToString(reflectedSizeType) + "\u001f" +
                        width + "\u001f" +
                        height + "\u001f" +
                        name;
            return true;
        }


        private static PropertyInfo FindPropertyInHierarchy(
            Type type,
            string name,
            BindingFlags flags)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(
                    name,
                    flags | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static int ReadSelectedSizeIndex(
            EditorWindow gameView,
            PropertyInfo selectedSizeIndexProperty)
        {
            return Convert.ToInt32(
                selectedSizeIndexProperty.GetValue(gameView, null));
        }

        private static bool TrySelectSize(
            EditorWindow gameView,
            PropertyInfo selectedSizeIndexProperty,
            MethodInfo sizeSelectionCallback,
            int sizeIndex,
            out string error)
        {
            error = string.Empty;
            if (gameView == null)
            {
                error = "The Game View window is no longer available.";
                return false;
            }

            try
            {
                if (ReadSelectedSizeIndex(gameView, selectedSizeIndexProperty) != sizeIndex)
                {
                    sizeSelectionCallback.Invoke(
                        gameView,
                        new object[] { sizeIndex, null });
                }

                int selectedSizeIndex = ReadSelectedSizeIndex(
                    gameView,
                    selectedSizeIndexProperty);
                if (selectedSizeIndex != sizeIndex)
                {
                    error = "Unity rejected Game View size index " + sizeIndex + ".";
                    return false;
                }

                gameView.Repaint();
                return true;
            }
            catch (Exception exception)
            {
                error = GetExceptionMessage(exception);
                return false;
            }
        }

        private static void AppendRollbackError(
            EditorWindow gameView,
            PropertyInfo selectedSizeIndexProperty,
            MethodInfo sizeSelectionCallback,
            int previousSelectedSizeIndex,
            ref string error)
        {
            if (gameView == null ||
                selectedSizeIndexProperty == null ||
                sizeSelectionCallback == null)
            {
                return;
            }

            if (!TrySelectSize(
                    gameView,
                    selectedSizeIndexProperty,
                    sizeSelectionCallback,
                    previousSelectedSizeIndex,
                    out string rollbackError))
            {
                error += " Restoring the previous Game View size also failed: " + rollbackError;
            }
        }

        private static string GetExceptionMessage(Exception exception)
        {
            Exception root = exception.GetBaseException();
            return string.IsNullOrEmpty(root.Message)
                ? root.GetType().Name
                : root.Message;
        }
    }
}
