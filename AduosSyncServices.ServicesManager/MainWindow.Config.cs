using AduosSyncServices.ServicesManager.Controls;
using AduosSyncServices.ServicesManager.Helpers;
using AduosSyncServices.ServicesManager.Models;
using AduosSyncServices.ServicesManager.Resources;
using AduosSyncServices.ServicesManager.Validation;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AduosSyncServices.Contracts.Data.Enums;

namespace AduosSyncServices.ServicesManager
{
    public partial class MainWindow
    {
        private void ShowConfigViewInternal()
        {
            LoadConfig();
            ShowConfigView();
        }

        private IConfigurationRoot LoadAppSettings(string path)
        {
            return _configService.LoadAppSettings(path);
        }

        private void SaveAppSettings(string path, Dictionary<string, string> values)
        {
            _configService.SaveAppSettings(path, values);
        }

        private void LoadConfig()
        {
            if (_selectedService == null) return;

            try
            {
                var config = LoadAppSettings(_selectedService.ExternalConfigPath);

                ConfigStackPanel.Children.Clear();

                var groupedFields = ConfigFieldDefinitions.AllFields.GroupBy(f => f.Group);

                foreach (var group in groupedFields)
                {
                    var existingFields = group
                        .Where(f => config.GetSection(f.Key).Exists())
                        .ToList();

                    if (!existingFields.Any() && group.Key != "Narzuty")
                        continue;

                    var groupBox = new GroupBox { Header = group.Key, Margin = new Thickness(0, 6, 0, 6) };
                    var groupPanel = new StackPanel { Margin = new Thickness(6) };

                    _configFieldUiBuilder.AddFields(groupPanel, existingFields, config);

                    if (group.Key == "Narzuty")
                    {
                        LoadMarginRanges(config, groupPanel);
                    }

                    groupBox.Content = groupPanel;
                    ConfigStackPanel.Children.Add(groupBox);
                }
                LoadDeliveries(config);

                var saveButton = new Button
                {
                    Content = "Zapisz",
                    Margin = new Thickness(6, 12, 6, 24),
                    Padding = new Thickness(12, 6, 12, 6),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xE2)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x35, 0x7A, 0xBD)),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Width = 120
                };

                saveButton.Click += BtnSaveConfig_Click;
                ConfigStackPanel.Children.Add(saveButton);

                ConfigViewContainer.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(string.Format(UiMessages.ConfigLoadFailed, ex.Message));
            }
        }

        private void LoadDeliveries(IConfiguration config)
        {
            var section = config.GetSection("AppSettings:Deliveries");
            _deliveriesSectionExists = section.Exists();
            if (!_deliveriesSectionExists)
            {
                _deliveryEditor = null;
                return;
            }

            _deliveries = section.Get<List<Delivery>>() ?? new List<Delivery>();
            var matchMode = config.GetValue("AppSettings:DeliveryMatchMode", DeliveryMatchMode.Weight);

            var groupBox = new GroupBox
            {
                Header = "Dostawy",
                Margin = new Thickness(0, 6, 0, 6)
            };

            _deliveryEditor = new DeliveryEditor();
            _deliveryEditor.SetDeliveries(_deliveries, matchMode);
            groupBox.Content = _deliveryEditor;
            ConfigStackPanel.Children.Add(groupBox);
        }

        private void LoadMarginRanges(IConfiguration config, StackPanel groupPanel)
        {
            if (!config.GetSection("PriceSettings").Exists())
            {
                _marginRangeEditor = null;
                return;
            }

            var section = config.GetSection("PriceSettings:MarginRanges");
            _marginRangesSectionExists = section.Exists();
            if (!_marginRangesSectionExists)
            {
                _marginRangeEditor = null;
                return;
            }

            _marginRanges = section.Get<List<MarginRange>>() ?? new List<MarginRange>();

            _marginRangeEditor = new MarginRangeEditor();
            _marginRangeEditor.SetRanges(_marginRanges);
            groupPanel.Children.Add(_marginRangeEditor);
        }

        private void BtnReloadConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadConfig();
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedService == null) return;

            try
            {
                var errors = new List<string>();

                var (valuesToSave, fieldErrors) = _configFieldValueCollector
                    .CollectValues(ConfigStackPanel, ConfigFieldDefinitions.AllFields);
                errors.AddRange(fieldErrors);

                var deliveries = BuildDeliveries(errors);
                var marginRanges = BuildMarginRanges(errors);

                if (!TryShowValidationErrors(errors))
                    return;

                if (_deliveriesSectionExists)
                {
                    valuesToSave["AppSettings:DeliveryMatchMode"] = _deliveryEditor?.GetSelectedMatchMode().ToString() ?? DeliveryMatchMode.Weight.ToString();
                    valuesToSave["AppSettings:Deliveries"] =
                        JsonSerializer.Serialize(deliveries, new JsonSerializerOptions
                        {
                            Converters = { new JsonStringEnumConverter() }
                        });
                }

                if (_marginRangesSectionExists)
                {
                    valuesToSave["PriceSettings:MarginRanges"] =
                        JsonSerializer.Serialize(marginRanges);
                }

                SaveAppSettings(_selectedService.ExternalConfigPath, valuesToSave);

                _dialogService.ShowInfo(UiMessages.ConfigSaved);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(string.Format(UiMessages.ConfigSaveFailed, ex.Message));
            }
        }


        private List<Delivery> BuildDeliveries(List<string> errors)
        {
            var deliveries = new List<Delivery>();

            if (!_deliveriesSectionExists)
                return deliveries;

            if (_deliveryEditor == null)
                return deliveries;

            var inputs = _deliveryEditor.GetInputs();
            var matchMode = _deliveryEditor.GetSelectedMatchMode();

            var (validated, validationErrors) = DeliveryValidator.Validate(matchMode, inputs);
            errors.AddRange(validationErrors);
            return validated;
        }

        private List<MarginRange> BuildMarginRanges(List<string> errors)
        {
            var marginRanges = new List<MarginRange>();

            if (!_marginRangesSectionExists)
                return marginRanges;

            if (_marginRangeEditor == null)
                return marginRanges;

            var inputs = _marginRangeEditor.GetInputs();

            var (validated, validationErrors) = MarginRangeValidator.Validate(inputs);
            errors.AddRange(validationErrors);
            return validated;
        }

        private bool TryShowValidationErrors(List<string> errors)
        {
            if (!errors.Any())
                return true;

            _dialogService.ShowWarning(string.Join("\n", errors));
            return false;
        }

    }
}
