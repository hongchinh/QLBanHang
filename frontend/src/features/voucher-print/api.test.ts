import { beforeEach, describe, expect, it, vi } from 'vitest';

const requestBlob = vi.fn();
vi.mock('@/lib/blob-download', () => ({ requestBlob: (...a: unknown[]) => requestBlob(...a) }));

import { fetchVoucherPrint } from './api';

const a = { id: 'a', code: 'X1' };
const b = { id: 'b', code: 'X2' };

describe('fetchVoucherPrint', () => {
  beforeEach(() => {
    requestBlob.mockReset();
    requestBlob.mockResolvedValue(new Blob());
  });

  it('gets one cash voucher', async () => {
    await fetchVoucherPrint({ kind: 'cash', type: 'Receipt', vouchers: [a], format: 'pdf' });
    expect(requestBlob).toHaveBeenCalledWith({ method: 'get', url: '/cash-vouchers/a/print', params: { format: 'pdf' } });
  });

  it('gets one stock voucher with the price mode', async () => {
    await fetchVoucherPrint({ kind: 'stock', type: 'In', vouchers: [a], format: 'xlsx', priceMode: 'Cost' });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'get',
      url: '/stock-vouchers/a/print',
      params: { format: 'xlsx', priceMode: 'Cost' },
    });
  });

  it('posts several cash vouchers', async () => {
    await fetchVoucherPrint({ kind: 'cash', type: 'Payment', vouchers: [a, b], format: 'xlsx' });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'post',
      url: '/cash-vouchers/print',
      params: { format: 'xlsx' },
      body: { ids: ['a', 'b'] },
    });
  });

  it('posts several stock vouchers with the price mode', async () => {
    await fetchVoucherPrint({ kind: 'stock', type: 'Out', vouchers: [a, b], format: 'pdf', priceMode: 'None' });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'post',
      url: '/stock-vouchers/print',
      params: { format: 'pdf' },
      body: { ids: ['a', 'b'], priceMode: 'None' },
    });
  });

  it('gets one cash voucher with the QR flag', async () => {
    await fetchVoucherPrint({ kind: 'cash', type: 'Receipt', vouchers: [a], format: 'pdf', includeQr: true });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'get',
      url: '/cash-vouchers/a/print',
      params: { format: 'pdf', includeQr: true },
    });
  });

  it('posts several cash vouchers with the QR flag', async () => {
    await fetchVoucherPrint({ kind: 'cash', type: 'Receipt', vouchers: [a, b], format: 'pdf', includeQr: true });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'post',
      url: '/cash-vouchers/print',
      params: { format: 'pdf' },
      body: { ids: ['a', 'b'], includeQr: true },
    });
  });

  it('keeps the price mode and the QR flag together for stock', async () => {
    await fetchVoucherPrint({
      kind: 'stock',
      type: 'Out',
      vouchers: [a],
      format: 'pdf',
      priceMode: 'Voucher',
      includeQr: true,
    });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'get',
      url: '/stock-vouchers/a/print',
      params: { format: 'pdf', priceMode: 'Voucher', includeQr: true },
    });
    requestBlob.mockClear();
    await fetchVoucherPrint({
      kind: 'stock',
      type: 'Out',
      vouchers: [a, b],
      format: 'pdf',
      priceMode: 'Voucher',
      includeQr: true,
    });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'post',
      url: '/stock-vouchers/print',
      params: { format: 'pdf' },
      body: { ids: ['a', 'b'], priceMode: 'Voucher', includeQr: true },
    });
  });

  it('adds nothing when the QR flag is false', async () => {
    await fetchVoucherPrint({ kind: 'cash', type: 'Receipt', vouchers: [a], format: 'pdf', includeQr: false });
    expect(requestBlob).toHaveBeenCalledWith({ method: 'get', url: '/cash-vouchers/a/print', params: { format: 'pdf' } });
    requestBlob.mockClear();
    await fetchVoucherPrint({ kind: 'cash', type: 'Receipt', vouchers: [a, b], format: 'pdf', includeQr: false });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'post',
      url: '/cash-vouchers/print',
      params: { format: 'pdf' },
      body: { ids: ['a', 'b'] },
    });
    requestBlob.mockClear();
    await fetchVoucherPrint({
      kind: 'stock',
      type: 'Out',
      vouchers: [a, b],
      format: 'pdf',
      priceMode: 'Voucher',
      includeQr: false,
    });
    expect(requestBlob).toHaveBeenCalledWith({
      method: 'post',
      url: '/stock-vouchers/print',
      params: { format: 'pdf' },
      body: { ids: ['a', 'b'], priceMode: 'Voucher' },
    });
  });
});
