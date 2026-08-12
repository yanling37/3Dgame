using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(float a, float b, float c) => Max(Max(a, b), c);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        public static bool Approximately(float a, float b)
        {
            return Math.Abs(a - b) < 1e-5f * Math.Max(1f, Math.Max(Math.Abs(a), Math.Abs(b)));
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; }
        public float Max { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) => Header = header;
        public string Header { get; }
    }

    public class Object
    {
    }

    public class MonoBehaviour : Object
    {
    }

    public class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
    }
}
