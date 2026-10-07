import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useForm } from 'react-hook-form';
import {
  deleteStockVoucherDraft,
  readStockVoucherDraft,
  useStockVoucherDraft,
  writeStockVoucherDraft,
} from './use-stock-voucher-draft';
import { toFormDefaults } from './payload';
import type { StockVoucherFormValues } from './schema';
import type { StockDirection } from './types';

const VALUES: StockVoucherFormValues = toFormDefaults(undefined, {
  nextCode: 'PN00001',
  voucherAt: '2026-10-07T01:00:00Z',
  warehouseId: '11111111-1111-1111-1111-111111111111',
  reasonId: '22222222-2222-2222-2222-222222222222',
});

describe('useStockVoucherDraft', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  function renderDraftHook(opts: { type?: StockDirection; isEdit?: boolean; branchId?: string } = {}) {
    return renderHook(() => {
      const form = useForm<StockVoucherFormValues>({ defaultValues: VALUES });
      const draft = useStockVoucherDraft({
        form,
        type: opts.type ?? 'In',
        userId: 'user1',
        branchId: opts.branchId ?? 'branch1',
        isEdit: opts.isEdit ?? false,
        getSelectedPartner: () => ({ id: 'p1', code: 'NCC01', name: 'Nhà cung cấp 1' }),
        initialHasDraft: false,
        initialSavedAt: null,
      });
      return { form, draft };
    });
  }

  it('writes a per-type, per-branch draft after debounce for new vouchers and clearDraft removes it', async () => {
    const { result } = renderDraftHook({ type: 'Out' });

    act(() => {
      result.current.form.setValue('note', 'Ghi chú', { shouldDirty: true });
    });
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(localStorage.getItem('stock_voucher_draft_Out_user1_branch1')).toBeNull();

    act(() => {
      vi.advanceTimersByTime(600);
    });
    await waitFor(() => {
      expect(localStorage.getItem('stock_voucher_draft_Out_user1_branch1')).not.toBeNull();
    });
    expect(localStorage.getItem('stock_voucher_draft_In_user1_branch1')).toBeNull();

    const stored = readStockVoucherDraft('Out', 'user1', 'branch1');
    expect(stored?.values.note).toBe('Ghi chú');
    expect(stored?.selectedPartner).toEqual({ id: 'p1', code: 'NCC01', name: 'Nhà cung cấp 1' });
    expect(result.current.draft.hasDraft).toBe(true);

    act(() => {
      result.current.draft.clearDraft();
    });
    expect(localStorage.getItem('stock_voucher_draft_Out_user1_branch1')).toBeNull();
    expect(result.current.draft.hasDraft).toBe(false);
  });

  it('a draft of another branch is not read', () => {
    writeStockVoucherDraft('In', 'user1', 'branch1', VALUES, null);

    expect(readStockVoucherDraft('In', 'user1', 'branch2')).toBeNull();
    expect(readStockVoucherDraft('Out', 'user1', 'branch1')).toBeNull();
    expect(readStockVoucherDraft('In', 'user1', 'branch1')?.values.reasonId).toBe(VALUES.reasonId);

    deleteStockVoucherDraft('In', 'user1', 'branch1');
    expect(readStockVoucherDraft('In', 'user1', 'branch1')).toBeNull();
  });

  it('does not write in edit mode', () => {
    const { result } = renderDraftHook({ isEdit: true });

    act(() => {
      result.current.form.setValue('note', 'Changed', { shouldDirty: true });
    });
    act(() => {
      vi.advanceTimersByTime(2000);
    });

    expect(localStorage.getItem('stock_voucher_draft_In_user1_branch1')).toBeNull();
  });

  it('survives a storage that throws', () => {
    const spy = vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    const getSpy = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    expect(() => deleteStockVoucherDraft('In', 'user1', 'branch1')).not.toThrow();
    expect(readStockVoucherDraft('In', 'user1', 'branch1')).toBeNull();
    spy.mockRestore();
    getSpy.mockRestore();
  });
});
