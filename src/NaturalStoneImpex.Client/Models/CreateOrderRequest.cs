namespace NaturalStoneImpex.Client.Models;

public class CreateOrderRequest
{
    public int CustomerType { get; set; }
    public int DeliveryMethod { get; set; }
    public CreateOrderCustomerInfo CustomerInfo { get; set; } = new();
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderCustomerInfo
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? CompanyName { get; set; }
    public string? Eik { get; set; }
    public string? Mol { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
}

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}
