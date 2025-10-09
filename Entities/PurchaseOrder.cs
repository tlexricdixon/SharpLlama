namespace SharpLlama.Entities;

public partial class PurchaseOrder
{
    public short PurchaseOrderId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public short? ApprovedById { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? Notes { get; set; }

    public float? PaymentAmount { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? PaymentMethod { get; set; }

    public DateTime? ReceivedDate { get; set; }

    public float? ShippingFee { get; set; }

    public short? StatusId { get; set; }

    public short? SubmittedById { get; set; }

    public DateTime? SubmittedDate { get; set; }

    public float? TaxAmount { get; set; }

    public short? VendorId { get; set; }

    public virtual Employee? ApprovedBy { get; set; }

    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();

    public virtual PurchaseOrderStatus? Status { get; set; }

    public virtual Employee? SubmittedBy { get; set; }

    public virtual Company? Vendor { get; set; }
}
