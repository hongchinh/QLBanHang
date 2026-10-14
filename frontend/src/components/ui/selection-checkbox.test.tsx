import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SelectionCheckbox } from './selection-checkbox';

describe('SelectionCheckbox', () => {
  it('sets the indeterminate property and aria-label', () => {
    render(
      <SelectionCheckbox
        checked={false}
        indeterminate
        onCheckedChange={() => {}}
        aria-label="Chọn tất cả"
      />,
    );
    const box = screen.getByLabelText('Chọn tất cả') as HTMLInputElement;
    expect(box.indeterminate).toBe(true);
  });

  it('reports the new state and does not reach a parent click handler', () => {
    const onCheckedChange = vi.fn();
    const onRowClick = vi.fn();
    render(
      <>
        {/* eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions -- stands in for a clickable table row */}
        <div onClick={onRowClick}>
          <SelectionCheckbox checked={false} onCheckedChange={onCheckedChange} aria-label="Chọn dòng" />
        </div>
      </>,
    );
    fireEvent.click(screen.getByLabelText('Chọn dòng'));
    expect(onCheckedChange).toHaveBeenCalledWith(true);
    expect(onRowClick).not.toHaveBeenCalled();
  });
});
