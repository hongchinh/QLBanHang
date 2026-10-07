import { useCallback, useEffect, useRef, useState } from 'react';
import type { UseFormReturn } from 'react-hook-form';
import type { StockVoucherFormValues } from './schema';
import type { StockDirection } from './types';

export interface StockVoucherDraftPartner {
  id: string;
  code: string;
  name: string;
}

export interface StockVoucherDraftStorage {
  savedAt: string;
  values: StockVoucherFormValues;
  selectedPartner: StockVoucherDraftPartner | null;
}

const DEBOUNCE_MS = 1500;

// A draft holds warehouses of one branch, so it is keyed per branch as well as per type and user.
function draftKey(type: StockDirection, userId: string, branchId: string): string {
  return `stock_voucher_draft_${type}_${userId}_${branchId}`;
}

export function readStockVoucherDraft(
  type: StockDirection,
  userId: string,
  branchId: string,
): StockVoucherDraftStorage | null {
  if (!userId || !branchId) return null;
  try {
    const raw = localStorage.getItem(draftKey(type, userId, branchId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<StockVoucherDraftStorage>;
    if (!parsed.values || !parsed.savedAt) return null;
    return { savedAt: parsed.savedAt, values: parsed.values, selectedPartner: parsed.selectedPartner ?? null };
  } catch {
    return null;
  }
}

export function writeStockVoucherDraft(
  type: StockDirection,
  userId: string,
  branchId: string,
  values: StockVoucherFormValues,
  selectedPartner: StockVoucherDraftPartner | null,
): void {
  if (!userId || !branchId) return;
  try {
    const storage: StockVoucherDraftStorage = { savedAt: new Date().toISOString(), values, selectedPartner };
    localStorage.setItem(draftKey(type, userId, branchId), JSON.stringify(storage));
  } catch {
    // ignore QuotaExceededError / blocked storage
  }
}

export function deleteStockVoucherDraft(type: StockDirection, userId: string, branchId: string): void {
  try {
    localStorage.removeItem(draftKey(type, userId, branchId));
  } catch {
    // ignore blocked storage
  }
}

export interface UseStockVoucherDraftOptions {
  form: Pick<UseFormReturn<StockVoucherFormValues>, 'watch' | 'formState'>;
  type: StockDirection;
  userId: string;
  branchId: string;
  isEdit: boolean;
  getSelectedPartner: () => StockVoucherDraftPartner | null;
  initialHasDraft: boolean;
  initialSavedAt: Date | null;
}

export interface UseStockVoucherDraftResult {
  hasDraft: boolean;
  draftSavedAt: Date | null;
  clearDraft: () => void;
}

// Mirrors useQuotationDraft: debounced write of a new voucher's values while the form is dirty.
export function useStockVoucherDraft({
  form,
  type,
  userId,
  branchId,
  isEdit,
  getSelectedPartner,
  initialHasDraft,
  initialSavedAt,
}: UseStockVoucherDraftOptions): UseStockVoucherDraftResult {
  const [hasDraft, setHasDraft] = useState(initialHasDraft);
  const [draftSavedAt, setDraftSavedAt] = useState<Date | null>(initialSavedAt);

  const keyRef = useRef({ type, userId, branchId });
  keyRef.current = { type, userId, branchId };
  const getSelectedPartnerRef = useRef(getSelectedPartner);
  getSelectedPartnerRef.current = getSelectedPartner;
  const debounceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Reading isDirty during render subscribes RHF's formState proxy to it.
  const isDirtyRef = useRef(false);
  isDirtyRef.current = form.formState.isDirty;

  const { watch } = form;
  useEffect(() => {
    if (isEdit) return;

    const subscription = watch((values) => {
      if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = setTimeout(() => {
        // isDirty is false after form.reset() (e.g. "Xóa nháp"): do not write the defaults back.
        if (!isDirtyRef.current) return;
        const key = keyRef.current;
        writeStockVoucherDraft(
          key.type,
          key.userId,
          key.branchId,
          values as StockVoucherFormValues,
          getSelectedPartnerRef.current(),
        );
        setHasDraft(true);
        setDraftSavedAt(new Date());
      }, DEBOUNCE_MS);
    });

    return () => {
      subscription.unsubscribe();
      if (debounceTimerRef.current) clearTimeout(debounceTimerRef.current);
    };
  }, [watch, isEdit]);

  const clearDraft = useCallback(() => {
    if (debounceTimerRef.current) {
      clearTimeout(debounceTimerRef.current);
      debounceTimerRef.current = null;
    }
    const key = keyRef.current;
    deleteStockVoucherDraft(key.type, key.userId, key.branchId);
    setHasDraft(false);
    setDraftSavedAt(null);
  }, []);

  return { hasDraft, draftSavedAt, clearDraft };
}
