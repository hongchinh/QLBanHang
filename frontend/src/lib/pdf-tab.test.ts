import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { openPdfTab } from './pdf-tab';

function makeFakeTab() {
  return {
    document: { open: vi.fn(), write: vi.fn(), close: vi.fn() },
    close: vi.fn(),
    closed: false,
  };
}

describe('openPdfTab', () => {
  let openSpy: ReturnType<typeof vi.spyOn>;
  let revoke: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.useFakeTimers();
    URL.createObjectURL = vi.fn(() => 'blob:pdf');
    revoke = vi.fn();
    URL.revokeObjectURL = revoke;
    openSpy = vi.spyOn(window, 'open');
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('opens a blank tab at once with a loading page', () => {
    const tab = makeFakeTab();
    openSpy.mockReturnValue(tab as unknown as Window);
    openPdfTab('PT00001');
    expect(openSpy).toHaveBeenCalledWith('about:blank', '_blank');
    const html = tab.document.write.mock.calls.map((c) => c[0]).join('');
    expect(html).toContain('<title>PT00001</title>');
    expect(html).toContain('Đang tải');
  });

  it('show puts the PDF in the tab', () => {
    const tab = makeFakeTab();
    openSpy.mockReturnValue(tab as unknown as Window);
    const pdf = openPdfTab('PT00001');
    pdf.show(new Blob(['%PDF']));
    const last = tab.document.write.mock.calls.at(-1)![0] as string;
    expect(last).toContain('src="blob:pdf"');
    vi.advanceTimersByTime(60_000);
    expect(revoke).toHaveBeenCalledWith('blob:pdf');
  });

  it('show opens a new window when the tab was blocked', () => {
    openSpy.mockReturnValueOnce(null);
    const pdf = openPdfTab('PT00001');
    pdf.show(new Blob(['%PDF']));
    expect(openSpy).toHaveBeenLastCalledWith('blob:pdf', '_blank', 'noopener,noreferrer');
  });

  it('close closes the tab', () => {
    const tab = makeFakeTab();
    openSpy.mockReturnValueOnce(tab as unknown as Window);
    openPdfTab('PT00001').close();
    expect(tab.close).toHaveBeenCalled();

    openSpy.mockReturnValueOnce(null);
    expect(() => openPdfTab('PT00001').close()).not.toThrow();
  });

  it('escapes the title', () => {
    const tab = makeFakeTab();
    openSpy.mockReturnValue(tab as unknown as Window);
    openPdfTab('<b>');
    const html = tab.document.write.mock.calls.map((c) => c[0]).join('');
    expect(html).toContain('&lt;b&gt;');
  });
});
