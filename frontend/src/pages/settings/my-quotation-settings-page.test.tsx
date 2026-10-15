import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { MyQuotationSettingsPage } from './my-quotation-settings-page';

vi.mock('@/features/me-settings/hooks', () => ({
  useMySettings: () => ({ data: undefined, isLoading: false }),
  useUploadTemplate: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteTemplate: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useUploadHandoverTemplate: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteHandoverTemplate: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean }) => unknown) =>
    selector({ hasPermission: () => false }),
}));

vi.mock('@/features/bank-accounts/bank-accounts-tab', () => ({ BankAccountsTab: () => null }));
vi.mock('@/features/branding/branding-tab', () => ({ BrandingTab: () => null }));
vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

describe('MyQuotationSettingsPage', () => {
  it('links back to the settings hub', () => {
    render(
      <MemoryRouter>
        <MyQuotationSettingsPage />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'Quay lại Cấu hình' })).toHaveAttribute('href', '/settings');
  });
});
