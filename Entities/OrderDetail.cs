namespace SharpLlama.Entities;

public partial class OrderDetail
{
    public short OrderDetailId { get; set; }

    public short? OrderId { get; set; }

    public short? OrderDetailStatusId { get; set; }

    public short? ProductId { get; set; }

    public int? Discount { get; set; }

    public short? Quantity { get; set; }

    public float? UnitPrice { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
