import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { StockDirection, StockVoucherStatusFilter } from '@/features/stock-vouchers/types';

interface UiState {
  sidebarCollapsed: boolean;
  mobileDrawerOpen: boolean;
  quotationStatusFilter: string[] | null;
  // null = never chosen; 'all' is a real choice (shows cancelled vouchers too).
  stockVoucherStatusFilter: Record<StockDirection, StockVoucherStatusFilter | null>;
  toggleSidebar: () => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  openMobileDrawer: () => void;
  closeMobileDrawer: () => void;
  setQuotationStatusFilter: (next: string[]) => void;
  setStockVoucherStatusFilter: (type: StockDirection, value: StockVoucherStatusFilter) => void;
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      sidebarCollapsed: false,
      mobileDrawerOpen: false,
      quotationStatusFilter: null,
      stockVoucherStatusFilter: { In: null, Out: null },
      toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
      setSidebarCollapsed: (collapsed) => set({ sidebarCollapsed: collapsed }),
      openMobileDrawer: () => set({ mobileDrawerOpen: true }),
      closeMobileDrawer: () => set({ mobileDrawerOpen: false }),
      setQuotationStatusFilter: (next) => set({ quotationStatusFilter: next }),
      setStockVoucherStatusFilter: (type, value) =>
        set((s) => ({ stockVoucherStatusFilter: { ...s.stockVoucherStatusFilter, [type]: value } })),
    }),
    {
      name: 'qldonhang-ui-store',
      version: 1,
      partialize: (state) => ({
        sidebarCollapsed: state.sidebarCollapsed,
        quotationStatusFilter: state.quotationStatusFilter,
        stockVoucherStatusFilter: state.stockVoucherStatusFilter,
      }),
    },
  ),
);
