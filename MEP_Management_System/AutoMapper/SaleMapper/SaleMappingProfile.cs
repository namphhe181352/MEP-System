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
            .ForMember(d => d.HoldNote, o => o.MapFrom(s => s.HoldNote))
            .ForMember(d => d.HeldAt, o => o.MapFrom(s => s.HeldAt))
            .ForMember(d => d.Subtotal, o => o.MapFrom(s => s.InvoiceItems.Sum(i => i.Quantity * i.SellPrice)))
            .ForMember(d => d.DiscountAmount, o => o.MapFrom(s => s.DiscountAmount))
            .ForMember(d => d.DiscountPercent, o => o.MapFrom(s => s.DiscountPercent))
            .ForMember(d => d.TotalAmount, o => o.MapFrom(s => s.TotalAmount))
            .ForMember(d => d.CustomerName, o => o.MapFrom(s => s.Order != null && s.Order.Customer != null ? s.Order.Customer.CustomerName : null))
            .ForMember(d => d.CustomerId, o => o.MapFrom(s => s.Order != null ? s.Order.CustomerId : (Guid?)null))
            .ForMember(d => d.Items, o => o.MapFrom(s => s.InvoiceItems));

        CreateMap<InvoiceItem, DTOs.SaleDTOs.InvoiceItemDto>()
            .ForMember(d => d.ProductId, o => o.MapFrom(s => s.BranchProduct != null ? s.BranchProduct.ProductId : Guid.Empty))
            .ForMember(d => d.UnitPrice, o => o.MapFrom(s => s.SellPrice))
            .ForMember(d => d.LineTotal, o => o.MapFrom(s => s.Quantity * s.SellPrice))
            .ForMember(d => d.ProductName, o => o.MapFrom(s => s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.ProductName : null))
            .ForMember(d => d.ProductCode, o => o.MapFrom(s => s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.ProductCode : null))
            .ForMember(d => d.Barcode, o => o.MapFrom(s => s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.Barcode : null))
            .ForMember(d => d.Unit, o => o.MapFrom(s => s.UnitName ?? (s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.BaseUnit : null)))
            .ForMember(d => d.SelectedUnit, o => o.MapFrom(s => s.SelectedUnit))
            .ForMember(d => d.ConvertValue, o => o.MapFrom(s => s.ConvertValue))
            .ForMember(d => d.CategoryId, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null
                    ? s.BranchProduct.Product.CategoryTaxonomyId : (Guid?)null))
            .ForMember(d => d.CategoryName, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null && s.BranchProduct.Product.CategoryTaxonomy != null
                    ? s.BranchProduct.Product.CategoryTaxonomy.Name : (s.BranchProduct != null && s.BranchProduct.Product != null
                        ? s.BranchProduct.Product.CategoryName : null)));

        // Payment
        CreateMap<Payment, DTOs.SaleDTOs.PaymentDto>();



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

        // Receipt
        CreateMap<Receipt, DTOs.SaleDTOs.ReceiptDto>();

        // ReturnOrder
        CreateMap<ReturnOrder, DTOs.SaleDTOs.ReturnOrderDto>()
            // InvoiceCode = mã hóa đơn gốc (lấy từ invoice của order)
            .ForMember(d => d.InvoiceCode, o => o.MapFrom(s =>
                s.Order != null && s.Order.Invoice != null ? s.Order.Invoice.InvoiceCode : null))
            .ForMember(d => d.CustomerId, o => o.MapFrom(s => s.Order != null ? s.Order.CustomerId : (Guid?)null))
            .ForMember(d => d.CustomerName, o => o.MapFrom(s =>
                s.Order != null && s.Order.Customer != null ? s.Order.Customer.CustomerName : null))
            .ForMember(d => d.StaffName, o => o.MapFrom(s => s.User != null ? s.User.FullName : null))
            .ForMember(d => d.Items, o => o.MapFrom(s => s.ReturnItems))
            .ForMember(d => d.ExchangeItems, o => o.MapFrom(s => s.ExchangeItems))
            .ForMember(d => d.DeltaLabel, o => o.MapFrom(s =>
                s.DeltaAmount.HasValue
                    ? (s.DeltaAmount.Value > 0 ? "Thu thêm"
                       : s.DeltaAmount.Value < 0 ? "Hoàn lại" : "Đổi ngang giá")
                    : null));

        CreateMap<ReturnItem, DTOs.SaleDTOs.ReturnItemDto>()
            .ForMember(d => d.ProductId, o => o.MapFrom(s => s.BranchProduct != null ? s.BranchProduct.ProductId : Guid.Empty))
            .ForMember(d => d.ProductName, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null
                    ? s.BranchProduct.Product.ProductName : null))
            .ForMember(d => d.ProductCode, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null
                    ? s.BranchProduct.Product.ProductCode : null))
            .ForMember(d => d.CategoryId, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null
                    ? s.BranchProduct.Product.CategoryTaxonomyId : (Guid?)null))
            .ForMember(d => d.CategoryName, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null && s.BranchProduct.Product.CategoryTaxonomy != null
                    ? s.BranchProduct.Product.CategoryTaxonomy.Name : (s.BranchProduct != null && s.BranchProduct.Product != null
                        ? s.BranchProduct.Product.CategoryName : null)))
            // Đơn vị cơ bản (Product.BaseUnit, vd "Cái")
            .ForMember(d => d.BaseUnit, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null
                    ? s.BranchProduct.Product.BaseUnit : null))
            // Đơn vị bán: khớp ProductConversionUnit có ConvertValue == ConversionRate.
            // Nếu không có (hoặc ConversionRate = 1) -> fallback BaseUnit.
            .ForMember(d => d.Unit, o => o.MapFrom(s => ResolveReturnItemUnit(s)));

        // WarrantyTicket
        CreateMap<WarrantyTicket, DTOs.SaleDTOs.WarrantyTicketDto>()
            .ForMember(d => d.SupplierName, o => o.MapFrom(s =>
                s.Supplier != null ? s.Supplier.SupplierName : null));

        // ExchangeItem (SP B - hàng mới xuất cho khách)
        CreateMap<ExchangeItem, DTOs.SaleDTOs.ExchangeItemDto>()
            .ForMember(d => d.ProductId, o => o.MapFrom(s => s.ProductId ?? (s.BranchProduct != null ? s.BranchProduct.ProductId : Guid.Empty)))
            .ForMember(d => d.ProductName, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.ProductName : null))
            .ForMember(d => d.ProductCode, o => o.MapFrom(s =>
                s.BranchProduct != null && s.BranchProduct.Product != null ? s.BranchProduct.Product.ProductCode : null));
    }

    private static string? CalculateTier(decimal? totalSpent)
    {
        if (!totalSpent.HasValue) return "Bronze";
        if (totalSpent >= 50000000) return "Platinum";
        if (totalSpent >= 20000000) return "Gold";
        if (totalSpent >= 5000000) return "Silver";
        return "Bronze";
    }

    /// <summary>Tên ĐVT bán của ReturnItem: khớp ProductConversionUnit có ConvertValue == ConversionRate;
    /// không khớp thì fallback về Product.BaseUnit.</summary>
    private static string? ResolveReturnItemUnit(ReturnItem s)
    {
        if (s.BranchProduct?.Product is null) return null;
        var product = s.BranchProduct.Product;
        var match = product.ProductConversionUnits?.FirstOrDefault(c => c.ConvertValue == s.ConversionRate);
        return !string.IsNullOrEmpty(match?.UnitName) ? match.UnitName : product.BaseUnit;
    }
}
