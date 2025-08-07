using AutoMapper;
using VDVT.Order.Services.Api.Models;
using VDVT.Order.Services.Api.Models.Dto;

namespace VDVT.Order.Services.Api
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<OrderHeaderDto, CartHeaderDto>()
                .ForMember(destino => destino.CartTotal, u => u.MapFrom(src => src.OrderTotal))
                .ReverseMap();
                config.CreateMap<CartDetailsDto, OrderDetailsDto>()
                .ForMember(destino => destino.ProductName, u => u.MapFrom(src => src.ProductDto.Name))
                .ForMember(dest => dest.Price, u => u.MapFrom(src => src.ProductDto.Price));

                config.CreateMap<OrderDetailsDto, CartDetailsDto>();

                config.CreateMap<OrderHeader, OrderHeaderDto>().ReverseMap();
                config.CreateMap<OrderDetailsDto, OrderDetails>().ReverseMap();
            });
            return mappingConfig;
        }
    }
}
