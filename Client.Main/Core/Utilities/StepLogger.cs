using System;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Synchronous step-by-step logger for crash diagnosis.
    /// Configure Write in the platform entry point before game start.
    /// </summary>
    public static class StepLogger
    {
        // Platform entry point overrides this with a synchronous file/logcat writer.
        public static Action<string> Write = msg =>
            System.Diagnostics.Debug.WriteLine($"[STEP] {msg}");

        public static void Log(string step)
        {
            try
            {
                Write($"{DateTime.Now:HH:mm:ss.fff} | {step}");
            }
            catch { /* never let logging crash the app */ }
        }
    }
}
