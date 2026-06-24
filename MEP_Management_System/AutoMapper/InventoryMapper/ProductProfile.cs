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
            // Cấu hình từ DTO sang Entity Product gốc (Chiều ghi dữ liệu)
            CreateMap<ProductUpsertDto, Product>()
                .ForMember(dest => dest.BaseUnit, opt => opt.MapFrom(src => src.Unit))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.IsActive ? (short)1 : (short)0))
                .ForMember(dest => dest.SalesChannelLinked, opt => opt.MapFrom(src => src.SalesChannelLinked ? (short)1 : (short)0))
                .ForMember(dest => dest.MediaResources, opt => opt.Ignore())
                .ForMember(dest => dest.ProductAttributes, opt => opt.Ignore())
                .ForMember(dest => dest.ProductConversionUnits, opt => opt.Ignore())

                // 🎯 THÊM VÀO: Bỏ qua các trường này để tránh bị Client gửi đè làm mất log gốc khi sửa sản phẩm
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore());
        }
    }
}