import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useAuthStore } from '@/stores/auth-store';
import { ReportScopeSelect } from './report-scope-select';

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

describe('ReportScopeSelect', () => {
  beforeEach(() => setPermissions([]));

  it('renders nothing without branches.access_all', () => {
    const { container } = render(<ReportScopeSelect id="scope" value="Branch" onChange={vi.fn()} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('offers both scopes and reports the choice', async () => {
    setPermissions(['branches.access_all']);
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<ReportScopeSelect id="scope" value="Branch" onChange={onChange} />);

    const select = screen.getByRole('combobox', { name: 'Phạm vi' });
    expect(select).toHaveTextContent('Chi nhánh đang làm việc');

    await user.click(select);
    expect(screen.getByRole('option', { name: 'Chi nhánh đang làm việc' })).toBeInTheDocument();
    await user.click(screen.getByRole('option', { name: 'Toàn công ty' }));

    expect(onChange).toHaveBeenCalledWith('Company');
  });
});
