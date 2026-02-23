namespace AduosSyncServices.Contracts.Models;

public class ProductPackage
{
    public int Id { get; set; }

    public string PackUnit { get; set; }
    public float PackQty { get; set; }
    public float PackNettWeight { get; set; }
    public float PackGrossWeight { get; set; }
    public string PackEan { get; set; }
    public int PackRequired { get; set; }

    public int ProductId { get; set; }
}
