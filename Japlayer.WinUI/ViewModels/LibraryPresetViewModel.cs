#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using Japlayer.Data.Models;

namespace Japlayer.ViewModels
{
    public partial class LibraryPresetViewModel(LibraryPresetModel model, string autoSummary, string descriptionTooltip) : ObservableObject
    {
        public int Id => Model.Id;
        public LibraryPresetModel Model { get; private set; } = model;

        [ObservableProperty]
        public partial string DisplayName { get; set; } = model.IsCustomName ? model.Name : autoSummary;

        [ObservableProperty]
        public partial string DescriptionTooltip { get; set; } = descriptionTooltip;

        [ObservableProperty]
        public partial bool IsActive { get; set; }

        public void UpdateFrom(LibraryPresetModel model, string autoSummary, string descriptionTooltip)
        {
            Model = model;
            DisplayName = model.IsCustomName ? model.Name : autoSummary;
            DescriptionTooltip = descriptionTooltip;
        }
    }
}
