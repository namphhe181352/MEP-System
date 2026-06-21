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

        // User -> CommunityUserDto
        CreateMap<User, CommunityUserDto>();

        // CommunityPost -> PostDto
        CreateMap<CommunityPost, PostDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.CategoryName : null))
            .ForMember(d => d.AuthorId, o => o.MapFrom(s => s.User != null ? s.User.UserId : Guid.Empty))
            .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null))
            .ForMember(d => d.AuthorEmail, o => o.MapFrom(s => s.User != null ? s.User.Email : null))
            .ForMember(d => d.CommentCount, o => o.MapFrom(s => s.Comments.Count))
            .ForMember(d => d.ReportCount, o => o.MapFrom(s => s.ViolationReports.Count));

        // ViolationReport -> ViolationReportDto
        CreateMap<ViolationReport, ViolationReportDto>()
            .ForMember(d => d.PostTitle, o => o.MapFrom(s => s.Post != null ? s.Post.Title : null))
            .ForMember(d => d.CommentText, o => o.MapFrom(s => s.Comment != null ? s.Comment.CommentText : null))
            .ForMember(d => d.ReportedUserId, o => o.MapFrom(s => s.Post != null ? s.Post.UserId : Guid.Empty))
            .ForMember(d => d.ReporterName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null))
            .ForMember(d => d.ReporterEmail, o => o.MapFrom(s => s.User != null ? s.User.Email : null))
            .ForMember(d => d.ResolvedByName, o => o.MapFrom(s => s.ResolvedByUser != null ? s.ResolvedByUser.FullName : null));

        // CommunityCategory -> CommunityCategoryDto
        CreateMap<CommunityCategory, CommunityCategoryDto>()
            .ForMember(d => d.ParentName, o => o.MapFrom(s => s.Parent != null ? s.Parent.CategoryName : null))
            .ForMember(d => d.PostCount, o => o.MapFrom(s => s.CommunityPosts.Count));

        // SystemNotification -> SystemNotificationDto
        CreateMap<SystemNotification, SystemNotificationDto>()
            .ForMember(d => d.CreatedByName, o => o.MapFrom(s => s.CreatedByUser != null ? s.CreatedByUser.FullName : null));

        // SystemLog -> SystemLogDto
        CreateMap<SystemLog, SystemLogDto>()
            .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null));

        // SystemLog -> SystemEventDto
        CreateMap<SystemLog, SystemEventDto>()
            .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null));

        // StoreApproval -> StoreApprovalDto
        CreateMap<StoreApproval, StoreApprovalDto>()
            .ForMember(dest => dest.OwnerEmail, opt => opt.MapFrom(src => src.User != null ? src.User.Email : null))
            .ForMember(dest => dest.OwnerName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : null))
            .ForMember(dest => dest.ReviewerName, opt => opt.MapFrom(src => src.ReviewedByUser != null ? src.ReviewedByUser.FullName : null))
            .ForMember(d => d.DocumentUrls, o => o.MapFrom(s => ParseDocumentUrls(s.DocumentUrls)));
    }

    private static List<string> ParseDocumentUrls(string? urls)
    {
        if (string.IsNullOrEmpty(urls)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(urls) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
