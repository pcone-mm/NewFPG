using System;

namespace FPG.Demo.Unity
{
    /// <summary>
    /// 为 D0 策划资产保留底层序列化字段名，同时提供 Inspector 中使用的中文字段名和说明。
    /// 该属性只描述编辑器展示，不参与运行时配置、战斗计算或 YAML 键名。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class D0PlannerFieldAttribute : Attribute
    {
        public D0PlannerFieldAttribute(string displayName, string tooltip)
        {
            DisplayName = displayName;
            Tooltip = tooltip;
        }

        public string DisplayName { get; }

        public string Tooltip { get; }
    }

    /// <summary>
    /// 定义 D0 策划 Inspector 中的中文分组标题。分组仅改善编辑体验，不改变字段顺序或序列化结构。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class D0PlannerSectionAttribute : Attribute
    {
        public D0PlannerSectionAttribute(string title)
        {
            Title = title;
        }

        public string Title { get; }
    }

    /// <summary>
    /// 标记仍需保存、但不应出现在 D0 策划 Inspector 中的工程字段。字段和 YAML 键保持不变，
    /// 仅由程序在配套技术容量或物理实现变更时维护。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class D0PlannerTechnicalFieldAttribute : Attribute
    {
        public D0PlannerTechnicalFieldAttribute(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }
}
