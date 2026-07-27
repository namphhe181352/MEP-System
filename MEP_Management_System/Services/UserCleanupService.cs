using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MEP_Management_System.Services;

/// <summary>
/// Background service to automatically purge soft-deleted users after retention period.
/// Runs every 24 hours and removes users where Status = "DELETED" and DeletedAt < cutoff.
/// </summary>
public class UserCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserCleanupService> _logger;
    private readonly int _retentionDays;

    public UserCleanupService(
        IServiceProvider serviceProvider,
        ILogger<UserCleanupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _retentionDays = configuration.GetValue<int>("AccountSettings:RetentionDays", 15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UserCleanupService started. Retention days: {RetentionDays}", _retentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeExpiredUsersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UserCleanupService");
            }

            // Run every 24 hours
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task PurgeExpiredUsersAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();

        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

        var expiredUsers = await context.Users
            .Where(u => u.Status == "DELETED" && u.DeletedAt.HasValue && u.DeletedAt < cutoff)
            .ToListAsync(stoppingToken);

        if (expiredUsers.Any())
        {
            _logger.LogInformation("Purging {Count} expired soft-deleted users", expiredUsers.Count);

            context.Users.RemoveRange(expiredUsers);
            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Successfully purged {Count} users", expiredUsers.Count);
        }
        else
        {
            _logger.LogDebug("No expired users to purge");
        }
    }
}
