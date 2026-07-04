import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { QuotationListPage } from './quotation-list-page';
import { useAuthStore } from '@/stores/auth-store';

vi.mock('@/features/quotations/api', () => ({
  quotationsApi: {
    list: vi.fn().mockResolvedValue({
      items: [],
      totalItems: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
      aggregates: {},
    }),
    listOwners: vi.fn().mockResolvedValue([]),
    downloadPdf: vi.fn(),
    downloadExcel: vi.fn(),
    downloadHandoverWithPricePdf: vi.fn(),
    downloadHandoverNoPricePdf: vi.fn(),
    downloadHandoverWithPriceExcel: vi.fn(),
    downloadHandoverNoPriceExcel: vi.fn(),
  },
}));

function renderPage() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <QuotationListPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('QuotationListPage column resizing', () => {
  beforeEach(() => {
    useAuthStore.setState({
      accessToken: 'test-token',
      expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      user: {
        id: 'u1',
        username: 'tester',
        email: 't@example.com',
        fullName: 'Tester',
        roles: ['ADMIN'],
        permissions: [
          'quotations.create',
          'quotations.update',
          'quotations.print',
          'quotations.view_all',
          'quotations.view_cost',
          'quotations.accounting_confirm',
        ],
      },
    });
  });

  it('renders a resize handle on regular column headers but not on the actions header', async () => {
    renderPage();
    const codeHeader = await screen.findByRole('columnheader', { name: 'Số báo giá' });
    expect(within(codeHeader).getByTestId('column-resize-handle')).toBeInTheDocument();

    const headers = screen.getAllByRole('columnheader');
    const actionsHeader = headers[headers.length - 1];
    expect(within(actionsHeader).queryByTestId('column-resize-handle')).not.toBeInTheDocument();
  });

  it('renders a colgroup with one col per header column', async () => {
    renderPage();
    await screen.findByRole('columnheader', { name: 'Số báo giá' });
    const headers = screen.getAllByRole('columnheader');
    const cols = document.querySelectorAll('col');
    expect(cols.length).toBe(headers.length);
  });
});
