import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface UiState {
  sidebarCollapsed: boolean;
  mobileDrawerOpen: boolean;
  quotationStatusFilter: string[] | null;
  toggleSidebar: () => void;
  setSidebarCollapsed: (collapsed: boolean) => void;
  openMobileDrawer: () => void;
  closeMobileDrawer: () => void;
  setQuotationStatusFilter: (next: string[]) => void;
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      sidebarCollapsed: false,
      mobileDrawerOpen: false,
      quotationStatusFilter: null,
      toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
      setSidebarCollapsed: (collapsed) => set({ sidebarCollapsed: collapsed }),
      openMobileDrawer: () => set({ mobileDrawerOpen: true }),
      closeMobileDrawer: () => set({ mobileDrawerOpen: false }),
      setQuotationStatusFilter: (next) => set({ quotationStatusFilter: next }),
    }),
    {
      name: 'qldonhang-ui-store',
      version: 1,
      partialize: (state) => ({
        sidebarCollapsed: state.sidebarCollapsed,
        quotationStatusFilter: state.quotationStatusFilter,
      }),
    },
  ),
);
