using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace VDVT.Order.Services.Api.Models.Dto
{
    public class OrderDetailsDto
    {
        public int OrderDetailsId { get; set; }
        public int OrderHeaderId { get; set; }
        [JsonIgnore]
        public OrderHeader? OrderHeader { get; set; }
        public int ProductId { get; set; }
        public ProductDto ProductDto { get; set; }
        public int Count { get; set; }
        public string ProductName { get; set; }

        public double Price { get; set; }
    }
}
