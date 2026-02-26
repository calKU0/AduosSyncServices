using Microsoft.Extensions.Configuration;
using AduosSyncServices.ServicesManager.Models;
using System.Windows;
using System.Windows.Controls;

namespace AduosSyncServices.ServicesManager.Helpers
{
    public class ConfigFieldUiBuilder
    {
        public void AddFields(StackPanel panel, IEnumerable<ConfigField> fields, IConfiguration config)
        {
            foreach (var field in fields)
            {
                var value = config[field.Key] ?? string.Empty;
                panel.Children.Add(CreateRow(field, value));
            }
        }

        public Grid CreateRow(ConfigField field, string value)
        {
            var label = new TextBlock
            {
                Text = field.Label,
                Margin = new Thickness(0, 4, 0, 4),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = string.IsNullOrEmpty(field.Description) ? null : field.Description
            };

            var textbox = new TextBox
            {
                Text = value,
                Margin = new Thickness(0, 4, 0, 4),
                IsEnabled = field.IsEnabled,
                Tag = field.Key,
                AcceptsReturn = field.Key == "AllegroSafetyMeasures",
                Height = field.Key == "AllegroSafetyMeasures" ? 120 : Double.NaN,
                TextWrapping = TextWrapping.Wrap
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(295) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(label, 0);
            Grid.SetColumn(textbox, 1);

            grid.Children.Add(label);
            grid.Children.Add(textbox);

            return grid;
        }
    }
}
