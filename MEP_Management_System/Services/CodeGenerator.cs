namespace MEP_Management_System.Services;

public static class CodeGenerator
{
    /// <summary>Format: INV-YYYY-NNNN (e.g. INV-2026-0001)</summary>
    public static string GenerateInvoiceCode(int sequence, DateTime? referenceDate = null)
    {
        var d = referenceDate ?? DateTime.UtcNow;
        return $"INV-{d:yyyy}-{sequence:D4}";
    }

    /// <summary>Format: POS-YYYYMMDD-NNN (e.g. POS-20260621-001)</summary>
    public static string GenerateOrderCode(int sequence, DateTime? referenceDate = null)
    {
        var d = referenceDate ?? DateTime.UtcNow;
        return $"POS-{d:yyyyMMdd}-{sequence:D3}";
    }

    /// <summary>Format: SHIFT-YYYYMMDD-NNN (e.g. SHIFT-20260621-001)</summary>
    public static string GenerateShiftCode(int sequence, DateTime? referenceDate = null)
    {
        var d = referenceDate ?? DateTime.UtcNow;
        return $"SHIFT-{d:yyyyMMdd}-{sequence:D3}";
    }

    /// <summary>Format: PROMO-NNNN (e.g. PROMO-0001)</summary>
    public static string GeneratePromotionCode(int sequence)
    {
        return $"PROMO-{sequence:D4}";
    }

    /// <summary>Format: RCP-YYYYMMDD-NNNNNN (e.g. RCP-20260621-000001)</summary>
    public static string GenerateReceiptCode(int sequence, DateTime? referenceDate = null)
    {
        var d = referenceDate ?? DateTime.UtcNow;
        return $"RCP-{d:yyyyMMdd}-{sequence:D6}";
    }

    /// <summary>Format: RFD-YYYYMMDD-NNNN (e.g. RFD-20260621-0001)</summary>
    public static string GenerateRefundCode(int sequence, DateTime? referenceDate = null)
    {
        var d = referenceDate ?? DateTime.UtcNow;
        return $"RFD-{d:yyyyMMdd}-{sequence:D4}";
    }
}
