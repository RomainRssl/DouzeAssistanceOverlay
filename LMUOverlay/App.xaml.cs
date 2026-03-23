using System.Windows;
using System.Windows.Threading;

namespace LMUOverlay
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Catch ALL unhandled exceptions and show full details
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            TaskScheduler.UnobservedTaskException += OnTaskException;

            base.OnStartup(e);
        }

        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowError("UI Thread Exception", e.Exception);
            e.Handled = true; // Don't crash — keep app alive
        }

        private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                ShowError("Domain Exception", ex);
        }

        private void OnTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowError("Task Exception", e.Exception);
            e.SetObserved();
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

            // Also write to debug output
            System.Diagnostics.Debug.WriteLine($"=== LMU OVERLAY ERROR ===\n{msg}");
        }
    }
}
