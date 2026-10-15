# Label check — round 1 help

Base: qlbanhang/main 1a877638 (unchanged by Task 4.0)

## customers.list

| label | evidence |
|---|---|
| Thêm khách hàng | frontend/src/pages/customers/customer-list-page.tsx:136 |
| Mã KH | frontend/src/pages/customers/customer-list-page.tsx:49 |
| Tên khách hàng | frontend/src/pages/customers/customer-list-page.tsx:50 |
| Nhóm | frontend/src/pages/customers/customer-list-page.tsx:55 |
| Trạng thái | frontend/src/pages/customers/customer-list-page.tsx:60 |
| Xóa khách hàng? | frontend/src/pages/customers/customer-list-page.tsx:221 |
| Xóa | frontend/src/pages/customers/customer-list-page.tsx:230 (confirmLabel) |
| Trước | frontend/src/pages/customers/customer-list-page.tsx:207 |
| Sau | frontend/src/pages/customers/customer-list-page.tsx:210 |

## quotations.form

| label | evidence |
|---|---|
| Mã KH | frontend/src/pages/quotations/quotation-form-page.tsx:810 |
| Ngày báo giá | frontend/src/pages/quotations/quotation-form-page.tsx:758 |
| Ngày giao | frontend/src/pages/quotations/quotation-form-page.tsx:788 |
| Địa chỉ giao | frontend/src/pages/quotations/quotation-form-page.tsx:843 |
| Người nhận | frontend/src/pages/quotations/quotation-form-page.tsx:848 |
| Điện thoại | frontend/src/pages/quotations/quotation-form-page.tsx:850 |
| Chi tiết hàng hóa | frontend/src/pages/quotations/components/line-items-grid.tsx:231 |
| Mã hàng | frontend/src/pages/quotations/components/line-items-grid.tsx:259 |
| Tên hàng | frontend/src/pages/quotations/components/line-items-grid.tsx:260 |
| ĐVT | frontend/src/pages/quotations/components/line-items-grid.tsx:261 |
| D × R × Dày × Tấm | frontend/src/pages/quotations/components/line-items-grid.tsx:262 |
| SL | frontend/src/pages/quotations/components/line-items-grid.tsx:263 |
| Đơn giá bán | frontend/src/pages/quotations/components/line-items-grid.tsx:264 |
| Tổng cộng | frontend/src/pages/quotations/components/totals-panel.tsx:40 |
| CK | frontend/src/pages/quotations/components/totals-panel.tsx:52 |
| VC | frontend/src/pages/quotations/components/totals-panel.tsx:58 |
| Thuế % | frontend/src/pages/quotations/components/totals-panel.tsx:68 |
| Tạm ứng | frontend/src/pages/quotations/components/totals-panel.tsx:95 |
| Còn lại | frontend/src/pages/quotations/components/totals-panel.tsx:110 |
| Lưu tạm | frontend/src/pages/quotations/quotation-form-page.tsx:554 |
| Lưu và thoát | frontend/src/pages/quotations/quotation-form-page.tsx:544 |
| Lưu và in | frontend/src/pages/quotations/quotation-form-page.tsx:533 |
| Cập nhật | frontend/src/pages/quotations/quotation-form-page.tsx:520 |
| Gửi | frontend/src/pages/quotations/quotation-form-page.tsx:569 |
| Xác nhận | frontend/src/pages/quotations/quotation-form-page.tsx:581 |
| Hủy | frontend/src/pages/quotations/quotation-form-page.tsx:615 |
| KT xác nhận | frontend/src/pages/quotations/quotation-form-page.tsx:594 |
| Nhân bản | frontend/src/pages/quotations/quotation-form-page.tsx:641 |
| Lịch sử | frontend/src/pages/quotations/quotation-form-page.tsx:751 |
| In | frontend/src/pages/quotations/quotation-form-page.tsx:676 |
| Excel | frontend/src/pages/quotations/quotation-form-page.tsx:652 |

## quotations.list

