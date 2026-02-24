namespace NaturalStoneImpex.Api.Models.DTOs;

public record CreateOrderRequest
{
    public int CustomerType { get; init; }
    public int DeliveryMethod { get; init; }
    public CreateOrderCustomerInfoRequest CustomerInfo { get; init; } = null!;
    public List<CreateOrderItemRequest> Items { get; init; } = new();
}

public record CreateOrderCustomerInfoRequest
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

public record CreateOrderItemRequest
{
    public int ProductId { get; init; }
    public decimal Quantity { get; init; }
}
