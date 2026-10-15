import { useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { PartnerAutocomplete } from './partner-autocomplete';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';

const usePartnerSearchMock = vi.fn();

vi.mock('@/features/stock-vouchers/hooks', () => ({
  usePartnerSearch: (...args: unknown[]) => usePartnerSearchMock(...args),
}));

const REASON = '22222222-2222-2222-2222-222222222222';

const partners: PartnerSearchItem[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    code: 'NCC01',
    name: 'Công ty Thép Việt',
    taxCode: '0100000001',
    companyAddress: '12 Đường Số 1',
    status: 'Active',
    isCustomer: false,
    isSupplier: true,
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    code: 'KH01',
    name: 'Công ty ABC',
    status: 'Active',
    isCustomer: true,
    isSupplier: true,
  },
];

type Props = React.ComponentProps<typeof PartnerAutocomplete>;

function renderAutocomplete(overrides: Partial<Props> = {}) {
  const props = {
    type: 'In',
    reasonId: REASON,
    partnerType: 'Supplier',
    value: null,
    onSelect: vi.fn(),
    inputId: 'partner',
    ...overrides,
  } as Props;
  render(<PartnerAutocomplete {...props} />);
  return props;
}

describe('PartnerAutocomplete', () => {
  beforeEach(() => {
    usePartnerSearchMock.mockReset();
    usePartnerSearchMock.mockReturnValue({ data: partners, isLoading: false, isError: false });
  });

  it('is disabled until a reason is chosen', () => {
    renderAutocomplete({ reasonId: undefined, partnerType: undefined });
    const input = screen.getByRole('combobox');
    expect(input).toBeDisabled();
    expect(input).toHaveAttribute('placeholder', 'Chọn lý do trước');
  });

  it('is disabled for partner type None', () => {
    renderAutocomplete({ partnerType: 'None' });
    const input = screen.getByRole('combobox');
    expect(input).toBeDisabled();
    expect(input).toHaveAttribute('placeholder', 'Không cần đối tượng');
  });

  it('filter mode (requireReason=false) is enabled without a reason', async () => {
    renderAutocomplete({ requireReason: false, reasonId: undefined, partnerType: undefined, type: 'Out' });
    const input = screen.getByRole('combobox');
    expect(input).toBeEnabled();
    fireEvent.change(input, { target: { value: 'cong' } });
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('Out', 'cong', undefined, undefined));
  });

  it('lists results and selects with Enter', async () => {
    const props = renderAutocomplete();
    const input = screen.getByRole('combobox');
    fireEvent.change(input, { target: { value: 'cong' } });
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(2));
    expect(usePartnerSearchMock).toHaveBeenLastCalledWith('In', 'cong', REASON, undefined);

    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(props.onSelect).toHaveBeenCalledWith(partners[1]);
  });

  it('a given search hook replaces the stock voucher partner search', async () => {
    const useSearch = vi.fn((keyword: string) => ({ data: keyword ? [partners[1]] : [], isFetching: false }));
    const props = renderAutocomplete({ type: undefined, partnerType: 'Customer', useSearch });
    const input = screen.getByRole('combobox');

    fireEvent.change(input, { target: { value: 'abc' } });

    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(1));
    expect(useSearch.mock.lastCall?.slice(0, 2)).toEqual(['abc', REASON]);
    expect(usePartnerSearchMock).not.toHaveBeenCalled();
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(props.onSelect).toHaveBeenCalledWith(partners[1]);
  });

  it('clear button calls onSelect(null)', () => {
    const props = renderAutocomplete({ value: { id: partners[0].id, code: 'NCC01', name: 'Công ty Thép Việt' } });
    expect(screen.getByRole('combobox')).toHaveValue('NCC01');
    fireEvent.click(screen.getByRole('button', { name: 'Bỏ chọn đối tượng' }));
    expect(props.onSelect).toHaveBeenCalledWith(null);
  });
});

function manyPartners(count: number): PartnerSearchItem[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `00000000-0000-4000-8000-${String(i).padStart(12, '0')}`,
    code: `KH${String(i).padStart(2, '0')}`,
    name: `Khách ${i}`,
    status: 'Active' as const,
    isCustomer: true,
    isSupplier: false,
  }));
}

