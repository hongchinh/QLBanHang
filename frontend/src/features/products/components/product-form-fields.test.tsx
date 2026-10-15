import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ProductFormFields } from './product-form-fields';
import type { Product } from '@/features/products/types';

const GROUP_ID = '11111111-1111-1111-1111-111111111111';
const UNIT_ID = '22222222-2222-2222-2222-222222222222';

const groups = [{ id: GROUP_ID, code: 'TON', name: 'Tôn' }];
const units = [{ id: UNIT_ID, code: 'TAM', name: 'Tấm' }];

function setup(props: Partial<React.ComponentProps<typeof ProductFormFields>> = {}) {
  const onSubmit = vi.fn();
  const onCancel = vi.fn();
  render(
    <MemoryRouter>
      <ProductFormFields
        isEdit={false}
        groups={groups}
        units={units}
        submitting={false}
        submitError=""
        hasSubmitError={false}
        showHeader={false}
        onSubmit={onSubmit}
        onCancel={onCancel}
        {...props}
      />
    </MemoryRouter>,
  );
  return { onSubmit, onCancel };
}

const EDITED: Product = {
  id: 'p1',
  code: 'SP01',
  name: 'Tôn mạ',
  productGroupId: GROUP_ID,
  unitId: UNIT_ID,
  unitName: 'Tấm',
  status: 'Active',
  pricingMode: 'PerUnit',
  trackInventory: true,
  purchaseDiscountRate: 0,
  salesDiscountRate: 0,
  priceIncludesVat: false,
  hasInventoryActivity: false,
  createdAt: '2026-10-01T00:00:00Z',
};

