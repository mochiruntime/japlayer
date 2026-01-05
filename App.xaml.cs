using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Japlayer
{
    public partial class App : Application
    {
        private Window m_window;

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"Critical Error: {e.Message}");
            try
            {
                var crashLogPath = System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log");
                var errorMessage = $"[{DateTime.Now}] Unhandled Exception: {e.Message}\n" +
                                   $"StackTrace: {e.Exception?.StackTrace}\n" +
                                   $"InnerException: {e.Exception?.InnerException?.Message}\n" +
                                   new string('-', 50) + "\n";
                System.IO.File.AppendAllText(crashLogPath, errorMessage);
            }
            catch
            {
                // Fallback to console if file logging fails
                Console.WriteLine($"Could not write to crash log");
            }
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}
