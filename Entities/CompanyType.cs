namespace SharpLlama.Entities;

public partial class CompanyType
{
    public short CompanyTypeId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? CompanyType1 { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public virtual ICollection<Company> Companies { get; set; } = new List<Company>();
}
