using Microsoft.Extensions.Configuration;
using AduosSyncServices.Infrastructure.Helpers;
using AduosSyncServices.ServicesManager.Controls;
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

                if (field.IsCategoryPicker)
                {
                    var selected = ConfigurationLists.ReadIntList(config.GetSection(field.Key)).Select(id => id.ToString());
                    panel.Children.Add(CreateCategoryPickerRow(field, selected, config.GetConnectionString("MyDbContext")));
                    continue;
                }

                panel.Children.Add(CreateRow(field, value));
            }
        }

        /// <summary>Wybór kategorii Gąski z listy (jak w JSAGRO). W appsettings zapisywana jest lista JSON [id, id].</summary>
        public Grid CreateCategoryPickerRow(ConfigField field, IEnumerable<string> selected, string? connectionString)
        {
            var label = new TextBlock
            {
                Text = field.Label,
                Margin = new Thickness(0, 6, 0, 4),
                VerticalAlignment = VerticalAlignment.Top,
                TextWrapping = TextWrapping.Wrap,
                ToolTip = string.IsNullOrEmpty(field.Description) ? null : field.Description
            };

            var picker = new CategoryPickerEditor
            {
                Tag = field.Key,
                IsEnabled = field.IsEnabled
            };

            picker.Initialize(connectionString, selected, field.EmptyText);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(295) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(label, 0);
            Grid.SetColumn(picker, 1);

            grid.Children.Add(label);
            grid.Children.Add(picker);

            return grid;
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
