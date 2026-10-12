import { useState, type ReactNode } from 'react';
import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { queryClient } from '@/lib/query-client';
import { ProtectedRoute } from '@/routes/protected-route';
import { AuthInit } from '@/routes/auth-init';
import { ErrorBoundary } from '@/components/error-boundary';
import { Toaster } from '@/components/ui/toaster';
import { AppLayout } from '@/components/layout/app-layout';
import { LoginPage } from '@/pages/login-page';
import { DashboardPage } from '@/pages/dashboard-page';
import { CustomerListPage } from '@/pages/customers/customer-list-page';
import { CustomerFormPage } from '@/pages/customers/customer-form-page';
import { SupplierListPage } from '@/pages/suppliers/supplier-list-page';
import { SupplierFormPage } from '@/pages/suppliers/supplier-form-page';
import { ProductListPage } from '@/pages/products/product-list-page';
import { ProductFormPage } from '@/pages/products/product-form-page';
import { ProductGroupListPage } from '@/pages/product-groups/product-group-list-page';
import { WarehouseListPage } from '@/pages/warehouses/warehouse-list-page';
import { StockReasonListPage } from '@/pages/stock-reasons/stock-reason-list-page';
import { PaymentMethodListPage } from '@/pages/payment-methods/payment-method-list-page';
// Round 2A: 10.4 cash reasons
import { CashReasonListPage } from '@/pages/cash-reasons/cash-reason-list-page';
// Round 2A: 10.5 funds
import { FundListPage } from '@/pages/funds/fund-list-page';
// Round 2A: 12.4 opening debts
import { OpeningDebtPage } from '@/pages/debt/opening-debt-page';
// Round 2A: 10.7 credit limits
import { CreditLimitListPage } from '@/pages/credit-limits/credit-limit-list-page';
import { QuotationListPage } from '@/pages/quotations/quotation-list-page';
import { QuotationFormPage } from '@/pages/quotations/quotation-form-page';
import { StockVoucherFormPage } from '@/pages/stock-vouchers/stock-voucher-form-page';
import { StockVoucherListPage } from '@/pages/stock-vouchers/stock-voucher-list-page';
// Round 2A: 11.3 receipts and payments
import { CashVoucherListPage } from '@/pages/cash-vouchers/cash-voucher-list-page';
// Round 2A: 11.4 receipt and payment form
import { CashVoucherFormPage } from '@/pages/cash-vouchers/cash-voucher-form-page';
import { MyQuotationSettingsPage } from '@/pages/settings/my-quotation-settings-page';
import { PaymentQrPage } from '@/pages/payment-qr/payment-qr-page';
import { QuotationSystemSettingsPage } from '@/pages/settings/quotation-system-settings-page';
import { SettingsHubPage } from '@/pages/settings/settings-hub-page';
import { BranchesPage } from '@/pages/settings/branches-page';
import { PeriodLockPage } from '@/pages/settings/period-lock-page';
import { InventorySettingsPage } from '@/pages/settings/inventory-settings-page';
import { NumberingSettingsPage } from '@/pages/settings/numbering-settings-page';
import { RecalcCostPage } from '@/pages/settings/recalc-cost-page';
// Round 2A: 10.8 cash settings
import { CashSettingsPage } from '@/pages/settings/cash-settings-page';
import { OpeningStockPage } from '@/pages/inventory/opening-stock-page';
import { StockOnHandPage } from '@/pages/inventory/stock-on-hand-page';
import { StockCardPage } from '@/pages/inventory/stock-card-page';
// Round 2A: 11.5 fund transfers
import { FundTransferListPage } from '@/pages/fund-transfers/fund-transfer-list-page';
import { FundTransferFormPage } from '@/pages/fund-transfers/fund-transfer-form-page';
// Round 2A: 12.2 debt matching
import { DebtMatchingPage } from '@/pages/debt/debt-matching-page';
// Round 2A: 12.3 debt offsets
import { DebtOffsetListPage } from '@/pages/debt/debt-offset-list-page';
import { DebtOffsetFormPage } from '@/pages/debt/debt-offset-form-page';
import { UserSettingsPage } from '@/pages/admin/user-settings-page';
import { UsersListPage } from '@/pages/admin/users-list-page';
import { BulkTransferPage } from '@/pages/admin/bulk-transfer-page';
import { AdminDashboardPage } from '@/pages/admin/admin-dashboard-page';
import { RolesMatrixPage } from '@/pages/admin/roles-matrix-page';
import { RevenuePage } from '@/pages/reports/revenue-page';
import { SalesPerformancePage } from '@/pages/reports/sales-performance-page';
import { SalesRevenuePage } from '@/pages/reports/sales-revenue-page';
import { SalesRevenueDetailPage } from '@/pages/reports/sales-revenue-detail-page';
import { VehicleRevenuePage } from '@/pages/reports/vehicle-revenue-page';
// Round 2A: 12.5 debt balance, debt ledger and cash book reports
import { DebtBalancePage } from '@/pages/cash-reports/debt-balance-page';
import { DebtLedgerPage } from '@/pages/cash-reports/debt-ledger-page';
import { DebtDocumentsPage } from '@/pages/cash-reports/debt-documents-page';
import { CashBookPage } from '@/pages/cash-reports/cash-book-page';
import { ForbiddenPage, NotFoundPage } from '@/pages/error-pages';
import { useRegisterSW } from 'virtual:pwa-register/react';
import { useBeforeInstallPrompt } from '@/hooks/useBeforeInstallPrompt';
import { InstallPrompt } from '@/components/InstallPrompt';
import { usePushNotification } from '@/hooks/usePushNotification';
import { PushPermissionPrompt } from '@/components/PushPermissionPrompt';

