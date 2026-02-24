namespace NaturalStoneImpex.Api.Models.Entities;

public class OrderCustomerInfo
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? CompanyName { get; set; }
    public string? Eik { get; set; }
    public string? Mol { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }

    public Order Order { get; set; } = null!;
}
