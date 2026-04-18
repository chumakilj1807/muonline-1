using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Client.Data.Texture;
using Client.Main;
using Client.Main.Content;
using Client.Main.Controls.UI;
using System;
using System.IO;
using AndroidGameActivity = Microsoft.Xna.Framework.AndroidGameActivity;

namespace MuAndroid
{
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@mipmap/ic_launcher",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.Landscape,
        ConfigurationChanges =
            ConfigChanges.Orientation |
            ConfigChanges.Keyboard |
            ConfigChanges.KeyboardHidden |
            ConfigChanges.ScreenSize)]
    public class MainActivity : AndroidGameActivity
    {
        private Client.Main.MuGame _game;
        private View _view;

        /// <summary>
        /// Gets the current MainActivity instance.
        /// </summary>
        public static MainActivity Instance { get; private set; }

        /// <summary>
        /// Actual screen width in pixels (set before game creation).
        /// </summary>
        public static int ScreenWidth { get; private set; }

        /// <summary>
        /// Actual screen height in pixels (set before game creation).
        /// </summary>
        public static int ScreenHeight { get; private set; }

        const int RequestWrite = 101;

        private void RequestLegacyWritePermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M &&
                Build.VERSION.SdkInt <= BuildVersionCodes.P &&
                CheckSelfPermission(Manifest.Permission.WriteExternalStorage)
                    != Permission.Granted)
            {
                RequestPermissions(
                    new[] { Manifest.Permission.WriteExternalStorage },
                    RequestWrite);
            }
        }

        /// <summary>
        /// Gets the real screen size, accounting for navigation bars, notches, etc.
        /// </summary>
        private void DetermineScreenSize()
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R) // Android 11+
                {
                    var windowMetrics = WindowManager.CurrentWindowMetrics;
                    var bounds = windowMetrics.Bounds;
                    ScreenWidth = bounds.Width();
                    ScreenHeight = bounds.Height();
                }
                else
                {
                    var displayMetrics = new DisplayMetrics();

                    // Try to get real metrics (includes nav bar, status bar areas)
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr1)
                    {
                        WindowManager.DefaultDisplay.GetRealMetrics(displayMetrics);
                    }
                    else
                    {
                        WindowManager.DefaultDisplay.GetMetrics(displayMetrics);
                    }

                    ScreenWidth = displayMetrics.WidthPixels;
                    ScreenHeight = displayMetrics.HeightPixels;
                }

                // Ensure landscape orientation (width > height)
                if (ScreenHeight > ScreenWidth)
                {
                    (ScreenWidth, ScreenHeight) = (ScreenHeight, ScreenWidth);
                }

                Android.Util.Log.Info("MuAndroid", $"Screen size determined: {ScreenWidth}x{ScreenHeight}");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("MuAndroid", $"Failed to determine screen size: {ex.Message}");
                // Fallback
                ScreenWidth = 1920;
                ScreenHeight = 1080;
            }
        }

        private static string EnsureAndroidConfig()
        {
            var ctx = Application.Context!;
            var dst = Path.Combine(ctx.FilesDir!.AbsolutePath, "appsettings.json");

            if (!File.Exists(dst))
            {
                try
                {
                    using var src = ctx.Assets!.Open("appsettings.json");
                    using var trg = File.Create(dst);
                    src.CopyTo(trg);
                }
                catch (Exception copyEx)
                {
                    Android.Util.Log.Error("MuGame", "Cannot copy appsettings.json: " + copyEx);
                }
            }
            return dst;
        }

        // Builds a full human-readable crash report, walking the entire inner-exception chain.
        private static string BuildExceptionReport(string title, Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== {title} ===");
            sb.AppendLine($"Time   : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Device : {Build.Manufacturer} {Build.Model}  Android {Build.VERSION.Release}");
            sb.AppendLine($"ABI    : {string.Join(",", Build.SupportedAbis ?? Array.Empty<string>())}");

            if (ex == null)
            {
                sb.AppendLine("(no exception object)");
            }
            else
            {
                int depth = 0;
                var current = ex;
                while (current != null)
                {
                    string prefix = depth == 0 ? "" : $"[Inner #{depth}] ";
                    sb.AppendLine($"{prefix}Type   : {current.GetType().FullName}");
                    sb.AppendLine($"{prefix}Message: {current.Message}");
                    if (!string.IsNullOrEmpty(current.StackTrace))
                    {
                        sb.AppendLine($"{prefix}Stack  :");
                        sb.AppendLine(current.StackTrace);
                    }
                    if (current is System.AggregateException agg)
                    {
                        foreach (var inner in agg.InnerExceptions)
                        {
                            sb.AppendLine("--- Aggregate inner ---");
                            sb.AppendLine(BuildExceptionReport("inner", inner));
                        }
                        break;
                    }
                    current = current.InnerException;
                    depth++;
                    if (depth > 10) { sb.AppendLine("... (too many nested exceptions)"); break; }
                }
            }

            sb.AppendLine(new string('-', 60));
            return sb.ToString();
        }

        // Appends text to the current session log file.
        // File: /storage/emulated/0/Download/MuAndroid_log_YYYYMMDD.txt
        // Falls back to app internal storage if external is unavailable.
        private static string SaveCrashLog(string text)
        {
            Android.Util.Log.Error("MuAndroidCrash", text);

            var ctx = Application.Context!;
            var date = DateTime.Now.ToString("yyyyMMdd");
            var name = $"MuAndroid_log_{date}.txt";

            // Try external Downloads first
            try
            {
                var dirPath = Android.OS.Environment
                    .GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)
                    .AbsolutePath;
                Directory.CreateDirectory(dirPath);
                var filePath = Path.Combine(dirPath, name);
                File.AppendAllText(filePath, text + System.Environment.NewLine);
                return filePath;
            }
            catch { /* fall through */ }

            // Android 10+: use MediaStore
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                try
                {
                    var values = new ContentValues();
                    values.Put(MediaStore.IMediaColumns.DisplayName, name);
                    values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
                    values.Put(MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads);
                    var uri = ctx.ContentResolver!.Insert(MediaStore.Downloads.ExternalContentUri, values);
                    if (uri != null)
                    {
                        using var stream = ctx.ContentResolver.OpenOutputStream(uri)!;
                        using var sw = new System.IO.StreamWriter(stream);
                        sw.Write(text);
                        return $"/storage/emulated/0/Download/{name}";
                    }
                }
                catch { /* fall through */ }
            }

            // Last resort: app-private files dir (readable via adb pull)
            try
            {
                var internalPath = Path.Combine(ctx.FilesDir!.AbsolutePath, name);
                File.AppendAllText(internalPath, text + System.Environment.NewLine);
                return internalPath;
            }
            catch { return "LOG_WRITE_FAILED"; }
        }
        private void ApplyAndroidDefaults()
        {
            Constants.DRAW_GRASS = false;
            Constants.ENABLE_DYNAMIC_LIGHTS = false;
            Constants.ENABLE_DYNAMIC_LIGHTING_SHADER = true;
            Constants.ENABLE_TERRAIN_GPU_LIGHTING = false;
            Constants.OPTIMIZE_FOR_INTEGRATED_GPU = true;
            Constants.ENABLE_LOW_QUALITY_IN_LOGIN_SCENE = true;
            Constants.ENABLE_ITEM_MATERIAL_SHADER = true;
            Constants.ENABLE_MONSTER_MATERIAL_SHADER = true;
            Constants.ENABLE_WEAPON_TRAIL = false;
            Constants.HIGH_QUALITY_TEXTURES = false;
            Constants.RENDER_SCALE = 0.75f;
            Constants.DYNAMIC_LIGHT_UPDATE_FPS = 30;
            Constants.FOV_SCALE = 0.8f;
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            Constants.SETTINGS_PATH = EnsureAndroidConfig();
            TextFieldControl.ControlType = typeof(AndroidTextFieldControl);

            ApplyAndroidDefaults();
            Instance = this;
            AndroidKeyboard.Activity = this;

            TextureLoader.Instance.CustomDecompressFunction = (textureInfo) =>
            {
                byte[] decompressedData = null;

                if (textureInfo.Format == TextureSurfaceFormat.Dxt1)
                    decompressedData = DxtDecoder.DecompressDXT1(textureInfo.Data, textureInfo.Width, textureInfo.Height);
                else if (textureInfo.Format == TextureSurfaceFormat.Dxt3)
                    decompressedData = DxtDecoder.DecompressDXT3(textureInfo.Data, textureInfo.Width, textureInfo.Height);
                else if (textureInfo.Format == TextureSurfaceFormat.Dxt5)
                    decompressedData = DxtDecoder.DecompressDXT5(textureInfo.Data, textureInfo.Width, textureInfo.Height);
                else
                    throw new NotSupportedException("Unsupported DXT format for decompression.");

                return decompressedData;
            };

            // Set window flags that don't require InsetsController
            Window.AddFlags(WindowManagerFlags.KeepScreenOn);
            Window.AddFlags(WindowManagerFlags.Fullscreen);
            RequestLegacyWritePermission();

            // IMPORTANT: Determine screen size BEFORE creating the game
            DetermineScreenSize();

            // --- Crash / error handlers (catch .NET exceptions before Android kills the process) ---
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                SaveCrashLog(BuildExceptionReport("UNHANDLED EXCEPTION", ex));
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                SaveCrashLog(BuildExceptionReport("UNOBSERVED TASK EXCEPTION", e.Exception));
                e.SetObserved();
            };

            AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            {
                SaveCrashLog(BuildExceptionReport("ANDROID UNHANDLED EXCEPTION", e.Exception));
                e.Handled = true;
            };

            try
            {
                var startMsg =
                    $"=== MuAndroid Session Start ===\n" +
                    $"Time       : {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Android    : {Build.VERSION.SdkInt} ({Build.VERSION.Release})\n" +
                    $"Device     : {Build.Manufacturer} {Build.Model}\n" +
                    $"ABI        : {string.Join(",", Build.SupportedAbis ?? Array.Empty<string>())}\n" +
                    $"Screen     : {ScreenWidth}x{ScreenHeight}\n" +
                    $"Runtime    : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}\n";
                SaveCrashLog(startMsg);
                Android.Util.Log.Info("MuAndroid", startMsg);

                _game = new Client.Main.MuGame();

                if (!Directory.Exists(Client.Main.Constants.DataPath))
                    Directory.CreateDirectory(Client.Main.Constants.DataPath);

                _view = (View)_game.Services.GetService(typeof(View));
                SetContentView(_view);

                // IMPORTANT: Configure immersive mode AFTER SetContentView
                // This ensures the DecorView is fully initialized
                HideSystemUI();

                _game.Run();
            }
            catch (Exception ex)
            {
                var report = BuildExceptionReport("STARTUP EXCEPTION", ex);
                var path = SaveCrashLog(report);
                Android.Util.Log.Error("MuAndroidCrash", $"Startup failed! Log: {path}\n{report}");
                throw;
            }
        }

        /// <summary>
        /// Configures immersive fullscreen mode. Must be called after SetContentView.
        /// </summary>
        private void HideSystemUI()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                Window.SetDecorFitsSystemWindows(false);

                // Try to get controller from view first, then window
                var controller = _view?.WindowInsetsController ?? Window.InsetsController;

                if (controller != null)
                {
                    controller.Hide(WindowInsets.Type.SystemBars());
                    controller.SystemBarsBehavior =
                        (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                }
            }
            else
            {
#pragma warning disable CS0618 // Obsolete for newer Android versions
                Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                    SystemUiFlags.ImmersiveSticky |
                    SystemUiFlags.Fullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.LayoutStable |
                    SystemUiFlags.LayoutHideNavigation |
                    SystemUiFlags.LayoutFullscreen);
