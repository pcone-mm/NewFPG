using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace NewFPG.CZN.Editor
{
    internal static class CznPlistReader
    {
        public static Dictionary<string, object> ReadDictionary(string absolutePath)
        {
            XDocument document = XDocument.Load(absolutePath, LoadOptions.None);
            XElement plist = document.Root;
            XElement dictionary = plist != null ? plist.Element("dict") : null;
            if (dictionary == null)
            {
                throw new FormatException("The plist does not contain a root dict: " + absolutePath);
            }

            return ParseDictionary(dictionary);
        }

        public static string String(Dictionary<string, object> owner, string key, string fallback = "")
        {
            if (owner == null || !owner.TryGetValue(key, out object value) || value == null)
            {
                return fallback;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
        }

        public static float Float(Dictionary<string, object> owner, string key, float fallback = 0f)
        {
            string value = String(owner, key, string.Empty);
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public static int Integer(Dictionary<string, object> owner, string key, int fallback = 0)
        {
            string value = String(owner, key, string.Empty);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public static bool Boolean(Dictionary<string, object> owner, string key, bool fallback = false)
        {
            if (owner == null || !owner.TryGetValue(key, out object value) || value == null)
            {
                return fallback;
            }

            if (value is bool flag)
            {
                return flag;
            }

            if (bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out bool parsed))
            {
                return parsed;
            }

            return fallback;
        }

        public static List<object> Array(Dictionary<string, object> owner, string key)
        {
            if (owner != null && owner.TryGetValue(key, out object value) && value is List<object> list)
            {
                return list;
            }

            return new List<object>();
        }

        private static Dictionary<string, object> ParseDictionary(XElement dictionary)
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            List<XElement> elements = new List<XElement>(dictionary.Elements());
            for (int i = 0; i < elements.Count; i++)
            {
                XElement keyElement = elements[i];
                if (keyElement.Name.LocalName != "key")
                {
                    continue;
                }

                string key = keyElement.Value;
                if (i + 1 >= elements.Count)
                {
                    result[key] = null;
                    break;
                }

                result[key] = ParseValue(elements[++i]);
            }

            return result;
        }

        private static object ParseValue(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "dict":
                    return ParseDictionary(element);
                case "array":
                    List<object> values = new List<object>();
                    foreach (XElement child in element.Elements())
                    {
                        values.Add(ParseValue(child));
                    }
                    return values;
                case "true":
                    return true;
                case "false":
                    return false;
                case "integer":
                    return long.TryParse(element.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)
                        ? integer
                        : 0L;
                case "real":
                    return double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real)
                        ? real
                        : 0d;
                case "data":
                case "date":
                case "string":
                default:
                    return element.Value;
            }
        }
    }
}
