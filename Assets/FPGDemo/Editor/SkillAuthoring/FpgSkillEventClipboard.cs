using System;
using System.Collections.Generic;
using FPG.Demo.Skills;
using UnityEditor;
using UnityEngine;

namespace FPG.Demo.Editor.SkillAuthoring
{
    internal sealed class FpgSkillEventClipboard
    {
        private readonly List<FpgSkillEventClipboardItem> items =
            new List<FpgSkillEventClipboardItem>();

        public IReadOnlyList<FpgSkillEventClipboardItem> Items => items;
        public int Count => items.Count;
        public bool IsEmpty => items.Count == 0;
        public int TickSpan { get; private set; }

        public void Clear()
        {
            items.Clear();
            TickSpan = 0;
        }

        public void Set(
            IEnumerable<FpgSkillEventClipboardItem> nextItems,
            int tickSpan)
        {
            Clear();
            if (nextItems != null)
            {
                items.AddRange(nextItems);
            }

            TickSpan = Mathf.Max(0, tickSpan);
        }
    }

    internal sealed class FpgSkillEventClipboardItem
    {
        public FpgSkillEventTrackKind Track;
        public FpgSkillActionKind ActionKind;
        public string PresentationTrackId;
        public int RelativeTick;
        public int DurationTicks;
        public int RelativeAuthoredOrdinal;
        public string SourceEventId;
        public FpgSerializedPropertySnapshot Snapshot;
    }

    internal sealed class FpgSerializedPropertySnapshot
    {
        private readonly List<Entry> entries = new List<Entry>();

        public static FpgSerializedPropertySnapshot Capture(
            SerializedProperty root)
        {
            FpgSerializedPropertySnapshot snapshot =
                new FpgSerializedPropertySnapshot();
            if (root == null)
            {
                return snapshot;
            }

            Dictionary<string, SerializedProperty> properties =
                BuildPropertyMap(root);
            foreach (KeyValuePair<string, SerializedProperty> pair in properties)
            {
                SerializedProperty property = pair.Value;
                if (property.isArray
                    && property.propertyType != SerializedPropertyType.String)
                {
                    snapshot.entries.Add(new Entry(
                        pair.Key,
                        property.propertyType,
                        property.arraySize,
                        true));
                    continue;
                }

                if (TryReadValue(property, out object value))
                {
                    snapshot.entries.Add(new Entry(
                        pair.Key,
                        property.propertyType,
                        value,
                        false));
                }
            }

            return snapshot;
        }

        public void ApplyTo(SerializedProperty root)
        {
            if (root == null)
            {
                return;
            }

            Dictionary<string, SerializedProperty> properties =
                BuildPropertyMap(root);
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (!entry.IsArray
                    || !properties.TryGetValue(
                        entry.RelativePath,
                        out SerializedProperty property)
                    || !property.isArray)
                {
                    continue;
                }

                property.arraySize = Mathf.Max(0, Convert.ToInt32(entry.Value));
            }

            properties = BuildPropertyMap(root);
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry.IsArray
                    || !properties.TryGetValue(
                        entry.RelativePath,
                        out SerializedProperty property))
                {
                    continue;
                }

