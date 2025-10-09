namespace Entities;

public partial class PurchaseOrderDetail
{
    public short PurchaseOrderDetailId { get; set; }

    public short? PurchaseOrderId { get; set; }

    public short? ProductId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public short? Quantity { get; set; }

    public DateTime? ReceivedDate { get; set; }

    public float? UnitCost { get; set; }

    public virtual Product? Product { get; set; }

    public virtual PurchaseOrder? PurchaseOrder { get; set; }
}
