namespace Entities;

public partial class StockTake
{
    public short StockTakeId { get; set; }

    public string? AddedBy { get; set; }

    public DateTime? AddedOn { get; set; }

    public short? ExpectedQuantity { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public short? ProductId { get; set; }

    public short? QuantityOnHand { get; set; }

    public DateTime? StockTakeDate { get; set; }
}
