using AutoMapper;
using MEP.Auth.Api.DTOs.Authentication_DTOs;
using MEP.Data.Models;

namespace MEP.Data.AutoMapper
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Ánh xạ từ Entity dưới Database sang DTO phản hồi cho Client (Dùng tại hàm Login và RegisterVerify)
            // Tự động đồng bộ vì cả 2 trường UserId giờ đều là kiểu dữ liệu Guid
            CreateMap<User, UserResponseDto>()
                .ForMember(dest => dest.Token, opt => opt.Ignore())  // Sẽ được gán thủ công Token ở Controller
                .ForMember(dest => dest.Roles, opt => opt.Ignore()); // Sẽ được gán thủ công danh sách quyền ở Controller
        }
    }
}