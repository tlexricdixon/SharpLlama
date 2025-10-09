namespace SharpLlama.Entities;

public partial class Product
{
    public short ProductId { get; set; }

    public short ProductCategoryId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public byte[]? Discontinued { get; set; }

    public short? MinimumReorderQuantity { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? ProductCode { get; set; }

    public string? ProductDescription { get; set; }

    public string? ProductName { get; set; }

    public string? QuantityPerUnit { get; set; }

    public short? ReorderLevel { get; set; }

    public float? StandardUnitCost { get; set; }

    public short? TargetLevel { get; set; }

    public float? UnitPrice { get; set; }

    public virtual ProductCategory ProductCategory { get; set; } = null!;

    public virtual ICollection<ProductVendor> ProductVendors { get; set; } = new List<ProductVendor>();

    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();
}
