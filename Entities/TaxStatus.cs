namespace Entities;

public partial class TaxStatus
{
    public int TaxId { get; set; }

    public decimal? TaxStatusId { get; set; }

    public string? TaxStatus1 { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