| label | evidence |
|---|---|
| Thêm báo giá | frontend/src/pages/quotations/quotation-list-page.tsx:609 |
| Trạng thái | frontend/src/pages/quotations/quotation-list-page.tsx:505 (column) and :636 (filter placeholder) |
| Ngày báo giá | frontend/src/pages/quotations/quotation-list-page.tsx:656 |
| N. Doanh thu | frontend/src/pages/quotations/quotation-list-page.tsx:409 (column) and :673 (filter caption) |
| Ngày Giao | frontend/src/pages/quotations/quotation-list-page.tsx:416 (column) and :690 (filter caption) |
| Tuỳ chỉnh | frontend/src/pages/quotations/components/quotation-date-filter.tsx:80 |
| Gửi | frontend/src/pages/quotations/quotation-list-page.tsx:205 |
| Xác nhận | frontend/src/pages/quotations/quotation-list-page.tsx:212 |
| Hủy | frontend/src/pages/quotations/quotation-list-page.tsx:227 |
| Nhân bản | frontend/src/pages/quotations/quotation-list-page.tsx:217 |
| Tiền hàng | frontend/src/pages/quotations/components/list-footer.tsx:73 |
| CK | frontend/src/pages/quotations/components/list-footer.tsx:74 |
| VC | frontend/src/pages/quotations/components/list-footer.tsx:75 |
| Tổng | frontend/src/pages/quotations/components/list-footer.tsx:84 (the bold grand total of the footer) |
| Tạm ứng | frontend/src/pages/quotations/components/list-footer.tsx:86 |
| Trước | frontend/src/pages/quotations/components/list-footer.tsx:110 |
| Sau | frontend/src/pages/quotations/components/list-footer.tsx:118 |
| Chủ sở hữu | frontend/src/pages/quotations/quotation-list-page.tsx:513 (column) and :649 (filter, only with view_all) |
| KT xác nhận đã nhận tiền | frontend/src/pages/quotations/quotation-list-page.tsx:182 (tooltip title of the row icon, needs accounting_confirm) |

## stock-in.form

| label | evidence |
|---|---|
| Ngày nhập | frontend/src/pages/stock-vouchers/labels.ts:17 (STOCK_VOUCHER_LABELS.In.dateLabel, shown in components/stock-voucher-main-fields.tsx:100) |
| Lý do nhập | frontend/src/pages/stock-vouchers/labels.ts:18 (reasonLabel, shown in components/stock-voucher-main-fields.tsx:105) |
| Kho | frontend/src/pages/stock-vouchers/components/stock-voucher-main-fields.tsx:122 |
| Mã đối tượng | frontend/src/pages/stock-vouchers/components/stock-voucher-main-fields.tsx:159 |
| Hàng hóa | frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.tsx:30 |
| Mã hàng | frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx:68 |
| SL | frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx:76 |
| Đơn giá | frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx:78 |
| Số tấm | frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx:72 |
| Dài | frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx:73 |
| Rộng | frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx:74 |
| Dày | frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx:75 |
| Phí vận chuyển | frontend/src/pages/stock-vouchers/components/stock-voucher-payment-tab.tsx:73 |
| CK cả đơn | frontend/src/pages/stock-vouchers/components/stock-voucher-payment-tab.tsx:76 |
| Thanh toán | frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.tsx:34 |
| Thông tin khác | frontend/src/pages/stock-vouchers/components/stock-voucher-tabs.tsx:33 |
| Người giao hàng | frontend/src/pages/stock-vouchers/labels.ts:19 (handlerLabel, shown in stock-voucher-form-page.tsx:831) |
| Tổng thanh toán | frontend/src/pages/stock-vouchers/components/stock-totals-bar.tsx:31 |
| HTTT | frontend/src/pages/stock-vouchers/components/stock-voucher-main-fields.tsx:139 |
| Số tiền thanh toán | frontend/src/pages/stock-vouchers/components/stock-voucher-payment-tab.tsx:84 |
| Lưu tạm | frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx:739 |
| Lưu và thoát | frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx:735 |
| Cập nhật | frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx:746 |
| % VAT cho mọi dòng | frontend/src/pages/stock-vouchers/components/stock-voucher-payment-tab.tsx:109 (only for the In variant: type === 'In' && onApplyVatToAll) |
| Áp dụng | frontend/src/pages/stock-vouchers/components/stock-voucher-payment-tab.tsx:130 |
| Hạn thanh toán | frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx:892 (only when the reason raises receivable or payable) |
| Hủy phiếu | frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx:752 |
| Khôi phục | frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx:758 |
| Xóa | frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx:764 |

## inventory-opening-stock

| label | evidence |
|---|---|
| Kho | frontend/src/pages/inventory/opening-stock-page.tsx:185 |
| Ngày đầu kỳ | frontend/src/pages/inventory/opening-stock-page.tsx:200 |
| Thêm dòng | frontend/src/pages/inventory/opening-stock-page.tsx:313 |
| Mã hàng | frontend/src/pages/inventory/opening-stock-page.tsx:220 |
| Số lượng | frontend/src/pages/inventory/opening-stock-page.tsx:223 |
| Giá trị | frontend/src/pages/inventory/opening-stock-page.tsx:224 |
| Đơn giá | frontend/src/pages/inventory/opening-stock-page.tsx:225 |
| Lưu | frontend/src/pages/inventory/opening-stock-page.tsx:178 |

