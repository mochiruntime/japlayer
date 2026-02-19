using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Japlayer.Contracts;
using Japlayer.Services;
using Japlayer.ViewModels;
using Japlayer.Data.Context;
using Japlayer.Data.Services;

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
            var settingsService = new SettingsService();
            services.AddSingleton<ISettingsService>(settingsService);
            services.AddTransient<SetupViewModel>();

            services.AddDbContext<DatabaseContext>((serviceProvider, options) =>
            {
                var settings = serviceProvider.GetRequiredService<ISettingsService>();
                options.UseSqlite($"Data Source={settings.SqliteDatabasePath}");
            });

            services.AddSingleton<Data.Contracts.IMediaProvider, DatabaseMediaProvider>();
            services.AddSingleton<Data.Contracts.IImageProvider, DatabaseImageProvider>();
            services.AddSingleton<Data.Contracts.IMediaSceneProvider, DatabaseMediaSceneProvider>();

            // ViewModels
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<MediaItemViewModel>();

            // Views
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        public static T GetService<T>() where T : class
            => Current.Services.GetRequiredService<T>();


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
