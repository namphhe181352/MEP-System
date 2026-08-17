using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.Models;

public class WarrantyClaim
{
    [Key]
    public Guid ClaimId { get; set; }
    public Guid TicketItemId { get; set; }
    public Guid ProductId { get; set; }
    public string? ClaimCode { get; set; }
    public DateTime? ClaimDate { get; set; }
    public decimal ClaimedQuantity { get; set; }
    public string? ClaimType { get; set; }
    public string? IssueDescription { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public Guid? ReplacementProductId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Status { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ClaimedByUserId { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public string? ProcessNotes { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CreatedAt { get; set; }

    // Navigation
    public virtual StockTicketItem? TicketItem { get; set; }
    public virtual Product? Product { get; set; }
    public virtual Supplier? Supplier { get; set; }
    public virtual User? ClaimedByUser { get; set; }
    public virtual User? ProcessedByUser { get; set; }
}
