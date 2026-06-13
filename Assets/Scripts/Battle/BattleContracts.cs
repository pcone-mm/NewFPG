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
        public int capacity = 10;
        public List<ArtifactRuntimeState> equippedArtifacts = new List<ArtifactRuntimeState>();
        public List<ArtifactQueueSlot> slots = new List<ArtifactQueueSlot>();
        public int usedCapacity;
        public bool isLoadoutValid = true;
        public string invalidReason;

        public int RemainingCapacity
        {
            get { return Mathf.Max(0, capacity - usedCapacity); }
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
