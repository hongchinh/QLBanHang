import { useCallback, useState } from 'react';
import { formatApiErrorDetails } from '@/lib/api-client';
import { saveBlob } from '@/lib/blob-download';
import { openPdfTab, type PdfTab } from '@/lib/pdf-tab';
import { toast } from '@/lib/use-toast';
import { fetchVoucherPrint } from './api';
import { voucherPrintFileBase, type VoucherPrintRequest } from './types';

// Prints vouchers: pdf opens in a tab (opened before any await), xlsx is saved.
// A failure becomes a toast, so callers need no try/catch.
export function useVoucherPrint() {
  const [isPending, setIsPending] = useState(false);

  const print = useCallback(async (request: VoucherPrintRequest) => {
    setIsPending(true);
    let tab: PdfTab | undefined;
    try {
      if (request.format === 'pdf') {
        const title =
          request.vouchers.length === 1 ? request.vouchers[0].code : `In ${request.vouchers.length} phiếu`;
        tab = openPdfTab(title);
      }
      const blob = await fetchVoucherPrint(request);
      if (tab) tab.show(blob);
      else saveBlob(blob, `${voucherPrintFileBase(request)}.${request.format}`);
    } catch (error) {
      tab?.close();
      toast({ variant: 'destructive', title: 'Không thể in phiếu', description: formatApiErrorDetails(error) });
    } finally {
      setIsPending(false);
    }
  }, []);

  return { print, isPending };
}