async function typeAndOpen(text = 'abc') {
  const input = screen.getByRole('combobox');
  fireEvent.change(input, { target: { value: text } });
  await screen.findByRole('listbox');
  return input;
}

describe('PartnerAutocomplete list and modes', () => {
  beforeEach(() => {
    usePartnerSearchMock.mockReset();
    usePartnerSearchMock.mockReturnValue({ data: partners, isLoading: false, isError: false });
  });

  it('header says both roles and active only for a form field', async () => {
    renderAutocomplete({ activeOnly: true, requireReason: false });
    await typeAndOpen();
    expect(await screen.findByText('Tìm thấy 2 đối tượng (KH + NCC) đang dùng')).toBeInTheDocument();
  });

  it('header has no "đang dùng" without activeOnly', async () => {
    renderAutocomplete({ requireReason: false });
    await typeAndOpen();
    expect(await screen.findByText('Tìm thấy 2 đối tượng')).toBeInTheDocument();
  });

  it('header names customers for noun="khách hàng"', async () => {
    renderAutocomplete({ activeOnly: true, noun: 'khách hàng', requireReason: false });
    await typeAndOpen();
    expect(await screen.findByText('Tìm thấy 2 khách hàng đang dùng')).toBeInTheDocument();
  });

  it('header says so when 20 results come back', async () => {
    usePartnerSearchMock.mockReturnValue({ data: manyPartners(20), isLoading: false, isError: false });
    renderAutocomplete({ activeOnly: true, requireReason: false });
    await typeAndOpen();
    expect(await screen.findByText('Hiển thị 20 kết quả đầu — gõ thêm để thu hẹp')).toBeInTheDocument();
  });

  it('has a SĐT column with the phone number', async () => {
    usePartnerSearchMock.mockReturnValue({
      data: [{ ...partners[0], phoneNumber: '0901234567' }],
      isLoading: false,
      isError: false,
    });
    renderAutocomplete({ requireReason: false });
    await typeAndOpen();
    expect(screen.getByRole('columnheader', { name: 'SĐT' })).toBeInTheDocument();
    expect(screen.getByText('0901234567')).toBeInTheDocument();
  });

  it('empty result text names the noun and the typed text', async () => {
    usePartnerSearchMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    const { unmount } = render(
      <PartnerAutocomplete type="In" requireReason={false} value={null} onSelect={vi.fn()} />,
    );
    await typeAndOpen('zzz');
    expect(await screen.findByText('Không tìm thấy đối tượng "zzz"')).toBeInTheDocument();
    unmount();

    render(<PartnerAutocomplete type="In" noun="khách hàng" requireReason={false} value={null} onSelect={vi.fn()} />);
    await typeAndOpen('zzz');
    expect(await screen.findByText('Không tìm thấy khách hàng "zzz"')).toBeInTheDocument();
  });

  it('shows the error text', async () => {
    usePartnerSearchMock.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    renderAutocomplete({ requireReason: false });
    await typeAndOpen();
    expect(await screen.findByText('Không thể tải danh sách. Vui lòng thử lại.')).toBeInTheDocument();
  });

  it('shows the loading text', async () => {
    usePartnerSearchMock.mockReturnValue({ data: undefined, isLoading: true, isFetching: true, isError: false });
    renderAutocomplete({ requireReason: false });
    await typeAndOpen();
    expect(await screen.findByText('Đang tìm kiếm...')).toBeInTheDocument();
  });

  it('passes (type, keyword, reasonId, activeOnly) to the default stock search', async () => {
    renderAutocomplete({ activeOnly: true });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'cong' } });
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('In', 'cong', REASON, true));
  });

  it('passes (keyword, reasonId, activeOnly) to a given search hook', async () => {
    const useSearch = vi.fn((keyword: string) => ({ data: keyword ? [partners[1]] : [], isFetching: false }));
    renderAutocomplete({ type: undefined, partnerType: 'Customer', useSearch, activeOnly: true });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'abc' } });
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(1));
    expect(useSearch.mock.lastCall?.slice(0, 3)).toEqual(['abc', REASON, true]);
  });

  it('key hints: F4 only with onAddNew', async () => {
    const { unmount } = render(
      <PartnerAutocomplete type="In" requireReason={false} value={null} onSelect={vi.fn()} />,
    );
    await typeAndOpen();
    expect(screen.getByText('↑↓ duyệt · Enter chọn · Esc đóng')).toBeInTheDocument();
    unmount();

    render(<PartnerAutocomplete type="In" requireReason={false} value={null} onSelect={vi.fn()} onAddNew={vi.fn()} />);
    await typeAndOpen();
    expect(screen.getByText('↑↓ duyệt · Enter chọn · F4 thêm mới · Esc đóng')).toBeInTheDocument();
  });

  it('partnerType None locks the input even with requireReason=false', () => {
    renderAutocomplete({ partnerType: 'None', requireReason: false });
    const input = screen.getByRole('combobox');
    expect(input).toBeDisabled();
    expect(input).toHaveAttribute('placeholder', 'Không cần đối tượng');
  });

  it('a Customer partner type with requireReason=false stays enabled', () => {
    renderAutocomplete({ partnerType: 'Customer', requireReason: false });
    expect(screen.getByRole('combobox')).toBeEnabled();
  });

  it('keeps the same input element when the parent sets the value', async () => {
    function Controlled() {
      const [value, setValue] = useState<{ id: string; code: string; name: string } | null>(null);
      return (
        <PartnerAutocomplete
          type="In"
          requireReason={false}
          value={value}
          onSelect={(p) => setValue(p ? { id: p.id, code: p.code, name: p.name } : null)}
        />
      );
    }
    render(<Controlled />);
    const before = await typeAndOpen('abc');
    fireEvent.keyDown(before, { key: 'Enter' });
    await waitFor(() => expect(screen.getByRole('combobox')).toHaveValue('NCC01'));
    expect(screen.getByRole('combobox')).toBe(before);
  });

  it('filter mode: no + button, no add row, no catalog row', async () => {
    renderAutocomplete({ requireReason: false });
    await typeAndOpen();
    expect(screen.queryByRole('button', { name: /Thêm nhanh/ })).not.toBeInTheDocument();
    expect(screen.queryByText(/Thêm mới/)).not.toBeInTheDocument();
    expect(screen.queryByText('Xem danh mục đầy đủ')).not.toBeInTheDocument();
  });
});

