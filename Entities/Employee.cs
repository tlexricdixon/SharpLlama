namespace Entities;

public partial class Employee
{
    public short EmployeeId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? Attachments { get; set; }

    public string? EmailAddress { get; set; }

    public string? FirstName { get; set; }

    public string? JobTitle { get; set; }

    public string? LastName { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? Notes { get; set; }

    public string? PrimaryPhone { get; set; }

    public string? SecondaryPhone { get; set; }

    public short? SupervisorId { get; set; }

    public string? Title { get; set; }

    public string? WindowsUserName { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<PurchaseOrder> PurchaseOrderApprovedBies { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<PurchaseOrder> PurchaseOrderSubmittedBies { get; set; } = new List<PurchaseOrder>();
}
