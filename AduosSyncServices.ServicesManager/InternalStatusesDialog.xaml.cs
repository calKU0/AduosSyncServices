using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.ServicesManager.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AduosSyncServices.ServicesManager
{
    public partial class InternalStatusesDialog : Window
    {
        // Preset palette offered when picking a status colour.
        private static readonly string[] Palette =
        {
            "#3B82F6", "#16A34A", "#F59E0B", "#EF4444", "#8B5CF6",
            "#14B8A6", "#EC4899", "#6B7280", "#F97316", "#0EA5E9"
        };

        private sealed class StatusEditRow : INotifyPropertyChanged
        {
            public int Id { get; init; }

            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } }
            }

            private string _color = Palette[0];
            public string Color
            {
                get => _color;
                set { if (_color != value) { _color = value; OnPropertyChanged(nameof(Color)); } }
            }

            // Palette plus this row's own colour (so an existing non-palette colour still shows/selects).
            public List<string> AvailableColors { get; }

            public StatusEditRow(int id, string name, string color)
            {
                Id = id;
                _name = name;
                _color = string.IsNullOrWhiteSpace(color) ? Palette[0] : color;

                AvailableColors = Palette.ToList();
                if (!AvailableColors.Contains(_color, StringComparer.OrdinalIgnoreCase))
                    AvailableColors.Insert(0, _color);
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private readonly IOrderInternalStatusRepository _repository;
        private readonly DialogService _dialogService = new();
        private readonly ObservableCollection<StatusEditRow> _rows = new();

        // Snapshot of what was loaded, to diff against on save.
        private Dictionary<int, (string Name, string Color)> _original = new();

        public InternalStatusesDialog(IOrderInternalStatusRepository repository)
        {
            InitializeComponent();

            _repository = repository;
            IcStatuses.ItemsSource = _rows;
            _rows.CollectionChanged += (_, _) => UpdateEmptyState();

            Loaded += async (_, _) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                var statuses = await _repository.GetAll();
                _original = statuses.ToDictionary(s => s.Id, s => (s.Name, s.Color));

                _rows.Clear();
                foreach (var s in statuses)
                    _rows.Add(new StatusEditRow(s.Id, s.Name, s.Color));

                UpdateEmptyState();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Nie udało się wczytać statusów: {ex.Message}");
            }
        }

        private void UpdateEmptyState()
        {
            EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            _rows.Add(new StatusEditRow(0, string.Empty, Palette[0]));
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: StatusEditRow row })
                _rows.Remove(row);
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var trimmed = _rows.Select(r => new { Row = r, Name = r.Name?.Trim() ?? string.Empty }).ToList();

            if (trimmed.Any(t => string.IsNullOrWhiteSpace(t.Name)))
            {
                _dialogService.ShowWarning("Nazwa statusu nie może być pusta.");
                return;
            }

            var duplicate = trimmed
                .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                _dialogService.ShowWarning($"Status o nazwie \"{duplicate.Key}\" występuje więcej niż raz.");
                return;
            }

            try
            {
                BtnSave.IsEnabled = false;

                var currentIds = _rows.Where(r => r.Id != 0).Select(r => r.Id).ToHashSet();

                // Deletions: originally present, now removed.
                foreach (var removedId in _original.Keys.Where(id => !currentIds.Contains(id)))
                    await _repository.Delete(removedId);

                // Additions and updates.
                foreach (var t in trimmed)
                {
                    if (t.Row.Id == 0)
                    {
                        await _repository.Add(t.Name, t.Row.Color);
                    }
                    else if (_original.TryGetValue(t.Row.Id, out var orig)
                             && (!string.Equals(orig.Name, t.Name, StringComparison.Ordinal)
                                 || !string.Equals(orig.Color, t.Row.Color, StringComparison.OrdinalIgnoreCase)))
                    {
                        await _repository.Update(t.Row.Id, t.Name, t.Row.Color);
                    }
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Nie udało się zapisać statusów: {ex.Message}");
                BtnSave.IsEnabled = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
