using AduosSyncServices.ServicesManager.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AduosSyncServices.ServicesManager.Controls
{
    public partial class MarginRangeEditor : UserControl
    {
        private readonly List<(TextBox Min, TextBox Max, TextBox Margin)> _rows = new();

        public MarginRangeEditor()
        {
            InitializeComponent();
        }

        public void SetRanges(IEnumerable<MarginRange> ranges)
        {
            RowsPanel.Children.Clear();
            _rows.Clear();

            foreach (var range in ranges)
            {
                AddRow(range);
            }
        }

        public IReadOnlyList<(string Min, string Max, string Margin)> GetInputs()
        {
            return _rows
                .Select(r => (r.Min.Text, r.Max.Text, r.Margin.Text))
                .ToList();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddRow(new MarginRange());
        }

        private void AddRow(MarginRange range)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };

            for (int i = 0; i < 4; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition());

            var minBox = new TextBox { Text = range.Min.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(2) };
            var maxBox = new TextBox { Text = range.Max.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(2) };
            var marginBox = new TextBox { Text = range.Margin.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(2) };

            Grid.SetColumn(minBox, 0);
            Grid.SetColumn(maxBox, 1);
            Grid.SetColumn(marginBox, 2);

            var removeBtn = new Button
            {
                Content = "✖",
                Foreground = Brushes.Red,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            removeBtn.Click += (_, _) =>
            {
                RowsPanel.Children.Remove(grid);
                _rows.Remove((minBox, maxBox, marginBox));
            };

            Grid.SetColumn(removeBtn, 3);

            grid.Children.Add(minBox);
            grid.Children.Add(maxBox);
            grid.Children.Add(marginBox);
            grid.Children.Add(removeBtn);

            RowsPanel.Children.Add(grid);
            _rows.Add((minBox, maxBox, marginBox));
        }
    }
}
