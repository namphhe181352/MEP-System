namespace MEP_Management_System.Authorization;

/// <summary>
/// Tập trung toàn bộ mã Permission (RBAC) của hệ thống.
/// Khi cần thêm quyền mới: thêm hằng số ở đây, KHÔNG cần sửa Handler hay Controller logic.
/// </summary>
public static class PermissionConstants
{
    // === EXPENSE / PHIẾU CHI TIỀN ===
    public const string EXPENSE_VIEW = "EXPENSE_VIEW";
    public const string EXPENSE_CREATE = "EXPENSE_CREATE";
    public const string EXPENSE_CONFIRM = "EXPENSE_CONFIRM";
    public const string EXPENSE_CANCEL = "EXPENSE_CANCEL";
    public const string EXPENSE_CATEGORY_MANAGE = "EXPENSE_CATEGORY_MANAGE";

    // === SUPPLIER (Nhà cung cấp) ===
    public const string SUPPLIER_VIEW = "SUPPLIER_VIEW";
    public const string SUPPLIER_CREATE = "SUPPLIER_CREATE";
    public const string SUPPLIER_UPDATE = "SUPPLIER_UPDATE";
    public const string SUPPLIER_DELETE = "SUPPLIER_DELETE";

    // === SUPPLIER PAYMENT (Thanh toán / Công nợ NCC) ===
    public const string SUPPLIER_PAYMENT_VIEW = "SUPPLIER_PAYMENT_VIEW";
    public const string SUPPLIER_PAYMENT_CREATE = "SUPPLIER_PAYMENT_CREATE";
    public const string SUPPLIER_PAYMENT_UPDATE = "SUPPLIER_PAYMENT_UPDATE";
    public const string SUPPLIER_PAYMENT_DELETE = "SUPPLIER_PAYMENT_DELETE";

    // === PRODUCT (Sản phẩm) ===
    public const string PRODUCT_VIEW = "PRODUCT_VIEW";
    public const string PRODUCT_CREATE = "PRODUCT_CREATE";
    public const string PRODUCT_UPDATE = "PRODUCT_UPDATE";
    public const string PRODUCT_DELETE = "PRODUCT_DELETE";

    // === LOCATION (Vị trí kho) ===
    public const string LOCATION_VIEW = "LOCATION_VIEW";
    public const string LOCATION_CREATE = "LOCATION_CREATE";
    public const string LOCATION_UPDATE = "LOCATION_UPDATE";
    public const string LOCATION_DELETE = "LOCATION_DELETE";

    // === STOCK / INVENTORY (Xuất nhập kho) ===
    public const string STOCK_VIEW = "STOCK_VIEW";
    public const string STOCK_INWARD_CREATE = "STOCK_INWARD_CREATE";
    public const string STOCK_INWARD_UPDATE = "STOCK_INWARD_UPDATE";
    public const string STOCK_INWARD_DELETE = "STOCK_INWARD_DELETE";
    public const string STOCK_OUTWARD_CREATE = "STOCK_OUTWARD_CREATE";
    public const string STOCK_OUTWARD_UPDATE = "STOCK_OUTWARD_UPDATE";
    public const string STOCK_OUTWARD_DELETE = "STOCK_OUTWARD_DELETE";
    public const string STOCK_OUTWARD_CONFIRM = "STOCK_OUTWARD_CONFIRM";
    public const string STOCK_CHECK_VIEW = "STOCK_CHECK_VIEW";
    public const string STOCK_CHECK_CREATE = "STOCK_CHECK_CREATE";
    public const string STOCK_CHECK_APPROVE = "STOCK_CHECK_APPROVE";
    public const string STOCK_CHECK_CANCEL = "STOCK_CHECK_CANCEL";

    // === SALE / POS ===
    public const string CUSTOMER_VIEW = "CUSTOMER_VIEW";
    public const string CUSTOMER_CREATE = "CUSTOMER_CREATE";
    public const string CUSTOMER_UPDATE = "CUSTOMER_UPDATE";
    public const string CUSTOMER_DELETE = "CUSTOMER_DELETE";
    public const string SALE_VIEW = "SALE_VIEW";
    public const string SALE_CREATE = "SALE_CREATE";
    public const string SALE_UPDATE = "SALE_UPDATE";
    public const string SALE_DELETE = "SALE_DELETE";
    public const string PAYMENT_VIEW = "PAYMENT_VIEW";
    public const string PAYMENT_CREATE = "PAYMENT_CREATE";
    public const string PRINT_VIEW = "PRINT_VIEW";
    public const string PROMOTION_VIEW = "PROMOTION_VIEW";
    public const string SHIFT_VIEW = "SHIFT_VIEW";
    public const string SHIFT_CREATE = "SHIFT_CREATE";
    public const string SHIFT_UPDATE = "SHIFT_UPDATE";
    public const string SHIFT_DELETE = "SHIFT_DELETE";
    public const string SHIFT_FORCE_CLOSE = "SHIFT_FORCE_CLOSE";

    // === SYSTEM / OWNER ===
    public const string OWNER_MANAGE = "OWNER_MANAGE";
    public const string SYSTEM_MANAGE = "SYSTEM_MANAGE";
    public const string REPORT_VIEW = "REPORT_VIEW";

    // === STAFF / EMPLOYEE (Nhân viên) ===
    public const string STAFF_VIEW = "STAFF_VIEW";
    public const string STAFF_CREATE = "STAFF_CREATE";
    public const string STAFF_UPDATE = "STAFF_UPDATE";
    public const string STAFF_DELETE = "STAFF_DELETE";
    public const string STAFF_ASSIGN_BRANCH = "STAFF_ASSIGN_BRANCH";

    // === ROLE & CLAIM TYPE ===
    /// <summary>Role Owner - được bypass mọi Permission check.</summary>
    public const string OWNER_ROLE = "Owner";

    /// <summary>Loại claim dùng để lưu PermissionCode trong JWT.</summary>
    public const string PERMISSION_CLAIM_TYPE = "permission";
}