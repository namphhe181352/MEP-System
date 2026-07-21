using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using AutoMapper;
using System.Text.Json;

namespace MEP_Management_System.AutoMapper;

public class AdminMappingProfile : Profile
{
    public AdminMappingProfile()
    {
        // User -> StaffAccountDto
        CreateMap<User, StaffAccountDto>()
            .ForMember(d => d.Roles, o => o.MapFrom(s => s.Roles.Select(r => r.RoleName).ToList()))
            .ForMember(d => d.BranchName, o => o.MapFrom(s =>
                s.StaffAssignments.FirstOrDefault()!.Branch!.BranchName));



        // SystemNotification -> SystemNotificationDto
        CreateMap<SystemNotification, SystemNotificationDto>()
            .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedByUser != null ? s.CreatedByUser.FullName : null));

        // SystemLog -> SystemLogDto
        CreateMap<SystemLog, SystemLogDto>()
            .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null));

        // SystemLog -> SystemEventDto
        CreateMap<SystemLog, SystemEventDto>()
            .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null));

    }
}
