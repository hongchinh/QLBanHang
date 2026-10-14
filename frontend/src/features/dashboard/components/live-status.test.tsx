import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LiveStatus } from './live-status';

const AT = new Date(2026, 9, 13, 14, 32, 5).getTime();

describe('LiveStatus', () => {
  it('connected: "Trực tiếp" and the time of the data to the second', () => {
    render(<LiveStatus status="connected" updatedAt={AT} />);

    const status = screen.getByRole('status');
    expect(status.textContent).toBe('Trực tiếp · cập nhật 14:32:05');
    expect(status.className).toContain('emerald');
  });

  it('connecting: "Đang kết nối…" without a time', () => {
    render(<LiveStatus status="connecting" updatedAt={AT} />);

    expect(screen.getByRole('status').textContent).toBe('Đang kết nối…');
  });

  it('disconnected: "Mất kết nối" and the time of the data to the minute', () => {
    render(<LiveStatus status="disconnected" updatedAt={AT} />);

    const status = screen.getByRole('status');
    expect(status.textContent).toBe('Mất kết nối · số liệu lúc 14:32');
    expect(status.className).toContain('amber');
  });

  it.each([
    ['connected', 'Trực tiếp'],
    ['connecting', 'Đang kết nối…'],
    ['disconnected', 'Mất kết nối'],
  ] as const)('%s without data leaves the time out', (status, text) => {
    render(<LiveStatus status={status} updatedAt={null} />);

    expect(screen.getByRole('status').textContent).toBe(text);
  });

  it('prints a time before 10 o\'clock with leading zeros', () => {
    render(<LiveStatus status="connected" updatedAt={new Date(2026, 9, 13, 7, 5, 9).getTime()} />);

    expect(screen.getByRole('status').textContent).toBe('Trực tiếp · cập nhật 07:05:09');
  });

  it('announces a change of the connection, not every new time', () => {
    render(<LiveStatus status="connected" updatedAt={AT} />);

    const time = screen.getByText(/cập nhật 14:32:05/);
    expect(time).toHaveAttribute('aria-live', 'off');
    expect(time).not.toHaveTextContent('Trực tiếp');
  });
});
