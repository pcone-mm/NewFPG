using System;
using UnityEngine;

namespace NewFPG.Monsters
{
    [Obsolete("旧怪物 AI 入口已停用。新怪物 AI 请在 Behavior Designer 行为树中配置，并通过 MonsterConfigBinding 提供的原子方法执行。")]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class MonsterBrain : MonoBehaviour
    {
        public void ApplyDefinition(MonsterAiDefinition definition)
        {
            // 保留空方法是为了兼容旧资源上的序列化引用；新 AI 不再从这里读取或执行。
        }
    }
}
