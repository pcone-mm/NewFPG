using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewFPG.Battle
{
    public sealed class ArtifactReleaseReport
    {
        public ArtifactRuntimeState artifact;
        public ArtifactCombatProfile profile;
        public EnemyCombatState target;
        public bool released;
        public bool usedShengBuff;
        public bool interrupted;
        public bool supplied;
        public float damageDealt;
        public float splashDamageDealt;
        public float shieldGained;
        public float healingDone;
        public float cooldownCharged;
        public Vector3 effectPosition;
        public ArtifactRuntimeState supplyReceiver;
        public ElementSupplyEvent supplyEvent;
        public ArtifactRuntimeState drawnArtifact;
        public int cycledSlotIndex = -1;
        public string message;
    }

    public sealed class ArtifactAutoReleaseSystem
    {
        private const float FireSplashDamage = 20f;
        private const float FireSplashRadiusSqr = 9f;
        private const float DuanjinDamage = 20f;
        private const float CatalystChargeSeconds = 2f;

        public event Action<ArtifactReleaseReport> Released;

        public void Tick(BattleSessionContext context, float deltaTime)
        {
            if (context == null || context.artifactQueue == null || context.artifactQueue.equippedArtifacts == null)
            {
                return;
            }

            TickCooldowns(context.artifactQueue.equippedArtifacts, deltaTime);
            context.artifactQueue.EnsureCycleInitialized();

            if (!context.isBattleRunning || !context.totalAutoEnabled)
            {
                return;
            }

            List<ArtifactRuntimeState> visibleArtifacts = context.artifactQueue.GetVisibleArtifacts();
            int visibleCount = visibleArtifacts != null ? visibleArtifacts.Count : 0;
            for (int i = 0; i < visibleCount; i++)
            {
                ArtifactRuntimeState runtime = visibleArtifacts[i];
                if (!CanAttemptRelease(runtime))
                {
                    continue;
                }

                ArtifactReleaseReport report;
                if (TryRelease(context, runtime, out report))
                {
                    ArtifactRuntimeState drawnArtifact;
                    int cycledSlotIndex;
                    if (context.artifactQueue.TryCycleAfterRelease(runtime, out drawnArtifact, out cycledSlotIndex))
                    {
                        report.drawnArtifact = drawnArtifact;
                        report.cycledSlotIndex = cycledSlotIndex;
                    }

                    Released?.Invoke(report);
                    break;
                }
            }
        }

        public void SetTotalAuto(BattleSessionContext context, bool enabled)
        {
            if (context != null)
            {
                context.totalAutoEnabled = enabled;
            }
        }

        public void SetArtifactAuto(ArtifactRuntimeState runtime, bool enabled)
        {
            if (runtime != null)
            {
                runtime.autoEnabled = enabled;
            }
        }

        private static void TickCooldowns(IReadOnlyList<ArtifactRuntimeState> artifacts, float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);
            for (int i = 0; i < artifacts.Count; i++)
            {
                ArtifactRuntimeState runtime = artifacts[i];
                if (runtime == null)
                {
                    continue;
                }

                runtime.cooldownRemaining = Mathf.Max(0f, runtime.cooldownRemaining - step);
                runtime.isReady = runtime.cooldownRemaining <= 0f;
            }
        }

        private static bool CanAttemptRelease(ArtifactRuntimeState runtime)
        {
            if (runtime == null || runtime.profile == null)
            {
                return false;
            }

            return runtime.autoEnabled && runtime.isReady && runtime.cooldownRemaining <= 0f;
        }

        private bool TryRelease(BattleSessionContext context, ArtifactRuntimeState runtime, out ArtifactReleaseReport report)
        {
            report = null;
            ArtifactCombatProfile profile = runtime.profile;
            EnemyCombatState target = null;

            if (profile.targetSelectorType != TargetSelectorType.none)
            {
                target = TargetSelector.SelectTarget(context, profile);
                if (target == null)
                {
                    runtime.runtimeNote = "等待目标";
                    return false;
                }
            }

            if (!CanCastNoTargetProfile(context, runtime, target))
            {
                return false;
            }

            report = new ArtifactReleaseReport
            {
                artifact = runtime,
                profile = profile,
                target = target,
                released = true,
                usedShengBuff = runtime.shengStacks > 0,
                effectPosition = target != null ? target.position : context.playerPosition,
            };

            ApplyProfileEffect(context, runtime, target, report);
            ConsumeShengStack(runtime, report);

            runtime.cooldownRemaining = Mathf.Max(0.1f, profile.cooldown);
            runtime.isReady = false;
            runtime.lastReleaseAt = context.elapsedSeconds;
            runtime.lastTarget = target;

            ApplySupply(context, runtime, report);
            runtime.runtimeNote = report.message;
            return true;
        }

        private static bool CanCastNoTargetProfile(BattleSessionContext context, ArtifactRuntimeState runtime, EnemyCombatState target)
        {
            ArtifactCombatProfile profile = runtime.profile;
            if (profile.targetSelectorType != TargetSelectorType.none && target == null)
            {
                return false;
            }

            switch (profile.artifactId)
            {
                case "duanjin_ring":
                    return target != null && target.isCharging && target.isInterruptible;
                case "jingshui_amulet":
                    return context.playerShield < 50f || PlayerHpRatio(context) < 0.8f;
                case "houtu_barrier":
                    return context.playerShield < 90f;
                case "qingmu_heal_orb":
                    return context.playerHp < context.playerMaxHp;
                case "lianhuo_catalyst_lamp":
                    return FindCatalystReceiver(context.artifactQueue, runtime) != null;
                default:
                    return true;
            }
        }

        private static void ApplyProfileEffect(
            BattleSessionContext context,
            ArtifactRuntimeState runtime,
            EnemyCombatState target,
            ArtifactReleaseReport report)
        {
            ArtifactCombatProfile profile = runtime.profile;
            float shengValue = report.usedShengBuff && profile.shengBuff != null ? profile.shengBuff.value : 0f;

            switch (profile.artifactId)
            {
                case "zhanfeng_short_blade":
                case "shuangshui_needle":
                case "baoyan_talisman":
                case "kunyan_seal":
                    ApplyDamage(context, target, profile.damage * (1f + Mathf.Max(0f, shengValue)), report);
                    if (profile.artifactId == "shuangshui_needle" && target != null)
                    {
                        target.isSlowed = true;
                        target.intent = "减速";
                    }

                    if (profile.artifactId == "baoyan_talisman")
                    {
                        ApplyFireSplash(context, target, report);
                    }

                    if (profile.artifactId == "kunyan_seal" && target != null)
                    {
                        target.intent = "压制";
                        target.threatScore = Mathf.Max(0f, target.threatScore - 0.5f);
                    }

                    break;
                case "jingshui_amulet":
                    ApplyShield(context, profile.shield + (report.usedShengBuff ? shengValue : 0f), report);
                    break;
                case "houtu_barrier":
                    ApplyShield(context, profile.shield, report);
                    context.resultSummary.secondaryFailureReason = report.usedShengBuff ? "厚土壁垒：减伤强化待接入" : "厚土壁垒：减伤占位";
                    break;
                case "fumu_bell":
                    if (target != null)
                    {
                        target.isSlowed = true;
                        target.intent = "定身";
                        if (target.isCharging)
                        {
                            target.chargeProgress = Mathf.Max(0f, target.chargeProgress - 0.3f);
                        }
                    }

                    report.message = "缚木铃定身";
                    break;
                case "duanjin_ring":
                    if (target != null)
                    {
                        target.isCharging = false;
                        target.isInterruptible = false;
                        target.chargeProgress = 0f;
                        target.state = EnemyState.Moving;
                        target.intent = "已打断";
                        report.interrupted = true;
                        context.resultSummary.interruptCount++;
                        ApplyDamage(context, target, DuanjinDamage * (1f + Mathf.Max(0f, shengValue)), report);
                    }

                    break;
                case "qingmu_heal_orb":
                    ApplyHeal(context, profile.heal + (report.usedShengBuff ? shengValue : 0f), report);
                    break;
                case "lianhuo_catalyst_lamp":
                    ApplyCatalystCharge(context, runtime, report);
                    break;
                default:
                    if (profile.damage > 0f)
                    {
                        ApplyDamage(context, target, profile.damage, report);
                    }

                    break;
            }

            if (string.IsNullOrWhiteSpace(report.message))
            {
                report.message = BuildReportMessage(report);
            }
        }

        private static void ApplyDamage(BattleSessionContext context, EnemyCombatState target, float amount, ArtifactReleaseReport report)
        {
            if (target == null || amount <= 0f)
            {
                return;
            }

            float damage = Mathf.Max(0f, amount);
            target.hp = Mathf.Max(0f, target.hp - damage);
            report.damageDealt += damage;
            if (target.hp <= 0f)
            {
                target.isDead = true;
                target.isTargetable = false;
                target.intent = "击破";
                if (context.focusTarget == target)
                {
                    context.focusTarget = null;
                }
            }
        }

        private static void ApplyFireSplash(BattleSessionContext context, EnemyCombatState mainTarget, ArtifactReleaseReport report)
        {
            if (context.enemies == null || mainTarget == null)
            {
                return;
            }

            for (int i = 0; i < context.enemies.Count; i++)
            {
                EnemyCombatState enemy = context.enemies[i];
                if (enemy == null || enemy == mainTarget || !enemy.CanBeTargeted)
                {
                    continue;
                }

                if ((enemy.position - mainTarget.position).sqrMagnitude <= FireSplashRadiusSqr)
                {
                    float before = enemy.hp;
                    ApplyDamage(context, enemy, FireSplashDamage, report);
                    report.splashDamageDealt += Mathf.Max(0f, before - enemy.hp);
                }
            }
        }

        private static void ApplyShield(BattleSessionContext context, float amount, ArtifactReleaseReport report)
        {
            float shield = Mathf.Max(0f, amount);
            context.playerShield += shield;
            report.shieldGained += shield;
        }

        private static void ApplyHeal(BattleSessionContext context, float amount, ArtifactReleaseReport report)
        {
            float before = context.playerHp;
            context.playerHp = Mathf.Min(context.playerMaxHp, context.playerHp + Mathf.Max(0f, amount));
            report.healingDone += Mathf.Max(0f, context.playerHp - before);
        }

        private static void ApplyCatalystCharge(BattleSessionContext context, ArtifactRuntimeState source, ArtifactReleaseReport report)
        {
            ArtifactRuntimeState receiver = FindCatalystReceiver(context.artifactQueue, source);
            if (receiver == null)
            {
                report.message = "炼火催灵灯等待可充能法宝";
                return;
            }

            float before = receiver.cooldownRemaining;
            float charge = CatalystChargeSeconds;
            if (report.usedShengBuff && source.profile.shengBuff != null)
            {
                charge += CatalystChargeSeconds * Mathf.Max(0f, source.profile.shengBuff.value);
            }

            receiver.cooldownRemaining = Mathf.Max(0f, receiver.cooldownRemaining - charge);
            receiver.isReady = receiver.cooldownRemaining <= 0f;
            report.cooldownCharged = Mathf.Max(0f, before - receiver.cooldownRemaining);
            report.supplyReceiver = receiver;
            report.message = "炼火催灵灯充能：" + receiver.profile.displayName;
        }

        private static void ConsumeShengStack(ArtifactRuntimeState runtime, ArtifactReleaseReport report)
        {
            if (runtime.shengStacks <= 0)
            {
                return;
            }

            runtime.shengStacks = Mathf.Max(0, runtime.shengStacks - 1);
            report.usedShengBuff = true;
        }

        private static void ApplySupply(BattleSessionContext context, ArtifactRuntimeState source, ArtifactReleaseReport report)
        {
            if (context.artifactQueue == null || source == null || source.profile == null || !source.canProcSupply || !source.profile.canProcSupply)
            {
                return;
            }

            List<ArtifactRuntimeState> receivers = CollectSupplyReceivers(context.artifactQueue, source);
            for (int i = 0; i < receivers.Count; i++)
            {
                ArtifactRuntimeState receiver = receivers[i];
                if (receiver == null || receiver.profile == null)
                {
                    continue;
                }

                bool accepted = receiver.profile.acceptedElements != null && receiver.profile.acceptedElements.Contains(source.profile.element);
                bool matched = IsShengRelation(source.profile.element, receiver.profile.element);
                if (!accepted || !matched)
                {
                    continue;
                }

                receiver.shengStacks = Mathf.Min(1, receiver.shengStacks + 1);
                ElementSupplyEvent supplyEvent = new ElementSupplyEvent
                {
                    sourceArtifactId = source.profile.artifactId,
                    receiverArtifactId = receiver.profile.artifactId,
                    sourceElement = source.profile.element,
                    receiverElement = receiver.profile.element,
                    supplyDirection = source.profile.supplyDirection,
                    receiverAcceptedElement = accepted,
                    matchedShengRelation = matched,
                    appliedBuff = receiver.profile.shengBuff,
                    createdAtSeconds = context.elapsedSeconds,
                    debugNote = source.profile.displayName + " 相生 " + receiver.profile.displayName,
                };

                receiver.lastSupplyEvent = supplyEvent;
                report.supplyEvent = supplyEvent;
                report.supplyReceiver = receiver;
                report.supplied = true;
                context.resultSummary.shengTriggerCount++;
                break;
            }
        }

        private static List<ArtifactRuntimeState> CollectSupplyReceivers(ArtifactQueueState queue, ArtifactRuntimeState source)
        {
            List<ArtifactRuntimeState> receivers = new List<ArtifactRuntimeState>();
            List<ArtifactRuntimeState> visibleArtifacts = queue != null ? queue.GetVisibleArtifacts() : null;
            if (visibleArtifacts == null)
            {
                return receivers;
            }

            int index = visibleArtifacts.IndexOf(source);
            if (index < 0)
            {
                return receivers;
            }

            SupplyDirection direction = source.profile.supplyDirection;
            if ((direction == SupplyDirection.Left || direction == SupplyDirection.Both) && index - 1 >= 0)
            {
                receivers.Add(visibleArtifacts[index - 1]);
            }

            if ((direction == SupplyDirection.Right || direction == SupplyDirection.Both) && index + 1 < visibleArtifacts.Count)
            {
                receivers.Add(visibleArtifacts[index + 1]);
            }

            return receivers;
        }

        private static ArtifactRuntimeState FindCatalystReceiver(ArtifactQueueState queue, ArtifactRuntimeState source)
        {
            List<ArtifactRuntimeState> visibleArtifacts = queue != null ? queue.GetVisibleArtifacts() : null;
            if (visibleArtifacts == null)
            {
                return null;
            }

            ArtifactRuntimeState best = null;
            float bestCooldown = 0f;
            int sourceIndex = visibleArtifacts.IndexOf(source);
            for (int i = 0; i < visibleArtifacts.Count; i++)
            {
                if (i != sourceIndex - 1 && i != sourceIndex + 1)
                {
                    continue;
                }

                ArtifactRuntimeState candidate = visibleArtifacts[i];
                if (candidate == null || candidate == source || candidate.cooldownRemaining <= bestCooldown)
                {
                    continue;
                }

                best = candidate;
                bestCooldown = candidate.cooldownRemaining;
            }

            return best;
        }

        private static bool IsShengRelation(Element source, Element receiver)
        {
            return (source == Element.Wood && receiver == Element.Fire)
                || (source == Element.Fire && receiver == Element.Earth)
                || (source == Element.Earth && receiver == Element.Metal)
                || (source == Element.Metal && receiver == Element.Water)
                || (source == Element.Water && receiver == Element.Wood);
        }

        private static float PlayerHpRatio(BattleSessionContext context)
        {
            return context.playerMaxHp <= 0f ? 0f : Mathf.Clamp01(context.playerHp / context.playerMaxHp);
        }

        private static string BuildReportMessage(ArtifactReleaseReport report)
        {
            if (report.profile == null)
            {
                return string.Empty;
            }

            if (report.damageDealt > 0f)
            {
                return report.profile.displayName + " 造成 " + report.damageDealt.ToString("0") + " 伤害";
            }

            if (report.shieldGained > 0f)
            {
                return report.profile.displayName + " 获得 " + report.shieldGained.ToString("0") + " 护盾";
            }

            if (report.healingDone > 0f)
            {
                return report.profile.displayName + " 回复 " + report.healingDone.ToString("0") + " 生命";
            }

            if (report.cooldownCharged > 0f)
            {
                return report.profile.displayName + " 充能 " + report.cooldownCharged.ToString("0.#") + " 秒";
            }

            return report.profile.displayName + " 已释放";
        }
    }
}