#pragma warning restore CS0618
            }
        }

        public override void OnRequestPermissionsResult(int req, string[] p, Permission[] res)
            => base.OnRequestPermissionsResult(req, p, res);

        /// <summary>
        /// Intercept key events from soft keyboard and forward to AndroidKeyboard.
        /// </summary>
        public override bool OnKeyDown(Keycode keyCode, KeyEvent e)
        {
            // Convert Android Keycode to character
            char character = GetCharacterFromKeyEvent(keyCode, e);

            // Convert Android Keycode to MonoGame Keys
            var mgKey = ConvertToMonoGameKeys(keyCode);

            // Debug logging
            Android.Util.Log.Debug("MainActivity", $"OnKeyDown: keyCode={keyCode}, char='{character}' (0x{(int)character:X2}), mgKey={mgKey}");

            // Raise event for TextFieldControl
            AndroidKeyboard.RaiseTextInput(character, mgKey);

            // Let the base handle system keys (back, home, etc.)
            return base.OnKeyDown(keyCode, e);
        }

        private static char GetCharacterFromKeyEvent(Keycode keyCode, KeyEvent e)
        {
            // Handle special keys first (they don't return valid unicode chars)
            switch (keyCode)
            {
                case Keycode.Del:
                    return '\b';        // Backspace (ASCII 8)
                case Keycode.Enter:
                    return '\r';        // Enter (ASCII 13)
                case Keycode.Space:
                    return ' ';         // Space (ASCII 32)
            }

            // Use KeyCharacterMap to get the actual character for regular keys
            var unicodeChar = e.GetUnicodeChar(e.MetaState);
            if (unicodeChar > 0 && unicodeChar < 127) // Valid ASCII
            {
                return (char)unicodeChar;
            }

            return '\0'; // Unknown/unsupported key
        }

        private Microsoft.Xna.Framework.Input.Keys ConvertToMonoGameKeys(Keycode keyCode)
        {
            return keyCode switch
            {
                Keycode.A => Microsoft.Xna.Framework.Input.Keys.A,
                Keycode.B => Microsoft.Xna.Framework.Input.Keys.B,
                Keycode.C => Microsoft.Xna.Framework.Input.Keys.C,
                Keycode.D => Microsoft.Xna.Framework.Input.Keys.D,
                Keycode.E => Microsoft.Xna.Framework.Input.Keys.E,
                Keycode.F => Microsoft.Xna.Framework.Input.Keys.F,
                Keycode.G => Microsoft.Xna.Framework.Input.Keys.G,
                Keycode.H => Microsoft.Xna.Framework.Input.Keys.H,
                Keycode.I => Microsoft.Xna.Framework.Input.Keys.I,
                Keycode.J => Microsoft.Xna.Framework.Input.Keys.J,
                Keycode.K => Microsoft.Xna.Framework.Input.Keys.K,
                Keycode.L => Microsoft.Xna.Framework.Input.Keys.L,
                Keycode.M => Microsoft.Xna.Framework.Input.Keys.M,
                Keycode.N => Microsoft.Xna.Framework.Input.Keys.N,
                Keycode.O => Microsoft.Xna.Framework.Input.Keys.O,
                Keycode.P => Microsoft.Xna.Framework.Input.Keys.P,
                Keycode.Q => Microsoft.Xna.Framework.Input.Keys.Q,
                Keycode.R => Microsoft.Xna.Framework.Input.Keys.R,
                Keycode.S => Microsoft.Xna.Framework.Input.Keys.S,
                Keycode.T => Microsoft.Xna.Framework.Input.Keys.T,
                Keycode.U => Microsoft.Xna.Framework.Input.Keys.U,
                Keycode.V => Microsoft.Xna.Framework.Input.Keys.V,
                Keycode.W => Microsoft.Xna.Framework.Input.Keys.W,
                Keycode.X => Microsoft.Xna.Framework.Input.Keys.X,
                Keycode.Y => Microsoft.Xna.Framework.Input.Keys.Y,
                Keycode.Z => Microsoft.Xna.Framework.Input.Keys.Z,
                Keycode.Num0 => Microsoft.Xna.Framework.Input.Keys.D0,
                Keycode.Num1 => Microsoft.Xna.Framework.Input.Keys.D1,
                Keycode.Num2 => Microsoft.Xna.Framework.Input.Keys.D2,
                Keycode.Num3 => Microsoft.Xna.Framework.Input.Keys.D3,
                Keycode.Num4 => Microsoft.Xna.Framework.Input.Keys.D4,
                Keycode.Num5 => Microsoft.Xna.Framework.Input.Keys.D5,
                Keycode.Num6 => Microsoft.Xna.Framework.Input.Keys.D6,
                Keycode.Num7 => Microsoft.Xna.Framework.Input.Keys.D7,
                Keycode.Num8 => Microsoft.Xna.Framework.Input.Keys.D8,
                Keycode.Num9 => Microsoft.Xna.Framework.Input.Keys.D9,
                Keycode.Space => Microsoft.Xna.Framework.Input.Keys.Space,
                Keycode.Del => Microsoft.Xna.Framework.Input.Keys.Back,
                Keycode.Enter => Microsoft.Xna.Framework.Input.Keys.Enter,
                _ => Microsoft.Xna.Framework.Input.Keys.None
            };
        }
    }
}