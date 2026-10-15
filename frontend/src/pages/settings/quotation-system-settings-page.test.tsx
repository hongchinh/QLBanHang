import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QuotationSystemSettingsPage } from './quotation-system-settings-page';

vi.mock('@/features/quotation-settings/hooks', () => ({
  useQuotationSystemSettings: () => ({
    data: { revenueReportingDateField: 'QuotationDate', updatedAt: null, updatedByName: null },
    isLoading: false,
  }),
  useUpdateQuotationSystemSettings: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

describe('QuotationSystemSettingsPage', () => {
  it('links back to the settings hub', () => {
    render(
      <MemoryRouter>
        <QuotationSystemSettingsPage />
      </MemoryRouter>,
    );

    expect(screen.getByRole('link', { name: 'Quay lại Cấu hình' })).toHaveAttribute('href', '/settings');
  });
});
