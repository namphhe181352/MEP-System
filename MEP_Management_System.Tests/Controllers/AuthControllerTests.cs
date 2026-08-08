using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AuthenticationDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class AuthControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IMapper> _mockMapper;
        private Mock<IEmailService> _mockEmailService;
        private JwtHelper _jwtHelper;

        public AuthControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
            _mockEmailService = new Mock<IEmailService>();

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["Jwt:Key"]).Returns("ThisIsAVerySecretKeyForJwtAuthentication123!_MustBeLongEnough");
            mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("MEP_System_Test");
            mockConfig.Setup(c => c["Jwt:Audience"]).Returns("MEP_System_Clients");
            _jwtHelper = new JwtHelper(mockConfig.Object);
        }

        private authController CreateController(MEPDatabaseContext context)
        {
            return new authController(context, _mockMapper.Object, _jwtHelper, _mockEmailService.Object);
        }

        // ==========================================
        // TEST SELF REGISTRATION (DISABLED)
        // ==========================================
        [Fact]
        public async Task RegisterVerify_Returns410Gone_BecauseSelfRegistrationIsDisabled()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var request = new RegisterVerifyDto 
            { 
                Email = "hacker.wannabe@mep.vn", 
                OtpCode = "123456", 
                Password = "Password123!", 
                FullName = "Hacker", 
                BranchName = "Chi nhánh Ảo" 
            };

            // Act
            var result = await controller.RegisterVerify(request);

            // Assert
            var goneResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status410Gone, goneResult.StatusCode);
            
            var valueType = goneResult.Value.GetType();
            var message = valueType.GetProperty("message")?.GetValue(goneResult.Value, null) as string;
            
            Assert.NotNull(message);
            Assert.Contains("ngừng hoạt động", message); // Feature disabled
            Assert.Contains("Admin", message); // Needs admin
        }

        // ==========================================
        // TEST LOGIN (RBAC FLOW)
        // ==========================================
        [Fact]
        public async Task Login_ValidOwner_ReturnsTokenAndRoles_WithBranchAssigned()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var roleOwner = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            context.Roles.Add(roleOwner);
            
            var branchId = Guid.NewGuid();
            var branch = new Branch { BranchId = branchId, BranchName = "MEP Tân Bình", IsActive = 1 };
            context.Branches.Add(branch);

            // Using BCrypt for password hashing as used in the controller
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("RealPass123!");
            
            var ownerUser = new User 
            { 
                UserId = Guid.NewGuid(), 
                FullName = "Lý Hải", 
                Email = "hai.ly@mep.vn", 
                Username = "hai.ly", 
                PasswordHash = passwordHash,
                IsActive = 1,
                IsVerified = true,
                Status = "ACTIVE"
            };
            ownerUser.Roles.Add(roleOwner);
            context.Users.Add(ownerUser);
            
            context.StaffAssignments.Add(new StaffAssignment { UserId = ownerUser.UserId, BranchId = branchId });
            await context.SaveChangesAsync();
            
            _mockMapper.Setup(m => m.Map<UserResponseDto>(It.IsAny<User>()))
                .Returns(new UserResponseDto { UserId = ownerUser.UserId, Email = ownerUser.Email, FullName = ownerUser.FullName });

            var request = new LoginDto { Email = "hai.ly@mep.vn", Password = "RealPass123!" };

            // Act
            var result = await controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var valueType = okResult.Value.GetType();
            
            var rolesObj = valueType.GetProperty("roles")?.GetValue(okResult.Value, null) as List<string>;
            var dataObj = valueType.GetProperty("data")?.GetValue(okResult.Value, null) as UserResponseDto;
            var msg = valueType.GetProperty("message")?.GetValue(okResult.Value, null) as string;

            Assert.Equal("Đăng nhập thành công!", msg);
            Assert.NotNull(rolesObj);
            Assert.Contains("Owner", rolesObj);
            
            Assert.NotNull(dataObj);
            Assert.Equal("hai.ly@mep.vn", dataObj.Email);
            Assert.NotNull(dataObj.Token); // JWT Token generated
        }

        [Fact]
        public async Task Login_LockedAccount_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword("RealPass123!");
            var lockedUser = new User 
            { 
                UserId = Guid.NewGuid(), 
                FullName = "Bị Khóa", 
                Email = "locked@mep.vn", 
                Username = "locked", 
                PasswordHash = passwordHash,
                IsActive = 0, // LOCKED
                IsVerified = true,
                Status = "SUSPENDED"
            };
            context.Users.Add(lockedUser);
            await context.SaveChangesAsync();

            var request = new LoginDto { Email = "locked@mep.vn", Password = "RealPass123!" };

            // Act
            var result = await controller.Login(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var valueType = badRequestResult.Value.GetType();
            var message = valueType.GetProperty("message")?.GetValue(badRequestResult.Value, null) as string;
            
            Assert.Contains("khóa", message);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword("RealPass123!");
            var user = new User 
            { 
                UserId = Guid.NewGuid(), 
                Email = "valid@mep.vn", 
                PasswordHash = passwordHash,
                IsActive = 1,
                IsVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new LoginDto { Email = "valid@mep.vn", Password = "WrongPassword!" };

            // Act
            var result = await controller.Login(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var valueType = badRequestResult.Value.GetType();
            var message = valueType.GetProperty("message")?.GetValue(badRequestResult.Value, null) as string;
            
            Assert.Equal("Mật khẩu không chính xác.", message);
        }
    }
}
