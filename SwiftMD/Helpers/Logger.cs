using System;
using System.Diagnostics;

namespace ReadU.Helpers
{
    public static class Logger
    {
        public static void LogInfo(string message)
        {
            Debug.WriteLine($"[INFO] {message}");
        }

        public static void LogWarning(string message)
        {
            Debug.WriteLine($"[WARN] {message}");
        }

        public static void LogError(string message)
        {
            Debug.WriteLine($"[ERROR] {message}");
        }

        public static void LogError(string message, Exception ex)
        {
            Debug.WriteLine($"[ERROR] {message}: {ex}");
        }
    }
}
