using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AutoMapper;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AccountSettings_DTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace MEP_Management_System.Tests;

public class AuthControllerTests
{
    private static MEPDatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MEPDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MEPDatabaseContext(options);
    }

    private static authController CreateController(MEPDatabaseContext context, IEmailService? emailService = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-key-for-tests-12345",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience"
            })
            .Build();

        var jwtHelper = new JwtHelper(configuration);
        var mapper = new MapperConfiguration(cfg => cfg.CreateMap<User, UserResponseDto>()).CreateMapper();
        return new authController(context, mapper, jwtHelper, emailService ?? Mock.Of<IEmailService>());
    }

    [Fact]
    public async Task RegisterStart_WhenEmailIsAvailable_SavesOtpAndSendsEmail()
    {
        await using var context = CreateContext();
        var emailService = new Mock<IEmailService>();
        var controller = CreateController(context, emailService.Object);

        var result = await controller.RegisterStart(new RegisterStartDto { Email = "newuser@gmail.com" });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("được gửi", ok.Value!.ToString()!);
        Assert.Single(await context.OtpVerifications.ToListAsync());
        emailService.Verify(x => x.SendOtpEmailAsync("newuser@gmail.com", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterStart_WhenEmailAlreadyExists_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            Username = "existing",
            Email = "existing@gmail.com",
            FullName = "Existing User",
            PasswordHash = "hash",
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.RegisterStart(new RegisterStartDto { Email = "existing@gmail.com" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void RegisterStartDto_WhenEmailFormatIsInvalid_FailsValidation()
    {
        var dto = new RegisterStartDto { Email = "invalid-email" };
        var results = new List<ValidationResult>();
        var context = new ValidationContext(dto);

        var isValid = Validator.TryValidateObject(dto, context, results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RegisterVerify_WhenOtpIsValid_CreatesUserBranchAndMarksOtpUsed()
    {
        await using var context = CreateContext();
        context.OtpVerifications.Add(new OtpVerification
        {
            OtpId = Guid.NewGuid(),
            Email = "owner@gmail.com",
            OtpCode = "123456",
            Purpose = "REGISTER",
            ExpiredAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(10), DateTimeKind.Unspecified),
            IsUsed = 0
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.RegisterVerify(new RegisterVerifyDto
        {
            Email = "owner@gmail.com",
            OtpCode = "123456",
            Password = "Strong123!",
            FullName = "Owner Store",
            BranchName = "Main Branch"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("Đăng ký thành công", ok.Value!.ToString()!);
        var createdUser = await context.Users.SingleAsync(u => u.Email == "owner@gmail.com");
        Assert.True(createdUser.IsVerified);
        var createdBranch = await context.Branches.SingleAsync();
        Assert.Equal("Main Branch", createdBranch.BranchName);
        Assert.Equal(createdBranch.BranchId, createdUser.DefaultBranchId);

        var otp = await context.OtpVerifications.SingleAsync();
        Assert.Equal((short)1, otp.IsUsed);
    }

    [Fact]
    public async Task RegisterVerify_WhenOtpIsExpired_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        context.OtpVerifications.Add(new OtpVerification
        {
            OtpId = Guid.NewGuid(),
            Email = "owner@gmail.com",
            OtpCode = "123456",
            Purpose = "REGISTER",
            ExpiredAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-5), DateTimeKind.Unspecified),
            IsUsed = 0
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.RegisterVerify(new RegisterVerifyDto
        {
            Email = "owner@gmail.com",
            OtpCode = "123456",
            Password = "Strong123!",
            FullName = "Owner Store",
            BranchName = "Main Branch"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void RegisterVerifyDto_WhenPasswordIsTooShort_FailsValidation()
    {
        var dto = new RegisterVerifyDto
        {
            Email = "owner@gmail.com",
            OtpCode = "123456",
            Password = "123",
            FullName = "Owner Store",
            BranchName = "Main Branch"
        };

        var results = new List<ValidationResult>();
        var context = new ValidationContext(dto);
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("6 ký tự", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_ReturnsToken()
    {
        await using var context = CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = "staff",
            Email = "staff@gmail.com",
            FullName = "Staff User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            DefaultBranchId = Guid.NewGuid()
        };

        var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff", Description = "Staff role", IsSystem = 0 };
        user.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.Login(new LoginDto { Email = "staff@gmail.com", Password = "Password123!" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = ok.Value!;
        var dataProperty = response.GetType().GetProperty("data");
        var data = dataProperty!.GetValue(response);
        var tokenProperty = data!.GetType().GetProperty("Token");
        var token = tokenProperty!.GetValue(data) as string;

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Login_WhenEmailDoesNotExist_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.Login(new LoginDto { Email = "missing@gmail.com", Password = "Password123!" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_WhenPasswordIsWrong_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            UserId = Guid.NewGuid(),
            Username = "staff",
            Email = "staff@gmail.com",
            FullName = "Staff User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            DefaultBranchId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var result = await controller.Login(new LoginDto { Email = "staff@gmail.com", Password = "WrongPass!" });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}

public class AccountSettingsControllerTests
{
    private static MEPDatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MEPDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MEPDatabaseContext(options);
    }

    private static AccountSettingsController CreateController(MEPDatabaseContext context)
    {
        return new AccountSettingsController(context);
    }

    private static void SetAuthenticatedUser(ControllerBase controller, Guid userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "TestAuth");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetProfile_WhenUserIsAuthenticated_ReturnsProfile()
    {
        await using var context = CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = "owner",
            Email = "owner@gmail.com",
            FullName = "Owner User",
            PasswordHash = "hash",
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            Status = "ACTIVE"
        };
        user.Roles.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "Owner", Description = "Owner role", IsSystem = 1 });
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        SetAuthenticatedUser(controller, user.UserId);

        var result = await controller.GetProfile();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AccountProfileDto>>(ok.Value);

        Assert.True(response.Success);
        Assert.Equal("Owner User", response.Data?.FullName);
    }

    [Fact]
    public async Task UpdateProfile_WhenPhoneNumberIsAlreadyUsed_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var currentUser = new User
        {
            UserId = Guid.NewGuid(),
            Username = "current",
            Email = "current@gmail.com",
            FullName = "Current User",
            PasswordHash = "hash",
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        var otherUser = new User
        {
            UserId = Guid.NewGuid(),
            Username = "other",
            Email = "other@gmail.com",
            FullName = "Other User",
            PasswordHash = "hash",
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            PhoneNumber = "01234567890"
        };
        context.Users.AddRange(currentUser, otherUser);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        SetAuthenticatedUser(controller, currentUser.UserId);

        var result = await controller.UpdateProfile(new UpdateProfileRequest
        {
            FullName = "Current User Updated",
            PhoneNumber = "01234567890"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AccountProfileDto>>(badRequest.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task UpdateProfile_WhenPayloadIsValid_UpdatesProfile()
    {
        await using var context = CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = "current",
            Email = "current@gmail.com",
            FullName = "Current User",
            PasswordHash = "hash",
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        SetAuthenticatedUser(controller, user.UserId);

        var result = await controller.UpdateProfile(new UpdateProfileRequest
        {
            FullName = "Current User Updated",
            PhoneNumber = "0987654321"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AccountProfileDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("Current User Updated", response.Data?.FullName);
        Assert.Equal("0987654321", response.Data?.PhoneNumber);
    }

    [Fact]
    public void UpdateProfileRequest_WhenPhoneNumberFormatIsInvalid_FailsValidation()
    {
        var dto = new UpdateProfileRequest
        {
            FullName = "Valid Name",
            PhoneNumber = "123"
        };

        var results = new List<ValidationResult>();
        var context = new ValidationContext(dto);
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Số điện thoại", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ChangePassword_WhenCurrentPasswordMatches_UpdatesPassword()
    {
        await using var context = CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = "current",
            Email = "current@gmail.com",
            FullName = "Current User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        SetAuthenticatedUser(controller, user.UserId);

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(ok.Value);
        Assert.True(response.Success);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123!", user.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_WhenCurrentPasswordIsIncorrect_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = "current",
            Email = "current@gmail.com",
            FullName = "Current User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!"),
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        SetAuthenticatedUser(controller, user.UserId);

        var result = await controller.ChangePassword(new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword!",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<bool>>(badRequest.Value);
        Assert.False(response.Success);
    }

    [Fact]
    public void ChangePasswordRequest_WhenNewPasswordIsTooShort_FailsValidation()
    {
        var dto = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "123",
            ConfirmPassword = "123"
        };

        var results = new List<ValidationResult>();
        var context = new ValidationContext(dto);
        var isValid = Validator.TryValidateObject(dto, context, results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("ít nhất 6 ký tự", StringComparison.OrdinalIgnoreCase));
    }
}

public class UsersControllerTests
{
    private static MEPDatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MEPDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MEPDatabaseContext(options);
    }

    private static UsersController CreateController(MEPDatabaseContext context)
    {
        return new UsersController(context);
    }

    private static void SetAuthenticatedUser(ControllerBase controller, Guid userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "TestAuth");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task ChangeDefaultBranch_WhenUserHasAccess_UpdatesDefaultBranch()
    {
        await using var context = CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = "manager",
            Email = "manager@gmail.com",
            FullName = "Manager User",
            PasswordHash = "hash",
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        var branch = new Branch { BranchId = Guid.NewGuid(), BranchName = "Branch A", IsActive = 1, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        context.Branches.Add(branch);
        context.StaffAssignments.Add(new StaffAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = user.UserId,
            BranchId = branch.BranchId,
            AssignedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        SetAuthenticatedUser(controller, user.UserId);

        var result = await controller.ChangeDefaultBranch(branch.BranchId);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("Default branch changed", ok.Value!.ToString()!);
        var updatedUser = await context.Users.SingleAsync(u => u.UserId == user.UserId);
        Assert.Equal(branch.BranchId, updatedUser.DefaultBranchId);
    }

    [Fact]
    public async Task ChangeDefaultBranch_WhenUserHasNoAccess_ReturnsForbidden()
    {
        await using var context = CreateContext();
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = "manager",
            Email = "manager@gmail.com",
            FullName = "Manager User",
            PasswordHash = "hash",
            IsActive = 1,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        var branch = new Branch { BranchId = Guid.NewGuid(), BranchName = "Branch A", IsActive = 1, CreatedAt = DateTime.UtcNow };
        context.Users.Add(user);
        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        SetAuthenticatedUser(controller, user.UserId);

        var result = await controller.ChangeDefaultBranch(branch.BranchId);

        Assert.IsType<ObjectResult>(result);
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);
    }
}
