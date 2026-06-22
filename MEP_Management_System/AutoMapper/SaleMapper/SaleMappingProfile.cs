using System;
using System.Linq;
using AutoMapper;
using MEP_Management_System.Models;

namespace MEP_Management_System.AutoMapper.SaleMapper;

public class SaleMappingProfile : Profile
{
    public SaleMappingProfile()
    {
        // Invoice
        CreateMap<Invoice, DTOs.SaleDTOs.InvoiceDto>()
            .ForMember(d => d.Subtotal, o => o.MapFrom(s => s.InvoiceItems.Sum(i => i.Quantity * i.SellPrice)))
            .ForMember(d => d.TaxAmount, o => o.MapFrom(s => Math.Round(s.InvoiceItems.Sum(i => i.Quantity * i.SellPrice) * 0.08m, 2)))
            .ForMember(d => d.DiscountAmount, o => o.MapFrom(s => s.Order != null ? s.Order.DiscountAmount ?? 0 : 0))
            .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Order != null && s.Order.Customer != null ? s.Order.Customer.CustomerName : null))
            .ForMember(d => d.CustomerId, o => o.MapFrom(s => s.Order != null ? s.Order.CustomerId : (Guid?)null))
            .ForMember(d => d.PromotionCode, o => o.MapFrom(s => s.Order != null ? s.Order.PromotionCode : null))
            .ForMember(d => d.Items, o => o.MapFrom(s => s.InvoiceItems));

        CreateMap<InvoiceItem, DTOs.SaleDTOs.InvoiceItemDto>()
            .ForMember(d => d.LineTotal, o => o.MapFrom(s => s.Quantity * s.SellPrice))
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.ProductName : null))
            .ForMember(d => d.ProductCode, o => o.MapFrom(s => s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.ProductCode : null))
            .ForMember(d => d.Barcode, o => o.MapFrom(s => s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.Barcode : null))
            .ForMember(d => d.Unit, o => o.MapFrom(s => s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.BaseUnit : null));

        // Payment
        CreateMap<Payment, DTOs.SaleDTOs.PaymentDto>();

        // Promotion
        CreateMap<Promotion, DTOs.SaleDTOs.PromotionDto>()
            .ForMember(d => d.DiscountAmount, o => o.Ignore());

        // Shift
        CreateMap<Shift, DTOs.SaleDTOs.ShiftDto>()
            .ForMember(d => d.UserName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null));

        // Customer
        CreateMap<Customer, DTOs.SaleDTOs.CustomerDto>()
            .ForMember(d => d.MembershipTier, o => o.MapFrom(s => CalculateTier(s.TotalSpent)));

        CreateMap<DTOs.SaleDTOs.CreateCustomerRequest, Customer>()
            .ForMember(d => d.CustomerId, o => o.Ignore())
            .ForMember(d => d.LoyaltyPoints, o => o.MapFrom(_ => 0))
            .ForMember(d => d.CurrentDebt, o => o.MapFrom(_ => 0m))
            .ForMember(d => d.Status, o => o.MapFrom(_ => (short)1))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(_ => DateTime.UtcNow))
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(_ => DateTime.UtcNow))
            .ForMember(d => d.TotalSpent, o => o.MapFrom(_ => 0m))
            .ForMember(d => d.OrderCount, o => o.MapFrom(_ => 0));

        CreateMap<DTOs.SaleDTOs.UpdateCustomerRequest, Customer>()
            .ForMember(d => d.CustomerId, o => o.Ignore())
            .ForMember(d => d.LoyaltyPoints, o => o.Ignore())
            .ForMember(d => d.CurrentDebt, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.TotalSpent, o => o.Ignore())
            .ForMember(d => d.OrderCount, o => o.Ignore())
            .ForMember(d => d.LastVisit, o => o.Ignore())
            .ForMember(d => d.MembershipTier, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.MapFrom(_ => DateTime.UtcNow));

        // LoyaltyTransaction -> LoyaltyHistoryDto
        CreateMap<LoyaltyTransaction, DTOs.SaleDTOs.LoyaltyPointsDto.LoyaltyHistoryDto>();

        // Receipt
        CreateMap<Receipt, DTOs.SaleDTOs.ReceiptDto>();
    }

    private static string? CalculateTier(decimal? totalSpent)
    {
        if (!totalSpent.HasValue) return "Bronze";
        if (totalSpent >= 50000000) return "Platinum";
        if (totalSpent >= 20000000) return "Gold";
        if (totalSpent >= 5000000) return "Silver";
        return "Bronze";
    }
}
