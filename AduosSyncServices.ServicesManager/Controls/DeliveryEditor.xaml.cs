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
    public partial class DeliveryEditor : UserControl
    {
        private readonly List<(TextBox Length, TextBox Width, TextBox Height, TextBox Weight, TextBox Name)> _rows = new();

        public DeliveryEditor()
        {
            InitializeComponent();
        }

        public void SetDeliveries(IEnumerable<Delivery> deliveries)
        {
            RowsPanel.Children.Clear();
            _rows.Clear();

            foreach (var delivery in deliveries)
            {
                AddRow(delivery);
            }
        }

        public IReadOnlyList<(string Length, string Width, string Height, string Weight, string Name)> GetInputs()
        {
            return _rows
                .Select(r => (r.Length.Text, r.Width.Text, r.Height.Text, r.Weight.Text, r.Name.Text))
                .ToList();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddRow(new Delivery());
        }

        private void AddRow(Delivery delivery)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };

            for (int i = 0; i < 6; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition());

            var lengthBox = new TextBox { Text = delivery.Length.ToString(), Margin = new Thickness(2) };
            var widthBox = new TextBox { Text = delivery.Width.ToString(), Margin = new Thickness(2) };
            var heightBox = new TextBox { Text = delivery.Height.ToString(), Margin = new Thickness(2) };
            var weightBox = new TextBox { Text = delivery.Weight.ToString(CultureInfo.InvariantCulture), Margin = new Thickness(2) };
            var nameBox = new TextBox { Text = delivery.DeliveryName, Margin = new Thickness(2) };

            Grid.SetColumn(lengthBox, 0);
            Grid.SetColumn(widthBox, 1);
            Grid.SetColumn(heightBox, 2);
            Grid.SetColumn(weightBox, 3);
            Grid.SetColumn(nameBox, 4);

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
                _rows.Remove((lengthBox, widthBox, heightBox, weightBox, nameBox));
            };

            Grid.SetColumn(removeBtn, 5);

            grid.Children.Add(lengthBox);
            grid.Children.Add(widthBox);
            grid.Children.Add(heightBox);
            grid.Children.Add(weightBox);
            grid.Children.Add(nameBox);
            grid.Children.Add(removeBtn);

            RowsPanel.Children.Add(grid);
            _rows.Add((lengthBox, widthBox, heightBox, weightBox, nameBox));
        }
    }
}
