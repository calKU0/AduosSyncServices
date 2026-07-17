using AduosSyncServices.ServicesManager.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AduosSyncServices.ServicesManager.Controls
{
    public partial class MultiSelectFilterControl : UserControl
    {
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(MultiSelectFilterControl), new PropertyMetadata(string.Empty));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public ObservableCollection<MultiSelectFilterItem> Items { get; } = new();

        public event EventHandler? SelectionChanged;

        // Popup.StaysOpen="False" closes the popup on ANY mouse-down outside it - including the
        // toggle button that opened it. That close happens on mouse-DOWN, then the same click's
        // mouse-UP fires the button's own Click (which flips IsChecked back to true), so a second
        // click meant to close the dropdown instead closes-then-immediately-reopens it. Track when
        // the popup was last auto-closed and suppress a reopen if it happens within the same click.
        private DateTime _popupClosedAtUtc = DateTime.MinValue;

        public MultiSelectFilterControl()
        {
            InitializeComponent();
            IcOptions.ItemsSource = Items;
        }

        private void ToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ToggleBtn.IsChecked == true && (DateTime.UtcNow - _popupClosedAtUtc).TotalMilliseconds < 250)
            {
                ToggleBtn.IsChecked = false;
                return;
            }

            OptionsPopup.IsOpen = ToggleBtn.IsChecked == true;
        }

        private void OptionsPopup_Closed(object sender, EventArgs e)
        {
            _popupClosedAtUtc = DateTime.UtcNow;
            ToggleBtn.IsChecked = false;
        }

        public void SetItems(IEnumerable<(string Display, object Value)> options)
        {
            foreach (var item in Items)
                item.PropertyChanged -= Item_PropertyChanged;

            Items.Clear();

            foreach (var (display, value) in options)
            {
                var item = new MultiSelectFilterItem(display, value);
                item.PropertyChanged += Item_PropertyChanged;
                Items.Add(item);
            }

            UpdateButtonText();
        }

        public IReadOnlyList<object> SelectedValues => Items.Where(i => i.IsSelected).Select(i => i.Value).ToList();

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MultiSelectFilterItem.IsSelected))
                return;

            UpdateButtonText();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateButtonText()
        {
            var selected = Items.Where(i => i.IsSelected).ToList();

            ButtonTextBlock.Text = selected.Count == 0 || selected.Count == Items.Count
                ? "Wszystkie"
                : selected.Count <= 2
                    ? string.Join(", ", selected.Select(s => s.Display))
                    : $"Wybrano ({selected.Count})";
        }

        private void SelectAll_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (var item in Items)
                item.IsSelected = true;
        }

        private void ClearAll_Click(object sender, MouseButtonEventArgs e)
        {
            foreach (var item in Items)
                item.IsSelected = false;
        }
    }
}
