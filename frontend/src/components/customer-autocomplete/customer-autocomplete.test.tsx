import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CustomerAutocomplete } from './customer-autocomplete';
import type { CustomerSearchItem, PagedResult, CustomerListItem } from '@/features/customers/types';

const searchMock = vi.fn();
const listMock = vi.fn();
const getMock = vi.fn();

vi.mock('@/features/customers/api', () => ({
  customersApi: {
    search: (...args: unknown[]) => searchMock(...args),
    list: (...args: unknown[]) => listMock(...args),
    get: (...args: unknown[]) => getMock(...args),
  },
}));

const sample: CustomerSearchItem[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    code: 'KH-001',
    name: 'Công ty TNHH ABC',
    taxCode: '0100000001',
    companyAddress: '12 Đường Số 1',
    phoneNumber: '0901111111',
    status: 'Active',
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    code: 'KH-002',
    name: 'Công ty TNHH XYZ',
    taxCode: '0100000002',
    companyAddress: '34 Lê Duẩn',
    phoneNumber: '0902222222',
    status: 'Active',
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    code: 'KH-003',
    name: 'Khách lẻ DEF',
    phoneNumber: '0903333333',
    status: 'Active',
  },
];

function renderWithClient(ui: React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

function baseProps(overrides: Partial<React.ComponentProps<typeof CustomerAutocomplete>> = {}) {
  return {
    value: null,
    onSelect: vi.fn(),
    onClear: vi.fn(),
    onAddNewClick: vi.fn(),
    ...overrides,
  };
}

async function typeAndWaitForResults(input: HTMLElement, text: string, count = sample.length) {
  fireEvent.change(input, { target: { value: text } });
  await waitFor(() => {
    expect(screen.getAllByRole('option').length).toBe(count);
  });
}

describe('CustomerAutocomplete', () => {
  beforeEach(() => {
    searchMock.mockReset();
    searchMock.mockResolvedValue(sample);
    listMock.mockReset();
    listMock.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      totalItems: 0,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    } as PagedResult<CustomerListItem>);
    getMock.mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('does not open dropdown when input is focused and empty', () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    fireEvent.focus(input);
    expect(input).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('listbox')).toBeNull();
  });

  it('opens dropdown and shows results after typing', async () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'cong');
    expect(searchMock).toHaveBeenCalled();
    expect(screen.getByRole('listbox')).toBeInTheDocument();
  });

  it('Arrow Down moves highlight; Arrow Up moves up', async () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'cong');
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    expect(screen.getAllByRole('option')[1]).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(input, { key: 'ArrowUp' });
    expect(screen.getAllByRole('option')[0]).toHaveAttribute('aria-selected', 'true');
  });

  it('Tab cycles highlight forward and wraps at end', async () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'cong');
    fireEvent.keyDown(input, { key: 'Tab' });
    expect(screen.getAllByRole('option')[1]).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(input, { key: 'Tab' });
    expect(screen.getAllByRole('option')[2]).toHaveAttribute('aria-selected', 'true');
    fireEvent.keyDown(input, { key: 'Tab' });
    expect(screen.getAllByRole('option')[0]).toHaveAttribute('aria-selected', 'true');
  });

  it('Shift+Tab cycles backward', async () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'cong');
    fireEvent.keyDown(input, { key: 'Tab', shiftKey: true });
    expect(screen.getAllByRole('option')[2]).toHaveAttribute('aria-selected', 'true');
  });

  it('Enter selects highlighted row', async () => {
    const onSelect = vi.fn();
    renderWithClient(<CustomerAutocomplete {...baseProps({ onSelect })} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'cong');
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onSelect).toHaveBeenCalledWith(sample[1]);
  });

  it('Enter with exact code match auto-selects even without highlight change', async () => {
    const onSelect = vi.fn();
    renderWithClient(<CustomerAutocomplete {...baseProps({ onSelect })} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'KH-002');
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onSelect).toHaveBeenCalledWith(sample[1]);
  });

  it('Escape closes dropdown but keeps keyword', async () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i }) as HTMLInputElement;
    await typeAndWaitForResults(input, 'cong');
    fireEvent.keyDown(input, { key: 'Escape' });
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(input.value).toBe('cong');
  });

  it('Add-new button calls onAddNewClick', () => {
    const onAddNewClick = vi.fn();
    renderWithClient(<CustomerAutocomplete {...baseProps({ onAddNewClick })} />);
    fireEvent.click(screen.getByRole('button', { name: /thêm mới khách hàng/i }));
    expect(onAddNewClick).toHaveBeenCalled();
  });

  it('Add-new button shows the add icon in the create tone', () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} />);
    const button = screen.getByRole('button', { name: /thêm mới khách hàng/i });
    expect(button.querySelector('[data-icon="add"]')).toHaveClass('text-icon-create');
  });

  it('renders empty state when no results', async () => {
    searchMock.mockResolvedValueOnce([]);
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    fireEvent.change(input, { target: { value: 'zzz' } });
    await waitFor(() => {
      expect(screen.getByText(/không tìm thấy khách hàng phù hợp/i)).toBeInTheDocument();
    });
  });

  it('renders code (not name) when value is selected', () => {
    renderWithClient(
      <CustomerAutocomplete
        {...baseProps({
          value: { id: sample[0].id, code: sample[0].code, name: sample[0].name },
        })}
        inputAriaLabel="cust"
      />,
    );
    const input = screen.getByRole('combobox', { name: /cust/i }) as HTMLInputElement;
    expect(input.value).toBe('KH-001');
    expect(input.value).not.toBe('Công ty TNHH ABC');
  });

  it('renders meta header with result count and keyboard hints', async () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'cong');
    expect(screen.getByText(/Tìm thấy 3 khách hàng đang hoạt động/i)).toBeInTheDocument();
    expect(screen.getByText('Tab')).toBeInTheDocument();
    expect(screen.getByText('Enter')).toBeInTheDocument();
    expect(screen.getByText('Esc')).toBeInTheDocument();
  });

  it('shows "Xem danh mục đầy đủ" button in dropdown when results are shown', async () => {
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'cong');
    expect(screen.getByRole('button', { name: /xem danh mục đầy đủ/i })).toBeInTheDocument();
  });

  it('clicking catalog button closes dropdown', async () => {
    const user = userEvent.setup();
    renderWithClient(<CustomerAutocomplete {...baseProps()} inputAriaLabel="cust" />);
    const input = screen.getByRole('combobox', { name: /cust/i });
    await typeAndWaitForResults(input, 'cong');
    const btn = screen.getByRole('button', { name: /xem danh mục đầy đủ/i });
    await user.click(btn);
    expect(input).toHaveAttribute('aria-expanded', 'false');
  });
});