describe('ProductFormFields', () => {
  it('submits the parsed values', async () => {
    const user = userEvent.setup();
    const { onSubmit } = setup();

    await user.type(screen.getByLabelText('Tên hàng hóa *'), 'Tôn lạnh');
    await user.click(screen.getByRole('combobox', { name: 'Nhóm hàng hóa *' }));
    await user.click(screen.getByRole('option', { name: 'Tôn' }));
    await user.type(screen.getByLabelText('Đơn vị tính *'), 'Tấm');
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await vi.waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
    expect(onSubmit.mock.calls[0][0]).toMatchObject({
      name: 'Tôn lạnh',
      productGroupId: GROUP_ID,
      unitId: UNIT_ID,
      pricingMode: 'PerUnit',
      trackInventory: true,
    });
  });

  it('Hủy calls onCancel and is a button', async () => {
    const user = userEvent.setup();
    const { onCancel } = setup();
    const cancel = screen.getByRole('button', { name: 'Hủy' });
    expect(cancel.tagName).toBe('BUTTON');
    await user.click(cancel);
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('has no Trạng thái field when creating', () => {
    setup();
    expect(screen.queryByText('Trạng thái')).not.toBeInTheDocument();
  });

  it('prefills code and name, and initial wins', () => {
    setup({ initialCode: 'TL-50', initialName: 'Tôn lạnh' });
    expect(screen.getByLabelText('Mã hàng hóa')).toHaveValue('TL-50');
    expect(screen.getByLabelText('Tên hàng hóa *')).toHaveValue('Tôn lạnh');
  });

  it('initial (edit) wins over initialCode and initialName', () => {
    setup({ isEdit: true, initial: EDITED, initialCode: 'TL-50', initialName: 'Tôn lạnh' });
    expect(screen.getByLabelText('Mã hàng hóa')).toHaveValue('SP01');
    expect(screen.getByLabelText('Tên hàng hóa *')).toHaveValue('Tôn mạ');
  });

  it('shows the submit error above the first card without a header', () => {
    setup({ hasSubmitError: true, submitError: 'Mã đã tồn tại' });
    const error = screen.getByText('Mã đã tồn tại');
    const firstCard = screen.getByText('Thông tin chung');
    expect(error.compareDocumentPosition(firstCard) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('keeps the submit error at the bottom with the default header', () => {
    setup({ showHeader: true, hasSubmitError: true, submitError: 'Mã đã tồn tại' });
    expect(screen.getByRole('heading', { name: 'Thêm hàng hóa' })).toBeInTheDocument();
    const error = screen.getByText('Mã đã tồn tại');
    const lastCard = screen.getByText('Ghi chú', { selector: 'h3, div, p, span' });
    expect(error.compareDocumentPosition(lastCard) & Node.DOCUMENT_POSITION_PRECEDING).toBeTruthy();
  });
});

describe('ProductFormFields Enter navigation', () => {
  const byId = (id: string) => document.getElementById(id) as HTMLElement;

  async function fillValid(user: ReturnType<typeof userEvent.setup>) {
    await user.type(byId('name'), 'Tôn lạnh');
    await user.click(byId('productGroupId'));
    await user.click(screen.getByRole('option', { name: 'Tôn' }));
    await user.type(byId('unitId'), 'Tấm');
  }

  it('(f) Enter walks Mã, Tên, Nhóm (closed), Đơn vị; Shift+Enter goes back', async () => {
    const user = userEvent.setup();
    setup({ enterNavigation: true });
    byId('code').focus();
    await user.keyboard('{Enter}');
    expect(byId('name')).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(byId('productGroupId')).toHaveFocus();
    expect(byId('productGroupId')).toHaveAttribute('aria-expanded', 'false');
    await user.keyboard('{Enter}');
    expect(byId('unitId')).toHaveFocus();
    await user.keyboard('{Shift>}{Enter}{/Shift}');
    expect(byId('productGroupId')).toHaveFocus();
  });

  it('(g) Enter on an open Nhóm Select picks the option and the form leaves focus alone', async () => {
    const user = userEvent.setup();
    setup({ enterNavigation: true });
    byId('productGroupId').focus();
    await user.keyboard('{ArrowDown}');
    expect(byId('productGroupId')).toHaveAttribute('aria-expanded', 'true');
    await user.keyboard('{Enter}');
    await vi.waitFor(() => expect(byId('productGroupId')).toHaveAttribute('aria-expanded', 'false'));
    expect(byId('productGroupId')).toHaveTextContent('Tôn');
    expect(byId('unitId')).not.toHaveFocus();
  });

  it('(h) Ctrl+Enter submits a valid form', async () => {
    const user = userEvent.setup();
    const { onSubmit } = setup({ enterNavigation: true });
    await fillValid(user);
    byId('code').focus();
    await user.keyboard('{Control>}{Enter}{/Control}');
    await vi.waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
  });

  it('(h) Ctrl+Enter with an empty Tên does not submit and shows the error', async () => {
    const user = userEvent.setup();
    const { onSubmit } = setup({ enterNavigation: true });
    byId('code').focus();
    await user.keyboard('{Control>}{Enter}{/Control}');
    await vi.waitFor(() => expect(screen.getAllByText(/bắt buộc|Vui lòng|không được/i).length).toBeGreaterThan(0));
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('(i) Enter on Ghi chú submits; Shift+Enter goes back to % CK bán', async () => {
    const user = userEvent.setup();
    const { onSubmit } = setup({ enterNavigation: true });
    await fillValid(user);
    byId('note').focus();
    await user.keyboard('{Shift>}{Enter}{/Shift}');
    expect(byId('salesDiscountRate')).toHaveFocus();
    expect(onSubmit).not.toHaveBeenCalled();
    byId('note').focus();
    await user.keyboard('{Enter}');
    await vi.waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1));
  });

  it('(j) without enterNavigation Enter in Tên does not move to Nhóm', async () => {
    const user = userEvent.setup();
    setup({ initialName: 'X' });
    byId('name').focus();
    await user.keyboard('{Enter}');
    expect(byId('productGroupId')).not.toHaveFocus();
  });

  it('(k) Enter in Đơn vị with the list open moves on and keeps the typed unit', async () => {
    const user = userEvent.setup();
    setup({ enterNavigation: true });
    await user.type(byId('unitId'), 'Tấm');
    expect(screen.getByRole('button', { name: 'Tấm' })).toBeInTheDocument();
    await user.keyboard('{Enter}');
    expect(byId('specification')).toHaveFocus();
    expect(byId('unitId')).toHaveValue('Tấm');
  });
});
