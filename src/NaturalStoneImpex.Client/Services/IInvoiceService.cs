using NaturalStoneImpex.Client.Models;

namespace NaturalStoneImpex.Client.Services;

public interface IInvoiceService
{
    Task<PaginatedResponse<InvoiceListDto>> GetAllAsync(int page, int pageSize);
    Task<InvoiceDetailDto> GetByIdAsync(int id);
    Task<(CreateInvoiceResponse? Response, string? Error)> CreateAsync(CreateInvoiceRequest request);
}
