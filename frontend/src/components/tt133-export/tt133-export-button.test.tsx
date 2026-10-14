import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { Toaster } from '@/components/ui/toaster';
import type { Tt133ExportForm } from '@/features/tt133-export/types';

const downloadTt133 = vi.fn();
vi.mock('@/features/tt133-export/api', () => ({ downloadTt133: (...a: unknown[]) => downloadTt133(...a) }));

const profile: { data: { name: string | null } | undefined } = { data: { name: 'Công ty A' } };
vi.mock('@/features/company-profile/hooks', () => ({ useCompanyProfile: () => profile }));

import { Tt133ExportButton } from './tt133-export-button';

const S04A: Tt133ExportForm = {
  code: 'S04a-DNN',
  report: 'cash-book',
  params: { kind: 'Cash', from: '2026-10-01', to: '2026-10-31' },
  fileBase: 'S04a-DNN_20261001_20261031',
};
const S05: Tt133ExportForm = {
  code: 'S05-DNN',
  report: 'cash-book',
  params: { kind: 'Bank', from: '2026-10-01', to: '2026-10-31' },
  fileBase: 'S05-DNN_20261001_20261031',
};

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderButton(props: React.ComponentProps<typeof Tt133ExportButton>) {
  return render(
    <MemoryRouter>
      <Tt133ExportButton {...props} />
      <Toaster />
    </MemoryRouter>,
  );
}

const TRIGGER = 'Xuất theo mẫu TT133';

describe('Tt133ExportButton', () => {
  beforeEach(() => {
    downloadTt133.mockReset();
    downloadTt133.mockResolvedValue(undefined);
    profile.data = { name: 'Công ty A' };
    setPermissions([]);
  });

  it('one form lists Excel and PDF, and PDF downloads the form', async () => {
    const user = userEvent.setup();
    renderButton({ forms: [S04A] });

    await user.click(screen.getByRole('button', { name: TRIGGER }));
    expect(screen.getByRole('menuitem', { name: 'Excel' })).toBeInTheDocument();
    await user.click(screen.getByRole('menuitem', { name: 'PDF' }));

    expect(downloadTt133).toHaveBeenCalledWith('cash-book', 'pdf', S04A.params, S04A.fileBase);
  });

  it('two forms list the code with each format', async () => {
    const user = userEvent.setup();
    renderButton({ forms: [S04A, S05] });

    await user.click(screen.getByRole('button', { name: TRIGGER }));

    expect(screen.getAllByRole('menuitem').map((i) => i.textContent)).toEqual([
      'S04a-DNN · Excel',
      'S04a-DNN · PDF',
      'S05-DNN · Excel',
      'S05-DNN · PDF',
    ]);
  });

  it('onSelect replaces the download', async () => {
    const onSelect = vi.fn();
    const user = userEvent.setup();
    renderButton({ forms: [S04A, S05], onSelect });

    await user.click(screen.getByRole('button', { name: TRIGGER }));
    await user.click(screen.getByRole('menuitem', { name: 'S05-DNN · Excel' }));

    expect(onSelect).toHaveBeenCalledWith(S05, 'xlsx');
    expect(downloadTt133).not.toHaveBeenCalled();
  });

  it('is disabled with no forms or when disabled', () => {
    const { unmount } = renderButton({ forms: [] });
    expect(screen.getByRole('button', { name: TRIGGER })).toBeDisabled();
    unmount();

    renderButton({ forms: [S04A], disabled: true });
    expect(screen.getByRole('button', { name: TRIGGER })).toBeDisabled();
  });

  it('shows the spinner button while exporting and the Excel icon when idle', async () => {
    downloadTt133.mockReturnValue(new Promise(() => {}));
    const user = userEvent.setup();
    renderButton({ forms: [S04A] });
    const trigger = screen.getByRole('button', { name: TRIGGER });
    expect(trigger.querySelector('[data-icon="exportExcel"]')).not.toBeNull();
    await user.click(trigger);
    await user.click(screen.getByRole('menuitem', { name: 'Excel' }));
    const busy = screen.getByRole('button', { name: TRIGGER });
    expect(busy.querySelector('.button-loader')).not.toBeNull();
    expect(busy.querySelector('svg.animate-spin')).toBeNull();
  });

  it('shows a destructive toast with the server message when the download fails', async () => {
    downloadTt133.mockRejectedValue(new Error('Xuất PDF thất bại, hãy dùng Excel.'));
    const user = userEvent.setup();
    renderButton({ forms: [S04A] });

    await user.click(screen.getByRole('button', { name: TRIGGER }));
    await user.click(screen.getByRole('menuitem', { name: 'PDF' }));

    expect(await screen.findByText('Xuất PDF thất bại, hãy dùng Excel.')).toBeInTheDocument();
    expect(screen.getByText('Không thể xuất file')).toBeInTheDocument();
  });

  it('hints at an empty company profile, with a link only for settings.company', () => {
    profile.data = { name: '' };
    setPermissions(['settings.company']);
    const { unmount } = renderButton({ forms: [S04A] });
    expect(screen.getByText(/Chưa có thông tin đơn vị/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Cài đặt' })).toHaveAttribute('href', '/settings/company');
    unmount();

    setPermissions([]);
    renderButton({ forms: [S04A] });
    expect(screen.getByText(/Chưa có thông tin đơn vị/)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Cài đặt' })).not.toBeInTheDocument();
  });

  it('shows no hint when the company name is present', () => {
    renderButton({ forms: [S04A] });
    expect(screen.queryByText(/Chưa có thông tin đơn vị/)).not.toBeInTheDocument();
  });
});
