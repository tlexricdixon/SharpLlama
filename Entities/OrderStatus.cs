namespace SharpLlama.Entities;

public partial class OrderStatus
{
    public short OrderStatusId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? OrderStatusCode { get; set; }

    public string? OrderStatusName { get; set; }

    public decimal? SortOrder { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
