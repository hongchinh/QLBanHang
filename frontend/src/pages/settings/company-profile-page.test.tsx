import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CompanyProfilePage } from './company-profile-page';
import type { CompanyProfile } from '@/features/company-profile/types';
import { ApiCallError } from '@/lib/api-client';

const updateMock = vi.fn();
const PROFILE: CompanyProfile = {
  name: 'Công ty TNHH ABC',
  address: '12 Lê Lợi, Hà Nội',
  taxCode: '0101234567',
  chiefAccountantName: 'Trần B',
  legalRepresentativeName: 'Nguyễn A',
  accountingServiceNote: 'Dịch vụ kế toán XYZ',
  updatedAt: '2026-10-01T00:00:00Z',
};

vi.mock('@/features/company-profile/hooks', () => ({
  useCompanyProfile: () => ({ data: PROFILE, isLoading: false, isError: false }),
  useUpdateCompanyProfile: () => ({ mutateAsync: updateMock, isPending: false }),
}));

const toastMock = vi.fn();
vi.mock('@/lib/use-toast', () => ({ toast: (...args: unknown[]) => toastMock(...args) }));

function renderPage() {
  return render(
    <MemoryRouter>
      <CompanyProfilePage />
    </MemoryRouter>,
  );
}

describe('CompanyProfilePage', () => {
  beforeEach(() => {
    updateMock.mockReset().mockResolvedValue(PROFILE);
    toastMock.mockReset();
  });

  it('renders the six fields with the loaded values', () => {
    renderPage();

    expect(screen.getByLabelText('Tên đơn vị')).toHaveValue('Công ty TNHH ABC');
    expect(screen.getByLabelText('Địa chỉ')).toHaveValue('12 Lê Lợi, Hà Nội');
    expect(screen.getByLabelText('Mã số thuế')).toHaveValue('0101234567');
    expect(screen.getByLabelText('Kế toán trưởng')).toHaveValue('Trần B');
    expect(screen.getByLabelText('Người đại diện theo pháp luật')).toHaveValue('Nguyễn A');
    expect(screen.getByLabelText('Ghi chú dịch vụ kế toán')).toHaveValue('Dịch vụ kế toán XYZ');
  });

  it('saves the edited values and shows the toast', async () => {
    const user = userEvent.setup();
    renderPage();

    const name = screen.getByLabelText('Tên đơn vị');
    await user.clear(name);
    await user.type(name, 'Công ty CP Mới');
    await user.clear(screen.getByLabelText('Kế toán trưởng'));
    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({
        name: 'Công ty CP Mới',
        address: '12 Lê Lợi, Hà Nội',
        taxCode: '0101234567',
        chiefAccountantName: null,
        legalRepresentativeName: 'Nguyễn A',
        accountingServiceNote: 'Dịch vụ kế toán XYZ',
      }),
    );
    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({ variant: 'success', title: 'Đã lưu thông tin đơn vị' }),
    );
  });

  it('requires the company name', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.clear(screen.getByLabelText('Tên đơn vị'));
    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    expect(await screen.findByText('Vui lòng nhập tên đơn vị')).toBeInTheDocument();
    expect(updateMock).not.toHaveBeenCalled();
  });

  it('shows a server tax code error under the field', async () => {
    const user = userEvent.setup();
    updateMock.mockRejectedValue(
      new ApiCallError({
        code: 'VALIDATION',
        message: 'Dữ liệu không hợp lệ.',
        details: { taxCode: ['Mã số thuế gồm 10 số, hoặc 10 số, dấu gạch và 3 số.'] },
      }),
    );
    renderPage();

    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    expect(
      await screen.findByText('Mã số thuế gồm 10 số, hoặc 10 số, dấu gạch và 3 số.', { selector: 'p' }),
    ).toBeInTheDocument();
    expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ variant: 'destructive' }));
  });
});
