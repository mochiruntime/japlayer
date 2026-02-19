using System;
using Japlayer.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace Japlayer.Views
{
    public sealed partial class SetupPage : Page
    {
        public SetupViewModel ViewModel { get; }

        public SetupPage()
        {
            ViewModel = App.GetService<SetupViewModel>();
            ViewModel.OnSetupCompleted += ViewModel_OnSetupCompleted;
            this.InitializeComponent();
        }

        private void ViewModel_OnSetupCompleted(object sender, EventArgs e)
        {
            // Navigate back to LibraryPage
            Frame.Navigate(typeof(LibraryPage));
        }
    }
}
