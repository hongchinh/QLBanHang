import { useState } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PartnerCatalogList } from './partner-catalog-list';
import type { PagedResult, CustomerListItem } from '@/features/customers/types';

const useCustomersMock = vi.hoisted(() => vi.fn());
const useSuppliersMock = vi.hoisted(() => vi.fn());
vi.mock('@/features/customers/hooks', () => ({ useCustomers: useCustomersMock }));
vi.mock('@/features/suppliers/hooks', () => ({ useSuppliers: useSuppliersMock }));

const sampleItems: CustomerListItem[] = [
  { id: 'id-1', code: 'KH-001', name: 'Công ty ABC', taxCode: '0100000001', phoneNumber: '0901', contactPerson: 'Anh A', group: 'Company', status: 'Active' },
  { id: 'id-2', code: 'KH-002', name: 'Đại lý XYZ', taxCode: '0100000002', phoneNumber: '0902', contactPerson: 'Chị B', group: 'Agent', status: 'Active' },
  { id: 'id-3', code: 'KH-003', name: 'Khách lẻ C', phoneNumber: '0903', group: 'Retail', status: 'Active' },
];

function paged(over: Partial<PagedResult<CustomerListItem>> = {}): PagedResult<CustomerListItem> {
  return {
    items: sampleItems,
    page: 1,
    pageSize: 20,
    totalItems: 3,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
    ...over,
  };
}

interface HarnessProps {
  partnerRole?: 'customer' | 'supplier';
  initialQuery?: string;
  onSelect?: (id: string) => void;
  onQueryChange?: (q: string) => void;
}

// The dialog owns `query`; this stands in for it.
function Harness({ partnerRole = 'customer', initialQuery = '', onSelect = vi.fn(), onQueryChange }: HarnessProps) {
  const [query, setQuery] = useState(initialQuery);
  return (
    <PartnerCatalogList
      role={partnerRole}
      query={query}
      onQueryChange={(q) => {
        setQuery(q);
        onQueryChange?.(q);
      }}
      onSelect={onSelect}
    />
  );
}

function searchInput() {
  return screen.getByRole('textbox') as HTMLInputElement;
}

function rows() {
  return screen.getAllByRole('row').slice(1);
}

function highlightedIndexes() {
  return rows()
    .map((r, i) => (r.getAttribute('aria-selected') === 'true' ? i : -1))
    .filter((i) => i >= 0);
}

beforeEach(() => {
  useCustomersMock.mockReset();
  useSuppliersMock.mockReset();
  useCustomersMock.mockReturnValue({ data: paged(), isLoading: false });
  useSuppliersMock.mockReturnValue({ data: paged(), isLoading: false });
});

