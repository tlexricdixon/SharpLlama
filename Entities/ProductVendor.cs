namespace Entities;

public partial class ProductVendor
{
    public short ProductVendorId { get; set; }

    public short VendorId { get; set; }

    public short ProductId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Company Vendor { get; set; } = null!;
}
