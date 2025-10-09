namespace SharpLlama.Entities;

public partial class PurchaseOrderStatus
{
    public short StatusId { get; set; }

    public string? StatusName { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public decimal? SortOrder { get; set; }

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
