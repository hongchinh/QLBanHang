using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.CashDebt.CashVouchers.Services;

/// Permission codes per voucher type (receipts.* / payments.*).
public static class CashVoucherPermissions
{
    public static string View(CashVoucherType type) => type == CashVoucherType.Receipt ? Permissions.Receipts.View : Permissions.Payments.View;
    public static string Create(CashVoucherType type) => type == CashVoucherType.Receipt ? Permissions.Receipts.Create : Permissions.Payments.Create;
    public static string Edit(CashVoucherType type) => type == CashVoucherType.Receipt ? Permissions.Receipts.Edit : Permissions.Payments.Edit;
    public static string Delete(CashVoucherType type) => type == CashVoucherType.Receipt ? Permissions.Receipts.Delete : Permissions.Payments.Delete;
    public static string Cancel(CashVoucherType type) => type == CashVoucherType.Receipt ? Permissions.Receipts.Cancel : Permissions.Payments.Cancel;
    public static string EditAll(CashVoucherType type) => type == CashVoucherType.Receipt ? Permissions.Receipts.EditAll : Permissions.Payments.EditAll;
}
