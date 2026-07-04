import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TableHead, TableColGroup } from './table';

describe('TableHead resize handle', () => {
  it('does not render a resize handle by default', () => {
    render(
      <table>
        <thead>
          <tr>
            <TableHead>Col A</TableHead>
          </tr>
        </thead>
      </table>,
    );
    expect(screen.queryByTestId('column-resize-handle')).not.toBeInTheDocument();
  });

  it('renders a resize handle when resizable props are provided', () => {
    const onResizeStart = vi.fn();
    render(
      <table>
        <thead>
          <tr>
            <TableHead
              resizable={{
                onResizeStart,
                isResizing: false,
              }}
            >
              Col A
            </TableHead>
          </tr>
        </thead>
      </table>,
    );
    expect(screen.getByTestId('column-resize-handle')).toBeInTheDocument();
  });

  it('calls onResizeStart on mousedown of the handle', () => {
    const onResizeStart = vi.fn();
    render(
      <table>
        <thead>
          <tr>
            <TableHead resizable={{ onResizeStart, isResizing: false }}>Col A</TableHead>
          </tr>
        </thead>
      </table>,
    );
    screen.getByTestId('column-resize-handle').dispatchEvent(
      new MouseEvent('mousedown', { bubbles: true }),
    );
    expect(onResizeStart).toHaveBeenCalledTimes(1);
  });

  it('applies an active-resize class when isResizing is true', () => {
    render(
      <table>
        <thead>
          <tr>
            <TableHead resizable={{ onResizeStart: vi.fn(), isResizing: true }}>Col A</TableHead>
          </tr>
        </thead>
      </table>,
    );
    expect(screen.getByTestId('column-resize-handle').className).toContain('bg-primary');
  });
});

describe('TableColGroup', () => {
  it('renders one col element per width entry', () => {
    const { container } = render(
      <table>
        <TableColGroup widths={[100, 200, 50]} />
      </table>,
    );
    const cols = container.querySelectorAll('col');
    expect(cols).toHaveLength(3);
    expect((cols[0] as HTMLElement).style.width).toBe('100px');
    expect((cols[1] as HTMLElement).style.width).toBe('200px');
    expect((cols[2] as HTMLElement).style.width).toBe('50px');
  });
});
