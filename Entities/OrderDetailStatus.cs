namespace SharpLlama.Entities;

public partial class OrderDetailStatus
{
    public short OrderDetailStatusId { get; set; }

    public string? OrderDetailStatusName { get; set; }

    public decimal? SortOrder { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }
}
