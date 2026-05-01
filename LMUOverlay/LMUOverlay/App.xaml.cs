using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;

namespace LMUOverlay
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            TaskScheduler.UnobservedTaskException += OnTaskException;

            base.OnStartup(e);
        }

        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowError("UI Thread Exception", e.Exception);
            e.Handled = true;
        }

        private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                ShowError("Domain Exception", ex);
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

            // Anything else is unexpected — show it so it can be reported.
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

            MessageBox.Show(msg, "Douze Assistance — Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Diagnostics.Debug.WriteLine($"=== LMU OVERLAY ERROR ===\n{msg}");
        }
    }
}
