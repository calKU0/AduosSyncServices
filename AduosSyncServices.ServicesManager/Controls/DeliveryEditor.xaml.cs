using AduosSyncServices.ServicesManager.Models;
using AduosSyncServices.Contracts.Settings;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AduosSyncServices.Contracts.Data.Enums;

namespace AduosSyncServices.ServicesManager.Controls
{
    public partial class DeliveryEditor : UserControl
    {
        private readonly List<(ComboBox RuleType, TextBox NetPriceThreshold, TextBox Weight, TextBox Length, TextBox Width, TextBox Height, TextBox Name)> _rows = new();
        private static readonly List<EnumOption<DeliveryMatchMode>> MatchModes = GetEnumOptions<DeliveryMatchMode>();
        private static readonly List<EnumOption<DeliveryRuleType>> RuleTypes = GetEnumOptions<DeliveryRuleType>();
        private DeliveryMatchMode _globalMatchMode = DeliveryMatchMode.Weight;

        public DeliveryEditor()
        {
            InitializeComponent();
            GlobalMatchModeBox.ItemsSource = MatchModes;
            GlobalMatchModeBox.DisplayMemberPath = nameof(EnumOption<DeliveryMatchMode>.Description);
            GlobalMatchModeBox.SelectedItem = MatchModes.First(m => m.Value == _globalMatchMode);
            UpdateHeaderVisibility(_globalMatchMode);
        }

        public void SetDeliveries(IEnumerable<Delivery> deliveries, DeliveryMatchMode matchMode)
        {
            RowsPanel.Children.Clear();
            _rows.Clear();

            _globalMatchMode = matchMode;
            GlobalMatchModeBox.SelectedItem = MatchModes.First(m => m.Value == _globalMatchMode);
            UpdateHeaderVisibility(_globalMatchMode);

            foreach (var delivery in deliveries)
            {
                AddRow(delivery);
            }
        }

        public DeliveryMatchMode GetSelectedMatchMode()
        {
            return (_globalMatchMode);
        }

        public IReadOnlyList<(string RuleType, string NetPriceThreshold, string Weight, string Length, string Width, string Height, string Name)> GetInputs()
        {
            return _rows
                .Select(r => (
                    ((r.RuleType.SelectedItem as EnumOption<DeliveryRuleType>)?.Value ?? DeliveryRuleType.Standard).ToString(),
                    r.NetPriceThreshold.Text,
                    r.Weight.Text,
                    r.Length.Text,
                    r.Width.Text,
                    r.Height.Text,
                    r.Name.Text))
                .ToList();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddRow(new Delivery());
        }

        private void AddRow(Delivery delivery)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "RuleTypeCol" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "ThresholdCol" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "WeightCol" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "LengthCol" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "WidthCol" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "HeightCol" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "NameCol" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ruleTypeBox = new ComboBox
            {
                Margin = new Thickness(2),
                Width = 170,
                ItemsSource = RuleTypes,
                DisplayMemberPath = nameof(EnumOption<DeliveryRuleType>.Description)
            };
            ruleTypeBox.SelectedItem = RuleTypes.FirstOrDefault(r => r.Value == delivery.RuleType)
                ?? RuleTypes.First(r => r.Value == DeliveryRuleType.Standard);

            var netPriceThresholdBox = new TextBox { Text = delivery.NetPriceThreshold?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, Margin = new Thickness(2), Width = 120 };
            var weightBox = new TextBox { Text = delivery.Weight > 0 ? delivery.Weight.ToString(CultureInfo.InvariantCulture) : string.Empty, Margin = new Thickness(2), Width = 90 };
            var lengthBox = new TextBox { Text = delivery.Length > 0 ? delivery.Length.ToString() : string.Empty, Margin = new Thickness(2), Width = 90 };
            var widthBox = new TextBox { Text = delivery.Width > 0 ? delivery.Width.ToString() : string.Empty, Margin = new Thickness(2), Width = 90 };
            var heightBox = new TextBox { Text = delivery.Height > 0 ? delivery.Height.ToString() : string.Empty, Margin = new Thickness(2), Width = 90 };
            var nameBox = new TextBox { Text = delivery.DeliveryName, Margin = new Thickness(2), Width = 260 };

