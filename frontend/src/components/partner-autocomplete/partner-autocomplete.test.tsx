import { useState } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { PartnerAutocomplete } from './partner-autocomplete';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';

const usePartnerSearchMock = vi.fn();

vi.mock('@/features/stock-vouchers/hooks', () => ({
  usePartnerSearch: (...args: unknown[]) => usePartnerSearchMock(...args),
}));

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

  it('is disabled for partner type None', () => {
    renderAutocomplete({ partnerType: 'None' });
    const input = screen.getByRole('combobox');
    expect(input).toBeDisabled();
    expect(input).toHaveAttribute('placeholder', 'Không cần đối tượng');
  });

  it('lists results and selects with Enter', async () => {
    const props = renderAutocomplete();
    const input = screen.getByRole('combobox');
    fireEvent.change(input, { target: { value: 'cong' } });
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(2));
    expect(usePartnerSearchMock).toHaveBeenLastCalledWith('In', 'cong', undefined);

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
    expect(useSearch.mock.lastCall?.slice(0, 2)).toEqual(['abc', undefined]);
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
    renderAutocomplete({ activeOnly: true });
    await typeAndOpen();
    expect(await screen.findByText('Tìm thấy 2 đối tượng (KH + NCC) đang dùng')).toBeInTheDocument();
  });

  it('header has no "đang dùng" without activeOnly', async () => {
    renderAutocomplete();
    await typeAndOpen();
    expect(await screen.findByText('Tìm thấy 2 đối tượng')).toBeInTheDocument();
  });

  it('header names customers for noun="khách hàng"', async () => {
    renderAutocomplete({ activeOnly: true, noun: 'khách hàng' });
    await typeAndOpen();
    expect(await screen.findByText('Tìm thấy 2 khách hàng đang dùng')).toBeInTheDocument();
  });

  it('header says so when 20 results come back', async () => {
    usePartnerSearchMock.mockReturnValue({ data: manyPartners(20), isLoading: false, isError: false });
    renderAutocomplete({ activeOnly: true });
    await typeAndOpen();
    expect(await screen.findByText('Hiển thị 20 kết quả đầu — gõ thêm để thu hẹp')).toBeInTheDocument();
  });

  it('has a SĐT column with the phone number', async () => {
    usePartnerSearchMock.mockReturnValue({
      data: [{ ...partners[0], phoneNumber: '0901234567' }],
      isLoading: false,
      isError: false,
    });
    renderAutocomplete();
    await typeAndOpen();
    expect(screen.getByRole('columnheader', { name: 'SĐT' })).toBeInTheDocument();
    expect(screen.getByText('0901234567')).toBeInTheDocument();
  });

  it('empty result text names the noun and the typed text', async () => {
    usePartnerSearchMock.mockReturnValue({ data: [], isLoading: false, isError: false });
    const { unmount } = render(
      <PartnerAutocomplete type="In" value={null} onSelect={vi.fn()} />,
    );
    await typeAndOpen('zzz');
    expect(await screen.findByText('Không tìm thấy đối tượng "zzz"')).toBeInTheDocument();
    unmount();

    render(<PartnerAutocomplete type="In" noun="khách hàng" value={null} onSelect={vi.fn()} />);
    await typeAndOpen('zzz');
    expect(await screen.findByText('Không tìm thấy khách hàng "zzz"')).toBeInTheDocument();
  });

  it('shows the error text', async () => {
    usePartnerSearchMock.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    renderAutocomplete();
    await typeAndOpen();
    expect(await screen.findByText('Không thể tải danh sách. Vui lòng thử lại.')).toBeInTheDocument();
  });

  it('shows the loading text', async () => {
    usePartnerSearchMock.mockReturnValue({ data: undefined, isLoading: true, isFetching: true, isError: false });
    renderAutocomplete();
    await typeAndOpen();
    expect(await screen.findByText('Đang tìm kiếm...')).toBeInTheDocument();
  });

  it('passes (type, keyword, activeOnly) to the default stock search', async () => {
    renderAutocomplete({ activeOnly: true });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'cong' } });
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('In', 'cong', true));
  });

  it('passes (keyword, activeOnly) to a given search hook', async () => {
    const useSearch = vi.fn((keyword: string) => ({ data: keyword ? [partners[1]] : [], isFetching: false }));
    renderAutocomplete({ type: undefined, partnerType: 'Customer', useSearch, activeOnly: true });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'abc' } });
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(1));
    expect(useSearch.mock.lastCall?.slice(0, 2)).toEqual(['abc', true]);
  });

  it('key hints: F4 only with onAddNew', async () => {
    const { unmount } = render(
      <PartnerAutocomplete type="In" value={null} onSelect={vi.fn()} />,
    );
    await typeAndOpen();
    expect(screen.getByText('↑↓ duyệt · Enter chọn · Esc đóng')).toBeInTheDocument();
    unmount();

    render(<PartnerAutocomplete type="In" value={null} onSelect={vi.fn()} onAddNew={vi.fn()} />);
    await typeAndOpen();
    expect(screen.getByText('↑↓ duyệt · Enter chọn · F4 thêm mới · Esc đóng')).toBeInTheDocument();
  });

  it('partnerType None locks the input', () => {
    renderAutocomplete({ partnerType: 'None' });
    const input = screen.getByRole('combobox');
    expect(input).toBeDisabled();
    expect(input).toHaveAttribute('placeholder', 'Không cần đối tượng');
  });

  it('a Customer partner type stays enabled', () => {
    renderAutocomplete({ partnerType: 'Customer' });
    expect(screen.getByRole('combobox')).toBeEnabled();
  });

  it('keeps the same input element when the parent sets the value', async () => {
    function Controlled() {
      const [value, setValue] = useState<{ id: string; code: string; name: string } | null>(null);
      return (
        <PartnerAutocomplete
          type="In"
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
    renderAutocomplete();
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
    return renderAutocomplete({ onAddNew, onOpenCatalog, ...overrides });
  }

  function renderFooterRaw() {
    return render(
      <PartnerAutocomplete type="In" value={null} onSelect={vi.fn()} onAddNew={onAddNew} onOpenCatalog={onOpenCatalog} />,
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
    renderAutocomplete();
    await typeAndOpen('zzz');
    expect(screen.queryByText(/Thêm mới/)).not.toBeInTheDocument();
    expect(screen.queryByText('Xem danh mục đầy đủ')).not.toBeInTheDocument();
  });
});

describe('PartnerAutocomplete keyboard', () => {
  type HookResult = { data?: PartnerSearchItem[]; isLoading?: boolean; isFetching?: boolean; isError?: boolean };
  let hookResult: HookResult;
  const picked = { id: partners[0].id, code: 'NCC01', name: 'Công ty Thép Việt' };

  function renderField(overrides: Partial<Props> = {}) {
    const props = {
      type: 'In',
      value: null,
      onSelect: vi.fn(),
      onUnpickedChange: vi.fn(),
      inputId: 'partner',
      ...overrides,
    } as Props;
    const view = render(<PartnerAutocomplete {...props} />);
    return {
      props,
      input: screen.getByRole('combobox') as HTMLInputElement,
      rerenderWith: (next: Partial<Props>) => view.rerender(<PartnerAutocomplete {...props} {...next} />),
    };
  }

  function lastUnpicked(props: Props) {
    return vi.mocked(props.onUnpickedChange!).mock.lastCall?.[0];
  }

  async function waitDebounced(keyword: string) {
    await waitFor(() => expect(usePartnerSearchMock.mock.lastCall?.[1]).toBe(keyword));
  }

  const settle = () => act(async () => { await new Promise((r) => setTimeout(r, 400)); });

  beforeEach(() => {
    hookResult = { data: partners, isLoading: false, isError: false };
    usePartnerSearchMock.mockReset();
    usePartnerSearchMock.mockImplementation(() => hookResult);
  });

  it('Enter selects the highlighted result and prevents the default', async () => {
    const { props, input } = renderField();
    await typeAndOpen('abc');
    expect(fireEvent.keyDown(input, { key: 'Enter' })).toBe(false);
    expect(props.onSelect).toHaveBeenCalledWith(partners[0]);
  });

  it('an exact code wins over the highlighted row', async () => {
    const { props, input } = renderField();
    await typeAndOpen('kh01');
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(props.onSelect).toHaveBeenCalledWith(partners[1]);
  });

  it('Enter before the debounce waits, then selects the exact match when results arrive', async () => {
    const { props, input } = renderField();
    fireEvent.change(input, { target: { value: 'KH01' } });
    expect(fireEvent.keyDown(input, { key: 'Enter' })).toBe(false);
    expect(props.onSelect).not.toHaveBeenCalled();
    await waitFor(() => expect(props.onSelect).toHaveBeenCalledWith(partners[1]));
  });

  it('stale results (still fetching) are not used by Enter', async () => {
    hookResult = { data: [partners[0]], isLoading: false, isFetching: true, isError: false };
    const { props, input, rerenderWith } = renderField();
    fireEvent.change(input, { target: { value: 'KH01' } });
    await waitDebounced('KH01');
    expect(fireEvent.keyDown(input, { key: 'Enter' })).toBe(false);
    expect(props.onSelect).not.toHaveBeenCalled();

    hookResult = { data: [partners[1]], isLoading: false, isFetching: false, isError: false };
    rerenderWith({});
    await waitFor(() => expect(props.onSelect).toHaveBeenCalledTimes(1));
    expect(props.onSelect).toHaveBeenCalledWith(partners[1]);
  });

  it('a waiting Enter with results but no exact match selects the first result', async () => {
    const { props, input } = renderField();
    fireEvent.change(input, { target: { value: 'cong' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    await waitFor(() => expect(props.onSelect).toHaveBeenCalledWith(partners[0]));
  });

  it('a waiting Enter with 0 results shows the add row selected and never opens the dialog', async () => {
    hookResult = { data: [], isLoading: false, isError: false };
    const onAddNew = vi.fn();
    const { input } = renderField({ onAddNew });
    fireEvent.change(input, { target: { value: 'zzz' } });
    fireEvent.keyDown(input, { key: 'Enter' });

    const addRow = await screen.findByRole('option', { name: /Thêm mới đối tượng "zzz"/ });
    expect(addRow).toHaveAttribute('aria-selected', 'true');
    await settle();
    expect(onAddNew).not.toHaveBeenCalled();

    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onAddNew).toHaveBeenCalledWith('zzz');
  });

  it('typing after Enter cancels the waiting', async () => {
    hookResult = { data: [partners[0]], isLoading: false, isFetching: true, isError: false };
    const { props, input, rerenderWith } = renderField();
    fireEvent.change(input, { target: { value: 'KH01' } });
    await waitDebounced('KH01');
    fireEvent.keyDown(input, { key: 'Enter' });
    fireEvent.change(input, { target: { value: 'KH0' } });

    hookResult = { data: [partners[1]], isLoading: false, isFetching: false, isError: false };
    rerenderWith({});
    await waitDebounced('KH0');
    await settle();
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('Enter with the list closed and text typed prevents the default and reopens the list', async () => {
    const { input } = renderField();
    await typeAndOpen('abc');
    fireEvent.keyDown(input, { key: 'Escape' });
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();

    expect(fireEvent.keyDown(input, { key: 'Enter' })).toBe(false);
    expect(await screen.findByRole('listbox')).toBeInTheDocument();
  });

  it('Enter on an empty input or with a picked value is left to the form', () => {
    const empty = renderField();
    expect(fireEvent.keyDown(empty.input, { key: 'Enter' })).toBe(true);
    cleanup();

    const withValue = renderField({ value: picked });
    expect(fireEvent.keyDown(withValue.input, { key: 'Enter' })).toBe(true);
  });

  it('Shift+Enter, Ctrl+Enter and composing Enter are not handled', async () => {
    const { props, input } = renderField();
    await typeAndOpen('abc');
    expect(fireEvent.keyDown(input, { key: 'Enter', shiftKey: true })).toBe(true);
    expect(fireEvent.keyDown(input, { key: 'Enter', ctrlKey: true })).toBe(true);
    expect(fireEvent.keyDown(input, { key: 'Enter', isComposing: true })).toBe(true);
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('F4 opens the quick add with the typed text, prevents the default and closes the list', async () => {
    const onAddNew = vi.fn();
    const { input } = renderField({ onAddNew });
    await typeAndOpen('abc');
    expect(fireEvent.keyDown(input, { key: 'F4' })).toBe(false);
    expect(onAddNew).toHaveBeenCalledWith('abc');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('F4 with a picked value opens the quick add with an empty name', () => {
    const onAddNew = vi.fn();
    const { input } = renderField({ onAddNew, value: picked });
    fireEvent.keyDown(input, { key: 'F4' });
    expect(onAddNew).toHaveBeenCalledWith('');
  });

  it('F4 does nothing without onAddNew or on a disabled field', () => {
    const plain = renderField();
    expect(fireEvent.keyDown(plain.input, { key: 'F4' })).toBe(true);
    cleanup();

    const onAddNew = vi.fn();
    const locked = renderField({ onAddNew, disabled: true });
    expect(fireEvent.keyDown(locked.input, { key: 'F4' })).toBe(true);
    expect(onAddNew).not.toHaveBeenCalled();
  });

  it('F4 cancels a waiting Enter', async () => {
    const onAddNew = vi.fn();
    const { props, input } = renderField({ onAddNew });
    fireEvent.change(input, { target: { value: 'KH01' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    fireEvent.keyDown(input, { key: 'F4' });
    await settle();
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('the + button exists only with onAddNew', () => {
    renderField();
    expect(screen.queryByRole('button', { name: /Thêm nhanh/ })).not.toBeInTheDocument();
  });

  it('the + button is labelled, reachable by Tab, and opens the quick add with the typed text', async () => {
    const onAddNew = vi.fn();
    renderField({ onAddNew });
    await typeAndOpen('abc');

    const button = screen.getByRole('button', { name: 'Thêm nhanh đối tượng (F4)' });
    expect(button).toHaveAttribute('aria-keyshortcuts', 'F4');
    expect(button).not.toHaveAttribute('tabindex', '-1');
    fireEvent.click(button);
    expect(onAddNew).toHaveBeenCalledWith('abc');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('the + button names customers and is disabled with the field', () => {
    renderField({ onAddNew: vi.fn(), noun: 'khách hàng', disabled: true });
    expect(screen.getByRole('button', { name: 'Thêm nhanh khách hàng (F4)' })).toBeDisabled();
  });

  it('a value set from outside closes the list; clearing it later leaves an empty input and nothing unpicked', async () => {
    const { props, input, rerenderWith } = renderField();
    await typeAndOpen('abc');
    expect(lastUnpicked(props)).toBe(true);

    rerenderWith({ value: picked });
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(input).toHaveValue('NCC01');

    rerenderWith({ value: null });
    expect(input).toHaveValue('');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
    expect(lastUnpicked(props)).toBe(false);
  });

  it('a value set from outside cancels a waiting Enter', async () => {
    const { props, input, rerenderWith } = renderField();
    fireEvent.change(input, { target: { value: 'KH01' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    rerenderWith({ value: picked });
    await settle();
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it.each([
    ['partnerType None', { partnerType: 'None' as const }],
    ['disabled', { disabled: true }],
  ])('a blocked field (%s) drops its typed text', async (_name, blocked) => {
    const { props, input, rerenderWith } = renderField();
    await typeAndOpen('zzz');
    expect(lastUnpicked(props)).toBe(true);

    rerenderWith(blocked);
    expect(input).toHaveValue('');
    expect(lastUnpicked(props)).toBe(false);
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it.each([
    ['Enter', (input: HTMLElement) => fireEvent.keyDown(input, { key: 'Enter' })],
    ['mouse', () => fireEvent.mouseDown(screen.getAllByRole('option')[0])],
  ])('focuses the name input after a pick by %s', async (_name, pick) => {
    render(
      <>
        <PartnerAutocomplete type="In" value={null} onSelect={vi.fn()} nameInputId="name" />
        <input id="name" />
      </>,
    );
    const input = await typeAndOpen('abc');
    pick(input);
    await waitFor(() => expect(document.activeElement).toBe(document.getElementById('name')));
  });

  it('leaves the focus alone after a pick without nameInputId', async () => {
    render(
      <>
        <PartnerAutocomplete type="In" value={null} onSelect={vi.fn()} />
        <input id="name" />
      </>,
    );
    const input = await typeAndOpen('abc');
    input.focus();
    fireEvent.keyDown(input, { key: 'Enter' });
    await settle();
    expect(document.activeElement).toBe(input);
  });

  it('reports unpicked text: true after typing, false after a pick or after clearing the text', async () => {
    const { props, input } = renderField();
    await typeAndOpen('abc');
    expect(lastUnpicked(props)).toBe(true);

    fireEvent.keyDown(input, { key: 'Enter' });
    expect(lastUnpicked(props)).toBe(false);

    fireEvent.change(input, { target: { value: 'x' } });
    expect(lastUnpicked(props)).toBe(true);
    fireEvent.change(input, { target: { value: '' } });
    expect(lastUnpicked(props)).toBe(false);
  });

  it('reports false for a field that starts with a value', () => {
    const { props } = renderField({ value: picked });
    expect(lastUnpicked(props)).toBe(false);
  });
});

describe('PartnerAutocomplete hidden list and waiting Enter edge cases', () => {
  type HookResult = { data?: PartnerSearchItem[]; isLoading?: boolean; isFetching?: boolean; isError?: boolean };
  let hookResult: HookResult;

  const settle = () => act(async () => { await new Promise((r) => setTimeout(r, 400)); });

  function renderField(overrides: Partial<Props> = {}) {
    const props = {
      type: 'In',
      value: null,
      onSelect: vi.fn(),
      inputId: 'partner',
      ...overrides,
    } as Props;
    const view = render(<PartnerAutocomplete {...props} />);
    return {
      props,
      input: screen.getByRole('combobox') as HTMLInputElement,
      rerenderWith: (next: Partial<Props>) => view.rerender(<PartnerAutocomplete {...props} {...next} />),
    };
  }

  beforeEach(() => {
    hookResult = { data: partners, isLoading: false, isError: false };
    usePartnerSearchMock.mockReset();
    usePartnerSearchMock.mockImplementation(() => hookResult);
  });

  it('Tab follows the DOM while the list is open but not shown yet', () => {
    const { input } = renderField({ onOpenCatalog: vi.fn() });
    fireEvent.change(input, { target: { value: 'K' } });
    expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(true);
    expect(fireEvent.keyDown(input, { key: 'Tab', shiftKey: true })).toBe(true);
  });

  it('Tab follows the DOM when only spaces are typed', async () => {
    const { input } = renderField({ onOpenCatalog: vi.fn() });
    fireEvent.change(input, { target: { value: '   ' } });
    expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(true);
    await settle();
    expect(fireEvent.keyDown(input, { key: 'Tab' })).toBe(true);
  });

  it('Escape before the debounce cancels a waiting Enter', async () => {
    const { props, input } = renderField();
    fireEvent.change(input, { target: { value: 'KH01' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    fireEvent.keyDown(input, { key: 'Escape' });
    await settle();
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('a waiting Enter does not pick when a value landed in the same commit as the results', async () => {
    hookResult = { data: [partners[0]], isLoading: false, isFetching: true, isError: false };
    const { props, input, rerenderWith } = renderField();
    fireEvent.change(input, { target: { value: 'KH01' } });
    await waitFor(() => expect(usePartnerSearchMock.mock.lastCall?.[1]).toBe('KH01'));
    fireEvent.keyDown(input, { key: 'Enter' });

    hookResult = { data: [partners[1]], isLoading: false, isFetching: false, isError: false };
    rerenderWith({ value: { id: partners[0].id, code: 'NCC01', name: 'x' } });
    await settle();
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('a waiting Enter is cancelled when focus leaves the component', async () => {
    const onSelect = vi.fn();
    render(
      <>
        <PartnerAutocomplete type="In" value={null} onSelect={onSelect} nameInputId="name" />
        <input id="name" />
        <input id="elsewhere" />
      </>,
    );
    const input = screen.getByRole('combobox');
    const elsewhere = document.getElementById('elsewhere')!;
    input.focus();
    fireEvent.change(input, { target: { value: 'KH01' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    elsewhere.focus();
    await settle();
    expect(onSelect).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(elsewhere);
  });

  it('keeps a waiting Enter when focus moves inside the component', async () => {
    const onAddNew = vi.fn();
    const { props, input } = renderField({ onAddNew });
    fireEvent.change(input, { target: { value: 'KH01' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    fireEvent.blur(input, { relatedTarget: screen.getByRole('button', { name: /Thêm nhanh/ }) });
    await waitFor(() => expect(props.onSelect).toHaveBeenCalledWith(partners[1]));
  });

  it('no empty row before the typed keyword was searched', async () => {
    hookResult = { data: [], isLoading: false, isError: false };
    const { input } = renderField();
    await typeAndOpen('abc');
    fireEvent.change(input, { target: { value: 'abcd' } });
    expect(screen.queryByText(/Không tìm thấy/)).not.toBeInTheDocument();
    expect(await screen.findByText('Không tìm thấy đối tượng "abcd"')).toBeInTheDocument();
  });

  it('Enter does not select data the list is not showing (error with old data)', async () => {
    hookResult = { data: partners, isLoading: false, isError: true };
    const { props, input } = renderField();
    await typeAndOpen('KH01');
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(props.onSelect).not.toHaveBeenCalled();
  });

  it('option ids are namespaced with the instance', async () => {
    renderField();
    const input = await typeAndOpen('abc');
    const option = screen.getAllByRole('option')[0];
    expect(option.id).toMatch(/^partner-listbox-.+-11111111-1111-1111-1111-111111111111$/);
    expect(input).toHaveAttribute('aria-activedescendant', option.id);
  });

  it('a waiting Enter never activates a footer row', async () => {
    hookResult = { data: undefined, isLoading: false, isError: true };
    const onOpenCatalog = vi.fn();
    const { input } = renderField({ onOpenCatalog });
    fireEvent.change(input, { target: { value: 'abc' } });
    expect(fireEvent.keyDown(input, { key: 'Enter' })).toBe(false);
    await screen.findByRole('listbox');
    await settle();
    expect(onOpenCatalog).not.toHaveBeenCalled();
  });

  it('a plain Enter in the error state with the list shown opens the catalog', async () => {
    hookResult = { data: undefined, isLoading: false, isError: true };
    const onOpenCatalog = vi.fn();
    const { input } = renderField({ onOpenCatalog });
    await typeAndOpen('abc');
    fireEvent.keyDown(input, { key: 'Enter' });
    expect(onOpenCatalog).toHaveBeenCalledWith('abc');
  });

  it('the post-pick focus timer is cleared on unmount', async () => {
    const outside = document.createElement('input');
    outside.id = 'name';
    document.body.appendChild(outside);
    const view = render(
      <PartnerAutocomplete type="In" value={null} onSelect={vi.fn()} nameInputId="name" />,
    );
    const input = await typeAndOpen('abc');
    fireEvent.keyDown(input, { key: 'Enter' });
    view.unmount();
    await settle();
    expect(document.activeElement).not.toBe(outside);
    outside.remove();
  });
});
