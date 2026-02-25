using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Services;

public interface IInvoiceService
{
    Task<PaginatedResponse<InvoiceListDto>> GetAllAsync(int page, int pageSize);
    Task<InvoiceDetailDto> GetByIdAsync(int id);
    Task<CreateInvoiceResponse> CreateAsync(CreateInvoiceRequest request);
}
