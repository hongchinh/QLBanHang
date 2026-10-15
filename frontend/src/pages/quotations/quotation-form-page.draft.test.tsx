import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QuotationFormPage } from './quotation-form-page';
import { writeQuotationDraft } from '@/features/quotations/use-quotation-draft';
import type { QuotationFormValues } from '@/features/quotations/schema';
import React from 'react';

// useAuthStore.getState() is called in component body (not as hook).
// Mock the module-level export so getState() returns a stable user.
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: Object.assign(
    (selector: (s: { user: { id: string } | null; permissions: string[]; hasPermission: () => boolean }) => unknown) =>
      selector({ user: { id: 'user-test' }, permissions: [], hasPermission: () => false }),
    {
      getState: () => ({ user: { id: 'user-test' }, hasPermission: () => false }),
    },
  ),
}));

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return {
    ...actual,
    // Route for new-quotation form — id='new' means isEdit=false
    useParams: () => ({ id: 'new' }),
    useNavigate: () => vi.fn(),
    Link: ({ children, to, ...rest }: { children: React.ReactNode; to: string }) =>
      React.createElement('a', { href: String(to), ...rest }, children),
  };
});

vi.mock('@/features/quotations/hooks', () => ({
  useQuotation: () => ({ data: undefined, isLoading: false }),
  useCreateQuotation: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
  }),
  useUpdateQuotation: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
  }),
  useTransitionQuotation: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
  }),
  useCloneQuotation: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useQuotationActivities: () => ({
    data: [],
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }),
}));

vi.mock('@/features/customers/hooks', () => ({
  useCustomer: () => ({ data: undefined }),
  useCustomersSearch: () => ({ data: [], isFetching: false }),
}));

vi.mock('@/components/auth/can', () => ({
  Can: ({ children }: { children: React.ReactNode }) => React.createElement(React.Fragment, null, children),
}));

// LineItemsGrid uses getBoundingClientRect and complex grid navigation — jsdom has no layout.
// TotalsPanel depends on computed line totals — stub both to isolate draft behavior.
// Async factory required: React.forwardRef() must be called after React is initialized (TDZ guard).
vi.mock('./components/line-items-grid', async (importOriginal) => {
  const { forwardRef, createElement } = await import('react');
  const actual = await importOriginal<typeof import('./components/line-items-grid')>();
  return {
    ...actual,
    LineItemsGrid: forwardRef((_props: unknown, _ref: unknown) =>
      createElement('div', { 'data-testid': 'line-items-grid' }),
    ),
  };
});

vi.mock('./components/totals-panel', () => ({
  TotalsPanel: () => React.createElement('div', { 'data-testid': 'totals-panel' }),
}));

const DRAFT_VALUES: QuotationFormValues = {
  customerId: 'cust-001',
  customerName: 'Khách hàng nháp',
  quotationDate: '2026-05-29',
  deliveryAddress: 'Địa chỉ nháp',
  deliveryRecipient: '',
  deliveryPhone: '',
  transportVehicleNumber: '',
  deliveryDate: '2026-05-30',
  deliveryNote: '',
  taxRate: 10,
  discount: 0,
  freight: 0,
  advancePayment: 0,
  internalNote: '',
  lines: [
    {
      _uiKey: 'key1',
      sortOrder: 0,
      productName: 'Hàng nháp A',
      unitName: 'Cái',
      pricingMode: 'PerUnit',
      quantity: 2,
      unitPrice: 50000,
    },
  ],
};

describe('QuotationFormPage — draft restore', () => {
  beforeEach(() => localStorage.clear());

  it('does not show draft badge when no draft in localStorage', () => {
    render(<QuotationFormPage />);
    expect(screen.queryByText(/nháp chưa lưu/i)).toBeNull();
  });

  it('shows draft badge when draft exists in localStorage', async () => {
    writeQuotationDraft('user-test', DRAFT_VALUES, null);
    render(<QuotationFormPage />);
    await waitFor(() => {
      expect(screen.getByText(/nháp chưa lưu/i)).toBeInTheDocument();
    });
  });

  it('pre-fills customerName from draft', async () => {
    writeQuotationDraft('user-test', DRAFT_VALUES, null);
    render(<QuotationFormPage />);
    await waitFor(() => {
      const input = screen.getByRole<HTMLInputElement>('textbox', { name: /tên kh/i });
      expect(input.value).toBe('Khách hàng nháp');
    });
  });
});
