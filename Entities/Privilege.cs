namespace Entities;

public partial class Privilege
{
    public short PrivilegeId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? PrivilegeName { get; set; }
}
