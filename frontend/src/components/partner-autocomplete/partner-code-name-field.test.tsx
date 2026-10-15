import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PartnerCodeNameField, type PartnerCodeNameFieldProps } from './partner-code-name-field';
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

function renderField(overrides: Partial<PartnerCodeNameFieldProps> = {}) {
  const props: PartnerCodeNameFieldProps = {
    label: 'Đối tượng',
    scope: 'partner',
    codeInputId: 'partner-code',
    nameInputId: 'partner-name',
    nameInputProps: {},
    value: null,
    onSelect: vi.fn(),
    ...overrides,
  };
  const view = render(<PartnerCodeNameField {...props} />);
  return { props, ...view };
}

describe('PartnerCodeNameField', () => {
  beforeEach(() => {
    usePartnerSearchMock.mockReset();
    usePartnerSearchMock.mockReturnValue({ data: partners, isFetching: false, isLoading: false, isError: false });
  });

  it('shows the label and focuses the code input when it is clicked', async () => {
    renderField();
    const label = screen.getByText('Đối tượng');
    expect(label).not.toHaveClass('required');
    await userEvent.click(label);
    expect(screen.getByRole('combobox')).toHaveFocus();
  });

  it('marks the label as required', () => {
    renderField({ required: true });
    expect(screen.getByText('Đối tượng')).toHaveClass('required');
  });

  it('renders the code combobox and the name input with their placeholders', () => {
    renderField();
    expect(screen.getByRole('combobox')).toHaveAttribute('id', 'partner-code');
    expect(screen.getByRole('combobox')).toHaveAttribute('placeholder', 'Nhập mã / tên / MST đối tượng...');
    const name = document.getElementById('partner-name');
    expect(name).toHaveAttribute('placeholder', 'Tên đối tượng');
  });

  it('uses customer placeholders for the customer scope and lets namePlaceholder win', () => {
    const { unmount } = renderField({ scope: 'customer', label: 'Khách hàng' });
    expect(screen.getByRole('combobox')).toHaveAttribute('placeholder', 'Nhập mã / tên / MST / địa chỉ / SĐT...');
    expect(document.getElementById('partner-name')).toHaveAttribute('placeholder', 'Tên khách hàng');
    unmount();

    renderField({ namePlaceholder: 'Tên người nhận' });
    expect(document.getElementById('partner-name')).toHaveAttribute('placeholder', 'Tên người nhận');
  });

  it('applies nameInputProps to the name input', () => {
    const onChange = vi.fn();
    renderField({ nameInputProps: { value: 'Anh Nam', onChange } });
    const name = document.getElementById('partner-name') as HTMLInputElement;
    expect(name.value).toBe('Anh Nam');
    fireEvent.change(name, { target: { value: 'Anh Nam B' } });
    expect(onChange).toHaveBeenCalledTimes(1);
  });

  it('renders the error as an alert that the code input points to', () => {
    const { rerender, props } = renderField();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.getByRole('combobox')).not.toHaveAttribute('aria-describedby');

    rerender(<PartnerCodeNameField {...props} error="Vui lòng chọn đối tượng từ danh sách" />);
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Vui lòng chọn đối tượng từ danh sách');
    expect(alert).toHaveAttribute('id', 'partner-code-error');
    expect(screen.getByRole('combobox')).toHaveAttribute('aria-describedby', 'partner-code-error');
  });

  it('selects with Enter and moves focus to the name input', async () => {
    const onSelect = vi.fn();
    renderField({ onSelect });
    const code = screen.getByRole('combobox');
    fireEvent.change(code, { target: { value: 'KH01' } });
    await waitFor(() => expect(screen.getByRole('listbox')).toBeInTheDocument());
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenCalledWith('In', 'KH01', undefined, true));
    fireEvent.keyDown(code, { key: 'Enter' });
    expect(onSelect).toHaveBeenCalledWith(partners[1]);
    await waitFor(() => expect(document.getElementById('partner-name')).toHaveFocus());
  });

  it('partner type None disables only the code input', () => {
    renderField({ partnerType: 'None' });
    expect(screen.getByRole('combobox')).toBeDisabled();
    expect(document.getElementById('partner-name')).toBeEnabled();
  });

  it('disabled disables the code input', () => {
    renderField({ disabled: true });
    expect(screen.getByRole('combobox')).toBeDisabled();
  });

  it('searches active partners without a reason', async () => {
    renderField();
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'cong' } });
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('In', 'cong', undefined, true));
  });

  it('searches the given stock direction, or uses its own search hook', async () => {
    const { unmount } = renderField({ type: 'Out' });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'cong' } });
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('Out', 'cong', undefined, true));
    unmount();

    usePartnerSearchMock.mockClear();
    const useSearch = vi.fn().mockReturnValue({ data: partners, isFetching: false, isLoading: false, isError: false });
    renderField({ useSearch });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'cong' } });
    await waitFor(() => expect(useSearch).toHaveBeenCalledWith('cong', undefined, true, undefined));
    expect(usePartnerSearchMock).not.toHaveBeenCalled();
  });

  it('forwards onUnpickedChange', () => {
    const onUnpickedChange = vi.fn();
    renderField({ onUnpickedChange });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'abc' } });
    expect(onUnpickedChange).toHaveBeenLastCalledWith(true);
  });
});
