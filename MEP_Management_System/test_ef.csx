using System;
using System.IO;
using System.Linq;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json");
var config = builder.Build();

var optionsBuilder = new DbContextOptionsBuilder<MEPDatabaseContext>();
optionsBuilder.UseNpgsql(config.GetConnectionString("DefaultConnection"));

using var context = new MEPDatabaseContext(optionsBuilder.Options);

var branch = context.Branches.FirstOrDefault();
if (branch == null) {
    Console.WriteLine("No branch found.");
    return;
}
var user = context.Users.FirstOrDefault();

var cat = new ExpenseCategory {
    CategoryId = Guid.NewGuid(),
    CategoryName = "Test Soft Delete",
    IsActive = 0,
    BranchId = branch.BranchId,
    CreatedBy = user?.UserId,
    CreatedAt = DateTime.UtcNow
};
context.ExpenseCategories.Add(cat);

var voucher = new ExpenseVoucher {
    VoucherId = Guid.NewGuid(),
    VoucherCode = "PXT-TEST",
    BranchId = branch.BranchId,
    CategoryId = cat.CategoryId,
    Amount = 1000,
    Reason = "Test soft delete voucher",
    Status = "COMPLETED",
    CreatedBy = user?.UserId ?? Guid.Empty,
    CreatedAt = DateTime.UtcNow
};
context.ExpenseVouchers.Add(voucher);
context.SaveChanges();

var fetchedVoucher = context.ExpenseVouchers
    .Include(v => v.Category)
    .FirstOrDefault(v => v.VoucherId == voucher.VoucherId);

Console.WriteLine($"Voucher: {fetchedVoucher.VoucherCode}, Category: {fetchedVoucher.Category?.CategoryName}");

// Cleanup
context.ExpenseVouchers.Remove(fetchedVoucher);
context.ExpenseCategories.Remove(cat);
context.SaveChanges();
Console.WriteLine("Cleanup done.");
