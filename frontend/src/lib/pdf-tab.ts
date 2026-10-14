export interface PdfTab {
  show(blob: Blob): void;
  close(): void;
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// Mở tab PDF trống ngay lập tức; gọi đồng bộ, TRƯỚC mọi await để không bị popup-blocker chặn.
export function openPdfTab(title: string): PdfTab {
  const safeTitle = escapeHtml(title);
  const newTab = window.open('about:blank', '_blank'); // đồng bộ — trước mọi await

  if (newTab) {
    newTab.document.write(
      `<!DOCTYPE html><html><head><title>${safeTitle}</title></head>` +
        `<body style="margin:0;background:#525659;color:#fff;display:flex;align-items:center;` +
        `justify-content:center;height:100vh;font-family:sans-serif">Đang tải...</body></html>`,
    );
    newTab.document.close();
  }

  return {
    show(blob: Blob) {
      const pdfUrl = URL.createObjectURL(blob);
      if (newTab) {
        newTab.document.open();
        newTab.document.write(
          `<!DOCTYPE html><html><head><title>${safeTitle}</title></head>` +
            `<body style="margin:0;padding:0;overflow:hidden">` +
            `<iframe src="${pdfUrl}" style="display:block;width:100vw;height:100vh;border:none;"></iframe>` +
            `</body></html>`,
        );
        newTab.document.close();
      } else {
        // fallback nếu popup bị chặn
        window.open(pdfUrl, '_blank', 'noopener,noreferrer');
      }
      window.setTimeout(() => URL.revokeObjectURL(pdfUrl), 60_000);
    },
    close() {
      newTab?.close();
    },
  };
}
