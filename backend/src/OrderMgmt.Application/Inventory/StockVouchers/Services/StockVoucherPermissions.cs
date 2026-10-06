using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;

namespace OrderMgmt.Application.Inventory.StockVouchers.Services;

/// Permission codes per voucher type (stock_in.* / stock_out.*).
public static class StockVoucherPermissions
{
    public static string View(StockDirection type) => type == StockDirection.In ? Permissions.StockIn.View : Permissions.StockOut.View;
    public static string Create(StockDirection type) => type == StockDirection.In ? Permissions.StockIn.Create : Permissions.StockOut.Create;
    public static string Edit(StockDirection type) => type == StockDirection.In ? Permissions.StockIn.Edit : Permissions.StockOut.Edit;
    public static string Delete(StockDirection type) => type == StockDirection.In ? Permissions.StockIn.Delete : Permissions.StockOut.Delete;
    public static string Cancel(StockDirection type) => type == StockDirection.In ? Permissions.StockIn.Cancel : Permissions.StockOut.Cancel;
    public static string EditAll(StockDirection type) => type == StockDirection.In ? Permissions.StockIn.EditAll : Permissions.StockOut.EditAll;
}
