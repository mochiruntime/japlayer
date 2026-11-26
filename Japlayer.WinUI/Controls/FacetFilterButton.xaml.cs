#nullable enable
using Japlayer.Helpers;
using Japlayer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Japlayer.Controls
{
    public sealed partial class FacetFilterButton : UserControl
    {
        public static readonly DependencyProperty FacetProperty =
            DependencyProperty.Register(
                nameof(Facet),
                typeof(FacetFilterGroup),
                typeof(FacetFilterButton),
                new PropertyMetadata(null));

        public static readonly DependencyProperty OverlayPassThroughElementProperty =
            DependencyProperty.Register(
                nameof(OverlayPassThroughElement),
                typeof(DependencyObject),
                typeof(FacetFilterButton),
                new PropertyMetadata(null));

        private bool _isAttached;

        public FacetFilterButton()
        {
            this.InitializeComponent();
            this.Loaded += FacetFilterButton_Loaded;
        }

        public FacetFilterGroup? Facet
        {
            get => (FacetFilterGroup?)GetValue(FacetProperty);
            set => SetValue(FacetProperty, value);
        }

        public DependencyObject? OverlayPassThroughElement
        {
            get => (DependencyObject?)GetValue(OverlayPassThroughElementProperty);
            set => SetValue(OverlayPassThroughElementProperty, value);
        }

        private void FacetFilterButton_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isAttached)
            {
                return;
            }

            _isAttached = true;
            FlyoutPassThroughHelper.Attach(
                FacetDropDownButton,
                () => OverlayPassThroughElement,
                onOpening: () => Facet?.BeginInteraction(),
                onClosed: () => Facet?.EndInteraction());
        }

        private void FacetListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FilterItem filterItem)
            {
                filterItem.CycleStateCommand.Execute(null);
            }
        }

        private void FacetListView_ContainerContentChanging(
            ListViewBase sender,
            ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                AutomationProperties.SetAutomationId(args.ItemContainer, string.Empty);
                return;
            }

            if (args.Item is FilterItem filterItem)
            {
                AutomationProperties.SetAutomationId(args.ItemContainer, filterItem.Name);
            }
        }
    }
}
