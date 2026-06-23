using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Battle
{
    public enum Element
    {
        None,
        Metal,
        Water,
        Wood,
        Fire,
        Earth,
    }

    public enum ArtifactCategory
    {
        None,
        Attack,
        Defense,
        Control,
        Support,
    }

    public enum SupplyDirection
    {
        None,
        Left,
        Right,
        Both,
    }

    public enum TargetSelectorType
    {
        none,
        focus_then_nearest,
        nearest,
        lowest_hp,
        charging_then_near_player,
        interruptible_charging_only,
        high_threat_near_player,
        slowed_then_focus,
    }

    public enum EnemyState
    {
        Moving,
        Attacking,
        Charging,
    }

    public enum RangeBand
    {
        Unknown,
        Far,
        Mid,
        Near,
        Melee,
    }

    public enum WaveMarkerType
    {
        Normal,
        Major,
        Final,
    }

    [Serializable]
    public sealed class ElementSupplyBuff
    {
        public string buffId;
        public string buffName;
        public string buffType;
        public string trigger;
        public float value;
        public float duration;
        public int maxStacks = 1;
        public bool canStack;
        public string consumeRule;
        public string sourceRelation;
    }

    [Serializable]
    public sealed class WaveSpawnGroup
    {
        public int meleePressureCount;
        public int rangedChargeCount;
        public float spawnInterval = 1f;
        public bool isMajorWave;
        public string hintText;
    }

    [Serializable]
    public sealed class ArtifactCombatProfile
    {
        public string artifactId;
        public string displayName;
        public Element element = Element.None;
        public ArtifactCategory category = ArtifactCategory.None;
        public int size = 1;
        public float cooldown = 1f;
        public bool autoEnabled = true;
        public string castConditionId;
        public TargetSelectorType targetSelectorType = TargetSelectorType.none;
        public string effectLogicId;
        public SupplyDirection supplyDirection = SupplyDirection.None;
        public List<Element> acceptedElements = new List<Element>();
        public ElementSupplyBuff shengBuff = new ElementSupplyBuff();
        public List<string> tags = new List<string>();
        public float damage;
        public float shield;
        public float heal;
        public float controlDuration;
        public bool canProcSupply = true;
    }

    [Serializable]
    public sealed class ArtifactRuntimeState
    {
        public string runtimeId;
        public ArtifactCombatProfile profile = new ArtifactCombatProfile();
        public float cooldownRemaining;
        public bool autoEnabled = true;
        public bool isReady = true;
        public int shengStacks;
        public float lastReleaseAt;
        public float nextSupplyAt;
        public bool canProcSupply = true;
        public EnemyCombatState lastTarget;
        public ElementSupplyEvent lastSupplyEvent;
        public string runtimeNote;

        public bool IsCoolingDown
        {
            get { return cooldownRemaining > 0f; }
        }
    }

    [Serializable]
    public sealed class ArtifactQueueSlot
    {
        public int slotIndex;
        public ArtifactRuntimeState occupant;
        public bool isLeftBoundary;
        public bool isRightBoundary;
    }

    [Serializable]
    public sealed class ArtifactQueueState
    {
        public const int DefaultVisibleArtifactCount = 3;

        public int capacity = 10;
        public List<ArtifactRuntimeState> equippedArtifacts = new List<ArtifactRuntimeState>();
        public int visibleArtifactCount = DefaultVisibleArtifactCount;
        public List<ArtifactRuntimeState> activeArtifacts = new List<ArtifactRuntimeState>();
        public List<ArtifactRuntimeState> drawPile = new List<ArtifactRuntimeState>();
        public List<ArtifactQueueSlot> slots = new List<ArtifactQueueSlot>();
        public int usedCapacity;
        public int cycleVersion;
        public bool isLoadoutValid = true;
        public string invalidReason;

        public int RemainingCapacity
        {
            get { return Mathf.Max(0, capacity - usedCapacity); }
        }

        public List<ArtifactRuntimeState> GetVisibleArtifacts()
        {
            if (activeArtifacts != null && activeArtifacts.Count > 0)
            {
                return activeArtifacts;
            }

            return equippedArtifacts;
        }

        public void EnsureCycleInitialized()
        {
            EnsureCycleInitialized(visibleArtifactCount > 0 ? visibleArtifactCount : DefaultVisibleArtifactCount);
        }

        public void EnsureCycleInitialized(int maxVisibleArtifacts)
        {
            visibleArtifactCount = maxVisibleArtifacts > 0 ? Mathf.Max(1, maxVisibleArtifacts) : DefaultVisibleArtifactCount;
            if (equippedArtifacts == null)
            {
                equippedArtifacts = new List<ArtifactRuntimeState>();
            }

            if (activeArtifacts == null)
            {
                activeArtifacts = new List<ArtifactRuntimeState>();
            }

            if (drawPile == null)
            {
                drawPile = new List<ArtifactRuntimeState>();
            }

            if (activeArtifacts.Count > 0 || drawPile.Count > 0)
            {
                NormalizeCycleLists();
                return;
            }

            if (equippedArtifacts.Count == 0)
            {
                return;
            }

            int visibleCount = Mathf.Min(visibleArtifactCount, equippedArtifacts.Count);
            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                ArtifactRuntimeState runtime = equippedArtifacts[i];
                if (runtime == null)
                {
                    continue;
                }

                if (activeArtifacts.Count < visibleCount)
                {
                    activeArtifacts.Add(runtime);
                }
                else
                {
                    drawPile.Add(runtime);
                }
            }

            cycleVersion++;
        }

        public bool TryCycleAfterRelease(
            ArtifactRuntimeState usedArtifact,
            out ArtifactRuntimeState drawnArtifact,
            out int slotIndex)
        {
            drawnArtifact = null;
            slotIndex = -1;
            EnsureCycleInitialized();

            if (usedArtifact == null || activeArtifacts == null)
            {
                return false;
            }

            slotIndex = activeArtifacts.IndexOf(usedArtifact);
            if (slotIndex < 0)
            {
                return false;
            }

            int targetVisibleCount = Mathf.Min(visibleArtifactCount, equippedArtifacts != null ? equippedArtifacts.Count : 0);
            if (equippedArtifacts == null || equippedArtifacts.Count <= targetVisibleCount || drawPile == null || drawPile.Count == 0)
            {
                return false;
            }

            activeArtifacts.RemoveAt(slotIndex);
            drawnArtifact = DrawNextArtifact();
            if (drawnArtifact != null)
            {
                activeArtifacts.Insert(Mathf.Clamp(slotIndex, 0, activeArtifacts.Count), drawnArtifact);
            }

            drawPile.Add(usedArtifact);
            NormalizeCycleLists();
            cycleVersion++;
            return drawnArtifact != null;
        }

        private ArtifactRuntimeState DrawNextArtifact()
        {
            if (drawPile == null)
            {
                return null;
            }

            while (drawPile.Count > 0)
            {
                ArtifactRuntimeState candidate = drawPile[0];
                drawPile.RemoveAt(0);
                if (candidate != null
                    && equippedArtifacts != null
                    && equippedArtifacts.Contains(candidate)
                    && (activeArtifacts == null || !activeArtifacts.Contains(candidate)))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void NormalizeCycleLists()
        {
            RemoveInvalidActiveArtifacts();
            RemoveInvalidDrawPileArtifacts();

            int targetVisibleCount = Mathf.Min(visibleArtifactCount, equippedArtifacts != null ? equippedArtifacts.Count : 0);
            while (activeArtifacts.Count > targetVisibleCount)
            {
                ArtifactRuntimeState overflow = activeArtifacts[activeArtifacts.Count - 1];
                activeArtifacts.RemoveAt(activeArtifacts.Count - 1);
                if (overflow != null && !drawPile.Contains(overflow))
                {
                    drawPile.Insert(0, overflow);
                }
            }

            while (activeArtifacts.Count < targetVisibleCount && drawPile.Count > 0)
            {
                ArtifactRuntimeState next = DrawNextArtifact();
                if (next == null)
                {
                    break;
                }

                activeArtifacts.Add(next);
            }

            if (equippedArtifacts == null)
            {
                return;
            }

            for (int i = 0; i < equippedArtifacts.Count; i++)
            {
                ArtifactRuntimeState runtime = equippedArtifacts[i];
                if (runtime == null || activeArtifacts.Contains(runtime) || drawPile.Contains(runtime))
                {
                    continue;
                }

                if (activeArtifacts.Count < targetVisibleCount)
                {
                    activeArtifacts.Add(runtime);
                }
                else
                {
                    drawPile.Add(runtime);
                }
            }
        }

        private void RemoveInvalidActiveArtifacts()
        {
            if (activeArtifacts == null)
            {
                activeArtifacts = new List<ArtifactRuntimeState>();
                return;
            }

            for (int i = activeArtifacts.Count - 1; i >= 0; i--)
            {
                ArtifactRuntimeState runtime = activeArtifacts[i];
                if (runtime == null
                    || equippedArtifacts == null
                    || !equippedArtifacts.Contains(runtime)
                    || activeArtifacts.IndexOf(runtime) != i)
                {
                    activeArtifacts.RemoveAt(i);
                }
            }
        }

        private void RemoveInvalidDrawPileArtifacts()
        {
            if (drawPile == null)
            {
                drawPile = new List<ArtifactRuntimeState>();
                return;
            }

            for (int i = drawPile.Count - 1; i >= 0; i--)
            {
                ArtifactRuntimeState runtime = drawPile[i];
                if (runtime == null
                    || equippedArtifacts == null
                    || !equippedArtifacts.Contains(runtime)
                    || activeArtifacts.Contains(runtime)
                    || drawPile.IndexOf(runtime) != i)
                {
                    drawPile.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class WaveMarker
    {
        public float position;
        public int waveIndex;
        public WaveMarkerType markerType = WaveMarkerType.Normal;
        public WaveSpawnGroup spawnGroup = new WaveSpawnGroup();
        public bool isMajorWave;
        public string hintText;
    }

    [Serializable]
    public sealed class BattleProgressTrack
    {
        public float progress;
        public List<WaveMarker> waveMarkers = new List<WaveMarker>();
        public float nextWaveAt = 0.35f;
        public float finalWaveAt = 0.7f;
        public string incomingHint;
        public int currentWaveIndex = -1;
        public bool isFinalWaveVisible;
        public WaveMarker currentWaveMarker;
    }

    [Serializable]
    public sealed class EnemyCombatState
    {
        public string enemyId;
        public string displayName;
        public float hp;
        public float maxHp = 1f;
        public Vector3 position;
        public RangeBand rangeBand = RangeBand.Unknown;
        public EnemyState state = EnemyState.Moving;
        public bool isTargetable = true;
        public bool isCharging;
        public bool isInterruptible;
        public bool isSlowed;
        public float chargeProgress;
        public float chargeDuration;
        public float moveSpeed;
        public float attackInterval;
        public float attackDamage;
        public float threatScore;
        public bool isDead;
        public string intent;
        public bool isHighlighted;
        public float distanceToPlayer;

        public float HpRatio
        {
            get
            {
                if (maxHp <= 0f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(hp / maxHp);
            }
        }

        public bool IsAlive
        {
            get { return !isDead && hp > 0f; }
        }

        public bool CanBeTargeted
        {
            get { return IsAlive && isTargetable; }
        }
    }

    [Serializable]
    public sealed class ElementSupplyEvent
    {
        public string sourceArtifactId;
        public string receiverArtifactId;
        public Element sourceElement = Element.None;
        public Element receiverElement = Element.None;
        public SupplyDirection supplyDirection = SupplyDirection.None;
        public bool receiverAcceptedElement;
        public bool matchedShengRelation;
        public ElementSupplyBuff appliedBuff = new ElementSupplyBuff();
        public float createdAtSeconds;
        public string debugNote;
    }

    [Serializable]
    public sealed class BattleResultSummary
    {
        public bool isVictory;
        public float clearTimeSeconds;
        public float remainingPlayerHp;
        public int shengTriggerCount;
        public int interruptCount;
        public string primaryFailureReason;
        public string secondaryFailureReason;
        public int waveReached;
    }

    [Serializable]
    public sealed class BattleSessionContext
    {
        public string sessionId;
        public bool isBattleRunning = true;
        public float elapsedSeconds;
        public bool totalAutoEnabled = true;
        public Vector3 playerPosition;
        public float playerHp;
        public float playerMaxHp = 1f;
        public float playerShield;
        public bool playerControlLocked;
        public ArtifactQueueState artifactQueue = new ArtifactQueueState();
        public BattleProgressTrack progressTrack = new BattleProgressTrack();
        public List<EnemyCombatState> enemies = new List<EnemyCombatState>();
        public EnemyCombatState focusTarget;
        public ArtifactRuntimeState focusedArtifact;
        public BattleResultSummary resultSummary = new BattleResultSummary();
        public string sceneName;
    }
}
