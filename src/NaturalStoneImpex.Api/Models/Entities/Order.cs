namespace NaturalStoneImpex.Api.Models.Entities;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DeliveryMethod DeliveryMethod { get; set; }
    public decimal? DeliveryFee { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public OrderCustomerInfo CustomerInfo { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
