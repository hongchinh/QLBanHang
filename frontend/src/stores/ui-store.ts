import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { StockDirection, StockVoucherStatusFilter } from '@/features/stock-vouchers/types';

interface UiState {
  sidebarCollapsed: boolean;
  mobileDrawerOpen: boolean;
  quotationStatusFilter: string[] | null;
  // null = never chosen; 'all' is a real choice (shows cancelled vouchers too).
  stockVoucherStatusFilter: Record<StockDirection, StockVoucherStatusFilter | null>;
  // Only groups the user opened/closed by clicking; others use NavGroup.defaultOpen.
  // Shared by every user of this browser (a display preference).
  sidebarGroupOpen: Record<string, boolean>;
  toggleSidebar: () => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  setSidebarGroupOpen: (groupId: string, open: boolean) => void;
  openMobileDrawer: () => void;
  closeMobileDrawer: () => void;
  setQuotationStatusFilter: (next: string[]) => void;
  setStockVoucherStatusFilter: (type: StockDirection, value: StockVoucherStatusFilter) => void;
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      sidebarCollapsed: false,
      sidebarGroupOpen: {},
      mobileDrawerOpen: false,
      quotationStatusFilter: null,
      stockVoucherStatusFilter: { In: null, Out: null },
      toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
      setSidebarCollapsed: (collapsed) => set({ sidebarCollapsed: collapsed }),
      setSidebarGroupOpen: (groupId, open) =>
        set((s) => ({ sidebarGroupOpen: { ...s.sidebarGroupOpen, [groupId]: open } })),
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
        sidebarGroupOpen: state.sidebarGroupOpen,
        quotationStatusFilter: state.quotationStatusFilter,
        stockVoucherStatusFilter: state.stockVoucherStatusFilter,
      }),
    },
  ),
);
