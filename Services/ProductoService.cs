using Newtonsoft.Json;
using VDVT.Order.Services.Api.Contract;
using VDVT.Order.Services.Api.Models.Dto;

namespace VDVT.Order.Services.Api.Services
{
    public class ProductoService : IProductoService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ProductoService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IEnumerable<ProductDto>> GetProductos()
        {
            var client = _httpClientFactory.CreateClient("Product");
            var response = await client.GetAsync($"/api/Product");
            var apiContent = await response.Content.ReadAsStringAsync();
            var resp = JsonConvert.DeserializeObject<ResponseDto>(apiContent);
            if (resp.IsSuccess)
            {
                return JsonConvert.DeserializeObject<IEnumerable<ProductDto>>(Convert.ToString(resp.Result));
            }
            return new List<ProductDto>();
        }
    }
}
