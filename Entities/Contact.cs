namespace Entities;

public partial class Contact
{
    public short ContactId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public short CompanyId { get; set; }

    public string? EmailAddress { get; set; }

    public string? FirstName { get; set; }

    public string? JobTitle { get; set; }

    public string? LastName { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? Notes { get; set; }

    public string? PrimaryPhone { get; set; }

    public string? SecondaryPhone { get; set; }

    public virtual Company Company { get; set; } = null!;
}
