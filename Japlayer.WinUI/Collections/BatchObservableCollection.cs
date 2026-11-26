#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Japlayer.Collections
{
    /// <summary>
    /// ObservableCollection supporting atomic batch replacement with a single Reset notification
    /// to avoid repeated UI layout passes when filtering or sorting large collections.
    /// </summary>
    public sealed class BatchObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotifications;

        public BatchObservableCollection()
        {
        }

        public BatchObservableCollection(IEnumerable<T> collection) : base(collection)
        {
        }

        /// <summary>
        /// Replaces all items in the collection and raises a single Reset notification if the sequence changed.
        /// </summary>
        public bool ReplaceAll(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            var targetList = items as IReadOnlyList<T> ?? [.. items];
            if (this.SequenceEqual(targetList))
            {
                return false;
            }

            _suppressNotifications = true;
            try
            {
                Items.Clear();
                foreach (var item in targetList)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            return true;
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotifications)
            {
                base.OnCollectionChanged(e);
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_suppressNotifications)
            {
                base.OnPropertyChanged(e);
            }
        }
    }
}
