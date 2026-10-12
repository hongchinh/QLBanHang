import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { SearchCombobox } from './search-combobox';

interface Item {
  id: string;
  code: string;
}

const ITEMS: Item[] = [
  { id: '1', code: 'A1' },
  { id: '2', code: 'B2' },
  { id: '3', code: 'C3' },
];

const useSearch = vi.fn((_keyword: string) => ({ data: ITEMS as Item[] | undefined, isLoading: false, isError: false }));
const onSelect = vi.fn();
const onClear = vi.fn();

function Harness({ withClear = false }: { withClear?: boolean }) {
  const [label, setLabel] = useState('');
  return (
    <SearchCombobox<Item>
      id="picker"
      ariaLabel="Chọn"
      placeholder="Tìm"
      selectedLabel={label}
      useSearch={useSearch}
      getKey={(i) => i.id}
      renderItem={(i) => <span>{i.code}</span>}
      onSelect={(i) => {
        onSelect(i);
        setLabel(i.code);
      }}
      onClear={withClear ? onClear : undefined}
    />
  );
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.clearAllMocks();
  useSearch.mockImplementation(() => ({ data: ITEMS, isLoading: false, isError: false }));
});

afterEach(() => {
  vi.useRealTimers();
});

describe('SearchCombobox', () => {
  it('opens on focus with the empty-keyword results', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox', { name: 'Chọn' });
    expect(input).toHaveAttribute('aria-expanded', 'false');

    fireEvent.focus(input);

    expect(input).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getAllByRole('option')).toHaveLength(3);
    expect(useSearch).toHaveBeenLastCalledWith('');
  });

  it('moves the highlight with the arrows, wrapping, and exposes it as the active descendant', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox');
    fireEvent.focus(input);
    const options = screen.getAllByRole('option');
    expect(options[0]).toHaveAttribute('aria-selected', 'true');
    expect(input).toHaveAttribute('aria-controls', screen.getByRole('listbox').id);
    expect(input).toHaveAttribute('aria-activedescendant', options[0]!.id);

    fireEvent.keyDown(input, { key: 'ArrowDown' });
    expect(screen.getAllByRole('option')[1]).toHaveAttribute('aria-selected', 'true');
    expect(input).toHaveAttribute('aria-activedescendant', options[1]!.id);

    fireEvent.keyDown(input, { key: 'ArrowUp' });
    fireEvent.keyDown(input, { key: 'ArrowUp' });
    expect(screen.getAllByRole('option')[2]).toHaveAttribute('aria-selected', 'true');
  });

  it('selects the highlighted option on Enter and closes', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox');
    fireEvent.focus(input);
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onSelect).toHaveBeenCalledWith(ITEMS[1]);
    expect(input).toHaveValue('B2');
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
  });

  it('selects an option on mouse down', () => {
    render(<Harness />);
    fireEvent.focus(screen.getByRole('combobox'));
    fireEvent.mouseDown(screen.getAllByRole('option')[2]!);
    expect(onSelect).toHaveBeenCalledWith(ITEMS[2]);
  });

  it('closes on Escape and restores the selected label', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox');
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: 'zz' } });
    expect(input).toHaveValue('zz');

    fireEvent.keyDown(input, { key: 'Escape' });

    expect(input).toHaveValue('');
    expect(input).toHaveAttribute('aria-expanded', 'false');
  });

  it('searches with the keyword debounced by 250 ms', () => {
    render(<Harness />);
    const input = screen.getByRole('combobox');
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: 'ab' } });
    expect(useSearch).not.toHaveBeenCalledWith('ab');

    act(() => {
      vi.advanceTimersByTime(250);
    });

    expect(useSearch).toHaveBeenLastCalledWith('ab');
  });

  it('shows the clear button only for a selected label and calls onClear', () => {
    render(<Harness withClear />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    const input = screen.getByRole('combobox');
    fireEvent.focus(input);
    fireEvent.keyDown(input, { key: 'Enter' });

    fireEvent.click(screen.getByRole('button'));

    expect(onClear).toHaveBeenCalledTimes(1);
  });

  it('shows the loading, error and empty rows', () => {
    useSearch.mockImplementation(() => ({ data: undefined, isLoading: true, isError: false }));
    const { unmount } = render(<Harness />);
    fireEvent.focus(screen.getByRole('combobox'));
    expect(screen.getByText('Đang tìm kiếm...')).toBeInTheDocument();
    unmount();

    useSearch.mockImplementation(() => ({ data: undefined, isLoading: false, isError: true }));
    const second = render(<Harness />);
    fireEvent.focus(screen.getByRole('combobox'));
    expect(screen.getByText('Không thể tải danh sách đối tượng.')).toBeInTheDocument();
    second.unmount();

    useSearch.mockImplementation(() => ({ data: [], isLoading: false, isError: false }));
    render(<Harness />);
    fireEvent.focus(screen.getByRole('combobox'));
    expect(screen.getByText('Không tìm thấy đối tượng phù hợp')).toBeInTheDocument();
  });
});
