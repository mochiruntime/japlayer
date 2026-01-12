using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using Microsoft.Extensions.DependencyInjection;
using Japlayer.Contracts;
using Japlayer.Services;
using Japlayer.ViewModels;

namespace Japlayer
{
    public partial class App : Application
    {
        private Window m_window;

        public IServiceProvider Services { get; }
        public static new App Current => (App)Application.Current;

        public App()
        {
            Services = ConfigureServices();
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Services
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IMediaProvider, FileSystemMediaProvider>();
            services.AddSingleton<IImageProvider, FileSystemImageProvider>();
            services.AddSingleton<IMediaSceneProvider, FileSystemMediaSceneProvider>();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<MediaItemViewModel>();

            // Views
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        public static T GetService<T>() where T : class
            => Current.Services.GetService<T>();

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
            m_window = GetService<MainWindow>();
            m_window.Activate();
        }
    }
}
