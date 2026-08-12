using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(float a, float b, float c) => Max(Max(a, b), c);
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int value) => Math.Abs(value);
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

        public static float Exp(float value) => (float)Math.Exp(value);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Sqrt(float value) => (float)Math.Sqrt(value);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
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
        public static void LogError(object message) => Console.Error.WriteLine(message);
    }
}
