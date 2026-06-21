using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;

namespace MEP_Management_System.AutoMapper.InventoryMapper
{
    public class InventoryAutoMapper : Profile
    {
        public InventoryAutoMapper()
        {
            // Cấu hình từ DTO sang Entity Product gốc
            CreateMap<ProductUpsertDto, Product>()
                .ForMember(dest => dest.BaseUnit, opt => opt.MapFrom(src => src.Unit))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.IsActive ? (short)1 : (short)0))
                .ForMember(dest => dest.SalesChannelLinked, opt => opt.MapFrom(src => src.SalesChannelLinked ? (short)1 : (short)0))
                .ForMember(dest => dest.MediaResources, opt => opt.Ignore())
                .ForMember(dest => dest.ProductAttributes, opt => opt.Ignore())
                .ForMember(dest => dest.ProductConversionUnits, opt => opt.Ignore());
        }
    }
}
