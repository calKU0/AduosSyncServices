using AduosSyncServices.Contracts.Interfaces;
using AduosSyncServices.Contracts.Models;
using AduosSyncServices.Infrastructure.Helpers;
using AduosSyncServices.ServicesManager.Models;
using System.Windows;

namespace AduosSyncServices.ServicesManager
{
    public partial class OrderDetailsDialog : Window
    {
        private record LineItemRow(string? ImageUrl, string Code, string OfferName, int Quantity, string Unit, string DeliveryTypeDisplay, string PriceGross);

        private readonly OrderRowViewModel _row;
        private readonly IGaskaOrderPlacementService _placementService;
        private readonly IProductRepository _productRepository;

        public OrderDetailsDialog(OrderRowViewModel row, IGaskaOrderPlacementService placementService, IProductRepository productRepository)
        {
            InitializeComponent();

            _row = row;
            _placementService = placementService;
            _productRepository = productRepository;

            DataContext = row;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // DgLineItems.ItemsSource is assigned exactly once, after codes are resolved - see the
            // identical comment in OrderPlacementDialog.xaml.cs for why (Width="Auto" columns size
            // themselves once against whatever they're first given and don't re-measure on a swap).
            Dictionary<int, Product>? productsById = null;
            try
            {
                var productIds = _row.Order.Items.Select(i => i.ProductId).Distinct().ToList();
                var products = await _productRepository.GetProductsByIdsAsync(productIds, CancellationToken.None);
                productsById = products.ToDictionary(p => p.Id);
            }
            catch
            {
                // Best-effort: fall through with productsById == null, showing "-" for Code/Jm and no
                // delivery type.
            }

            DgLineItems.ItemsSource = _row.Order.Items
                .Select(i =>
                {
                    Product? product = null;
                    productsById?.TryGetValue(i.ProductId, out product);
                    return new LineItemRow(
                        ImageHelper.GetFirstImageFile(ImageHelper.DefaultImagesFolder, i.ProductId),
                        product?.Code ?? "-",
                        i.OfferName,
                        i.Quantity,
                        product?.Unit ?? "-",
                        OrderItemRowViewModel.FormatDeliveryType(product?.DeliveryType),
                        i.PriceGross);
                })
                .ToList();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BtnPlaceOrder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OrderPlacementDialog(_placementService, _productRepository, new List<AllegroOrder> { _row.Order }) { Owner = this };
            var result = dialog.ShowDialog();

            if (result == true)
                DialogResult = true;
        }
    }
}
