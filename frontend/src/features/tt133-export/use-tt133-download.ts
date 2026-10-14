import { useCallback, useState } from 'react';
import { formatApiErrorDetails } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { downloadTt133 } from './api';
import type { ExportFormat, Tt133ExportForm } from './types';

// Downloads a form; a failure becomes a toast, so callers need no try/catch.
// `extraParams` are merged over the form's params (pages that pick targets first pass them here).
export function useTt133Download() {
  const [isPending, setIsPending] = useState(false);

  const download = useCallback(
    async (form: Tt133ExportForm, format: ExportFormat, extraParams?: Record<string, unknown>) => {
      setIsPending(true);
      try {
        await downloadTt133(form.report, format, { ...form.params, ...extraParams }, form.fileBase);
      } catch (error) {
        toast({ variant: 'destructive', title: 'Không thể xuất file', description: formatApiErrorDetails(error) });
      } finally {
        setIsPending(false);
      }
    },
    [],
  );

  return { download, isPending };
}
