using System;
using System.Collections.Generic;
using UnityEngine;

namespace Glowpulse.LogicTests
{
    /// <summary>Minimal assertion + test runner, so the suite has no external dependencies.</summary>
    public static class Check
    {
        private static readonly List<string> Failures = new List<string>();
        private static string _currentTest = "?";
        private static int _testCount;
        private static int _assertCount;

        public static void Run(string name, Action body)
        {
            _currentTest = name;
            _testCount++;
            int before = Failures.Count;

            try
            {
                Time.Reset();
                body();
            }
            catch (Exception e)
            {
                // Anything invoked by reflection arrives wrapped; report the real one.
                while (e is System.Reflection.TargetInvocationException && e.InnerException != null)
                    e = e.InnerException;

                string where = e.StackTrace != null ? e.StackTrace.Split('\n')[0].Trim() : "";
                Failures.Add($"{name}: threw {e.GetType().Name}: {e.Message}  {where}");
            }

            Console.WriteLine((Failures.Count == before ? "  PASS  " : "  FAIL  ") + name);
        }

        public static int Report()
        {
            Console.WriteLine();
            Console.WriteLine($"{_testCount} tests, {_assertCount} assertions, {Failures.Count} failures.");

            if (Failures.Count == 0) return 0;

            Console.WriteLine();
            foreach (string f in Failures) Console.WriteLine("  x " + f);
            return 1;
        }

        public static void True(bool condition, string what)
        {
            _assertCount++;
            if (!condition) Failures.Add($"{_currentTest}: expected true - {what}");
        }

        public static void False(bool condition, string what) => True(!condition, what);

        public static void Near(float actual, float expected, string what, float tolerance = 1e-3f)
        {
            _assertCount++;
            if (Mathf.Abs(actual - expected) > tolerance)
                Failures.Add($"{_currentTest}: {what} - expected {expected:F4}, got {actual:F4}");
        }

        public static void Near(Vector3 actual, Vector3 expected, string what, float tolerance = 1e-3f)
        {
            _assertCount++;
            if ((actual - expected).magnitude > tolerance)
                Failures.Add($"{_currentTest}: {what} - expected {expected}, got {actual}");
        }

        public static void Equal(int actual, int expected, string what)
        {
            _assertCount++;
            if (actual != expected)
                Failures.Add($"{_currentTest}: {what} - expected {expected}, got {actual}");
        }

        public static void Equal(string actual, string expected, string what)
        {
            _assertCount++;
            if (actual != expected)
                Failures.Add($"{_currentTest}: {what} - expected '{expected}', got '{actual}'");
        }

        public static void Less(float a, float b, string what)
        {
            _assertCount++;
            if (!(a < b)) Failures.Add($"{_currentTest}: {what} - expected {a:F4} < {b:F4}");
        }

        public static void Greater(float a, float b, string what)
        {
            _assertCount++;
            if (!(a > b)) Failures.Add($"{_currentTest}: {what} - expected {a:F4} > {b:F4}");
        }

        public static void InRange(float v, float lo, float hi, string what)
        {
            _assertCount++;
            if (v < lo || v > hi)
                Failures.Add($"{_currentTest}: {what} - expected {v:F4} within [{lo:F4}, {hi:F4}]");
        }
    }
}
