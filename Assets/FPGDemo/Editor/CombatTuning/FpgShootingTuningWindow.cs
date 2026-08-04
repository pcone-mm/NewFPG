using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FPG.Demo.Combat;
using FPG.Demo.Editor.SkillAuthoring;
using FPG.Demo.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FPG.Demo.Editor
{
    public sealed class FpgShootingTuningWindow : EditorWindow
    {
        private const string CatalogGuidSessionKey =
            "FPGDemo.ShootingTuning.CatalogGuid";
        private const string CharacterIdSessionKey =
            "FPGDemo.ShootingTuning.CharacterId";
        private const string DefaultCatalogFilter =
            "t:FpgPlayableCharacterCatalog";
        private const string SnapshotWritebackUndoName =
            "应用射击调参快照到正式资产";
        private const float MaximumPrimarySpreadTangent = 0.5f;

        private static readonly string[] ThreeCWritebackProperties =
        {
            "reticleSafeViewport",
            "mouseReticleSensitivity",
            "mouseReferenceResolution",
            "gamepadReticleSpeed",
            "gamepadReticleDeadzone",
            "gamepadReticleResponseExponent",
            "inputBufferTicks",
            "peekTransitionSeconds",
            "facingFlipDelaySeconds",
            "facingFlipDurationSeconds",
            "retractTransitionSeconds",
            "coverTraversalSeconds",
            "primaryShotCameraKick",
            "secondaryShotCameraKick",
            "shotCameraKickRecoverySeconds"
        };

        private static readonly string[] CombatFeelWritebackProperties =
        {
            "maximumAimDistance",
            "primaryBaseSpreadTangent",
            "secondaryAreaRadius"
        };

        private static readonly string[] WeaponWritebackProperties =
        {
            "magazineCapacity"
        };

        private FpgPlayableCharacterCatalog catalog;
        private FpgPlayableCharacterSelection activeSelection;
        private FpgShootingTuningSnapshot currentSnapshot;
        private FpgShootingTuningSnapshot capturedSnapshot;
        private ScrollView workspaceScroll;
        private Vector2 retainedScrollOffset;
        private int selectedCharacterIndex;
        private bool hasSelection;
        private bool hasCurrentSnapshot;
        private bool hasCapturedSnapshot;
        private DateTime capturedAtLocal;
        private string catalogValidationError = string.Empty;
        private string selectionValidationError = string.Empty;
        private bool showAimAndInput = true;
        private bool showBallistics = true;
        private bool showWeaponTiming = true;
        private bool showCameraFeedback = true;
        private bool showReticlePreview = true;
        private bool showRuntimeDiagnostics = true;
        private bool showSnapshotComparison = true;
        private string snapshotWritebackStatus = string.Empty;
        private MessageType snapshotWritebackMessageType = MessageType.None;
        private string runtimePreviewStatus = string.Empty;
        private MessageType runtimePreviewMessageType = MessageType.None;
        private bool isRebuildingUi;
        private bool uiRefreshScheduled;
        private string initializationError = string.Empty;

        private static readonly Vector2 MinimumWindowSize =
            new Vector2(680f, 640f);

        [MenuItem("FPG Demo/Shooting Tuning", priority = 120)]
        public static void Open()
        {
            FpgShootingTuningWindow[] staleWindows =
                Resources.FindObjectsOfTypeAll<FpgShootingTuningWindow>();
            for (int index = 0; index < staleWindows.Length; index++)
            {
                if (staleWindows[index] != null)
                {
                    staleWindows[index].Close();
                }
            }

            FpgShootingTuningWindow window =
                CreateInstance<FpgShootingTuningWindow>();
            window.titleContent = new GUIContent("射击调参");
            window.minSize = MinimumWindowSize;
            EnsureWindowPosition(window);
            window.ShowUtility();
            window.Focus();
            window.Repaint();
            EditorApplication.delayCall += () =>
            {
                if (window == null)
                {
                    return;
                }

                EnsureWindowPosition(window);
                window.ShowUtility();
                window.Focus();
                window.Repaint();
            };
        }

        private static void EnsureWindowPosition(FpgShootingTuningWindow window)
        {
            if (window == null)
            {
                return;
            }

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            if (main.width <= 0f || main.height <= 0f)
            {
                main = new Rect(80f, 80f, 1280f, 720f);
            }

            Rect current = window.position;
            float width = Mathf.Max(MinimumWindowSize.x, current.width);
            float height = Mathf.Max(MinimumWindowSize.y, current.height);
            float x = current.x;
            float y = current.y;
            if (!IsPositionNearMainEditor(current))
            {
                x = main.x + (main.width - width) * 0.5f;
                y = main.y + (main.height - height) * 0.5f;
            }

            window.position = new Rect(x, y, width, height);
        }

        private static bool IsPositionNearMainEditor(Rect position)
        {
            if (position.width <= 0f || position.height <= 0f)
            {
                return false;
            }

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            if (main.width <= 0f || main.height <= 0f)
            {
                return position.x > -10000f && position.y > -10000f;
            }

            Rect expanded = new Rect(
                main.x - main.width,
                main.y - main.height,
                main.width * 3f,
                main.height * 3f);
            return expanded.Overlaps(position);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            try
            {
                LoadCatalogFromSessionOrProject();
                RestoreCharacterSelection();
                RefreshResolvedSelection();
                initializationError = string.Empty;
            }
            catch (Exception exception)
            {
                initializationError = exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ScheduleUiRefresh();
        }

        public void CreateGUI()
        {
            try
            {
                RebuildUi();
            }
            catch (Exception exception)
            {
                initializationError = exception.Message;
                Debug.LogException(exception, this);
                RenderInitializationError();
            }
        }

        private void ScheduleUiRefresh()
        {
            if (uiRefreshScheduled || rootVisualElement == null)
            {
                return;
            }

            uiRefreshScheduled = true;
            rootVisualElement.schedule.Execute(() =>
            {
                uiRefreshScheduled = false;
                RefreshResolvedSelection();
                RebuildUi();
            });
        }

        private void RebuildUi()
        {
            if (isRebuildingUi)
            {
                return;
            }

            isRebuildingUi = true;
            try
            {
                if (workspaceScroll != null)
                {
                    retainedScrollOffset = workspaceScroll.scrollOffset;
                }

                VisualElement root = rootVisualElement;
                root.Clear();
                root.style.flexGrow = 1f;
                root.style.paddingLeft = 8f;
                root.style.paddingRight = 8f;
                root.style.paddingBottom = 8f;

                if (!string.IsNullOrWhiteSpace(initializationError))
                {
                    root.Add(CreateHelpBox(
                        "Shooting Tuning 初始化失败: " + initializationError,
                        HelpBoxMessageType.Error));
                    workspaceScroll = null;
                    return;
                }

                root.Add(CreateConfigurationHeader());
                if (catalog == null)
                {
                    root.Add(CreateHelpBox(
                        "请选择正式角色目录资产。",
                        HelpBoxMessageType.Info));
                    workspaceScroll = null;
                    return;
                }

                root.Add(CreateValidationPanel());
                if (!hasSelection)
                {
                    workspaceScroll = null;
                    return;
                }

                ScrollView scroll = new ScrollView(ScrollViewMode.Vertical)
                {
                    name = "shooting-tuning-workspace"
                };
                scroll.style.flexGrow = 1f;
                scroll.Add(CreateAuthoritativeAssetsSection());
                scroll.Add(CreateAimAndInputSection());
                scroll.Add(CreateBallisticsSection());
                scroll.Add(CreateWeaponTimingSection());
                scroll.Add(CreateCameraFeedbackSection());
                scroll.Add(CreateReticlePreviewSection());
                scroll.Add(CreateRuntimeDiagnosticsSection());
                scroll.Add(CreateTemporarySnapshotSection());
                root.Add(scroll);
                workspaceScroll = scroll;

                Vector2 restoredOffset = retainedScrollOffset;
                scroll.schedule.Execute(() =>
                {
                    if (scroll.panel != null)
                    {
                        scroll.scrollOffset = restoredOffset;
                    }
                });
            }
            finally
            {
                isRebuildingUi = false;
            }
        }

        private void RenderInitializationError()
        {
            if (rootVisualElement == null)
            {
                return;
            }

            rootVisualElement.Clear();
            rootVisualElement.Add(CreateHelpBox(
                "Shooting Tuning 初始化失败: " + initializationError,
                HelpBoxMessageType.Error));
        }

        private VisualElement CreateConfigurationHeader()
        {
            VisualElement container = new VisualElement
            {
                name = "shooting-configuration-header"
            };
            container.style.marginBottom = 6f;

            Toolbar toolbar = new Toolbar();
            Label title = new Label("射击配置链");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginLeft = 4f;
            toolbar.Add(title);
            container.Add(toolbar);

            VisualElement selectors = new VisualElement();
            selectors.style.paddingTop = 6f;
            selectors.style.paddingLeft = 4f;
            selectors.style.paddingRight = 4f;

            ObjectField catalogField = new ObjectField("角色目录")
            {
                objectType = typeof(FpgPlayableCharacterCatalog),
                allowSceneObjects = false,
                value = catalog
            };
            catalogField.RegisterValueChangedCallback(evt =>
            {
                catalog = evt.newValue as FpgPlayableCharacterCatalog;
                selectedCharacterIndex = 0;
                PersistCatalogSelection();
                PersistCharacterSelection();
                ScheduleUiRefresh();
            });
            selectors.Add(catalogField);

            if (catalog != null && catalog.Count > 0)
            {
                string[] characterLabels = BuildCharacterLabels(
                    catalog.Entries);
                selectedCharacterIndex = Mathf.Clamp(
                    selectedCharacterIndex,
                    0,
                    characterLabels.Length - 1);
                List<string> choices = new List<string>(characterLabels);
                PopupField<string> characterField = new PopupField<string>(
                    "角色",
                    choices,
                    selectedCharacterIndex);
                characterField.RegisterValueChangedCallback(evt =>
                {
                    int nextIndex = choices.IndexOf(evt.newValue);
                    if (nextIndex < 0
                        || nextIndex == selectedCharacterIndex)
                    {
                        return;
                    }

                    selectedCharacterIndex = nextIndex;
                    PersistCharacterSelection();
                    ScheduleUiRefresh();
                });
                selectors.Add(characterField);
            }

            container.Add(selectors);
            return container;
        }

        private VisualElement CreateValidationPanel()
        {
            VisualElement container = new VisualElement();
            container.style.marginBottom = 4f;
            if (!string.IsNullOrWhiteSpace(catalogValidationError))
            {
                container.Add(CreateHelpBox(
                    "角色目录校验失败：" + catalogValidationError,
                    HelpBoxMessageType.Warning));
            }

            if (!string.IsNullOrWhiteSpace(selectionValidationError))
            {
                container.Add(CreateHelpBox(
                    "当前射击配置链校验失败："
                        + selectionValidationError,
                    HelpBoxMessageType.Error));
                return container;
            }

            container.Add(CreateHelpBox(
                "当前角色、3C、战斗手感、武器和主射/换弹技能均已通过校验。",
                HelpBoxMessageType.Info));
            return container;
        }

        private VisualElement CreateAuthoritativeAssetsSection()
        {
            VisualElement section = CreateSectionContainer("权威资产");
            section.Add(CreateReadOnlyAssetRow(
                "角色",
                activeSelection.CharacterDefinition,
                typeof(D0CharacterDefinition)));
            section.Add(CreateReadOnlyAssetRow(
                "3C",
                activeSelection.ThreeCProfile,
                typeof(D0ThreeCProfile)));
            section.Add(CreateReadOnlyAssetRow(
                "战斗手感",
                activeSelection.CombatFeelProfile,
                typeof(D0CombatFeelProfile)));

            D0WeaponDefinition weapon = GetWeapon();
            section.Add(CreateReadOnlyAssetRow(
                "武器",
                weapon,
                typeof(D0WeaponDefinition)));
            section.Add(CreateSkillAssetRow("主射技能", weapon?.PrimarySkill));
            section.Add(CreateSkillAssetRow(
                "当前副射技能",
                hasCurrentSnapshot ? currentSnapshot.SecondarySkill : null));
            section.Add(CreateSkillAssetRow("换弹技能", weapon?.ReloadSkill));
            return section;
        }

        private VisualElement CreateAimAndInputSection()
        {
            Foldout foldout = CreateFoldout(
                "瞄准、输入与掩体节奏",
                showAimAndInput,
                value => showAimAndInput = value);
            foldout.Add(CreateEditableProperties(
                activeSelection.ThreeCProfile,
                "调整射击瞄准参数",
                "reticleSafeViewport", "准星安全视口",
                "mouseReticleSensitivity", "鼠标准星灵敏度",
                "mouseReferenceResolution", "鼠标参考分辨率",
                "gamepadReticleSpeed", "手柄最大视口速度/秒",
                "gamepadReticleDeadzone", "手柄径向死区",
                "gamepadReticleResponseExponent", "手柄响应曲线指数",
                "inputBufferTicks", "攻击输入缓冲（Tick）",
                "peekTransitionSeconds", "\u63a2\u8eab\u8fc7\u6e21\uff08\u79d2\uff09",
                "facingFlipDelaySeconds", "\u8f6c\u5411\u5ef6\u8fdf\uff08\u79d2\uff09",
                "facingFlipDurationSeconds", "\u8f6c\u5411\u8017\u65f6\uff08\u79d2\uff09",
                "retractTransitionSeconds", "缩回过渡（秒）",
                "coverTraversalSeconds", "掩体移动（秒）"));

            if (hasCurrentSnapshot)
            {
                AddMetric(
                    foldout,
                    "输入缓冲（秒）",
                    currentSnapshot.InputBufferSeconds.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture));
            }

            return foldout;
        }

        private VisualElement CreateBallisticsSection()
        {
            Foldout foldout = CreateFoldout(
                "弹道与散布",
                showBallistics,
                value => showBallistics = value);
            foldout.Add(CreateEditableProperties(
                activeSelection.CombatFeelProfile,
                "调整射击弹道参数",
                "maximumAimDistance", "最大瞄准/查询距离",
                "secondaryAreaRadius", "副射范围半径"));
            foldout.Add(CreatePrimarySpreadHalfAngleEditor(
                activeSelection.CombatFeelProfile));

            if (hasCurrentSnapshot)
            {
                foldout.Add(CreateSpreadRadiusTable(currentSnapshot));
            }

            return foldout;
        }

        private VisualElement CreateWeaponTimingSection()
        {
            Foldout foldout = CreateFoldout(
                "弹药与射击时序",
                showWeaponTiming,
                value => showWeaponTiming = value);
            D0WeaponDefinition weapon = GetWeapon();
            foldout.Add(CreateEditableProperties(
                weapon,
                "调整武器弹匣参数",
                "magazineCapacity", "弹匣容量"));

            if (hasCurrentSnapshot)
            {
                AddMetric(
                    foldout,
                    "主射 Pellet 数",
                    currentSnapshot.PrimaryPelletCount.ToString(
                        CultureInfo.InvariantCulture));
                AddMetric(
                    foldout,
                    "主射伤害摘要（单 Pellet）",
                    FormatDamageSummary(currentSnapshot.PrimaryDamage));
                AddMetric(
                    foldout,
                    "主射完整弹耗",
                    currentSnapshot.PrimaryAmmoCost + " 发");
                AddTickMetric(
                    foldout,
                    "主射动作锁",
                    currentSnapshot.PrimaryActionLockTicks,
                    currentSnapshot.PrimaryActionLockSeconds);
                AddMetric(
                    foldout,
                    "主射技能冷却",
                    currentSnapshot.PrimaryCooldownTicks + " Tick");
                AddMetric(
                    foldout,
                    "主射攻击提交",
                    "Tick " + currentSnapshot.PrimaryAttackCommitTick);
                AddMetric(
                    foldout,
                    "每秒射击数",
                    currentSnapshot.PrimaryShotsPerSecond.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture));
                AddMetric(
                    foldout,
                    "射速（RPM）",
                    currentSnapshot.PrimaryRoundsPerMinute.ToString(
                        "0",
                        CultureInfo.InvariantCulture));
                AddMetric(
                    foldout,
                    "副射伤害摘要",
                    FormatDamageSummary(currentSnapshot.SecondaryDamage));
                AddMetric(
                    foldout,
                    "副射完整弹耗",
                    currentSnapshot.SecondaryAmmoCost + " 发");
                AddTickMetric(
                    foldout,
                    "副射动作锁",
                    currentSnapshot.SecondaryActionLockTicks,
                    currentSnapshot.SecondaryActionLockSeconds);
                AddMetric(
                    foldout,
                    "副射技能冷却",
                    currentSnapshot.SecondaryCooldownTicks + " Tick");
                AddMetric(
                    foldout,
                    "副射攻击提交",
                    "Tick " + currentSnapshot.SecondaryAttackCommitTick);
                AddMetric(
                    foldout,
                    "换弹提交",
                    "Tick " + currentSnapshot.ReloadCommitTick);
                AddTickMetric(
                    foldout,
                    "换弹时间轴",
                    currentSnapshot.ReloadDurationTicks,
                    currentSnapshot.ReloadDurationSeconds);
                AddTickMetric(
                    foldout,
                    "换弹锁定",
                    currentSnapshot.ReloadLockTicks,
                    currentSnapshot.ReloadLockSeconds);
            }

            VisualElement commands = CreateHorizontalRow();
            Button openPrimary = CreateCommandButton(
                "在技能编辑器中打开主射",
                () => FpgSkillEditorWindow.OpenAsset(weapon.PrimarySkill));
            openPrimary.SetEnabled(weapon != null && weapon.PrimarySkill != null);
            commands.Add(openPrimary);
            FpgPlayerSkillDefinition secondarySkill = hasCurrentSnapshot
                ? currentSnapshot.SecondarySkill
                : null;
            Button openSecondary = CreateCommandButton(
                "在技能编辑器中打开副射",
                () => FpgSkillEditorWindow.OpenAsset(secondarySkill));
            openSecondary.SetEnabled(secondarySkill != null);
            commands.Add(openSecondary);
            Button openReload = CreateCommandButton(
                "在技能编辑器中打开换弹",
                () => FpgSkillEditorWindow.OpenAsset(weapon.ReloadSkill));
            openReload.SetEnabled(weapon != null && weapon.ReloadSkill != null);
            commands.Add(openReload);
            foldout.Add(commands);
            return foldout;
        }

        private VisualElement CreateCameraFeedbackSection()
        {
            Foldout foldout = CreateFoldout(
                "射击镜头反馈",
                showCameraFeedback,
                value => showCameraFeedback = value);
            foldout.Add(CreateEditableProperties(
                activeSelection.ThreeCProfile,
                "调整射击镜头反馈",
                "primaryShotCameraKick", "主射镜头后坐",
                "secondaryShotCameraKick", "副射镜头后坐",
                "shotCameraKickRecoverySeconds", "镜头恢复（秒）"));
            return foldout;
        }

        private VisualElement CreateReticlePreviewSection()
        {
            Foldout foldout = CreateFoldout(
                "准星样式与状态预览",
                showReticlePreview,
                value => showReticlePreview = value);
            D0WeaponDefinition weapon = GetWeapon();
            foldout.Add(CreateEditableProperties(
                weapon,
                "调整准星样式",
                "aimIndicator", "武器准星样式"));

            PlayerAimIndicatorPresentationDefinition style =
                weapon == null ? null : weapon.AimIndicator;
            string styleError = string.Empty;
            if (style == null || !style.TryValidate(out styleError))
            {
                foldout.Add(CreateHelpBox(
                    string.IsNullOrWhiteSpace(styleError)
                        ? "当前武器缺少有效的准星样式。"
                        : "准星样式校验失败：" + styleError,
                    HelpBoxMessageType.Error));
                return foldout;
            }

            IMGUIContainer preview = new IMGUIContainer(() =>
                DrawReticleStatePreviews(style));
            preview.name = "shooting-reticle-state-preview";
            preview.style.height = 292f;
            preview.style.marginTop = 4f;
            foldout.Add(preview);
            return foldout;
        }

        private VisualElement CreateRuntimeDiagnosticsSection()
        {
            Foldout foldout = CreateFoldout(
                "运行时诊断与预览",
                showRuntimeDiagnostics,
                value => showRuntimeDiagnostics = value);
            IFpgShootingTuningPreviewHost host =
                FpgShootingTuningRuntimeRegistry.Current;
            bool canUseRuntime = EditorApplication.isPlaying && host != null;

            VisualElement commands = CreateHorizontalRow();
            Button applyLive = CreateCommandButton(
                "即时应用输入与准星预览",
                () => ApplyRuntimePreview(rebuildCombat: false));
            applyLive.tooltip = "把当前工作台资产值应用到运行中的输入、镜头与准星表现。";
            applyLive.SetEnabled(canUseRuntime && hasCurrentSnapshot);
            commands.Add(applyLive);

            Button rebuild = CreateCommandButton(
                "应用预览并重建战斗",
                () => ApplyRuntimePreview(rebuildCombat: true));
            rebuild.tooltip = "完整预检后，用当前工作台资产值原子重建正式战斗。";
            rebuild.SetEnabled(canUseRuntime && hasCurrentSnapshot);
            commands.Add(rebuild);
            foldout.Add(commands);

            Button restore = CreateCommandButton(
                "恢复当前正式资产值",
                RestoreRuntimeFromAuthoritativeAssets);
            restore.tooltip = "重新捕获当前角色的正式资产，并原子恢复运行中的战斗配置。";
            restore.SetEnabled(canUseRuntime && hasSelection);
            foldout.Add(restore);

            if (!string.IsNullOrWhiteSpace(runtimePreviewStatus))
            {
                foldout.Add(CreateHelpBox(
                    runtimePreviewStatus,
                    ToHelpBoxMessageType(runtimePreviewMessageType)));
            }

            if (!EditorApplication.isPlaying)
            {
                foldout.Add(CreateHelpBox(
                    "进入 Play Mode 后可读取权威瞄准、攻击门禁、换弹与探身诊断。",
                    HelpBoxMessageType.None));
                return foldout;
            }

            if (host == null)
            {
                foldout.Add(CreateHelpBox(
                    "当前没有已注册的正式射击调参 Host。",
                    HelpBoxMessageType.Warning));
                return foldout;
            }

            IMGUIContainer diagnostics = new IMGUIContainer(
                DrawRuntimeDiagnostics);
            diagnostics.name = "shooting-runtime-diagnostics";
            diagnostics.style.minHeight = 330f;
            diagnostics.style.marginTop = 4f;
            diagnostics.schedule.Execute(
                diagnostics.MarkDirtyRepaint).Every(100);
            foldout.Add(diagnostics);
            return foldout;
        }

        private void ApplyRuntimePreview(bool rebuildCombat)
        {
            IFpgShootingTuningPreviewHost host =
                FpgShootingTuningRuntimeRegistry.Current;
            if (!EditorApplication.isPlaying || host == null)
            {
                SetRuntimePreviewStatus(
                    "当前 Play Mode 没有可用的射击调参 Host。",
                    MessageType.Warning);
                return;
            }

            string error = string.Empty;
            if (!hasCurrentSnapshot
                || !currentSnapshot.TryValidate(out error))
            {
                SetRuntimePreviewStatus(
                    "当前正式资产无法生成有效预览：" + error,
                    MessageType.Error);
                return;
            }

            bool succeeded = rebuildCombat
                ? host.TryApplyShootingPreviewAndRebuild(
                    currentSnapshot,
                    out error)
                : host.TryApplyShootingLivePreview(
                    currentSnapshot,
                    out error);
            SetRuntimePreviewStatus(
                succeeded
                    ? rebuildCombat
                        ? "已完成原子预检并重建战斗。"
                        : "已即时应用输入、镜头与准星表现。"
                    : "运行时预览失败：" + error,
                succeeded ? MessageType.Info : MessageType.Error);
        }

        private void RestoreRuntimeFromAuthoritativeAssets()
        {
            RefreshResolvedSelection();
            ApplyRuntimePreview(rebuildCombat: true);
        }

        private void SetRuntimePreviewStatus(
            string message,
            MessageType messageType)
        {
            runtimePreviewStatus = message ?? string.Empty;
            runtimePreviewMessageType = messageType;
            ScheduleUiRefresh();
        }

        private void DrawRuntimeDiagnostics()
        {
            IFpgShootingTuningPreviewHost host =
                FpgShootingTuningRuntimeRegistry.Current;
            string error = string.Empty;
            if (host == null
                || !host.TryGetShootingDiagnostics(
                    out FpgShootingDiagnosticsSnapshot snapshot,
                    out error))
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(error)
                        ? "射击诊断暂不可用。"
                        : error,
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField(
                "Tick / 弹药",
                snapshot.Tick + " / " + snapshot.Ammo + "/"
                    + snapshot.MagazineCapacity);
            EditorGUILayout.LabelField(
                "武器 / 准星 / 探身",
                snapshot.WeaponState + " / " + snapshot.ReticleState
                    + " / " + snapshot.ExposureState);
            EditorGUILayout.LabelField(
                "换弹 / 探身请求",
                snapshot.ReloadProgress01.ToString(
                    "P0",
                    CultureInfo.InvariantCulture)
                    + " / " + snapshot.IsCoverPeekRequested
                    + " (Tick " + snapshot.CoverPeekStartedTick + ")");
            EditorGUILayout.LabelField(
                "Aim 版本（实时 / 使用 / 冻结）",
                snapshot.LiveAimVersion + " / "
                    + snapshot.ResolvedAimVersion + " / "
                    + snapshot.FrozenAimVersion);
            EditorGUILayout.LabelField(
                "准星视口",
                FormatVector2(snapshot.ReticleViewport));
            EditorGUILayout.LabelField(
                "相机射线起点",
                FormatVector3(snapshot.CameraRayOrigin));
            EditorGUILayout.LabelField(
                "相机射线方向",
                FormatVector3(snapshot.CameraRayDirection));
            EditorGUILayout.LabelField(
                "ShotOrigin",
                FormatVector3(snapshot.ShotOrigin));
            EditorGUILayout.LabelField(
                "中心方向",
                FormatVector3(snapshot.CenterDirection));
            EditorGUILayout.LabelField(
                "目标点 / 距离",
                FormatVector3(snapshot.SurfacePoint) + " / "
                    + snapshot.AimDistance.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                    + " m");
            EditorGUILayout.LabelField(
                "目标 / 部位",
                snapshot.TargetType + " / " + snapshot.TargetKind
                    + " / " + snapshot.HitPart);
            EditorGUILayout.LabelField(
                "TargetId / GeometryId",
                snapshot.TargetId + " / " + snapshot.GeometryId);
            EditorGUILayout.LabelField(
                "目标掩体 / 当前掩体",
                EmptyAsDash(snapshot.TargetCoverId) + " / "
                    + EmptyAsDash(snapshot.CurrentCoverId)
                    + (snapshot.IsCurrentCoverBlocked ? "（已阻挡）" : string.Empty));
            EditorGUILayout.LabelField(
                "主射许可",
                FormatAvailability(snapshot.PrimaryAttackAvailability));
            EditorGUILayout.LabelField(
                "副射许可",
                FormatAvailability(snapshot.SecondaryAttackAvailability));
            EditorGUILayout.LabelField(
                "Pellet 锥",
                snapshot.PelletCount + " 条 / 半角 "
                    + snapshot.PelletConeHalfAngleDegrees.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture)
                    + " 度 / 当前半径 "
                    + snapshot.PelletConeRadiusAtAimDistance.ToString(
                        "0.000",
                        CultureInfo.InvariantCulture)
                    + " m");
        }

        private static void DrawReticleStatePreviews(
            PlayerAimIndicatorPresentationDefinition style)
        {
            Rect area = GUILayoutUtility.GetRect(
                100f,
                284f,
                GUILayout.ExpandWidth(true));
            const int columns = 4;
            const int rows = 2;
            const float gap = 4f;
            float tileWidth = (area.width - gap * (columns - 1)) / columns;
            float tileHeight = (area.height - gap * (rows - 1)) / rows;
            string[] labels =
            {
                "正常",
                "敌人",
                "不可攻击",
                "当前掩体阻挡",
                "换弹 65%",
                "射击",
                "射击 + 命中",
                "副射蓄力"
            };
            FpgAimIndicatorBaseState[] states =
            {
                FpgAimIndicatorBaseState.Normal,
                FpgAimIndicatorBaseState.Enemy,
                FpgAimIndicatorBaseState.Unavailable,
                FpgAimIndicatorBaseState.CurrentCoverBlocked,
                FpgAimIndicatorBaseState.Reloading,
                FpgAimIndicatorBaseState.Normal,
                FpgAimIndicatorBaseState.Enemy,
                FpgAimIndicatorBaseState.Normal
            };

            Handles.BeginGUI();
            try
            {
                for (int index = 0; index < labels.Length; index++)
                {
                    int row = index / columns;
                    int column = index % columns;
                    Rect tile = new Rect(
                        area.x + column * (tileWidth + gap),
                        area.y + row * (tileHeight + gap),
                        tileWidth,
                        tileHeight);
                    DrawReticlePreviewTile(
                        tile,
                        labels[index],
                        states[index],
                        style,
                        showShot: index == 5 || index == 6,
                        showHit: index == 6,
                        showSecondary: index == 7);
                }
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private static void DrawReticlePreviewTile(
            Rect tile,
            string label,
            FpgAimIndicatorBaseState state,
            PlayerAimIndicatorPresentationDefinition style,
            bool showShot,
            bool showHit,
            bool showSecondary)
        {
            EditorGUI.DrawRect(
                tile,
                EditorGUIUtility.isProSkin
                    ? new Color(0.12f, 0.12f, 0.12f, 1f)
                    : new Color(0.82f, 0.82f, 0.82f, 1f));
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter
            };
            GUI.Label(
                new Rect(tile.x + 2f, tile.y + 3f, tile.width - 4f, 18f),
                label,
                labelStyle);

            Vector2 center = new Vector2(
                tile.center.x,
                tile.center.y + 8f);
            DrawPreviewRing(
                center,
                Mathf.Min(38f, tile.width * 0.32f),
                1f,
                new Color(1f, 1f, 1f, 0.14f));

            Color baseColor = ResolvePreviewBaseColor(state, style);
            switch (state)
            {
                case FpgAimIndicatorBaseState.CurrentCoverBlocked:
                    DrawPreviewRing(
                        center,
                        style.ProhibitedRadius,
                        style.ProhibitedThickness,
                        baseColor);
                    Handles.color = baseColor;
                    Vector2 diagonal = Vector2.one.normalized
                        * style.ProhibitedRadius * 0.72f;
                    Handles.DrawAAPolyLine(
                        style.ProhibitedThickness,
                        center - diagonal,
                        center + diagonal);
                    break;

                case FpgAimIndicatorBaseState.Reloading:
                    DrawPreviewRing(
                        center,
                        style.ReloadRadius,
                        style.ReloadThickness,
                        ColorWithAlpha(baseColor, baseColor.a * 0.22f));
                    Handles.color = baseColor;
                    Handles.DrawWireArc(
                        center,
                        Vector3.forward,
                        Vector3.up,
                        234f,
                        style.ReloadRadius,
                        style.ReloadThickness);
                    break;

                default:
                    DrawPreviewCrosshair(center, baseColor, style);
                    break;
            }

            if (showSecondary)
            {
                DrawPreviewRing(
                    center,
                    Mathf.Min(45f, tile.width * 0.38f),
                    style.SecondaryRangeThickness,
                    style.SecondaryRangeColor);
                Handles.color = style.SecondaryRangeColor;
                Handles.DrawWireArc(
                    center,
                    Vector3.forward,
                    Vector3.up,
                    270f,
                    Mathf.Min(45f, tile.width * 0.38f),
                    style.SecondaryRangeThickness + 1f);
            }

            if (showShot)
            {
                DrawPreviewRing(
                    center,
                    Mathf.Lerp(style.BaseRadius, style.ShotRadius, 0.72f),
                    style.RingThickness,
                    ColorWithAlpha(style.ShotColor, 0.88f));
            }

            if (showHit)
            {
                Handles.color = style.HitColor;
                for (int index = 0; index < 4; index++)
                {
                    float radians = (45f + index * 90f) * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(
                        Mathf.Cos(radians),
                        Mathf.Sin(radians));
                    Handles.DrawAAPolyLine(
                        style.HitMarkerThickness,
                        center + direction * style.HitMarkerRadius,
                        center + direction
                            * (style.HitMarkerRadius
                                + Mathf.Max(5f, style.HitExpansion)));
                }
            }
        }

        private static void DrawPreviewCrosshair(
            Vector2 center,
            Color color,
            PlayerAimIndicatorPresentationDefinition style)
        {
            float gap = style.CrosshairGap;
            float arm = style.CrosshairArmLength;
            float thickness = style.CrosshairThickness;
            EditorGUI.DrawRect(
                new Rect(
                    center.x - gap - arm,
                    center.y - thickness * 0.5f,
                    arm,
                    thickness),
                color);
            EditorGUI.DrawRect(
                new Rect(
                    center.x + gap,
                    center.y - thickness * 0.5f,
                    arm,
                    thickness),
                color);
            EditorGUI.DrawRect(
                new Rect(
                    center.x - thickness * 0.5f,
                    center.y - gap - arm,
                    thickness,
                    arm),
                color);
            EditorGUI.DrawRect(
                new Rect(
                    center.x - thickness * 0.5f,
                    center.y + gap,
                    thickness,
                    arm),
                color);
        }

        private static void DrawPreviewRing(
            Vector2 center,
            float radius,
            float thickness,
            Color color)
        {
            Handles.color = color;
            Handles.DrawWireDisc(
                center,
                Vector3.forward,
                Mathf.Max(1f, radius),
                Mathf.Max(0.5f, thickness));
        }

        private static Color ResolvePreviewBaseColor(
            FpgAimIndicatorBaseState state,
            PlayerAimIndicatorPresentationDefinition style)
        {
            switch (state)
            {
                case FpgAimIndicatorBaseState.Enemy:
                    return style.EnemyColor;
                case FpgAimIndicatorBaseState.Unavailable:
                    return style.UnavailableColor;
                case FpgAimIndicatorBaseState.CurrentCoverBlocked:
                    return style.CurrentCoverBlockedColor;
                case FpgAimIndicatorBaseState.Reloading:
                    return style.ReloadColor;
                default:
                    return style.NormalColor;
            }
        }

        private static Color ColorWithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private VisualElement CreateTemporarySnapshotSection()
        {
            VisualElement section = CreateSectionContainer("临时调参快照");
            VisualElement captureCommands = CreateHorizontalRow();
            Button captureAssets = CreateCommandButton(
                "捕获当前正式资产值",
                () =>
                {
                    CaptureSnapshot(currentSnapshot);
                    ScheduleUiRefresh();
                });
            captureAssets.SetEnabled(hasCurrentSnapshot);
            captureCommands.Add(captureAssets);

            Button captureRuntime = CreateCommandButton(
                "读取游戏内临时值",
                () =>
                {
                    TryCaptureRuntimeSnapshot();
                    ScheduleUiRefresh();
                });
            captureRuntime.SetEnabled(
                EditorApplication.isPlaying
                && FpgShootingTuningRuntimeRegistry.Current != null);
            captureCommands.Add(captureRuntime);
            section.Add(captureCommands);

            if (!hasCapturedSnapshot)
            {
                if (!string.IsNullOrWhiteSpace(snapshotWritebackStatus))
                {
                    section.Add(CreateHelpBox(
                        snapshotWritebackStatus,
                        ToHelpBoxMessageType(snapshotWritebackMessageType)));
                }

                section.Add(CreateHelpBox(
                    "尚未捕获快照。可捕获当前正式资产值，或在 Play Mode 读取游戏内临时值。",
                    HelpBoxMessageType.None));
                return section;
            }

            bool snapshotMatchesSelection = hasSelection
                && capturedSnapshot.MatchesSelection(activeSelection)
                && ReferenceEquals(capturedSnapshot.Weapon, GetWeapon());
            if (!snapshotMatchesSelection)
            {
                section.Add(CreateHelpBox(
                    "捕获快照不属于当前角色配置链。切回对应角色后才能写回正式资产。",
                    HelpBoxMessageType.Warning));
            }

            Button applyToAssets = CreateCommandButton(
                "应用到正式资产",
                () =>
                {
                    if (TryApplySnapshotToAuthoritativeAssets(
                            capturedSnapshot,
                            out string error))
                    {
                        snapshotWritebackStatus =
                            "已在一个 Undo 操作中写入全部工作台权威字段。技能时间轴仍由技能编辑器管理。";
                        snapshotWritebackMessageType = MessageType.Info;
                    }
                    else
                    {
                        snapshotWritebackStatus = "写回失败：" + error;
                        snapshotWritebackMessageType = MessageType.Error;
                    }

                    ScheduleUiRefresh();
                });
            applyToAssets.SetEnabled(
                hasCurrentSnapshot && snapshotMatchesSelection);
            section.Add(applyToAssets);

            if (!string.IsNullOrWhiteSpace(snapshotWritebackStatus))
            {
                section.Add(CreateHelpBox(
                    snapshotWritebackStatus,
                    ToHelpBoxMessageType(snapshotWritebackMessageType)));
            }

            Foldout comparison = CreateFoldout(
                "与 "
                    + capturedAtLocal.ToString(
                        "HH:mm:ss",
                        CultureInfo.InvariantCulture)
                    + " 的快照对比",
                showSnapshotComparison,
                value => showSnapshotComparison = value);
            if (hasCurrentSnapshot)
            {
                AddMetric(
                    comparison,
                    "准星安全视口",
                    FormatDelta(
                        capturedSnapshot.ReticleSafeViewport,
                        currentSnapshot.ReticleSafeViewport));
                AddMetric(
                    comparison,
                    "鼠标准星灵敏度",
                    FormatDelta(
                        capturedSnapshot.MouseReticleSensitivity,
                        currentSnapshot.MouseReticleSensitivity));
                AddMetric(
                    comparison,
                    "鼠标参考分辨率",
                    FormatDelta(
                        capturedSnapshot.MouseReferenceResolution,
                        currentSnapshot.MouseReferenceResolution));
                AddMetric(
                    comparison,
                    "手柄最大视口速度/秒",
                    FormatDelta(
                        capturedSnapshot.GamepadReticleSpeed,
                        currentSnapshot.GamepadReticleSpeed));
                AddMetric(
                    comparison,
                    "手柄径向死区",
                    FormatDelta(
                        capturedSnapshot.GamepadReticleDeadzone,
                        currentSnapshot.GamepadReticleDeadzone));
                AddMetric(
                    comparison,
                    "手柄响应曲线指数",
                    FormatDelta(
                        capturedSnapshot.GamepadReticleResponseExponent,
                        currentSnapshot.GamepadReticleResponseExponent));
                AddMetric(
                    comparison,
                    "最大瞄准距离",
                    FormatDelta(
                        capturedSnapshot.MaximumAimDistance,
                        currentSnapshot.MaximumAimDistance));
                AddMetric(
                    comparison,
                    "输入缓冲（Tick）",
                    FormatDelta(
                        capturedSnapshot.InputBufferTicks,
                        currentSnapshot.InputBufferTicks));
                AddMetric(
                    comparison,
                    "主射散布半角（度）",
                    FormatDelta(
                        capturedSnapshot.PrimarySpreadHalfAngleDegrees,
                        currentSnapshot.PrimarySpreadHalfAngleDegrees));
                AddMetric(
                    comparison,
                    "弹匣容量",
                    FormatDelta(
                        capturedSnapshot.MagazineCapacity,
                        currentSnapshot.MagazineCapacity));
                AddMetric(
                    comparison,
                    "主射间隔（Tick）",
                    FormatDelta(
                        capturedSnapshot.PrimaryIntervalTicks,
                        currentSnapshot.PrimaryIntervalTicks));
                AddMetric(
                    comparison,
                    "换弹时长（Tick）",
                    FormatDelta(
                        capturedSnapshot.ReloadDurationTicks,
                        currentSnapshot.ReloadDurationTicks));
                AddMetric(
                    comparison,
                    "主射镜头后坐",
                    FormatDelta(
                        capturedSnapshot.PrimaryCameraKick,
                        currentSnapshot.PrimaryCameraKick));
            }

            section.Add(comparison);
            return section;
        }

        private void CaptureSnapshot(FpgShootingTuningSnapshot snapshot)
        {
            capturedSnapshot = snapshot;
            capturedAtLocal = DateTime.Now;
            hasCapturedSnapshot = true;
            snapshotWritebackStatus = string.Empty;
            snapshotWritebackMessageType = MessageType.None;
        }

        private void TryCaptureRuntimeSnapshot()
        {
            IFpgShootingTuningPreviewHost host =
                FpgShootingTuningRuntimeRegistry.Current;
            if (host == null)
            {
                snapshotWritebackStatus =
                    "当前 Play Mode 没有可用的射击调参 Host。";
                snapshotWritebackMessageType = MessageType.Warning;
                return;
            }

            if (!host.TryGetShootingTuning(
                    out FpgShootingTuningSnapshot snapshot,
                    out string error))
            {
                snapshotWritebackStatus = "读取游戏内临时值失败：" + error;
                snapshotWritebackMessageType = MessageType.Error;
                return;
            }

            if (!snapshot.TryValidate(out error))
            {
                snapshotWritebackStatus = "游戏内临时值无效：" + error;
                snapshotWritebackMessageType = MessageType.Error;
                return;
            }

            CaptureSnapshot(snapshot);
            snapshotWritebackStatus = "已读取游戏内临时值，尚未写入正式资产。";
            snapshotWritebackMessageType = MessageType.Info;
        }

        private bool TryApplySnapshotToAuthoritativeAssets(
            FpgShootingTuningSnapshot snapshot,
            out string error)
        {
            if (!snapshot.TryValidate(out error))
            {
                error = "临时快照未通过完整校验：" + error;
                return false;
            }

            if (!hasSelection
                || !snapshot.MatchesSelection(activeSelection)
                || !ReferenceEquals(snapshot.Weapon, GetWeapon()))
            {
                error = "临时快照与当前角色配置链不一致。";
                return false;
            }

            if (!TryPrepareWritebackTarget(
                    snapshot.ThreeCProfile,
                    ThreeCWritebackProperties,
                    out error)
                || !TryPrepareWritebackTarget(
                    snapshot.CombatFeelProfile,
                    CombatFeelWritebackProperties,
                    out error)
                || !TryPrepareWritebackTarget(
                    snapshot.Weapon,
                    WeaponWritebackProperties,
                    out error))
            {
                return false;
            }

            UnityEngine.Object[] targets =
            {
                snapshot.ThreeCProfile,
                snapshot.CombatFeelProfile,
                snapshot.Weapon
            };
            int undoGroup = -1;
            bool undoRegistered = false;
            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(SnapshotWritebackUndoName);
                Undo.RegisterCompleteObjectUndo(
                    targets,
                    SnapshotWritebackUndoName);
                undoRegistered = true;

                ApplyThreeCSnapshot(snapshot);
                ApplyCombatFeelSnapshot(snapshot);
                ApplyWeaponSnapshot(snapshot);

                if (!FpgShootingTuningSnapshot.TryCapture(
                        activeSelection,
                        out FpgShootingTuningSnapshot appliedSnapshot,
                        out string validationError))
                {
                    throw new InvalidOperationException(
                        "写入后的完整配置链校验失败：" + validationError);
                }

                if (!WritableFieldsMatch(snapshot, appliedSnapshot))
                {
                    throw new InvalidOperationException(
                        "写入后的正式资产值与临时快照不一致。");
                }

                for (int index = 0; index < targets.Length; index++)
                {
                    EditorUtility.SetDirty(targets[index]);
                }

                Undo.CollapseUndoOperations(undoGroup);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetBaseException().Message;
                if (undoRegistered)
                {
                    try
                    {
                        Undo.RevertAllDownToGroup(undoGroup);
                    }
                    catch (Exception rollbackException)
                    {
                        error += " Undo 回滚也失败："
                            + rollbackException.GetBaseException().Message;
                    }
                }

                return false;
            }
        }

        private static bool TryPrepareWritebackTarget(
            UnityEngine.Object target,
            IReadOnlyList<string> requiredProperties,
            out string error)
        {
            if (target == null)
            {
                error = "写回目标资产为空。";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(target);
            if (!EditorUtility.IsPersistent(target)
                || string.IsNullOrWhiteSpace(assetPath))
            {
                error = "写回目标不是正式资产：" + target.name;
                return false;
            }

            if (!AssetDatabase.IsOpenForEdit(
                    target,
                    out string editError,
                    StatusQueryOptions.UseCachedIfPossible))
            {
                error = target.name + " 当前不可编辑：" + editError;
                return false;
            }

            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.UpdateIfRequiredOrScript();
            for (int index = 0; index < requiredProperties.Count; index++)
            {
                string propertyName = requiredProperties[index];
                if (serializedTarget.FindProperty(propertyName) == null)
                {
                    error = target.name + " 缺少写回字段：" + propertyName;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static void ApplyThreeCSnapshot(
            FpgShootingTuningSnapshot snapshot)
        {
            SerializedObject serialized = new SerializedObject(
                snapshot.ThreeCProfile);
            serialized.UpdateIfRequiredOrScript();
            serialized.FindProperty("reticleSafeViewport").rectValue =
                snapshot.ReticleSafeViewport;
            serialized.FindProperty("mouseReticleSensitivity").floatValue =
                snapshot.MouseReticleSensitivity;
            serialized.FindProperty("mouseReferenceResolution").vector2Value =
                snapshot.MouseReferenceResolution;
            serialized.FindProperty("gamepadReticleSpeed").floatValue =
                snapshot.GamepadReticleSpeed;
            serialized.FindProperty("gamepadReticleDeadzone").floatValue =
                snapshot.GamepadReticleDeadzone;
            serialized.FindProperty(
                    "gamepadReticleResponseExponent").floatValue =
                snapshot.GamepadReticleResponseExponent;
            serialized.FindProperty("inputBufferTicks").intValue =
                snapshot.InputBufferTicks;
            serialized.FindProperty("peekTransitionSeconds").floatValue =
                snapshot.PeekTransitionSeconds;
            serialized.FindProperty("facingFlipDelaySeconds").floatValue =
                snapshot.FacingFlipDelaySeconds;
            serialized.FindProperty("facingFlipDurationSeconds").floatValue =
                snapshot.FacingFlipDurationSeconds;
            serialized.FindProperty("retractTransitionSeconds").floatValue =
                snapshot.RetractTransitionSeconds;
            serialized.FindProperty("coverTraversalSeconds").floatValue =
                snapshot.CoverTraversalSeconds;
            serialized.FindProperty("primaryShotCameraKick").floatValue =
                snapshot.PrimaryCameraKick;
            serialized.FindProperty("secondaryShotCameraKick").floatValue =
                snapshot.SecondaryCameraKick;
            serialized.FindProperty(
                    "shotCameraKickRecoverySeconds").floatValue =
                snapshot.CameraKickRecoverySeconds;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyCombatFeelSnapshot(
            FpgShootingTuningSnapshot snapshot)
        {
            SerializedObject serialized = new SerializedObject(
                snapshot.CombatFeelProfile);
            serialized.UpdateIfRequiredOrScript();
            serialized.FindProperty("maximumAimDistance").floatValue =
                snapshot.MaximumAimDistance;
            serialized.FindProperty("primaryBaseSpreadTangent").floatValue =
                snapshot.PrimarySpreadTangent;
            serialized.FindProperty("secondaryAreaRadius").floatValue =
                snapshot.SecondaryAreaRadius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyWeaponSnapshot(
            FpgShootingTuningSnapshot snapshot)
        {
            SerializedObject serialized = new SerializedObject(snapshot.Weapon);
            serialized.UpdateIfRequiredOrScript();
            serialized.FindProperty("magazineCapacity").intValue =
                snapshot.MagazineCapacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool WritableFieldsMatch(
            FpgShootingTuningSnapshot expected,
            FpgShootingTuningSnapshot actual)
        {
            return RectApproximately(
                    expected.ReticleSafeViewport,
                    actual.ReticleSafeViewport)
                && VectorApproximately(
                    expected.MouseReferenceResolution,
                    actual.MouseReferenceResolution)
                && Mathf.Approximately(
                    expected.MouseReticleSensitivity,
                    actual.MouseReticleSensitivity)
                && Mathf.Approximately(
                    expected.GamepadReticleSpeed,
                    actual.GamepadReticleSpeed)
                && Mathf.Approximately(
                    expected.GamepadReticleDeadzone,
                    actual.GamepadReticleDeadzone)
                && Mathf.Approximately(
                    expected.GamepadReticleResponseExponent,
                    actual.GamepadReticleResponseExponent)
                && Mathf.Approximately(
                    expected.MaximumAimDistance,
                    actual.MaximumAimDistance)
                && expected.InputBufferTicks == actual.InputBufferTicks
                && Mathf.Approximately(
                    expected.PeekTransitionSeconds,
                    actual.PeekTransitionSeconds)
                && Mathf.Approximately(
                    expected.FacingFlipDelaySeconds,
                    actual.FacingFlipDelaySeconds)
                && Mathf.Approximately(
                    expected.FacingFlipDurationSeconds,
                    actual.FacingFlipDurationSeconds)
                && Mathf.Approximately(
                    expected.RetractTransitionSeconds,
                    actual.RetractTransitionSeconds)
                && Mathf.Approximately(
                    expected.CoverTraversalSeconds,
                    actual.CoverTraversalSeconds)
                && Mathf.Approximately(
                    expected.PrimarySpreadTangent,
                    actual.PrimarySpreadTangent)
                && Mathf.Approximately(
                    expected.SecondaryAreaRadius,
                    actual.SecondaryAreaRadius)
                && expected.MagazineCapacity == actual.MagazineCapacity
                && Mathf.Approximately(
                    expected.PrimaryCameraKick,
                    actual.PrimaryCameraKick)
                && Mathf.Approximately(
                    expected.SecondaryCameraKick,
                    actual.SecondaryCameraKick)
                && Mathf.Approximately(
                    expected.CameraKickRecoverySeconds,
                    actual.CameraKickRecoverySeconds);
        }

        private static bool RectApproximately(Rect first, Rect second)
        {
            return Mathf.Approximately(first.x, second.x)
                && Mathf.Approximately(first.y, second.y)
                && Mathf.Approximately(first.width, second.width)
                && Mathf.Approximately(first.height, second.height);
        }

        private static bool VectorApproximately(
            Vector2 first,
            Vector2 second)
        {
            return Mathf.Approximately(first.x, second.x)
                && Mathf.Approximately(first.y, second.y);
        }

        private void RefreshResolvedSelection()
        {
            hasSelection = false;
            hasCurrentSnapshot = false;
            activeSelection = default;
            currentSnapshot = default;
            catalogValidationError = string.Empty;
            selectionValidationError = string.Empty;

            if (catalog == null)
            {
                return;
            }

            if (!catalog.TryValidate(out catalogValidationError))
            {
                catalogValidationError = catalogValidationError
                    ?? "角色目录无效。";
            }

            IReadOnlyList<FpgPlayableCharacterCatalogEntry> entries =
                catalog.Entries;
            if (entries == null || entries.Count == 0)
            {
                selectionValidationError = "角色目录没有可调试条目。";
                return;
            }

            selectedCharacterIndex = Mathf.Clamp(
                selectedCharacterIndex,
                0,
                entries.Count - 1);
            FpgPlayableCharacterCatalogEntry entry =
                entries[selectedCharacterIndex];
            if (entry == null)
            {
                selectionValidationError = "当前角色条目为空。";
                return;
            }

            bool selectionIsValid = entry.TryCreateSelection(
                out activeSelection,
                out string selectionError);
            hasSelection = activeSelection.CharacterDefinition != null
                || activeSelection.ThreeCProfile != null
                || activeSelection.CombatFeelProfile != null;
            if (!hasSelection)
            {
                selectionValidationError = string.IsNullOrWhiteSpace(selectionError)
                    ? "当前角色条目未能解析配置资产。"
                    : selectionError;
                return;
            }

            if (!selectionIsValid)
            {
                selectionValidationError = selectionError;
            }

            D0WeaponDefinition weapon = GetWeapon();
            if (weapon == null)
            {
                AppendSelectionValidationError("当前角色缺少武器配置。");
            }

            if (!FpgShootingTuningSnapshot.TryCapture(
                    activeSelection,
                    out currentSnapshot,
                    out string snapshotError))
            {
                AppendSelectionValidationError(
                    string.IsNullOrWhiteSpace(snapshotError)
                    ? "无法生成射击调参快照。"
                    : snapshotError);
                return;
            }

            hasCurrentSnapshot = true;
        }

        private void AppendSelectionValidationError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return;
            }

            selectionValidationError = string.IsNullOrWhiteSpace(
                selectionValidationError)
                ? error
                : selectionValidationError + "\n" + error;
        }

        private VisualElement CreateEditableProperties(
            UnityEngine.Object target,
            string undoName,
            params string[] propertyNamesAndLabels)
        {
            VisualElement container = new VisualElement();
            if (target == null)
            {
                container.Add(CreateHelpBox(
                    "缺少对应的权威配置资产。",
                    HelpBoxMessageType.Error));
                return container;
            }

            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.UpdateIfRequiredOrScript();
            bool hasMissingProperty = false;
            for (int index = 0;
                index + 1 < propertyNamesAndLabels.Length;
                index += 2)
            {
                SerializedProperty property = serializedTarget.FindProperty(
                    propertyNamesAndLabels[index]);
                if (property == null)
                {
                    hasMissingProperty = true;
                    continue;
                }

                PropertyField propertyField = new PropertyField(
                    property,
                    propertyNamesAndLabels[index + 1]);
                propertyField.style.marginBottom = 2f;
                propertyField.RegisterCallback<SerializedPropertyChangeEvent>(
                    _ =>
                    {
                        Undo.SetCurrentGroupName(undoName);
                        EditorUtility.SetDirty(target);
                        ScheduleUiRefresh();
                    });
                propertyField.BindProperty(property);
                container.Add(propertyField);
            }

            if (hasMissingProperty)
            {
                container.Add(CreateHelpBox(
                    "部分参数字段已变更，请同步更新射击调参窗口。",
                    HelpBoxMessageType.Warning));
            }

            return container;
        }

        private VisualElement CreatePrimarySpreadHalfAngleEditor(
            D0CombatFeelProfile target)
        {
            if (target == null)
            {
                return CreateHelpBox(
                    "缺少战斗手感资产，无法编辑主射散布。",
                    HelpBoxMessageType.Error);
            }

            SerializedObject serializedTarget = new SerializedObject(target);
            serializedTarget.UpdateIfRequiredOrScript();
            SerializedProperty tangentProperty = serializedTarget.FindProperty(
                "primaryBaseSpreadTangent");
            if (tangentProperty == null)
            {
                return CreateHelpBox(
                    "战斗手感资产缺少主射散布字段。",
                    HelpBoxMessageType.Error);
            }

            float maximumHalfAngle =
                FpgShootingTuningSnapshot.SpreadTangentToHalfAngleDegrees(
                    MaximumPrimarySpreadTangent);
            FloatField halfAngleField = new FloatField(
                "主射散布半角（度）")
            {
                isDelayed = true,
                value = FpgShootingTuningSnapshot
                    .SpreadTangentToHalfAngleDegrees(
                        Mathf.Max(0f, tangentProperty.floatValue)),
                tooltip =
                    "以权威中心方向为轴的散布锥半角。工作台按度显示和输入，正式资产仍写入兼容字段 primaryBaseSpreadTangent。"
            };
            halfAngleField.RegisterValueChangedCallback(evt =>
            {
                float requested = evt.newValue;
                if (float.IsNaN(requested) || float.IsInfinity(requested))
                {
                    halfAngleField.SetValueWithoutNotify(
                        FpgShootingTuningSnapshot
                            .SpreadTangentToHalfAngleDegrees(
                                Mathf.Max(0f, tangentProperty.floatValue)));
                    return;
                }

                float clamped = Mathf.Clamp(
                    requested,
                    0f,
                    maximumHalfAngle);
                Undo.RecordObject(target, "调整主射散布半角");
                SerializedObject currentTarget = new SerializedObject(target);
                currentTarget.UpdateIfRequiredOrScript();
                SerializedProperty currentTangent =
                    currentTarget.FindProperty("primaryBaseSpreadTangent");
                currentTangent.floatValue = FpgShootingTuningSnapshot
                    .SpreadHalfAngleDegreesToTangent(clamped);
                currentTarget.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                halfAngleField.SetValueWithoutNotify(clamped);
                ScheduleUiRefresh();
            });
            return halfAngleField;
        }

        private static VisualElement CreateReadOnlyAssetRow(
            string label,
            UnityEngine.Object asset,
            Type type)
        {
            VisualElement row = CreateHorizontalRow();
            ObjectField assetField = new ObjectField(label)
            {
                objectType = type,
                allowSceneObjects = false,
                value = asset
            };
            assetField.SetEnabled(false);
            assetField.style.flexGrow = 1f;
            row.Add(assetField);

            Button locate = new Button(() =>
            {
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            })
            {
                text = "定位",
                tooltip = "在 Project 窗口中定位该资产"
            };
            locate.style.width = 56f;
            locate.SetEnabled(asset != null);
            row.Add(locate);
            return row;
        }

        private static VisualElement CreateSkillAssetRow(
            string label,
            FpgPlayerSkillDefinition skill)
        {
            VisualElement row = CreateHorizontalRow();
            ObjectField skillField = new ObjectField(label)
            {
                objectType = typeof(FpgPlayerSkillDefinition),
                allowSceneObjects = false,
                value = skill
            };
            skillField.SetEnabled(false);
            skillField.style.flexGrow = 1f;
            row.Add(skillField);

            Button open = new Button(() =>
            {
                if (skill != null)
                {
                    FpgSkillEditorWindow.OpenAsset(skill);
                }
            })
            {
                text = "打开",
                tooltip = "在 Skill Editor 中打开该技能"
            };
            open.style.width = 56f;
            open.SetEnabled(skill != null);
            row.Add(open);
            return row;
        }

        private static VisualElement CreateSpreadRadiusTable(
            FpgShootingTuningSnapshot snapshot)
        {
            VisualElement table = new VisualElement();
            Label title = new Label("不同距离的散布半径");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 4f;
            title.style.marginBottom = 2f;
            table.Add(title);
            float maximum = Mathf.Max(0f, snapshot.MaximumAimDistance);
            float[] requestedDistances = { 10f, 25f, 50f };
            float lastDistance = -1f;
            for (int index = 0; index < requestedDistances.Length; index++)
            {
                float distance = Mathf.Min(requestedDistances[index], maximum);
                if (distance <= 0f || Mathf.Approximately(distance, lastDistance))
                {
                    continue;
                }

                lastDistance = distance;
                AddMetric(
                    table,
                    distance.ToString("0.#", CultureInfo.InvariantCulture) + " 米",
                    (distance * snapshot.PrimarySpreadTangent).ToString(
                        "0.000",
                        CultureInfo.InvariantCulture));
            }

            AddMetric(
                table,
                "最大距离",
                snapshot.PrimarySpreadRadiusAtMaximumAimDistance.ToString(
                    "0.000",
                    CultureInfo.InvariantCulture));
            return table;
        }

        private static void AddTickMetric(
            VisualElement parent,
            string label,
            int ticks,
            float seconds)
        {
            AddMetric(
                parent,
                label,
                ticks + " Tick / "
                    + seconds.ToString("0.000", CultureInfo.InvariantCulture)
                    + " 秒");
        }

        private static string FormatDamageSummary(DamageSpec damage)
        {
            return "生命 " + damage.BaseDamage
                + " / 韧性 " + damage.BreakDamage
                + " / 弱点生命 x"
                + (damage.WeakpointDamageMultiplierBasisPoints / 10000f)
                    .ToString("0.00", CultureInfo.InvariantCulture)
                + " / 弱点韧性 x"
                + (damage.WeakpointBreakMultiplierBasisPoints / 10000f)
                    .ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static void AddMetric(
            VisualElement parent,
            string label,
            string value)
        {
            VisualElement row = CreateHorizontalRow();
            Label nameLabel = new Label(label);
            nameLabel.style.flexGrow = 1f;
            nameLabel.style.minWidth = 180f;
            Label valueLabel = new Label(value);
            valueLabel.style.flexGrow = 1f;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(nameLabel);
            row.Add(valueLabel);
            parent.Add(row);
        }

        private static string FormatDelta(
            float captured,
            float current)
        {
            float delta = current - captured;
            return captured.ToString("0.###", CultureInfo.InvariantCulture)
                + " -> "
                + current.ToString("0.###", CultureInfo.InvariantCulture)
                + "  ("
                + delta.ToString(
                    "+0.###;-0.###;0",
                    CultureInfo.InvariantCulture)
                + ")";
        }

        private static string FormatDelta(
            int captured,
            int current)
        {
            int delta = current - captured;
            return captured + " -> " + current + "  ("
                + delta.ToString("+0;-0;0", CultureInfo.InvariantCulture)
                + ")";
        }

        private static string FormatDelta(
            Vector2 captured,
            Vector2 current)
        {
            return FormatVector2(captured) + " -> " + FormatVector2(current);
        }

        private static string FormatDelta(
            Rect captured,
            Rect current)
        {
            return FormatRect(captured) + " -> " + FormatRect(current);
        }

        private static string FormatVector2(Vector2 value)
        {
            return "("
                + value.x.ToString("0.###", CultureInfo.InvariantCulture)
                + ", "
                + value.y.ToString("0.###", CultureInfo.InvariantCulture)
                + ")";
        }

        private static string FormatVector3(Vector3 value)
        {
            return "("
                + value.x.ToString("0.###", CultureInfo.InvariantCulture)
                + ", "
                + value.y.ToString("0.###", CultureInfo.InvariantCulture)
                + ", "
                + value.z.ToString("0.###", CultureInfo.InvariantCulture)
                + ")";
        }

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string FormatAvailability(
            FpgAttackAvailability availability)
        {
            return availability.Ready
                ? "Ready（弹药 " + availability.Ammo + "/"
                    + availability.RequiredAmmo + "）"
                : availability.Reason + "（弹药 "
                    + availability.Ammo + "/"
                    + availability.RequiredAmmo + "）";
        }

        private static string FormatRect(Rect value)
        {
            return "("
                + value.x.ToString("0.###", CultureInfo.InvariantCulture)
                + ", "
                + value.y.ToString("0.###", CultureInfo.InvariantCulture)
                + ", "
                + value.width.ToString("0.###", CultureInfo.InvariantCulture)
                + ", "
                + value.height.ToString("0.###", CultureInfo.InvariantCulture)
                + ")";
        }

        private static VisualElement CreateSectionContainer(string title)
        {
            VisualElement section = new VisualElement();
            section.style.marginTop = 8f;
            section.style.paddingLeft = 6f;
            section.style.paddingRight = 6f;
            section.style.paddingBottom = 6f;
            section.style.borderBottomWidth = 1f;
            section.style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.27f, 0.27f, 0.27f, 1f)
                : new Color(0.68f, 0.68f, 0.68f, 1f);

            Label titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 5f;
            section.Add(titleLabel);
            return section;
        }

        private static Foldout CreateFoldout(
            string title,
            bool expanded,
            Action<bool> onChanged)
        {
            Foldout foldout = new Foldout
            {
                text = title,
                value = expanded
            };
            foldout.style.marginTop = 8f;
            foldout.style.paddingLeft = 6f;
            foldout.style.paddingRight = 6f;
            foldout.style.paddingBottom = 6f;
            foldout.style.borderBottomWidth = 1f;
            foldout.style.borderBottomColor = EditorGUIUtility.isProSkin
                ? new Color(0.27f, 0.27f, 0.27f, 1f)
                : new Color(0.68f, 0.68f, 0.68f, 1f);
            foldout.RegisterValueChangedCallback(evt =>
                onChanged?.Invoke(evt.newValue));
            return foldout;
        }

        private static HelpBox CreateHelpBox(
            string message,
            HelpBoxMessageType messageType)
        {
            HelpBox helpBox = new HelpBox
            {
                text = message ?? string.Empty,
                messageType = messageType
            };
            helpBox.style.marginTop = 3f;
            helpBox.style.marginBottom = 3f;
            return helpBox;
        }

        private static HelpBoxMessageType ToHelpBoxMessageType(
            MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Error:
                    return HelpBoxMessageType.Error;
                case MessageType.Warning:
                    return HelpBoxMessageType.Warning;
                case MessageType.Info:
                    return HelpBoxMessageType.Info;
                default:
                    return HelpBoxMessageType.None;
            }
        }

        private static VisualElement CreateHorizontalRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2f;
            row.style.marginBottom = 2f;
            return row;
        }

        private static Button CreateCommandButton(
            string text,
            Action action)
        {
            Button button = new Button(action)
            {
                text = text
            };
            button.style.flexGrow = 1f;
            button.style.height = 26f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            return button;
        }

        private D0WeaponDefinition GetWeapon()
        {
            return activeSelection.CharacterDefinition == null
                ? null
                : activeSelection.CharacterDefinition.Weapon;
        }

        private void LoadCatalogFromSessionOrProject()
        {
            string guid = SessionState.GetString(
                CatalogGuidSessionKey,
                string.Empty);
            string path = string.IsNullOrWhiteSpace(guid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(guid);
            catalog = string.IsNullOrWhiteSpace(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                    path);
            if (catalog != null)
            {
                return;
            }

            string[] catalogGuids = AssetDatabase.FindAssets(
                DefaultCatalogFilter,
                new[] { "Assets/FPGDemo" });
            if (catalogGuids == null || catalogGuids.Length == 0)
            {
                return;
            }

            Array.Sort(catalogGuids, StringComparer.Ordinal);
            string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[0]);
            catalog = AssetDatabase.LoadAssetAtPath<FpgPlayableCharacterCatalog>(
                catalogPath);
            PersistCatalogSelection();
        }

        private void RestoreCharacterSelection()
        {
            selectedCharacterIndex = 0;
            if (catalog == null)
            {
                return;
            }

            string characterId = SessionState.GetString(
                CharacterIdSessionKey,
                string.Empty);
            IReadOnlyList<FpgPlayableCharacterCatalogEntry> entries =
                catalog.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index]?.Character != null
                    && string.Equals(
                        entries[index].Character.CharacterId,
                        characterId,
                        StringComparison.Ordinal))
                {
                    selectedCharacterIndex = index;
                    return;
                }
            }
        }

        private void PersistCatalogSelection()
        {
            string path = catalog == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(catalog);
            string guid = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(path);
            SessionState.SetString(CatalogGuidSessionKey, guid);
        }

        private void PersistCharacterSelection()
        {
            string characterId = string.Empty;
            if (catalog != null
                && selectedCharacterIndex >= 0
                && selectedCharacterIndex < catalog.Count)
            {
                characterId = catalog.Entries[selectedCharacterIndex]
                    ?.Character?.CharacterId ?? string.Empty;
            }

            SessionState.SetString(CharacterIdSessionKey, characterId);
        }

        private static string[] BuildCharacterLabels(
            IReadOnlyList<FpgPlayableCharacterCatalogEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] labels = new string[entries.Count];
            for (int index = 0; index < entries.Count; index++)
            {
                D0CharacterDefinition character = entries[index]?.Character;
                if (character == null)
                {
                    labels[index] = "缺失角色（条目 " + index + "）";
                    continue;
                }

                StringBuilder label = new StringBuilder();
                label.Append(string.IsNullOrWhiteSpace(character.DisplayName)
                    ? character.name
                    : character.DisplayName);
                if (!string.IsNullOrWhiteSpace(character.CharacterId))
                {
                    label.Append(" [");
                    label.Append(character.CharacterId);
                    label.Append(']');
                }

                labels[index] = label.ToString();
            }

            return labels;
        }
    }
}
