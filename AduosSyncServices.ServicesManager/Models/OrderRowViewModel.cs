using AduosSyncServices.Contracts.Data.Enums;
using AduosSyncServices.Contracts.Extensions;
using AduosSyncServices.Contracts.Models;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace AduosSyncServices.ServicesManager.Models
{
    public class OrderRowViewModel : INotifyPropertyChanged
    {
        public AllegroOrder Order { get; }

        public OrderRowViewModel(AllegroOrder order)
        {
            Order = order;
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public bool CanSelect =>
            !Order.SentToExternalCompany
            && Order.RealizeStatus == AllegroOrderStatus.NEW
            && Order.Status == AllegroCheckoutFormStatus.READY_FOR_PROCESSING;

        // Only manual orders that haven't been sent to the supplier yet can be deleted - synced
        // Allegro orders and anything already placed in Gąska are protected (also enforced in the SP).
        public bool CanDelete =>
            Order.Source == OrderSource.Manual && !Order.SentToExternalCompany;

        public string CannotPlaceReason
        {
            get
            {
                if (Order.SentToExternalCompany)
                    return "Nie można złożyć zamówienia - zostało ono już złożone u dostawcy.";

                if (Order.RealizeStatus != AllegroOrderStatus.NEW || Order.Status != AllegroCheckoutFormStatus.READY_FOR_PROCESSING)
                    return "Nie można złożyć zamówienia - status realizacji musi być \"Nowe\", a status \"Gotowe do realizacji\".";

                return string.Empty;
            }
        }

        public int Id => Order.Id;
        public string AllegroId => Order.AllegroId;
        public string RecipientFirstName => Order.RecipientFirstName;
        public string RecipientLastName => Order.RecipientLastName;
        public string FullName => $"{Order.RecipientFirstName} {Order.RecipientLastName}".Trim();
        public AllegroCheckoutFormStatus Status => Order.Status;
        public string StatusDisplay => Order.Status.GetDescription();
        public AllegroOrderStatus RealizeStatus => Order.RealizeStatus;
        public string RealizeStatusDisplay => Order.RealizeStatus.GetDescription();
        public string DeliveryMethodName => Order.DeliveryMethodName;
        public AllegroPaymentType PaymentType => Order.PaymentType;
        public string PaymentTypeDisplay => Order.PaymentType.GetDescription();
        public string? ExternalOrderNumber => Order.ExternalOrderNumber;
        public string? ExternalOrderStatus => Order.ExternalOrderStatus;
        public string? ExternalDeliveryName => Order.ExternalDeliveryName;
        public bool? IsDropshipping => Order.IsDropshipping;
        public bool SentToExternalCompany => Order.SentToExternalCompany;
        public DateTime CreatedAt => Order.CreatedAt;
        public decimal Amount => Order.Amount;
        public string AmountDisplay => Order.Amount.ToString("N2", CultureInfo.GetCultureInfo("pl-PL")) + " zł";
        public OrderSource Source => Order.Source;
        public string SourceDisplay => Order.Source.GetDescription();

        public string TrackingNumbers
        {
            get
            {
                var numbers = Order.Items
                    .Select(i => i.ExternalTrackingNumber)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct()
                    .ToList();

                return numbers.Count > 0 ? string.Join(", ", numbers) : "-";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
