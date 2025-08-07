using VDVT.Order.Services.Api.Models.Dto;

namespace VDVT.Order.Services.Api.Contract
{
    public interface IProductoService
    {
        Task<IEnumerable<ProductDto>> GetProductos();
    }
}
