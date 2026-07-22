using AduosSyncServices.Contracts.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AduosSyncServices.ServicesManager
{
    public partial class SetInternalStatusDialog : Window
    {
        // Wraps a status for the combo; a null Id is the "clear status" option.
        private record StatusOption(int? Id, string Name, string? Color);

        public int? SelectedStatusId { get; private set; }

        public SetInternalStatusDialog(IReadOnlyList<OrderInternalStatus> statuses, int orderCount)
        {
            InitializeComponent();

            HeaderText.Text = orderCount == 1
                ? "Wybierz status wewnętrzny dla 1 zamówienia."
                : $"Wybierz status wewnętrzny dla {orderCount} zamówień.";

            var options = new List<StatusOption> { new(null, "Bez statusu (wyczyść)", null) };
            options.AddRange(statuses.Select(s => new StatusOption(s.Id, s.Name, s.Color)));

            CbStatus.ItemsSource = options;
            CbStatus.SelectedIndex = 0;
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            SelectedStatusId = (CbStatus.SelectedItem as StatusOption)?.Id;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
