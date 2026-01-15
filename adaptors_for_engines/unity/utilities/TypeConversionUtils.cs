using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mycology_ECS.Utils
{
    public static class TypeConversionUtils
    {
        public static int GetInt(Dictionary<string, object> obj, string key, int fallback)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return fallback;
            if (!obj.TryGetValue(key, out var v) || v == null) return fallback;
            try
            {
                if (v is int i) return i;
                if (v is long l) return (int)l;
                if (v is float f) return (int)f;
                if (v is double d) return (int)d;
                if (v is string s && int.TryParse(s, out var parsed)) return parsed;
            }
            catch { }
            return fallback;
        }

        public static float GetFloat(Dictionary<string, object> obj, string key, float fallback)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return fallback;
            if (!obj.TryGetValue(key, out var v) || v == null) return fallback;
            try
            {
                if (v is float f) return f;
                if (v is double d) return (float)d;
                if (v is int i) return i;
                if (v is long l) return l;
                if (v is string s && float.TryParse(s, out var parsed)) return parsed;
            }
            catch { }
            return fallback;
        }

        public static double ToDouble(object v, double fallback)
        {
            if (v == null) return fallback;
            try
            {
                if (v is double d) return d;
                if (v is float f) return f;
                if (v is int i) return i;
                if (v is long l) return l;
                if (v is string s && double.TryParse(s, out var parsed)) return parsed;
            }
            catch { }
            return fallback;
        }

        public static string GetString(Dictionary<string, object> obj, string key)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return null;
            if (!obj.TryGetValue(key, out var v) || v == null) return null;
            return v.ToString();
        }

        public static bool GetBool(Dictionary<string, object> obj, string key, bool fallback)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return fallback;
            if (!obj.TryGetValue(key, out var v) || v == null) return fallback;
            try
            {
                if (v is bool b) return b;
                if (v is int i) return i != 0;
                if (v is long l) return l != 0;
                if (v is float f) return !Mathf.Approximately(f, 0);
                if (v is double d) return Math.Abs(d) > double.Epsilon;
                if (v is string s && bool.TryParse(s, out var parsed)) return parsed;
            }
            catch { }
            return fallback;
        }

        public static Vector2 GetVector2(Dictionary<string, object> obj, string key, Vector2 fallback)
        {
            if (obj == null || string.IsNullOrWhiteSpace(key)) return fallback;
            if (!obj.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is not List<object> list || list.Count < 2) return fallback;
            return new Vector2((float)ToDouble(list[0], fallback.x), (float)ToDouble(list[1], fallback.y));
        }
    }
}
