using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NewFPG.CZN
{
    [ExecuteAlways]
    public sealed class CznSpineSkillPreviewMenu : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private CznSpineSkillPlayer player;
        [SerializeField] private CznSpineSkillSequence[] skills;
        [SerializeField] private TimelineAsset[] timelines;
        [SerializeField] private int selectedIndex;
        [SerializeField] private string previewTitle = "CZN 技能组合预览";
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool returnToIdleOnComplete = true;
        [SerializeField] private bool showOverlay = true;

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private PlayableDirector subscribedDirector;
        private bool playbackActive;
        private bool playbackCompleted;
        private bool pausedByUser;

        public bool IsPlaybackCompleted => playbackCompleted;
        public bool LoopsPlayback => loop;
        public bool ReturnsToIdleOnComplete => returnToIdleOnComplete;

        public void ConfigurePlaybackMode(bool loopPlayback, bool returnToIdleAfterPlayback)
        {
            loop = loopPlayback;
            returnToIdleOnComplete = returnToIdleAfterPlayback;
        }

        public void Configure(
            PlayableDirector directorBinding,
            CznSpineSkillPlayer playerBinding,
            CznSpineSkillSequence[] generatedSkills,
            TimelineAsset[] generatedTimelines,
            int initialIndex,
            string title = null,
            bool loopPlayback = true,
            bool returnToIdleAfterPlayback = true)
        {
            UnsubscribeFromDirector();
            director = directorBinding;
            player = playerBinding;
            skills = generatedSkills;
            timelines = generatedTimelines;
            selectedIndex = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, generatedSkills != null ? generatedSkills.Length - 1 : 0));
            loop = loopPlayback;
            returnToIdleOnComplete = returnToIdleAfterPlayback;
            SubscribeToDirector();
            previewTitle = string.IsNullOrWhiteSpace(title) ? "CZN 技能组合预览" : title;
        }

        private void OnEnable()
        {
            SubscribeToDirector();
        }

        private void OnDisable()
        {
            playbackActive = false;
            playbackCompleted = false;
            pausedByUser = false;
            UnsubscribeFromDirector();
        }

        private void Start()
        {
            if (Application.isPlaying && playOnStart)
            {
                PlaySelected();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            int requestedIndex = ReadNumberShortcut();
            if (requestedIndex >= 0 && requestedIndex < SkillCount)
            {
                selectedIndex = requestedIndex;
                PlaySelected();
            }

            if (WasPreviousPressed())
            {
                selectedIndex = WrapIndex(selectedIndex - 1);
                PlaySelected();
            }
            else if (WasNextPressed())
            {
                selectedIndex = WrapIndex(selectedIndex + 1);
                PlaySelected();
            }

            if (WasRestartPressed())
            {
                PlaySelected();
            }

            if (WasPausePressed() && director != null)
            {
                TogglePause();
            }
        }

        public void PlaySelected()
        {
            PlayIndex(selectedIndex);
        }

        public void PlayIndex(int index)
        {
            if (director == null || timelines == null || timelines.Length == 0)
            {
                return;
            }

            selectedIndex = WrapIndex(index);
            TimelineAsset timeline = timelines[selectedIndex];
            if (timeline == null)
            {
                return;
            }

            playbackActive = false;
            playbackCompleted = false;
            pausedByUser = false;
            director.Stop();
            director.playableAsset = timeline;
            director.extrapolationMode = loop ? DirectorWrapMode.Loop : DirectorWrapMode.None;
            BindPlayer(timeline);
            director.RebuildGraph();
            director.time = 0d;
            if (player != null && selectedIndex >= 0 && selectedIndex < SkillCount)
            {
                player.RestartSequence(skills[selectedIndex]);
            }
            director.Evaluate();
            playbackActive = true;
            director.Play();
        }

        private void SubscribeToDirector()
        {
            if (subscribedDirector == director)
            {
                return;
            }

            UnsubscribeFromDirector();
            subscribedDirector = director;
            if (subscribedDirector != null)
            {
                subscribedDirector.stopped += HandleDirectorStopped;
            }
        }

        private void UnsubscribeFromDirector()
        {
            if (subscribedDirector != null)
            {
                subscribedDirector.stopped -= HandleDirectorStopped;
                subscribedDirector = null;
            }
        }

        private void HandleDirectorStopped(PlayableDirector stoppedDirector)
        {
            if (!Application.isPlaying || stoppedDirector != director || !playbackActive)
            {
                return;
            }

            playbackActive = false;
            playbackCompleted = true;
            pausedByUser = false;
            if (!loop && returnToIdleOnComplete && player != null)
            {
                player.ResetToIdle();
            }
        }

        private void BindPlayer(TimelineAsset timeline)
        {
            if (director == null || player == null || timeline == null)
            {
                return;
            }

            IEnumerable<TrackAsset> tracks = timeline.GetOutputTracks();
            foreach (TrackAsset track in tracks)
            {
                if (track is CznSpineSkillTrack)
                {
                    director.SetGenericBinding(track, player);
                }
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || SkillCount == 0)
            {
                return;
            }

            EnsureStyles();
            float width = Mathf.Min(430f, Screen.width - 24f);
            Rect panel = new Rect(12f, 12f, width, Mathf.Min(Screen.height - 24f, 300f));
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f));
            GUILayout.Label(previewTitle, titleStyle);

            CznSpineSkillSequence selected = SelectedSkill;
            string label = selected != null
                ? $"{selectedIndex + 1}. {selected.DisplayName}  [{selected.SkillId}]"
                : $"{selectedIndex + 1}. <missing>";
            GUILayout.Label(label, bodyStyle);

            double duration = selected != null ? selected.Duration : 0d;
            double time = playbackCompleted ? duration : director != null ? director.time : 0d;
            GUILayout.Label(
                $"Time {time:0.000} / {duration:0.000}s    Spine {SafeActiveSpineCount}    Particle {SafeActiveParticleCount}",
                bodyStyle);

            if (playbackCompleted)
            {
                string idleName = player != null ? player.CurrentActorAnimationName : "idle";
                GUILayout.Label($"已结束 → {idleName}（按 R 重播）", bodyStyle);
            }

            if (player != null && !string.IsNullOrWhiteSpace(player.ActiveMarkerLabel))
            {
                GUILayout.Label("Event: " + player.ActiveMarkerLabel, bodyStyle);
            }

            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ 上一个", buttonStyle))
            {
                selectedIndex = WrapIndex(selectedIndex - 1);
                PlaySelected();
            }
            if (GUILayout.Button("重播 R", buttonStyle))
            {
                PlaySelected();
            }
            if (GUILayout.Button("暂停/继续 Space", buttonStyle))
            {
                TogglePause();
            }
            if (GUILayout.Button("下一个 ▶", buttonStyle))
            {
                selectedIndex = WrapIndex(selectedIndex + 1);
                PlaySelected();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < SkillCount; i++)
            {
                string buttonLabel = (i + 1).ToString();
                if (GUILayout.Button(buttonLabel, buttonStyle, GUILayout.Width(28f)))
                {
                    selectedIndex = i;
                    PlaySelected();
                }
            }
            GUILayout.EndHorizontal();

            if (selected != null && selected.UnresolvedResources.Count > 0)
            {
                GUILayout.Space(5f);
                GUILayout.Label($"未精确恢复：{selected.UnresolvedResources.Count} 项（详情见 Skill Asset）", bodyStyle);
            }

            GUILayout.Label("快捷键：1-9/0 选择前 10 项，←/→ 切换全部，R 重播，Space 暂停。", bodyStyle);
            GUILayout.EndArea();
        }

        private void TogglePause()
        {
            if (director == null || playbackCompleted)
            {
                return;
            }

            if (director.state == PlayState.Playing)
            {
                director.Pause();
                pausedByUser = true;
            }
            else if (pausedByUser)
            {
                director.Resume();
                pausedByUser = false;
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };
            titleStyle.normal.textColor = new Color(0.82f, 0.94f, 1f, 1f);

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
            };
            bodyStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
            };
        }

        private int SkillCount => Mathf.Min(skills != null ? skills.Length : 0, timelines != null ? timelines.Length : 0);
        private CznSpineSkillSequence SelectedSkill => selectedIndex >= 0 && selectedIndex < SkillCount ? skills[selectedIndex] : null;
        private int SafeActiveSpineCount => player != null ? player.ActiveSpineLayerCount : 0;
        private int SafeActiveParticleCount => player != null ? player.ActiveParticleLayerCount : 0;

        private int WrapIndex(int index)
        {
            int count = SkillCount;
            if (count <= 0)
            {
                return 0;
            }

            return (index % count + count) % count;
        }

        private static int ReadNumberShortcut()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return -1;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame) return 4;
            if (keyboard.digit6Key.wasPressedThisFrame) return 5;
            if (keyboard.digit7Key.wasPressedThisFrame) return 6;
            if (keyboard.digit8Key.wasPressedThisFrame) return 7;
            if (keyboard.digit9Key.wasPressedThisFrame) return 8;
            if (keyboard.digit0Key.wasPressedThisFrame) return 9;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha4)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha5)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha6)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha7)) return 6;
            if (Input.GetKeyDown(KeyCode.Alpha8)) return 7;
            if (Input.GetKeyDown(KeyCode.Alpha9)) return 8;
            if (Input.GetKeyDown(KeyCode.Alpha0)) return 9;
#endif
            return -1;
        }

        private static bool WasPreviousPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.LeftArrow);
#else
            return false;
#endif
        }

        private static bool WasNextPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.RightArrow);
#else
            return false;
#endif
        }

        private static bool WasRestartPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }

        private static bool WasPausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }
    }
}
