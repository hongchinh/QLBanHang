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
import { ProductListPage } from '@/pages/products/product-list-page';
import { ProductFormPage } from '@/pages/products/product-form-page';
import { ProductGroupListPage } from '@/pages/product-groups/product-group-list-page';
import { WarehouseListPage } from '@/pages/warehouses/warehouse-list-page';
import { StockReasonListPage } from '@/pages/stock-reasons/stock-reason-list-page';
import { PaymentMethodListPage } from '@/pages/payment-methods/payment-method-list-page';
import { QuotationListPage } from '@/pages/quotations/quotation-list-page';
import { QuotationFormPage } from '@/pages/quotations/quotation-form-page';
import { MyQuotationSettingsPage } from '@/pages/settings/my-quotation-settings-page';
import { PaymentQrPage } from '@/pages/payment-qr/payment-qr-page';
import { QuotationSystemSettingsPage } from '@/pages/settings/quotation-system-settings-page';
import { SettingsHubPage } from '@/pages/settings/settings-hub-page';
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
