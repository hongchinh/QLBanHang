import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
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
  const props: Props = {
    type: 'In',
    reasonId: REASON,
    partnerType: 'Supplier',
    value: null,
    onSelect: vi.fn(),
    inputId: 'partner',
    ...overrides,
  };
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
    await waitFor(() => expect(usePartnerSearchMock).toHaveBeenLastCalledWith('Out', 'cong', undefined));
  });

  it('lists results and selects with Enter', async () => {
    const props = renderAutocomplete();
    const input = screen.getByRole('combobox');
    fireEvent.change(input, { target: { value: 'cong' } });
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(2));
    expect(usePartnerSearchMock).toHaveBeenLastCalledWith('In', 'cong', REASON);

    fireEvent.keyDown(input, { key: 'ArrowDown' });
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