                WriteValue(property, entry.Type, entry.Value);
            }
        }

        private static Dictionary<string, SerializedProperty> BuildPropertyMap(
            SerializedProperty root)
        {
            Dictionary<string, SerializedProperty> result =
                new Dictionary<string, SerializedProperty>(StringComparer.Ordinal);
            SerializedProperty iterator = root.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            int rootDepth = root.depth;
            string rootPath = root.propertyPath;
            while (iterator.Next(true)
                && !SerializedProperty.EqualContents(iterator, end))
            {
                if (iterator.depth <= rootDepth
                    || !iterator.propertyPath.StartsWith(
                        rootPath + ".",
                        StringComparison.Ordinal))
                {
                    break;
                }

                string relativePath = iterator.propertyPath.Substring(
                    rootPath.Length + 1);
                result[relativePath] = iterator.Copy();
            }

            return result;
        }

        private static bool TryReadValue(
            SerializedProperty property,
            out object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    value = property.longValue;
                    return true;
                case SerializedPropertyType.Boolean:
                    value = property.boolValue;
                    return true;
                case SerializedPropertyType.Float:
                    value = property.doubleValue;
                    return true;
                case SerializedPropertyType.String:
                    value = property.stringValue;
                    return true;
                case SerializedPropertyType.Color:
                    value = property.colorValue;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    value = property.objectReferenceValue;
                    return true;
                case SerializedPropertyType.Enum:
                    value = property.intValue;
                    return true;
                case SerializedPropertyType.Vector2:
                    value = property.vector2Value;
                    return true;
                case SerializedPropertyType.Vector3:
                    value = property.vector3Value;
                    return true;
                case SerializedPropertyType.Vector4:
                    value = property.vector4Value;
                    return true;
                case SerializedPropertyType.Rect:
                    value = property.rectValue;
                    return true;
                case SerializedPropertyType.Bounds:
                    value = property.boundsValue;
                    return true;
                case SerializedPropertyType.Quaternion:
                    value = property.quaternionValue;
                    return true;
                case SerializedPropertyType.Vector2Int:
                    value = property.vector2IntValue;
                    return true;
                case SerializedPropertyType.Vector3Int:
                    value = property.vector3IntValue;
                    return true;
                case SerializedPropertyType.RectInt:
                    value = property.rectIntValue;
                    return true;
                case SerializedPropertyType.BoundsInt:
                    value = property.boundsIntValue;
                    return true;
                case SerializedPropertyType.AnimationCurve:
                    AnimationCurve curve = property.animationCurveValue;
                    value = curve == null
                        ? null
                        : new AnimationCurve(curve.keys);
                    return true;
                case SerializedPropertyType.ExposedReference:
                    value = property.exposedReferenceValue;
                    return true;
                case SerializedPropertyType.Hash128:
                    value = property.hash128Value;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        private static void WriteValue(
            SerializedProperty property,
            SerializedPropertyType type,
            object value)
        {
            if (property.propertyType != type)
            {
                return;
            }

            switch (type)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    property.longValue = Convert.ToInt64(value);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.Float:
                    property.doubleValue = Convert.ToDouble(value);
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value as string ?? string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = (Color)value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value as UnityEngine.Object;
                    break;
                case SerializedPropertyType.Enum:
                    property.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = (Vector2)value;
                    break;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = (Vector3)value;
                    break;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = (Vector4)value;
                    break;
                case SerializedPropertyType.Rect:
                    property.rectValue = (Rect)value;
                    break;
                case SerializedPropertyType.Bounds:
                    property.boundsValue = (Bounds)value;
                    break;
                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = (Quaternion)value;
                    break;
                case SerializedPropertyType.Vector2Int:
                    property.vector2IntValue = (Vector2Int)value;
                    break;
                case SerializedPropertyType.Vector3Int:
                    property.vector3IntValue = (Vector3Int)value;
                    break;
                case SerializedPropertyType.RectInt:
                    property.rectIntValue = (RectInt)value;
                    break;
                case SerializedPropertyType.BoundsInt:
                    property.boundsIntValue = (BoundsInt)value;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = value as AnimationCurve;
                    break;
                case SerializedPropertyType.ExposedReference:
                    property.exposedReferenceValue = value as UnityEngine.Object;
                    break;
                case SerializedPropertyType.Hash128:
                    property.hash128Value = (Hash128)value;
                    break;
            }
        }

        private readonly struct Entry
        {
            public Entry(
                string relativePath,
                SerializedPropertyType type,
                object value,
                bool isArray)
            {
                RelativePath = relativePath;
                Type = type;
                Value = value;
                IsArray = isArray;
            }

            public string RelativePath { get; }
            public SerializedPropertyType Type { get; }
            public object Value { get; }
            public bool IsArray { get; }
        }
    }
}