describe('PartnerAutocomplete footer rows', () => {
  let onAddNew: ReturnType<typeof vi.fn>;
  let onOpenCatalog: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    usePartnerSearchMock.mockReset();
    usePartnerSearchMock.mockReturnValue({ data: partners, isLoading: false, isError: false });
    onAddNew = vi.fn();
    onOpenCatalog = vi.fn();
  });

  function renderFooter(overrides: Partial<Props> = {}) {
    return renderAutocomplete({ requireReason: false, onAddNew, onOpenCatalog, ...overrides });
  }

  function renderFooterRaw() {
    return render(
      <PartnerAutocomplete type="In" requireReason={false} value={null} onSelect={vi.fn()} onAddNew={onAddNew} onOpenCatalog={onOpenCatalog} />,
    );
  }

  function selectedIndex() {
    return screen.getAllByRole('option').findIndex((o) => o.getAttribute('aria-selected') === 'true');
  }

  it('0 results: the add row is row 0, selected, with the Enter / F4 hint; the catalog row follows', async () => {
    usePartnerSearchMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    renderFooter();
    await typeAndOpen('zzz');

    const addRow = screen.getByRole('option', { name: /Thêm mới đối tượng "zzz"/ });
    expect(addRow).toHaveAttribute('aria-selected', 'true');
    expect(within(addRow).getByText('Enter / F4')).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Xem danh mục đầy đủ/ })).toBeInTheDocument();
    expect(screen.getAllByRole('option')).toHaveLength(2);
  });

  it('with results the add row hint is F4 and ArrowDown walks results, add row, catalog row, wraps', async () => {
    renderFooter();
    const input = await typeAndOpen('abc');

    const addRow = screen.getByRole('option', { name: /Thêm mới đối tượng "abc"/ });
    expect(within(addRow).getByText('F4')).toBeInTheDocument();
    expect(screen.getAllByRole('option')).toHaveLength(4);
    expect(selectedIndex()).toBe(0);

    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    expect(selectedIndex()).toBe(2);
    expect(addRow).toHaveAttribute('aria-selected', 'true');
    expect(input).toHaveAttribute('aria-activedescendant', addRow.id);

    fireEvent.keyDown(input, { key: 'ArrowDown' });
    expect(selectedIndex()).toBe(3);
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    expect(selectedIndex()).toBe(0);
    fireEvent.keyDown(input, { key: 'ArrowUp' });
    expect(selectedIndex()).toBe(3);
  });

  it('Tab and Shift+Tab move the same way and prevent the default', async () => {
    renderFooter();
    const input = await typeAndOpen('abc');

    expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(false);
    expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(false);
    expect(selectedIndex()).toBe(2);
    expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(false);
    expect(selectedIndex()).toBe(3);
    expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(false);
    expect(selectedIndex()).toBe(0);
    expect(fireEvent.keyDown(input, { key: 'Tab', shiftKey: true })).toBe(false);
    expect(selectedIndex()).toBe(3);
  });

  it('hides the add row while loading, on error and while pending, but keeps the catalog row', async () => {
    usePartnerSearchMock.mockReturnValue({ data: undefined, isLoading: true, isFetching: true, isError: false });
    const { unmount } = renderFooterRaw();
    await typeAndOpen('abc');
    expect(screen.queryByText(/Thêm mới/)).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Xem danh mục đầy đủ/ })).toBeInTheDocument();
    unmount();

    usePartnerSearchMock.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    const second = renderFooterRaw();
    await typeAndOpen('abc');
    expect(screen.queryByText(/Thêm mới/)).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Xem danh mục đầy đủ/ })).toBeInTheDocument();
    second.unmount();

    usePartnerSearchMock.mockReturnValue({ data: partners, isLoading: false, isError: false });
    renderFooter();
    const input = await typeAndOpen('abc');
    fireEvent.change(input, { target: { value: 'abcd' } });
    expect(screen.queryByText(/Thêm mới/)).not.toBeInTheDocument();
    expect(screen.getByRole('option', { name: /Xem danh mục đầy đủ/ })).toBeInTheDocument();
    expect(await screen.findByText('Thêm mới đối tượng "abcd"')).toBeInTheDocument();
  });

  it('mouse down on the add row calls onAddNew with the keyword and closes the list', async () => {
    renderFooter();
    await typeAndOpen('zzz');
    fireEvent.mouseDown(screen.getByRole('option', { name: /Thêm mới đối tượng "zzz"/ }));
    expect(onAddNew).toHaveBeenCalledWith('zzz');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('mouse down on the catalog row calls onOpenCatalog with the keyword and closes the list', async () => {
    renderFooter();
    await typeAndOpen('zzz');
    fireEvent.mouseDown(screen.getByRole('option', { name: /Xem danh mục đầy đủ/ }));
    expect(onOpenCatalog).toHaveBeenCalledWith('zzz');
    expect(onAddNew).not.toHaveBeenCalled();
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('passes the trimmed keyword on', async () => {
    renderFooter();
    await typeAndOpen('  zzz  ');
    const addRow = screen.getByRole('option', { name: /Thêm mới đối tượng "zzz"/ });
    fireEvent.mouseDown(addRow);
    expect(onAddNew).toHaveBeenCalledWith('zzz');
  });

  it('names customers for noun="khách hàng"', async () => {
    renderFooter({ noun: 'khách hàng' });
    await typeAndOpen('zzz');
    expect(screen.getByText('Thêm mới khách hàng "zzz"')).toBeInTheDocument();
  });

  it('without the props neither footer row renders', async () => {
    renderAutocomplete({ requireReason: false });
    await typeAndOpen('zzz');
    expect(screen.queryByText(/Thêm mới/)).not.toBeInTheDocument();
    expect(screen.queryByText('Xem danh mục đầy đủ')).not.toBeInTheDocument();
  });
});
