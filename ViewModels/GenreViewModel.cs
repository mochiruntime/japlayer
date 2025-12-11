using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Japlayer.ViewModels
{
    public class GenreViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; }

        public event Action<GenreViewModel> OnSelectionChanged;

        public GenreViewModel(string name, bool isSelected = true)
        {
            Name = name;
            _isSelected = isSelected;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    OnSelectionChanged?.Invoke(this);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
