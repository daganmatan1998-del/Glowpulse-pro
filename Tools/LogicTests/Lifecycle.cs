using System;
using System.Reflection;
using UnityEngine;

namespace Glowpulse.LogicTests
{
    /// <summary>
    /// Drives Unity's message methods by hand. The engine normally calls Awake
    /// and Update for you; under test we call them explicitly so the clock and
    /// the number of ticks are fully controlled.
    /// </summary>
    public static class Lifecycle
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;

        public static T Create<T>(Action<T> configure = null) where T : Component, new()
        {
            var go = new GameObject(typeof(T).Name);
            T component = go.AddComponent<T>();
            configure?.Invoke(component);
            Invoke(component, "Awake");
            return component;
        }

        public static void Invoke(object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, Flags, null, Type.EmptyTypes, null);
            m?.Invoke(target, null);
        }

        /// <summary>Advances the clock in fixed steps, calling Update each step.</summary>
        public static void Tick(object target, float seconds, float step = 1f / 60f)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / step));
            for (int i = 0; i < steps; i++)
            {
                Time.Advance(step);
                Invoke(target, "Update");
            }
        }

        /// <summary>Reads a private serialized field, for asserting on internal state.</summary>
        public static object Field(object target, string name)
        {
            FieldInfo f = target.GetType().GetField(name, Flags);
            return f?.GetValue(target);
        }

        public static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, Flags);
            f?.SetValue(target, value);
        }
    }
}
