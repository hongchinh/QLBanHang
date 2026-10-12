import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { allNavGroups } from './sidebar-test-utils';
import { SidebarPinChips } from './sidebar-pin-chips';

const ITEMS = allNavGroups().flatMap((g) => g.items).slice(0, 7);

function renderChips(count: number, activePath: string | null = null, onUnpin = vi.fn()) {
  render(
    <MemoryRouter>
      <SidebarPinChips items={ITEMS.slice(0, count)} activePath={activePath} onUnpin={onUnpin} />
    </MemoryRouter>,
  );
  return { onUnpin, list: () => screen.getByRole('list', { name: 'Mục đã ghim' }) };
}

describe('SidebarPinChips', () => {
  it('shows 6 chips and +N for the rest, in pin order', () => {
    const { list } = renderChips(7);
    const links = within(list()).getAllByRole('link');
    expect(links.map((a) => a.textContent)).toEqual(ITEMS.slice(0, 6).map((i) => i.label));
    expect(within(list()).getByRole('button', { name: '+1' })).toHaveAttribute('aria-expanded', 'false');
  });

  it('+N expands in place and Thu gọn collapses', async () => {
    const user = userEvent.setup();
    const { list } = renderChips(7);
    await user.click(screen.getByRole('button', { name: '+1' }));
    expect(within(list()).getAllByRole('link')).toHaveLength(7);
    await user.click(screen.getByRole('button', { name: 'Thu gọn' }));
    expect(within(list()).getAllByRole('link')).toHaveLength(6);
  });

  it('shows no +N chip with 6 pins or fewer', () => {
    renderChips(6);
    expect(screen.queryByRole('button', { name: /^\+/ })).not.toBeInTheDocument();
  });

  it('× unpins an item', async () => {
    const user = userEvent.setup();
    const { onUnpin } = renderChips(2);
    await user.click(screen.getByRole('button', { name: `Bỏ ghim ${ITEMS[1].label}` }));
    expect(onUnpin).toHaveBeenCalledWith(ITEMS[1].to);
  });

  it('marks the chip of the current page and keeps the full name in a tooltip', () => {
    renderChips(3, ITEMS[2].to);
    const chip = screen.getByRole('link', { name: ITEMS[2].label });
    expect(chip).toHaveAttribute('aria-current', 'page');
    expect(chip).toHaveAttribute('title', ITEMS[2].label);
  });
});