            Grid.SetColumn(ruleTypeBox, 0);
            Grid.SetColumn(netPriceThresholdBox, 1);
            Grid.SetColumn(weightBox, 2);
            Grid.SetColumn(lengthBox, 3);
            Grid.SetColumn(widthBox, 4);
            Grid.SetColumn(heightBox, 5);
            Grid.SetColumn(nameBox, 6);

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
                _rows.Remove((ruleTypeBox, netPriceThresholdBox, weightBox, lengthBox, widthBox, heightBox, nameBox));
            };

            Grid.SetColumn(removeBtn, 7);

            grid.Children.Add(ruleTypeBox);
            grid.Children.Add(netPriceThresholdBox);
            grid.Children.Add(weightBox);
            grid.Children.Add(lengthBox);
            grid.Children.Add(widthBox);
            grid.Children.Add(heightBox);
            grid.Children.Add(nameBox);
            grid.Children.Add(removeBtn);

            RowsPanel.Children.Add(grid);
            _rows.Add((ruleTypeBox, netPriceThresholdBox, weightBox, lengthBox, widthBox, heightBox, nameBox));
            ApplyModeVisibility(netPriceThresholdBox, weightBox, lengthBox, widthBox, heightBox);
        }

        private void GlobalMatchModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GlobalMatchModeBox.SelectedItem is not EnumOption<DeliveryMatchMode> selected)
                return;

            _globalMatchMode = selected.Value;

            foreach (var row in _rows)
            {
                ApplyModeVisibility(row.NetPriceThreshold, row.Weight, row.Length, row.Width, row.Height);
            }

            UpdateHeaderVisibility(selected.Value);
        }

        private void ApplyModeVisibility(TextBox netPriceThreshold, TextBox weight, TextBox length, TextBox width, TextBox height)
        {
            var isPrice = _globalMatchMode == DeliveryMatchMode.Price;

            netPriceThreshold.Visibility = isPrice ? Visibility.Visible : Visibility.Collapsed;
            weight.Visibility = isPrice ? Visibility.Collapsed : Visibility.Visible;
            length.Visibility = isPrice ? Visibility.Collapsed : Visibility.Visible;
            width.Visibility = isPrice ? Visibility.Collapsed : Visibility.Visible;
            height.Visibility = isPrice ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateHeaderVisibility(DeliveryMatchMode mode)
        {
            var isPrice = mode == DeliveryMatchMode.Price;
            ThresholdHeader.Visibility = isPrice ? Visibility.Visible : Visibility.Collapsed;
            WeightHeader.Visibility = isPrice ? Visibility.Collapsed : Visibility.Visible;
            LengthHeader.Visibility = isPrice ? Visibility.Collapsed : Visibility.Visible;
            WidthHeader.Visibility = isPrice ? Visibility.Collapsed : Visibility.Visible;
            HeightHeader.Visibility = isPrice ? Visibility.Collapsed : Visibility.Visible;
        }

        private static List<EnumOption<TEnum>> GetEnumOptions<TEnum>() where TEnum : struct, Enum
        {
            return Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(v => new EnumOption<TEnum>(v, GetEnumDescription(v)))
                .ToList();
        }

        private static string GetEnumDescription<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            var member = typeof(TEnum).GetMember(value.ToString()).FirstOrDefault();
            var description = member?.GetCustomAttribute<DescriptionAttribute>();
            return description?.Description ?? value.ToString();
        }

        private sealed record EnumOption<TEnum>(TEnum Value, string Description) where TEnum : struct, Enum;
    }
}
