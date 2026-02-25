namespace NaturalStoneImpex.Client.Models;

public record OrderDetailDto
{
    public int Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public int Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public int CustomerType { get; init; }
    public string CustomerTypeDisplay { get; init; } = string.Empty;
    public int DeliveryMethod { get; init; }
    public string DeliveryMethodDisplay { get; init; } = string.Empty;
    public decimal? DeliveryFee { get; init; }
    public bool IsCancelled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public CustomerInfoDto CustomerInfo { get; init; } = null!;
    public List<OrderItemDto> Items { get; init; } = new();
    public decimal SubtotalWithoutVat { get; init; }
    public decimal TotalVat { get; init; }
    public decimal SubtotalWithVat { get; init; }
    public decimal GrandTotal { get; init; }
}

public record CustomerInfoDto
{
    public string? FullName { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? CompanyName { get; init; }
    public string? Eik { get; init; }
    public string? Mol { get; init; }
    public string? ContactPerson { get; init; }
    public string? ContactPhone { get; init; }
}

public record OrderItemDto
{
    public int Id { get; init; }
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPriceWithoutVat { get; init; }
    public decimal VatAmount { get; init; }
    public decimal UnitPriceWithVat { get; init; }
    public int Unit { get; init; }
    public string UnitDisplay { get; init; } = string.Empty;
    public decimal RowTotalWithoutVat { get; init; }
    public decimal RowVatTotal { get; init; }
    public decimal RowTotalWithVat { get; init; }
}
