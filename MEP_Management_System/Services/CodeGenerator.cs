namespace MEP_Management_System.Services;

public static class CodeGenerator
{
    /// <summary>Format: INV-YYYY-NNNN (e.g. INV-2026-0001)</summary>
    public static string GenerateInvoiceCode(int sequence)
    {
        return $"INV-{DateTime.UtcNow:yyyy}-{sequence:D4}";
    }

    /// <summary>Format: POS-YYYYMMDD-NNN (e.g. POS-20260621-001)</summary>
    public static string GenerateOrderCode(int sequence)
    {
        return $"POS-{DateTime.UtcNow:yyyyMMdd}-{sequence:D3}";
    }

    /// <summary>Format: SHIFT-YYYYMMDD-NNN (e.g. SHIFT-20260621-001)</summary>
    public static string GenerateShiftCode(int sequence)
    {
        return $"SHIFT-{DateTime.UtcNow:yyyyMMdd}-{sequence:D3}";
    }

    /// <summary>Format: PROMO-NNNN (e.g. PROMO-0001)</summary>
    public static string GeneratePromotionCode(int sequence)
    {
        return $"PROMO-{sequence:D4}";
    }

    /// <summary>Format: RCP-YYYYMMDD-NNNNNN (e.g. RCP-20260621-000001)</summary>
    public static string GenerateReceiptCode(int sequence)
    {
        return $"RCP-{DateTime.UtcNow:yyyyMMdd}-{sequence:D6}";
    }

    /// <summary>Format: RFD-YYYYMMDD-NNNN (e.g. RFD-20260621-0001)</summary>
    public static string GenerateRefundCode(int sequence)
    {
        return $"RFD-{DateTime.UtcNow:yyyyMMdd}-{sequence:D4}";
    }
}