function RouteErrorBoundary({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();
  return <ErrorBoundary key={pathname}>{children}</ErrorBoundary>;
}

export function App() {
  const { canShow, install, dismiss } = useBeforeInstallPrompt();
  const {
    needRefresh: [needRefresh, setNeedRefresh],
    updateServiceWorker,
  } = useRegisterSW();

  const PUSH_DISMISS_KEY = 'push_prompt_dismissed_until';
  const vapidKey = import.meta.env.VITE_VAPID_PUBLIC_KEY;
  const { state: pushState, subscribe: subscribePush } = usePushNotification(vapidKey);

  const [pushDismissed, setPushDismissed] = useState(() => {
    const until = localStorage.getItem(PUSH_DISMISS_KEY);
    return until ? Date.now() < Number(until) : false;
  });

  const handlePushDismiss = () => {
    const until = Date.now() + 30 * 24 * 60 * 60 * 1000;
    localStorage.setItem(PUSH_DISMISS_KEY, String(until));
    setPushDismissed(true);
  };

  return (
    <ErrorBoundary>
      <InstallPrompt canShow={canShow} onInstall={install} onDismiss={dismiss} />
      {!pushDismissed && (
        <PushPermissionPrompt
          state={pushState}
          onEnable={subscribePush}
          onDismiss={handlePushDismiss}
        />
      )}
      <QueryClientProvider client={queryClient}>
        <BrowserRouter>
          <AuthInit>
            <RouteErrorBoundary>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/403" element={<ForbiddenPage />} />

              <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
                <Route index element={<DashboardPage />} />

                <Route path="customers">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="customers.view">
                        <CustomerListPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="customers.create">
                        <CustomerFormPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="customers.update">
                        <CustomerFormPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>

                <Route path="suppliers">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="suppliers.view">
                        <SupplierListPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="suppliers.create">
                        <SupplierFormPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="suppliers.update">
                        <SupplierFormPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>

                <Route path="products">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="products.view">
                        <ProductListPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="products.create">
                        <ProductFormPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="products.update">
                        <ProductFormPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                <Route path="product-groups">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="products.view">
                        <ProductGroupListPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                <Route
                  path="warehouses"
                  element={
                    <ProtectedRoute permission="inventory.catalogs.manage">
                      <WarehouseListPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="stock-reasons"
                  element={
                    <ProtectedRoute permission="inventory.catalogs.manage">
                      <StockReasonListPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="payment-methods"
                  element={
                    <ProtectedRoute permission="inventory.catalogs.manage">
                      <PaymentMethodListPage />
                    </ProtectedRoute>
                  }
                />
                {/* Round 2A: 10.4 cash reasons */}
                <Route
                  path="cash-reasons"
                  element={
                    <ProtectedRoute permission="cash.catalogs.manage">
                      <CashReasonListPage />
                    </ProtectedRoute>
                  }
                />
                {/* Round 2A: 10.5 funds */}
                <Route
                  path="funds"
                  element={
                    // Either permission (route rule); the page hides the actions the user lacks.
                    <ProtectedRoute anyPermission={['cash.catalogs.manage', 'cash.opening_balance']}>
                      <FundListPage />
                    </ProtectedRoute>
                  }
                />
                {/* Round 2A: 12.4 opening debts */}
                <Route
                  path="debt/opening"
                  element={
                    <ProtectedRoute permission="debt.opening_balance">
                      <OpeningDebtPage />
                    </ProtectedRoute>
                  }
                />
                {/* Round 2A: 10.7 credit limits */}
                <Route
                  path="credit-limits"
                  element={
                    <ProtectedRoute permission="credit_limit.manage">
                      <CreditLimitListPage />
                    </ProtectedRoute>
                  }
                />
                <Route path="quotations">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="quotations.view">
                        <QuotationListPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="quotations.create">
                        <QuotationFormPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="quotations.update">
                        <QuotationFormPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                <Route path="stock-in">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="stock_in.view">
                        <StockVoucherListPage key="In" type="In" />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="stock_in.create">
                        <StockVoucherFormPage key="In" type="In" />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="stock_in.view">
                        <StockVoucherFormPage key="In" type="In" />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                <Route path="stock-out">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="stock_out.view">
                        <StockVoucherListPage key="Out" type="Out" />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="stock_out.create">
                        <StockVoucherFormPage key="Out" type="Out" />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="stock_out.view">
                        <StockVoucherFormPage key="Out" type="Out" />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                {/* Round 2A: 11.3 receipts and payments (list); 11.4 form for new and :id. Both types share one page component, so each element is keyed by type to never reuse the other's state. */}
                <Route path="receipts">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="receipts.view">
                        <CashVoucherListPage key="Receipt" type="Receipt" />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="receipts.create">
                        <CashVoucherFormPage key="Receipt" type="Receipt" />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="receipts.view">
                        <CashVoucherFormPage key="Receipt" type="Receipt" />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                <Route path="payments">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="payments.view">
                        <CashVoucherListPage key="Payment" type="Payment" />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="payments.create">
                        <CashVoucherFormPage key="Payment" type="Payment" />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="payments.view">
                        <CashVoucherFormPage key="Payment" type="Payment" />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                <Route
                  path="inventory/opening-stock"
                  element={
                    <ProtectedRoute permission="inventory.opening_stock">
                      <OpeningStockPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="inventory/stock-on-hand"
                  element={
                    <ProtectedRoute permission="reports.inventory">
                      <StockOnHandPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="inventory/stock-card"
                  element={
                    <ProtectedRoute permission="reports.inventory">
                      <StockCardPage />
                    </ProtectedRoute>
                  }
                />
                {/* Round 2A: 11.5 fund transfers */}
                <Route path="fund-transfers">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="fund_transfers.view">
                        <FundTransferListPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="fund_transfers.create">
                        <FundTransferFormPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      // Readable with view; the form hides the actions the user lacks.
                      <ProtectedRoute permission="fund_transfers.view">
                        <FundTransferFormPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                {/* Round 2A: 12.2 debt matching */}
                <Route
                  path="debt/matching"
                  element={
                    <ProtectedRoute permission="debt.matching">
                      <DebtMatchingPage />
                    </ProtectedRoute>
                  }
                />
                {/* Round 2A: 12.3 debt offsets */}
                <Route path="debt/offsets">
                  <Route
                    index
                    element={
                      <ProtectedRoute permission="debt_offset.view">
                        <DebtOffsetListPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="new"
                    element={
                      <ProtectedRoute permission="debt_offset.create">
                        <DebtOffsetFormPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path=":id"
                    element={
                      <ProtectedRoute permission="debt_offset.view">
                        <DebtOffsetFormPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                <Route
                  path="settings/my-quotation-settings"
                  element={<MyQuotationSettingsPage />}
                />
                <Route path="qr-thanh-toan" element={<PaymentQrPage />} />
                <Route
                  path="settings/quotation"
                  element={
                    <ProtectedRoute permission="system.manage_settings">
                      <QuotationSystemSettingsPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="settings/branches"
                  element={
                    <ProtectedRoute permission="branches.manage">
                      <BranchesPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="settings/period-lock"
                  element={
                    <ProtectedRoute permission="period_lock.manage">
                      <PeriodLockPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="settings/inventory"
                  element={
                    <ProtectedRoute permission="inventory.settings">
                      <InventorySettingsPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="settings/numbering"
                  element={
                    <ProtectedRoute anyPermission={['inventory.settings', 'cash.settings']}>
                      <NumberingSettingsPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="settings/recalc-cost"
                  element={
                    // The optional product picker also needs products.view (hidden without it).
                    <ProtectedRoute permission="inventory.recalc_cost">
                      <RecalcCostPage />
                    </ProtectedRoute>
                  }
                />
                {/* Round 2A: 10.8 cash settings */}
                <Route
                  path="settings/cash"
                  element={
                    <ProtectedRoute permission="cash.settings">
                      <CashSettingsPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="admin/users"
                  element={
                    <ProtectedRoute permission="user_settings.manage">
                      <UsersListPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="admin/user-settings/:userId"
                  element={
                    <ProtectedRoute permission="user_settings.manage">
                      <UserSettingsPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="admin/users/:userId/transfer-quotations"
                  element={
                    <ProtectedRoute permission="quotations.transfer_any">
                      <BulkTransferPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="admin/roles"
                  element={
                    <ProtectedRoute permission="roles.view">
                      <RolesMatrixPage />
                    </ProtectedRoute>
                  }
                />
                <Route
                  path="admin/dashboard"
                  element={
                    <ProtectedRoute permission="quotations.view_all">
                      <AdminDashboardPage />
                    </ProtectedRoute>
                  }
                />
                <Route path="reports">
                  <Route index element={<Navigate to="revenue" replace />} />
                  <Route
                    path="revenue"
                    element={
                      <ProtectedRoute permission="reports.revenue">
                        <RevenuePage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="sales-performance"
                    element={
                      <ProtectedRoute permission="quotations.view_all">
                        <SalesPerformancePage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="sales-revenue"
                    element={
                      <ProtectedRoute permission="reports.revenue">
                        <SalesRevenuePage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="sales-revenue/:saleUserId"
                    element={
                      <ProtectedRoute permission="reports.revenue">
                        <SalesRevenueDetailPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="vehicle-revenue"
                    element={
                      <ProtectedRoute permission="reports.revenue">
                        <VehicleRevenuePage />
                      </ProtectedRoute>
                    }
                  />
                  {/* Round 2A: 12.5 debt balance, debt ledger and cash book reports */}
                  <Route
                    path="debt-balance"
                    element={
                      <ProtectedRoute permission="reports.debt">
                        <DebtBalancePage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="debt-ledger"
                    element={
                      <ProtectedRoute permission="reports.debt">
                        <DebtLedgerPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="debt-documents"
                    element={
                      <ProtectedRoute permission="reports.debt">
                        <DebtDocumentsPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="cash-book"
                    element={
                      <ProtectedRoute permission="reports.cash">
                        <CashBookPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>
                <Route path="settings" element={<SettingsHubPage />} />
              </Route>

              <Route path="/404" element={<NotFoundPage />} />
              <Route path="*" element={<Navigate to="/404" replace />} />
            </Routes>
            </RouteErrorBoundary>
          </AuthInit>
        </BrowserRouter>
        <Toaster />
        <ReactQueryDevtools initialIsOpen={false} />
      </QueryClientProvider>
      {needRefresh && (
        <div className="fixed bottom-4 right-4 z-50 flex items-center gap-3 rounded-lg bg-gray-900 px-4 py-3 text-sm text-white shadow-xl">
          <span>Có phiên bản mới</span>
          <button
            onClick={() => updateServiceWorker(true)}
            className="rounded bg-blue-500 px-3 py-1 text-xs font-semibold hover:bg-blue-400"
          >
            Cập nhật ngay
          </button>
          <button onClick={() => setNeedRefresh(false)} aria-label="Đóng" className="text-gray-400 hover:text-white">✕</button>
        </div>
      )}
    </ErrorBoundary>
  );
}
