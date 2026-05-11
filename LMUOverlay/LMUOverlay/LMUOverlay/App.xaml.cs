using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;

namespace LMUOverlay
{
    public partial class App : Application
    {
        // Crash log: %APPDATA%\DouzeAssistance\crash.log
        private static readonly string CrashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DouzeAssistance", "crash.log");

        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            TaskScheduler.UnobservedTaskException += OnTaskException;

            // Initialiser le système de thèmes (crée le fichier par défaut si absent, charge Endurance Noir)
            try
            {
                Helpers.ThemeManager.EnsureDefaultThemeExists();
                Helpers.ThemeManager.Load("endurance-noir");
            }
            catch (Exception tex)
            {
                WriteCrashLog($"[STARTUP] ThemeManager init error: {tex.Message}\n");
            }

            // Write startup marker so we know the app launched
            WriteCrashLog($"[STARTUP] Douze Assistance démarré — {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                          $"  OS: {Environment.OSVersion}\n" +
                          $"  .NET: {Environment.Version}\n" +
                          $"  CPU: {Environment.ProcessorCount} cores\n" +
                          $"  Screen: {System.Windows.SystemParameters.PrimaryScreenWidth}x{System.Windows.SystemParameters.PrimaryScreenHeight}\n");

            base.OnStartup(e);
        }

        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteCrashLog(FormatError("UI Thread Exception", e.Exception));
            ShowError("UI Thread Exception", e.Exception);
            e.Handled = true;
        }

        private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                // Write synchronously FIRST — process may terminate immediately after
                WriteCrashLog(FormatError($"Domain Exception (IsTerminating={e.IsTerminating})", ex));
                // Attempt to show dialog (may not appear if IsTerminating=true)
                try
                {
                    Dispatcher?.Invoke(() => ShowError("Domain Exception", ex));
                }
                catch { /* process terminating, best effort */ }
            }
        }

        private void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Mark as observed unconditionally to prevent app crash
            e.SetObserved();

            // Network / IO errors in fire-and-forget tasks (leaderboard, updater, etc.)
            // are transient and non-actionable — just log them silently.
            if (IsNetworkException(e.Exception))
            {
                System.Diagnostics.Debug.WriteLine($"[Task] Erreur réseau ignorée : {e.Exception.InnerException?.Message ?? e.Exception.Message}");
                return;
            }

            // Anything else is unexpected — log and show.
            WriteCrashLog(FormatError("Task Exception", e.Exception));
            ShowError("Task Exception", e.Exception);
        }

        // Returns true if all inner exceptions are network/cancellation related
        private static bool IsNetworkException(AggregateException agg)
        {
            foreach (var inner in agg.Flatten().InnerExceptions)
            {
                if (inner is IOException
                 || inner is SocketException
                 || inner is HttpRequestException
                 || inner is OperationCanceledException
                 || inner is TaskCanceledException)
                    continue;

                return false; // at least one non-network exception
            }
            return true;
        }

        private static string FormatError(string context, Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}]");
            sb.AppendLine($"  Type   : {ex.GetType().FullName}");
            sb.AppendLine($"  Message: {ex.Message}");
            sb.AppendLine($"  Stack  :\n{ex.StackTrace}");
            if (ex.InnerException != null)
            {
                sb.AppendLine($"  --- Inner ---");
                sb.AppendLine($"  Type   : {ex.InnerException.GetType().FullName}");
                sb.AppendLine($"  Message: {ex.InnerException.Message}");
                sb.AppendLine($"  Stack  :\n{ex.InnerException.StackTrace}");
            }
            sb.AppendLine();
            return sb.ToString();
        }

        private static void ShowError(string context, Exception ex)
        {
            string msg = $"[{context}]\n\n"
                       + $"Type: {ex.GetType().Name}\n"
                       + $"Message: {ex.Message}\n\n"
                       + $"Stack Trace:\n{ex.StackTrace}";

            if (ex.InnerException != null)
                msg += $"\n\n--- Inner Exception ---\n"
                     + $"Type: {ex.InnerException.GetType().Name}\n"
                     + $"Message: {ex.InnerException.Message}\n"
                     + $"Stack: {ex.InnerException.StackTrace}";

            msg += $"\n\n📄 Log complet : {CrashLogPath}";

            MessageBox.Show(msg, "Douze Assistance — Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Diagnostics.Debug.WriteLine($"=== LMU OVERLAY ERROR ===\n{msg}");
        }

        /// <summary>Writes a line to the crash log (synchronous, fail-silent). Call from anywhere.</summary>
        public static void WriteCrashLog(string text)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
                // Keep last 200 lines to avoid unbounded growth
                const int MaxLines = 200;
                if (File.Exists(CrashLogPath))
                {
                    var lines = File.ReadAllLines(CrashLogPath);
                    if (lines.Length > MaxLines)
                        File.WriteAllLines(CrashLogPath, lines[^MaxLines..]);
                }
                File.AppendAllText(CrashLogPath, text);
            }
            catch { /* never crash the crash handler */ }
        }
    }
}
