import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { QuotationListPage } from './quotation-list-page';
import { quotationsApi } from '@/features/quotations/api';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';

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

function renderPage(initialEntries: string[] = ['/quotations']) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={initialEntries}>
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

const ADMIN_USER = {
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
};

function lastListStatuses() {
  const calls = vi.mocked(quotationsApi.list).mock.calls;
  return calls[calls.length - 1][0].statuses;
}

describe('QuotationListPage status filter persistence', () => {
  beforeEach(() => {
    localStorage.clear();
    useUiStore.setState({ sidebarCollapsed: false, quotationStatusFilter: null });
    vi.mocked(quotationsApi.list).mockClear();
    useAuthStore.setState({
      accessToken: 'test-token',
      expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      user: ADMIN_USER,
    });
  });

  it('uses the default active statuses when nothing is stored', async () => {
    renderPage();
    await screen.findByRole('columnheader', { name: 'Số báo giá' });
    expect(lastListStatuses()).toEqual(['Draft', 'Sent', 'Confirmed', 'AccountingConfirmed']);
  });

  it('restores the stored selection on the very first request', async () => {
    useUiStore.setState({ quotationStatusFilter: ['Confirmed'] });

    renderPage();
    await screen.findByRole('columnheader', { name: 'Số báo giá' });

    expect(vi.mocked(quotationsApi.list)).toHaveBeenCalledTimes(1);
    expect(lastListStatuses()).toEqual(['Confirmed']);
  });

  it('lets an explicit URL status win over the stored selection', async () => {
    useUiStore.setState({ quotationStatusFilter: ['Confirmed'] });

    renderPage(['/quotations?status=Draft']);
    await screen.findByRole('columnheader', { name: 'Số báo giá' });

    expect(lastListStatuses()).toEqual(['Draft']);
  });

  it('falls back to the defaults when the stored selection is not a known status', async () => {
    useUiStore.setState({ quotationStatusFilter: ['Foo'] });

    renderPage();
    await screen.findByRole('columnheader', { name: 'Số báo giá' });

    expect(lastListStatuses()).toEqual(['Draft', 'Sent', 'Confirmed', 'AccountingConfirmed']);
  });

  it('treats a stored empty selection as the defaults', async () => {
    useUiStore.setState({ quotationStatusFilter: [] });

    renderPage();
    await screen.findByRole('columnheader', { name: 'Số báo giá' });

    expect(lastListStatuses()).toEqual(['Draft', 'Sent', 'Confirmed', 'AccountingConfirmed']);
  });

  it('saves the new selection to the store when the user changes it', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByRole('columnheader', { name: 'Số báo giá' });

    await user.click(screen.getByRole('button', { name: 'Trạng thái' }));
    await user.click(await screen.findByRole('menuitemcheckbox', { name: 'Nháp' }));

    await waitFor(() => {
      expect(useUiStore.getState().quotationStatusFilter).toEqual([
        'Sent',
        'Confirmed',
        'AccountingConfirmed',
      ]);
    });
    await waitFor(() => {
      expect(lastListStatuses()).toEqual(['Sent', 'Confirmed', 'AccountingConfirmed']);
    });
  });
});
