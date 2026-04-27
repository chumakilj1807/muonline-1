using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Main.Core.Utilities
{
    public static class StepLogger
    {
        // Set to false to disable ALL logging (zero overhead in production)
        public static bool Enabled = true;

        // Set to upload URL to enable automatic log push, e.g. "http://192.168.1.222:8765/log"
        // Set to null to disable upload
        public static string UploadUrl = null;

        // How often to push the log to the server (seconds)
        public static int UploadIntervalSeconds = 30;

        // Platform entry point overrides this with a synchronous file writer
        public static Action<string> Write = msg =>
            System.Diagnostics.Debug.WriteLine($"[STEP] {msg}");

        // Internal: path to the log file (set by platform to enable upload reads)
        public static string LogFilePath = null;

        private static Timer _uploadTimer;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        public static void Log(string step)
        {
            if (!Enabled) return;
            try
            {
                Write($"{DateTime.Now:HH:mm:ss.fff} | {step}");
            }
            catch { }
        }

        // Call once from platform code after configuring Write and LogFilePath
        public static void StartUpload()
        {
            if (UploadUrl == null || LogFilePath == null) return;
            _uploadTimer?.Dispose();
            _uploadTimer = new Timer(_ => _ = PushAsync(), null,
                TimeSpan.FromSeconds(UploadIntervalSeconds),
                TimeSpan.FromSeconds(UploadIntervalSeconds));
        }

        public static void StopUpload()
        {
            _uploadTimer?.Dispose();
            _uploadTimer = null;
        }

        // Push immediately (also callable manually)
        public static async Task PushAsync()
        {
            if (UploadUrl == null || LogFilePath == null) return;
            try
            {
                string content;
                // Read with shared access so the log file can still be written
                using (var fs = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                    content = await sr.ReadToEndAsync();

                var payload = new StringContent(content, Encoding.UTF8, "text/plain");
                await _http.PostAsync(UploadUrl, payload);
            }
            catch { /* never crash the app on upload failure */ }
        }
    }
}
