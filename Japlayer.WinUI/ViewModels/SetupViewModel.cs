#nullable enable
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Japlayer.Contracts;
using Windows.Storage.Pickers;


namespace Japlayer.ViewModels
{
    public partial class SetupViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        public partial string? ImagePath { get; set; }

        [ObservableProperty]
        public partial string? SqliteDatabasePath { get; set; }

        [ObservableProperty]
        public partial string? ErrorMessage { get; set; }


        public IAsyncRelayCommand PickDatabaseCommand { get; }
        public IAsyncRelayCommand PickImagePathCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }

        public SetupViewModel(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            ImagePath = _settingsService.ImagePath;
            SqliteDatabasePath = _settingsService.SqliteDatabasePath;

            PickDatabaseCommand = new AsyncRelayCommand(PickDatabaseAsync);
            PickImagePathCommand = new AsyncRelayCommand(PickImagePathAsync);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
        }

        private async Task PickDatabaseAsync()
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".db");
            picker.FileTypeFilter.Add(".sqlite");

            // For WinUI3 we need to associate the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Japlayer.App.GetService<MainWindow>());

            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);


            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                SqliteDatabasePath = file.Path;
            }
        }

        private async Task PickImagePathAsync()
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(Japlayer.App.GetService<MainWindow>());

            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);


            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                ImagePath = folder.Path;
            }
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(SqliteDatabasePath) || !System.IO.File.Exists(SqliteDatabasePath))
            {
                ErrorMessage = "Please select a valid SQLite database file.";
                return;
            }

            if (string.IsNullOrWhiteSpace(ImagePath))
            {
                ErrorMessage = "Please select an image folder path.";
                return;
            }

            _settingsService.SqliteDatabasePath = SqliteDatabasePath;
            _settingsService.ImagePath = ImagePath;

            await _settingsService.SaveAsync();

            // After saving, we should trigger a restart or navigate to the main page.
            // For now, let's assume the caller handles navigation.
            OnSetupCompleted?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? OnSetupCompleted;

    }
}
