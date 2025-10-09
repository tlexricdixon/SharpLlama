namespace SharpLlama.Entities;

public partial class ProductCategory
{
    public short ProductCategoryId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public string? ProductCategoryCode { get; set; }

    public string? ProductCategoryDesc { get; set; }

    public string? ProductCategoryImage { get; set; }

    public string? ProductCategoryName { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
