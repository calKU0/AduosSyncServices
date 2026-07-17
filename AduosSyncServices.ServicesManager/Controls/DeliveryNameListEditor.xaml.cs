using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AduosSyncServices.ServicesManager.Controls
{
    public partial class DeliveryNameListEditor : UserControl
    {
        private readonly List<TextBox> _rows = new();

        public DeliveryNameListEditor()
        {
            InitializeComponent();
        }

        public void SetItems(IEnumerable<string> items)
        {
            RowsPanel.Children.Clear();
            _rows.Clear();

            foreach (var item in items)
                AddRow(item);
        }

        public IReadOnlyList<string> GetInputs() =>
            _rows.Select(r => r.Text).ToList();

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddRow(string.Empty);
        }

        private void AddRow(string value)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBox = new TextBox { Text = value, Margin = new Thickness(2) };
            Grid.SetColumn(textBox, 0);

            var removeBtn = new Button
            {
                Content = "✖",
                Foreground = Brushes.Red,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            Grid.SetColumn(removeBtn, 1);

            removeBtn.Click += (_, _) =>
            {
                RowsPanel.Children.Remove(grid);
                _rows.Remove(textBox);
            };

            grid.Children.Add(textBox);
            grid.Children.Add(removeBtn);

            RowsPanel.Children.Add(grid);
            _rows.Add(textBox);
        }
    }
}