describe('PartnerCatalogList', () => {
  it('customer role: headers, placeholder, footer and the useCustomers params', () => {
    render(<Harness />);

    expect(screen.getAllByRole('columnheader').map((h) => h.textContent)).toEqual([
      'Mã', 'Tên', 'MST', 'Người LH', 'SĐT', 'Nhóm',
    ]);
    expect(screen.getByPlaceholderText('Tìm mã, tên khách hàng...')).toBeInTheDocument();
    expect(screen.getByText('(3 khách hàng)')).toBeInTheDocument();
    expect(useCustomersMock).toHaveBeenLastCalledWith({
      page: 1, pageSize: 20, search: undefined, group: undefined, status: 'Active',
    });
    expect(useSuppliersMock).not.toHaveBeenCalled();
  });

  it('supplier role: useSuppliers with the same params and supplier wording', () => {
    useSuppliersMock.mockReturnValue({ data: paged({ items: [], totalItems: 0 }), isLoading: false });
    render(<Harness partnerRole="supplier" />);

    expect(screen.getByPlaceholderText('Tìm mã, tên nhà cung cấp...')).toBeInTheDocument();
    expect(screen.getByText('Không tìm thấy nhà cung cấp phù hợp')).toBeInTheDocument();
    expect(screen.getByText('(0 nhà cung cấp)')).toBeInTheDocument();
    expect(useSuppliersMock).toHaveBeenLastCalledWith({
      page: 1, pageSize: 20, search: undefined, group: undefined, status: 'Active',
    });
    expect(useCustomersMock).not.toHaveBeenCalled();
  });

  it('shows the empty text for customers', () => {
    useCustomersMock.mockReturnValue({ data: paged({ items: [], totalItems: 0 }), isLoading: false });
    render(<Harness />);

    expect(screen.getByText('Không tìm thấy khách hàng phù hợp')).toBeInTheDocument();
  });

  it('the search input is focused and shows the query', () => {
    render(<Harness initialQuery="ABC" />);

    expect(searchInput()).toHaveValue('ABC');
    expect(searchInput()).toHaveFocus();
  });

  it('renders all 5 group tabs', () => {
    render(<Harness />);

    expect(screen.getByRole('tab', { name: /tất cả/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /công ty/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /đại lý/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /khách lẻ/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /công trình/i })).toBeInTheDocument();
  });

  it('renders the rows and shows the pagination info', () => {
    render(<Harness />);

    expect(screen.getByText('KH-001')).toBeInTheDocument();
    expect(screen.getByText('KH-002')).toBeInTheDocument();
    expect(screen.getByText(/trang 1 \/ 1/i)).toBeInTheDocument();
  });

  it('calls onSelect with the id when a row is clicked', async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Harness onSelect={onSelect} />);

    await user.click(screen.getByText('KH-002').closest('tr')!);

    expect(onSelect).toHaveBeenCalledWith('id-2');
  });

  it('typing calls onQueryChange, queries after the debounce and resets the page to 1', async () => {
    const user = userEvent.setup();
    const onQueryChange = vi.fn();
    useCustomersMock.mockReturnValue({ data: paged({ totalPages: 3 }), isLoading: false });
    render(<Harness onQueryChange={onQueryChange} />);
    await user.click(screen.getByRole('button', { name: /sau/i }));
    expect(useCustomersMock).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 }));

    await user.type(searchInput(), 'ab');

    expect(onQueryChange).toHaveBeenLastCalledWith('ab');
    expect(searchInput()).toHaveValue('ab');
    expect(useCustomersMock).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1 }));
    await waitFor(() =>
      expect(useCustomersMock).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1, search: 'ab' })),
    );
  });

  it('clicking the Đại lý tab queries group=Agent', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('tab', { name: /đại lý/i }));

    expect(useCustomersMock).toHaveBeenLastCalledWith(expect.objectContaining({ group: 'Agent', page: 1 }));
  });

  describe('keyboard', () => {
    it('highlights row 0 first, ArrowDown / ArrowUp move it and stop at the ends', () => {
      render(<Harness />);
      expect(highlightedIndexes()).toEqual([0]);
      expect(rows()[0]).toHaveClass('qldh-lookup-row-highlight');

      fireEvent.keyDown(searchInput(), { key: 'ArrowUp' });
      expect(highlightedIndexes()).toEqual([0]);

      fireEvent.keyDown(searchInput(), { key: 'ArrowDown' });
      expect(highlightedIndexes()).toEqual([1]);
      expect(rows()[1]).toHaveClass('qldh-lookup-row-highlight');
      expect(rows()[0]).not.toHaveClass('qldh-lookup-row-highlight');

      fireEvent.keyDown(searchInput(), { key: 'ArrowDown' });
      fireEvent.keyDown(searchInput(), { key: 'ArrowDown' });
      expect(highlightedIndexes()).toEqual([2]);

      fireEvent.keyDown(searchInput(), { key: 'ArrowUp' });
      expect(highlightedIndexes()).toEqual([1]);
    });

    it('Enter selects row 0 at first, then the highlighted row', () => {
      const onSelect = vi.fn();
      render(<Harness onSelect={onSelect} />);

      fireEvent.keyDown(searchInput(), { key: 'Enter' });
      expect(onSelect).toHaveBeenLastCalledWith('id-1');

      fireEvent.keyDown(searchInput(), { key: 'ArrowDown' });
      fireEvent.keyDown(searchInput(), { key: 'Enter' });
      expect(onSelect).toHaveBeenLastCalledWith('id-2');
    });

    it('Enter does nothing when there are no rows', () => {
      useCustomersMock.mockReturnValue({ data: paged({ items: [], totalItems: 0 }), isLoading: false });
      const onSelect = vi.fn();
      render(<Harness onSelect={onSelect} />);

      fireEvent.keyDown(searchInput(), { key: 'Enter' });

      expect(onSelect).not.toHaveBeenCalled();
    });

    it('the highlight returns to row 0 when the query changes', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      fireEvent.keyDown(searchInput(), { key: 'ArrowDown' });
      expect(highlightedIndexes()).toEqual([1]);

      await user.type(searchInput(), 'a');

      expect(highlightedIndexes()).toEqual([0]);
    });

    it('the highlight returns to row 0 when the group tab changes', async () => {
      const user = userEvent.setup();
      render(<Harness />);
      fireEvent.keyDown(searchInput(), { key: 'ArrowDown' });
      expect(highlightedIndexes()).toEqual([1]);

      await user.click(screen.getByRole('tab', { name: /đại lý/i }));

      expect(highlightedIndexes()).toEqual([0]);
    });

    it('the highlight returns to row 0 when the page changes', async () => {
      const user = userEvent.setup();
      useCustomersMock.mockReturnValue({ data: paged({ totalPages: 2 }), isLoading: false });
      render(<Harness />);
      fireEvent.keyDown(searchInput(), { key: 'ArrowDown' });
      expect(highlightedIndexes()).toEqual([1]);

      await user.click(screen.getByRole('button', { name: /sau/i }));

      expect(highlightedIndexes()).toEqual([0]);
    });
  });
});
