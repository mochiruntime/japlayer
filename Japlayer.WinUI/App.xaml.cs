#nullable enable
using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Japlayer.Contracts;
using Japlayer.Data.Context;
using Japlayer.Data.Services;
using Japlayer.Logging;
using Japlayer.Services;
using Japlayer.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Japlayer
{
    public partial class App : Application
    {
        private Window? _window;

        private static string LogDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Japlayer", "Logs");

        public IServiceProvider Services { get; }
        public static new App Current => (App)Application.Current;

        public App()
        {
            Services = ConfigureServices();
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

#if DEBUG
            this.DebugSettings.BindingFailed += DebugSettings_BindingFailed;
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
#endif
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Logging
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddDebug();
                loggingBuilder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

                var logPath = Path.Combine(LogDirectory, "app.log");
                loggingBuilder.AddFile(logPath);
            });

            // Services
            var settingsService = new SettingsService();
            services.AddSingleton<ISettingsService>(settingsService);

            var sqlitePragmaInterceptor = new SqlitePragmaInterceptor();
            services.AddDbContextFactory<DatabaseContext>((serviceProvider, options) =>
            {
                var settings = serviceProvider.GetRequiredService<ISettingsService>();
                options.UseSqlite($"Data Source={settings.SqliteDatabasePath}")
                    .AddInterceptors(sqlitePragmaInterceptor);
            });

            services.AddSingleton<Data.Contracts.IMediaProvider, DatabaseMediaProvider>();
            services.AddSingleton<Data.Contracts.IImageProvider, DatabaseImageProvider>();
            services.AddSingleton<Data.Contracts.IMediaSceneProvider, DatabaseMediaSceneProvider>();
            services.AddSingleton<Data.Contracts.IMediaThumbnailProvider, DatabaseMediaThumbnailProvider>();
            services.AddSingleton<Data.Contracts.IMediaHighlightProvider, DatabaseMediaHighlightProvider>();
            services.AddSingleton<Data.Contracts.ILibraryPresetProvider, DatabaseLibraryPresetProvider>();
            services.AddSingleton<IDatabaseChangeMonitor, DatabaseChangeMonitor>();
            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

            // ViewModels
            services.AddTransient<LibraryViewModel>();
            services.AddTransient<MediaItemViewModel>();

            // Views
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        public static T GetService<T>() where T : class
            => Current.Services.GetRequiredService<T>();

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs eventArgs) => WriteCrashLog("UI UnhandledException", eventArgs.Message, eventArgs.Exception);

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs eventArgs)
        {
            var exception = eventArgs.ExceptionObject as Exception;
            WriteCrashLog("AppDomain UnhandledException", exception?.Message ?? eventArgs.ExceptionObject?.ToString() ?? "Unknown", exception);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs) => WriteCrashLog("UnobservedTaskException", eventArgs.Exception.Message, eventArgs.Exception);

#if DEBUG
        private void DebugSettings_BindingFailed(object sender, BindingFailedEventArgs eventArgs)
        {
            try
            {
                GetService<ILogger<App>>().LogWarning("XAML BindingFailed: {Message}", eventArgs.Message);
            }
            catch
            {
            }
        }

        private void CurrentDomain_FirstChanceException(object? sender, FirstChanceExceptionEventArgs eventArgs)
        {
            if (eventArgs.Exception is OperationCanceledException or FileNotFoundException)
            {
                return;
            }

            WriteCrashLog("FirstChanceException", eventArgs.Exception.Message, eventArgs.Exception);
        }
#endif

        private void WriteCrashLog(string source, string message, Exception? exception)
        {
            try
            {
                GetService<ILogger<App>>().LogCritical(exception, "{Source}: {Message}", source, message);
            }
            catch
            {
            }

            try
            {
                Directory.CreateDirectory(LogDirectory);
                var crashLogPath = Path.Combine(LogDirectory, "crash.log");
                var errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {message}\n" +
                                   $"Exception: {exception?.GetType().FullName}\n" +
                                   $"StackTrace: {exception?.StackTrace}\n" +
                                   $"InnerException: {exception?.InnerException?.Message}\n" +
                                   new string('-', 50) + "\n";
                File.AppendAllText(crashLogPath, errorMessage);
            }
            catch
            {
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            EnsureDatabaseCreated();
            _window = GetService<MainWindow>();
            _window.Activate();
        }

        private void EnsureDatabaseCreated()
        {
            var contextFactory = Services.GetRequiredService<IDbContextFactory<DatabaseContext>>();
            using var context = contextFactory.CreateDbContext();
            context.Database.Migrate();
        }
    }
}
