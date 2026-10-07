import { fromDateTimeLocalValue, toDateTimeLocalValue } from '@/lib/vn-datetime';
import type { StockLineFormValues, StockVoucherFormParsed, StockVoucherFormValues } from './schema';
import type {
  StockDirection,
  StockVoucher,
  StockVoucherDefaults,
  UpsertStockVoucherRequest,
} from './types';

export function toUpsertPayload(
  type: StockDirection,
  values: StockVoucherFormParsed,
  acknowledgeNegativeStock = false,
): UpsertStockVoucherRequest {
  const isEdit = values.version !== undefined;
  return {
    type,
    voucherAt: fromDateTimeLocalValue(values.voucherAt),
    warehouseId: values.warehouseId,
    reasonId: values.reasonId,
    partnerId: values.partnerId,
    partnerName: values.partnerName,
    partnerAddress: values.partnerAddress,
    partnerTaxCode: values.partnerTaxCode,
    handlerName: values.handlerName,
    paymentMethodId: values.paymentMethodId,
    note: values.note,
    freight: values.freight,
    orderDiscount: values.orderDiscount,
    // An untouched new voucher lets the server use Total; an edit keeps what is stored (review M13).
    paidAmount: values.paidAmountTouched || isEdit ? values.paidAmount : undefined,
    version: values.version,
    acknowledgeNegativeStock,
    lines: values.lines.map((line, idx) => ({
      id: line.id,
      sortOrder: idx,
      productId: line.productId,
      warehouseId: line.warehouseId,
      sheetCount: line.sheetCount,
      length: line.length,
      width: line.width,
      thickness: line.thickness,
      // The backend computes the quantity of dimension-based lines.
      quantity: line.pricingMode === 'PerUnit' ? line.quantity : 0,
      unitPrice: line.unitPrice,
      discountRate: line.discountRate,
      discountAmount: line.discountManual ? line.discountAmount : undefined,
      discountManual: line.discountManual,
      vatRate: line.vatRate,
      note: line.note,
    })),
  };
}

export function createEmptyStockLine(sortOrder: number, warehouseId = ''): StockLineFormValues {
  return {
    _uiKey: crypto.randomUUID(),
    sortOrder,
    productId: '',
    productCode: '',
    productName: '',
    pricingMode: 'PerUnit',
    unitName: '',
    trackInventory: true,
    priceIncludesVat: false,
    warehouseId,
    sheetCount: '',
    length: '',
    width: '',
    thickness: '',
    quantity: 0,
    unitPrice: 0,
    discountRate: 0,
    discountAmount: '',
    discountManual: false,
    vatRate: 0,
    note: '',
  };
}

export function toFormDefaults(
  voucher?: StockVoucher,
  defaults?: StockVoucherDefaults,
): StockVoucherFormValues {
  if (!voucher) {
    const warehouseId = defaults?.warehouseId ?? '';
    return {
      voucherAt: toDateTimeLocalValue(defaults?.voucherAt ?? new Date().toISOString()),
      warehouseId,
      reasonId: defaults?.reasonId ?? '',
      partnerId: '',
      partnerName: '',
      partnerAddress: '',
      partnerTaxCode: '',
      handlerName: '',
      paymentMethodId: defaults?.paymentMethodId ?? '',
      note: '',
      freight: 0,
      orderDiscount: 0,
      paidAmount: undefined,
      paidAmountTouched: false,
      version: undefined,
      lines: [createEmptyStockLine(0, warehouseId)],
    };
  }

  return {
    voucherAt: toDateTimeLocalValue(voucher.voucherAt),
    warehouseId: voucher.warehouseId,
    reasonId: voucher.reasonId,
    partnerId: voucher.partnerId ?? '',
    partnerName: voucher.partnerName ?? '',
    partnerAddress: voucher.partnerAddress ?? '',
    partnerTaxCode: voucher.partnerTaxCode ?? '',
    handlerName: voucher.handlerName ?? '',
    paymentMethodId: voucher.paymentMethodId ?? '',
    note: voucher.note ?? '',
    freight: voucher.freight,
    orderDiscount: voucher.orderDiscount,
    paidAmount: voucher.paidAmount,
    // A partial payment stays as entered; a full payment keeps following the total (review M13).
    paidAmountTouched: voucher.paidAmount !== voucher.total,
    version: voucher.version,
    lines: voucher.lines.map((line) => ({
      _uiKey: line.id,
      id: line.id,
      sortOrder: line.sortOrder,
      productId: line.productId,
      productCode: line.productCode,
      productName: line.productName,
      pricingMode: line.pricingMode,
      unitName: line.unitName,
      trackInventory: line.trackInventory,
      priceIncludesVat: line.priceIncludesVat,
      warehouseId: line.warehouseId,
      sheetCount: line.sheetCount ?? '',
      length: line.length ?? '',
      width: line.width ?? '',
      thickness: line.thickness ?? '',
      quantity: line.quantity,
      unitPrice: line.unitPrice,
      discountRate: line.discountRate,
      discountAmount: line.discountAmount,
      discountManual: line.discountManual,
      vatRate: line.vatRate,
      note: line.note ?? '',
    })),
  };
}
