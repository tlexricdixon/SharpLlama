namespace SharpLlama.Entities;

public partial class Order
{
    public short OrderId { get; set; }

    public short OrderStatusId { get; set; }

    public short? CustomerId { get; set; }

    public short? EmployeeId { get; set; }

    public short? ShipperId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public DateTime? InvoiceDate { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? Notes { get; set; }

    public DateTime? OrderDate { get; set; }

    public DateTime? PaidDate { get; set; }

    public string? PaymentMethod { get; set; }

    public DateTime? ShippedDate { get; set; }

    public float? ShippingFee { get; set; }

    public int? TaxRate { get; set; }

    public decimal? TaxStatusId { get; set; }

    public int TaxId { get; set; }

    public virtual Company? Customer { get; set; }

    public virtual Employee? Employee { get; set; }

    public virtual OrderStatus OrderStatus { get; set; } = null!;

    public virtual Company? Shipper { get; set; }

    public virtual TaxStatus Tax { get; set; } = null!;
}
