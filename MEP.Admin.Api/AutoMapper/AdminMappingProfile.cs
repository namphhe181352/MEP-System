using MEP.Admin.Api.DTOs;
using MEP.Data.Models;
using AutoMapper;
using System.Text.Json;

namespace MEP.Admin.Api.AutoMapper;

public class AdminMappingProfile : Profile
{
    public AdminMappingProfile()
    {
        // User -> StaffAccountDto
        CreateMap<User, StaffAccountDto>()
            .ForMember(d => d.Roles, o => o.MapFrom(s => s.Roles.Select(r => r.RoleName).ToList()))
            .ForMember(d => d.BranchName, o => o.MapFrom(s =>
                s.StaffAssignments.FirstOrDefault().Branch != null
                    ? s.StaffAssignments.FirstOrDefault().Branch!.BranchName
                    : null));

        // User -> CommunityUserDto
        CreateMap<User, CommunityUserDto>();

        // CommunityPost -> PostDto
        CreateMap<CommunityPost, PostDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.CategoryName : null))
            .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null))
            .ForMember(d => d.AuthorEmail, o => o.MapFrom(s => s.User != null ? s.User.Email : null))
            .ForMember(d => d.CommentCount, o => o.MapFrom(s => s.Comments.Count))
            .ForMember(d => d.ReportCount, o => o.MapFrom(s => s.ViolationReports.Count));

        // ViolationReport -> ViolationReportDto
        CreateMap<ViolationReport, ViolationReportDto>()
            .ForMember(d => d.PostTitle, o => o.MapFrom(s => s.Post != null ? s.Post.Title : null))
            .ForMember(d => d.CommentText, o => o.MapFrom(s => s.Comment != null ? s.Comment.CommentText : null))
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
            .ForMember(d => d.UserEmail, o => o.MapFrom(s => s.User != null ? s.User.Email : null))
            .ForMember(d => d.UserFullName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null))
            .ForMember(d => d.ReviewedByName, o => o.MapFrom(s => s.ReviewedByUser != null ? s.ReviewedByUser.FullName : null))
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
