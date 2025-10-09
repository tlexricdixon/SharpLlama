namespace Entities;

public partial class Company
{
    public short CompanyId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? Address { get; set; }

    public string? BusinessPhone { get; set; }

    public string? City { get; set; }

    public string? CompanyName { get; set; }

    public short CompanyTypeId { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? Notes { get; set; }

    public decimal? StandardTaxStatusId { get; set; }

    public string? StateAbbrev { get; set; }

    public string? Website { get; set; }

    public string? Zip { get; set; }

    public virtual CompanyType CompanyType { get; set; } = null!;

    public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();

    public virtual ICollection<Order> OrderCustomers { get; set; } = new List<Order>();

    public virtual ICollection<Order> OrderShippers { get; set; } = new List<Order>();

    public virtual ICollection<ProductVendor> ProductVendors { get; set; } = new List<ProductVendor>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