## payments.form

| label | evidence |
|---|---|
| Ngày giờ | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:97 |
| Quỹ | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:99 |
| Lý do | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:129 |
| Đối tượng | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:143 |
| Kèm theo | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:172 |
| Diễn giải | frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx:78 |
| Số tiền | frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx:79 |
| Thêm dòng | frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx:175 |
| Tổng | frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx:156 |
| Phân bổ chứng từ | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:562 |
| Phân bổ | frontend/src/pages/cash-vouchers/components/allocation-grid.tsx:89 |
| Gợi ý | frontend/src/pages/cash-vouchers/components/allocation-grid.tsx:184 |
| Lưu tạm | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:473 |
| Lưu và thoát | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:469 |
| Cập nhật | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:486 |
| In | frontend/src/components/voucher-print/voucher-print-button.tsx:36 (default label, outside sources) |
| Tự động | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:451 |
| Mở phiếu kho | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:519 |
| Đóng | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:480 |
| Quỹ bị âm | frontend/src/lib/confirm-required.ts:39 (shared 422 dialog title, outside sources) |
| Không đủ tiền trong quỹ | frontend/src/lib/confirm-required.ts:40 (shared 422 dialog title, outside sources) |
| Bỏ đối trừ chứng từ | frontend/src/lib/confirm-required.ts:36 (shared 422 dialog title, outside sources) |
| Người nhận | dynamic: the page picks personLabel by voucher type, text at frontend/src/pages/cash-vouchers/labels.ts:29 |
| Lý do chi | dynamic: the page picks descriptionLabel by voucher type, text at frontend/src/pages/cash-vouchers/labels.ts:30 |

## receipts.form

| label | evidence |
|---|---|
| Ngày giờ | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:97 |
| Quỹ | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:99 |
| Lý do | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:129 |
| Đối tượng | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:143 |
| Kèm theo | frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx:172 |
| Diễn giải | frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx:78 |
| Số tiền | frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx:79 |
| Thêm dòng | frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx:175 |
| Tổng | frontend/src/pages/cash-vouchers/components/cash-line-grid.tsx:156 |
| Phân bổ chứng từ | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:562 |
| Phân bổ | frontend/src/pages/cash-vouchers/components/allocation-grid.tsx:89 |
| Gợi ý | frontend/src/pages/cash-vouchers/components/allocation-grid.tsx:184 |
| Lưu tạm | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:473 |
| Lưu và thoát | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:469 |
| Cập nhật | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:486 |
| In | frontend/src/components/voucher-print/voucher-print-button.tsx:36 (default label, outside sources) |
| Tự động | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:451 |
| Mở phiếu kho | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:519 |
| Đóng | frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx:480 |
| Quỹ bị âm | frontend/src/lib/confirm-required.ts:39 (shared 422 dialog title, outside sources) |
| Không đủ tiền trong quỹ | frontend/src/lib/confirm-required.ts:40 (shared 422 dialog title, outside sources) |
| Bỏ đối trừ chứng từ | frontend/src/lib/confirm-required.ts:36 (shared 422 dialog title, outside sources) |
| Người nộp | dynamic: the page picks personLabel by voucher type, text at frontend/src/pages/cash-vouchers/labels.ts:19 |
| Lý do nộp | dynamic: the page picks descriptionLabel by voucher type, text at frontend/src/pages/cash-vouchers/labels.ts:20 |

## fund-transfers.form

| label | evidence |
|---|---|
| Số phiếu | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:349 |
| Ngày giờ | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:358 |
| Quỹ đi | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:364 |
| Quỹ đến | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:383 |
| Số tiền | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:406 |
| Diễn giải | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:413 |
| Ghi chú | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:419 |
| Lưu tạm | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:289 |
| Lưu và thoát | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:285 |
| Cập nhật | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:296 |
| Khôi phục | frontend/src/pages/fund-transfers/fund-transfer-form-page.tsx:308 |
| Quỹ bị âm | frontend/src/lib/confirm-required.ts:39 (shared 422 dialog title, outside sources) |
| Không đủ tiền trong quỹ | frontend/src/lib/confirm-required.ts:40 (shared 422 dialog title, outside sources) |
