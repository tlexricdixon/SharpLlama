namespace Entities;

public partial class EmployeePrivilege
{
    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public short? EmployeeId { get; set; }

    public short? EmployeePrivilegeId { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public short? PrivilegeId { get; set; }
}
