// A small, real implementation of the Unity types the project's pure-logic
// classes depend on. Unity's own reference assemblies are metadata only and
// throw when called, so they cannot be used to *run* anything.
//
// This shim exists so the algorithmic core - stamina rules, health rules, input
// buffering, animation-clip sampling, math helpers - can be executed and
// asserted on in CI without a Unity install. It is never shipped and never
// compiled into the game.
#pragma warning disable
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = 3.14159274f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Deg2Rad = PI * 2f / 360f;
        public const float Rad2Deg = 360f / (PI * 2f);
        public const float Epsilon = 1.401298E-45f;

        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Asin(float v) => (float)Math.Asin(v);
        public static float Acos(float v) => (float)Math.Acos(Clamp(v, -1f, 1f));
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sign(float v) => v >= 0f ? 1f : -1f;

        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;

        public static float InverseLerp(float a, float b, float v)
        {
            if (Math.Abs(a - b) < 1e-9f) return 0f;
            return Clamp01((v - a) / (b - a));
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Sign(target - current) * maxDelta;
        }

        public static float Repeat(float t, float length)
        {
            return Clamp(t - (float)Math.Floor(t / length) * length, 0f, length);
        }

        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }

        public static float LerpAngle(float a, float b, float t)
        {
            float delta = Repeat(b - a, 360f);
            if (delta > 180f) delta -= 360f;
            return a + delta * Clamp01(t);
        }

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = DeltaAngle(current, target);
            if (-maxDelta < delta && delta < maxDelta) return target;
            return MoveTowards(current, current + delta, maxDelta);
        }

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }

        public static bool Approximately(float a, float b)
        {
            return Math.Abs(b - a) < Max(1E-06f * Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
        }

        /// <summary>Deterministic value noise. Not Unity's exact output, but smooth and in 0..1.</summary>
        public static float PerlinNoise(float x, float y)
        {
            int xi = FloorToInt(x), yi = FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);

            float n00 = Hash(xi, yi), n10 = Hash(xi + 1, yi);
            float n01 = Hash(xi, yi + 1), n11 = Hash(xi + 1, yi + 1);

            return LerpUnclamped(LerpUnclamped(n00, n10, u), LerpUnclamped(n01, n11, u), v);
        }

        private static float Hash(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }

        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);

        public float magnitude => Mathf.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;

        public Vector2 normalized
        {
            get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : zero; }
        }

        public void Normalize() { this = normalized; }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public static Vector2 operator *(float s, Vector2 a) => a * s;
        public static Vector2 operator /(Vector2 a, float s) => new Vector2(a.x / s, a.y / s);

        public override string ToString() => $"({x:F3}, {y:F3})";
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0f; }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 back => new Vector3(0f, 0f, -1f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);
        public static Vector3 left => new Vector3(-1f, 0f, 0f);

        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;

        public Vector3 normalized
        {
            get { float m = magnitude; return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : zero; }
        }

        public void Normalize() { this = normalized; }

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x);

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new Vector3(
            a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);

        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDelta)
        {
            Vector3 d = target - current;
            float m = d.magnitude;
            if (m <= maxDelta || m < 1e-6f) return target;
            return current + d / m * maxDelta;
        }

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

        public static float Angle(Vector3 from, Vector3 to)
        {
            float denom = Mathf.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (denom < 1e-15f) return 0f;
            return Mathf.Acos(Mathf.Clamp(Dot(from, to) / denom, -1f, 1f)) * Mathf.Rad2Deg;
        }

        public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal)
        {
            float sqr = Dot(planeNormal, planeNormal);
            if (sqr < 1e-15f) return vector;
            return vector - planeNormal * (Dot(vector, planeNormal) / sqr);
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

        public override bool Equals(object o) => o is Vector3 v && this == v;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        /// <summary>Unity's ZXY intrinsic rotation order.</summary>
        public static Quaternion Euler(float xDeg, float yDeg, float zDeg)
        {
            float cx = Mathf.Cos(xDeg * Mathf.Deg2Rad * 0.5f), sx = Mathf.Sin(xDeg * Mathf.Deg2Rad * 0.5f);
            float cy = Mathf.Cos(yDeg * Mathf.Deg2Rad * 0.5f), sy = Mathf.Sin(yDeg * Mathf.Deg2Rad * 0.5f);
            float cz = Mathf.Cos(zDeg * Mathf.Deg2Rad * 0.5f), sz = Mathf.Sin(zDeg * Mathf.Deg2Rad * 0.5f);

            var qx = new Quaternion(sx, 0f, 0f, cx);
            var qy = new Quaternion(0f, sy, 0f, cy);
            var qz = new Quaternion(0f, 0f, sz, cz);
            return qy * qx * qz;
        }

        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);

        public static Quaternion AngleAxis(float angleDeg, Vector3 axis)
        {
            axis = axis.normalized;
            float h = angleDeg * Mathf.Deg2Rad * 0.5f;
            float s = Mathf.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h));
        }

        public float sqrMagnitude => x * x + y * y + z * z + w * w;

        public Quaternion normalized
        {
            get
            {
                float m = Mathf.Sqrt(sqrMagnitude);
                return m < 1e-8f ? identity : new Quaternion(x / m, y / m, z / m, w / m);
            }
        }

        public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static float Angle(Quaternion a, Quaternion b)
        {
            float d = Mathf.Min(Mathf.Abs(Dot(a.normalized, b.normalized)), 1f);
            return Mathf.Acos(d) * 2f * Mathf.Rad2Deg;
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) =>
            SlerpUnclamped(a, b, Mathf.Clamp01(t));

        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            a = a.normalized;
            b = b.normalized;
            float dot = Dot(a, b);

            if (dot < 0f)
            {
                b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
                dot = -dot;
            }

            if (dot > 0.9995f)
            {
                return new Quaternion(
                    a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t,
                    a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
            }

            float theta = Mathf.Acos(dot);
            float sin = Mathf.Sin(theta);
            float wa = Mathf.Sin((1f - t) * theta) / sin;
            float wb = Mathf.Sin(t * theta) / sin;

            return new Quaternion(
                a.x * wa + b.x * wb, a.y * wa + b.y * wb,
                a.z * wa + b.z * wb, a.w * wa + b.w * wb);
        }

        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            forward = forward.normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (right.sqrMagnitude < 1e-8f) right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 1e-8f) right = Vector3.right;
            Vector3 realUp = Vector3.Cross(forward, right);

            float m00 = right.x, m01 = realUp.x, m02 = forward.x;
            float m10 = right.y, m11 = realUp.y, m12 = forward.y;
            float m20 = right.z, m21 = realUp.z, m22 = forward.z;

            float trace = m00 + m11 + m22;
            if (trace > 0f)
            {
                float s = Mathf.Sqrt(trace + 1f) * 2f;
                return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }

            if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }

            if (m11 > m22)
            {
                float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }

            float s2 = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
            return new Quaternion((m02 + m20) / s2, (m12 + m21) / s2, 0.25f * s2, (m10 - m01) / s2);
        }

        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public Vector3 eulerAngles
        {
            get
            {
                // Inverse of the ZXY construction above.
                float sinX = 2f * (w * x - y * z);
                float ex, ey, ez;

                if (Mathf.Abs(sinX) > 0.9999f)
                {
                    ex = Mathf.Sign(sinX) * 90f;
                    ey = Mathf.Atan2(2f * (w * y + x * z), 1f - 2f * (x * x + y * y)) * Mathf.Rad2Deg;
                    ez = 0f;
                }
                else
                {
                    ex = Mathf.Asin(Mathf.Clamp(sinX, -1f, 1f)) * Mathf.Rad2Deg;
                    ey = Mathf.Atan2(2f * (w * y + x * z), 1f - 2f * (x * x + y * y)) * Mathf.Rad2Deg;
                    ez = Mathf.Atan2(2f * (w * z + x * y), 1f - 2f * (x * x + z * z)) * Mathf.Rad2Deg;
                }

                return new Vector3(Norm(ex), Norm(ey), Norm(ez));

                float Norm(float a) => a < 0f ? a + 360f : a;
            }
        }

        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x2 = q.x * 2f, y2 = q.y * 2f, z2 = q.z * 2f;
            float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2;
            float xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;

            return new Vector3(
                (1f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1f - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1f - (xx + yy)) * v.z);
        }

        public override string ToString() => $"({x:F3}, {y:F3}, {z:F3}, {w:F3})";
    }

    /// <summary>Test-controllable clock. Advance it explicitly with <see cref="Advance"/>.</summary>
    public static class Time
    {
        public static float time { get; set; }
        public static float deltaTime { get; set; } = 1f / 60f;
        public static float unscaledDeltaTime { get; set; } = 1f / 60f;
        public static float unscaledTime { get; set; }
        public static float timeScale { get; set; } = 1f;
        public static float fixedDeltaTime { get; set; } = 0.02f;

        public static void Reset()
        {
            time = 0f;
            unscaledTime = 0f;
            deltaTime = 1f / 60f;
            unscaledDeltaTime = 1f / 60f;
            timeScale = 1f;
        }

        public static void Advance(float seconds)
        {
            deltaTime = seconds;
            unscaledDeltaTime = seconds;
            time += seconds;
            unscaledTime += seconds;
        }
    }

    public static class Debug
    {
        public static readonly List<string> Messages = new List<string>();
        public static void Log(object m, object ctx = null) => Messages.Add("LOG: " + m);
        public static void LogWarning(object m, object ctx = null) => Messages.Add("WARN: " + m);
        public static void LogError(object m, object ctx = null) => Messages.Add("ERROR: " + m);
    }

    public static class Random
    {
        private static System.Random _rng = new System.Random(12345);
        public static void InitState(int seed) => _rng = new System.Random(seed);
        public static float value => (float)_rng.NextDouble();
        public static float Range(float a, float b) => a + (b - a) * value;
        public static int Range(int a, int b) => _rng.Next(a, b);
    }

    // ---- object model ---------------------------------------------------------

    public class Object
    {
        public string name = string.Empty;
        public override string ToString() => name;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);

        // Destruction is a no-op under test: nothing here owns native resources,
        // and tests assert on state rather than on object lifetime.
        public static void Destroy(Object o) { }
        public static void Destroy(Object o, float delay) { }
        public static void DestroyImmediate(Object o) { }
    }

    public class Transform : Object
    {
        public Vector3 position = Vector3.zero;
        public Vector3 localPosition = Vector3.zero;
        public Quaternion rotation = Quaternion.identity;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
        public Transform parent;
        private readonly List<Transform> _children = new List<Transform>();

        public int childCount => _children.Count;
        public Transform GetChild(int i) => _children[i];

        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 right => rotation * Vector3.right;

        public Vector3 eulerAngles => rotation.eulerAngles;

        public void SetParent(Transform p, bool worldPositionStays = true)
        {
            parent?._children.Remove(this);
            parent = p;
            p?._children.Add(this);
        }

        public Vector3 InverseTransformDirection(Vector3 v) => Quaternion.Inverse(rotation) * v;
        public Vector3 TransformDirection(Vector3 v) => rotation * v;
    }

    public class Component : Object
    {
        public Transform transform { get; internal set; } = new Transform();
        public GameObject gameObject { get; internal set; }
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled;
    }

    public class MonoBehaviour : Behaviour
    {
        public T GetComponent<T>() where T : class => gameObject?.GetComponent<T>();
    }

    public class GameObject : Object
    {
        private readonly List<Component> _components = new List<Component>();
        public Transform transform { get; } = new Transform();
        public int layer;
        public string tag = "Untagged";
        public bool activeSelf = true;

        public GameObject(string n = "GameObject") { name = n; }

        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this, transform = transform, name = typeof(T).Name };
            _components.Add(c);
            return c;
        }

        public T GetComponent<T>() where T : class
        {
            for (int i = 0; i < _components.Count; i++)
                if (_components[i] is T t) return t;
            return null;
        }

        public void SetActive(bool v) => activeSelf = v;
    }

    // ---- physics (enough for the helpers that query the world) ------------------

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 o, Vector3 d) { origin = o; direction = d.normalized; }
    }

    public struct RaycastHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
    }

    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }

    /// <summary>
    /// Empty world: every query misses. Logic under test must behave sensibly
    /// when the physics scene has nothing in it, which is exactly what we want
    /// to assert.
    /// </summary>
    public static class Physics
    {
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit,
            float maxDistance, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal)
        {
            hit = default;
            return false;
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask,
            QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => false;
    }

    // ---- attributes ------------------------------------------------------------

    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HideInInspector : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
    public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class RequireComponent : Attribute { public RequireComponent(Type t) { } }

    [AttributeUsage(AttributeTargets.Method)]
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad, BeforeSceneLoad, AfterAssembliesLoaded,
        BeforeSplashScreen, SubsystemRegistration
    }
}
