using System.Collections;
using System.Collections.Generic;
using NewFPG.Combat;
using UnityEngine;

namespace NewFPG.Monsters
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    [RequireComponent(typeof(MonsterMechanicRunner))]
    public sealed class MonsterSkillController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MonoBehaviour movementBehaviour;
        [SerializeField] private Animator animator;
        [SerializeField] private AttackWarningIndicator warningIndicator;
        [SerializeField] private MonsterMechanicRunner mechanicRunner;

        private IMonsterLocomotion movement;
        private readonly Dictionary<string, MonsterSkillDefinition> skillsById = new Dictionary<string, MonsterSkillDefinition>();
        private readonly Dictionary<string, float> nextReadyAtBySkill = new Dictionary<string, float>();
        private MonsterSkillDefinition activeSkill;
        private Transform activeTarget;
        private IDamageable lockedTarget;
        private bool casting;
        private int activeTriggerHash;
        private bool animatorHasActiveTrigger;
        private bool ownsRuntimeWarningIndicator;

        public bool IsCasting => casting;
        public Transform ActiveTarget => activeTarget;

        private void Reset()
        {
            CacheReferences();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            FinishCastCleanup();
            HideWarning();
        }

        private void OnDestroy()
        {
            if (!ownsRuntimeWarningIndicator || warningIndicator == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(warningIndicator.gameObject);
            }
            else
            {
                DestroyImmediate(warningIndicator.gameObject);
            }
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        public void ApplyDefinition(MonsterDefinition definition)
        {
            skillsById.Clear();
            nextReadyAtBySkill.Clear();
            if (definition == null)
            {
                return;
            }

            definition.Normalize();
            for (int i = 0; i < definition.skills.Count; i++)
            {
                MonsterSkillDefinition skill = definition.skills[i];
                if (skill == null || string.IsNullOrWhiteSpace(skill.skillId))
                {
                    continue;
                }

                skillsById[skill.skillId] = skill;
                nextReadyAtBySkill[skill.skillId] = 0f;
            }
        }

        public bool CanUse(string skillId, Transform target)
        {
            if (casting || target == null || string.IsNullOrWhiteSpace(skillId) || !skillsById.ContainsKey(skillId))
            {
                return false;
            }

            return !nextReadyAtBySkill.TryGetValue(skillId, out float nextReadyAt) || Time.time >= nextReadyAt;
        }

        public bool TryUseSkill(string skillId, Transform target)
        {
            if (!CanUse(skillId, target))
            {
                return false;
            }

            MonsterSkillDefinition skill = skillsById[skillId];
            IDamageable damageable = ResolveDamageable(target);
            nextReadyAtBySkill[skill.skillId] = Time.time + skill.cooldown;
            BeginCast(skill, target, damageable);
            StartCoroutine(CastRoutine(skill, target, damageable));
            return true;
        }

        public MonsterSkillDefinition GetSkill(string skillId)
        {
            return !string.IsNullOrWhiteSpace(skillId) && skillsById.TryGetValue(skillId, out MonsterSkillDefinition skill)
                ? skill
                : null;
        }

        private void BeginCast(MonsterSkillDefinition skill, Transform target, IDamageable damageable)
        {
            casting = true;
            activeSkill = skill;
            activeTarget = target;
            lockedTarget = damageable;
            CacheAnimatorParameter(skill.animationTriggerParameter);

            if (skill.stopMovementDuringCast && movement != null)
            {
                movement.SetMovementEnabled(false);
            }

            if (skill.showWarning)
            {
                PlayWarning(skill);
            }
        }

        private IEnumerator CastRoutine(MonsterSkillDefinition skill, Transform target, IDamageable damageable)
        {
            if (skill.windup > 0f)
            {
                yield return new WaitForSeconds(skill.windup);
            }

            TriggerAnimation();

            for (int i = 0; i < skill.mechanics.Count; i++)
            {
                mechanicRunner.Run(skill.mechanics[i], target, damageable);
            }

            float tailSeconds = skill.activeDuration + skill.recovery;
            if (tailSeconds > 0f)
            {
                yield return new WaitForSeconds(tailSeconds);
            }

            FinishCastCleanup();
        }

        private void PlayWarning(MonsterSkillDefinition skill)
        {
            MonsterMechanicDefinition damageMechanic = FindDamageMechanic(skill);
            float radius = damageMechanic != null ? damageMechanic.radius : 1f;
            float heightOffset = damageMechanic != null ? damageMechanic.heightOffset : skill.warningHeightOffset;

            if (warningIndicator == null)
            {
                warningIndicator = AttackWarningIndicator.CreateRuntime("MonsterAttackWarning", null);
                ownsRuntimeWarningIndicator = true;
            }

            warningIndicator.PlayFollow(transform, Vector3.up * heightOffset, radius, Mathf.Max(0.01f, skill.windup));
        }

        private void HideWarning()
        {
            if (warningIndicator != null)
            {
                warningIndicator.Hide();
            }
        }

        private void TriggerAnimation()
        {
            if (animator == null || !animatorHasActiveTrigger)
            {
                return;
            }

            animator.ResetTrigger(activeTriggerHash);
            animator.SetTrigger(activeTriggerHash);
        }

        private void FinishCastCleanup()
        {
            bool wasCasting = casting;
            casting = false;
            activeSkill = null;
            activeTarget = null;
            lockedTarget = null;

            if (wasCasting && movement != null)
            {
                movement.SetMovementEnabled(true);
            }
        }

        private void CacheReferences()
        {
            if (movementBehaviour == null || !(movementBehaviour is IMonsterLocomotion))
            {
                MonoBehaviour[] candidates = GetComponents<MonoBehaviour>();
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (candidates[i] is IMonsterLocomotion)
                    {
                        movementBehaviour = candidates[i];
                        break;
                    }
                }
            }

            movement = movementBehaviour as IMonsterLocomotion;

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (mechanicRunner == null)
            {
                mechanicRunner = GetComponent<MonsterMechanicRunner>();
            }
        }

        private void CacheAnimatorParameter(string parameter)
        {
            activeTriggerHash = Animator.StringToHash(parameter);
            animatorHasActiveTrigger = false;
            if (animator == null || string.IsNullOrWhiteSpace(parameter))
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger
                    && parameters[i].nameHash == activeTriggerHash)
                {
                    animatorHasActiveTrigger = true;
                    return;
                }
            }
        }

        private static MonsterMechanicDefinition FindDamageMechanic(MonsterSkillDefinition skill)
        {
            if (skill == null || skill.mechanics == null)
            {
                return null;
            }

            for (int i = 0; i < skill.mechanics.Count; i++)
            {
                MonsterMechanicDefinition mechanic = skill.mechanics[i];
                if (mechanic != null && MonsterMechanicTypes.Parse(mechanic.type) == MonsterMechanicKind.DamageArea)
                {
                    return mechanic;
                }
            }

            return null;
        }

        private static IDamageable ResolveDamageable(Transform candidate)
        {
            return candidate != null ? candidate.GetComponentInParent<IDamageable>() : null;
        }
    }
}
