import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebtLedger } from '@/features/cash-reports/hooks';
import type { DebtPartnerSearchItem } from '@/features/debt/types';
import { DEBT_SIDE_LABELS } from '@/features/cash-common/labels';
import type { DebtSide } from '@/features/cash-common/types';
import { PeriodPresetSelect } from '@/components/period-preset-select/period-preset-select';
import { ReportScopeSelect } from '@/components/report-scope-select/report-scope-select';
import { useReportScope } from '@/components/report-scope-select/use-report-scope';
import { ExportTargetsDialog, type ExportTargetChoice } from '@/components/tt133-export/export-targets-dialog';
import { Tt133ExportButton } from '@/components/tt133-export/tt133-export-button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { tt133FileBase, type ExportFormat, type Tt133ExportForm } from '@/features/tt133-export/types';
import { useTt133Download } from '@/features/tt133-export/use-tt133-download';
import { getErrorMessage } from '@/lib/api-client';
import { firstDayOfMonthYmd, todayYmd } from '@/lib/vn-datetime';
import { parseDebtSide } from './document-link';
import { DebtLedgerTable } from './debt-ledger-table';
import { ReportPartnerPicker } from './report-partner-picker';

const SIDES: DebtSide[] = ['Receivable', 'Payable'];

// Sổ chi tiết công nợ of one partner and side. Route permission: reports.debt. Filters live in the URL,
// so the balance report can open it for a partner.
export function DebtLedgerPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const side = parseDebtSide(searchParams.get('side'));
  const partnerId = searchParams.get('partnerId') ?? '';
  const from = searchParams.get('from') || firstDayOfMonthYmd();
  const to = searchParams.get('to') || todayYmd();
  const [scope, setScope] = useReportScope();
  const [picked, setPicked] = useState<{ id: string; label: string } | null>(null);
  const [exportRequest, setExportRequest] = useState<{ form: Tt133ExportForm; format: ExportFormat } | null>(null);
  const { download } = useTt133Download();

  const rangeError = from > to ? 'Từ ngày không được sau Đến ngày' : null;

  const { data: ledger, isLoading, isError, error } = useDebtLedger(
    { side, partnerId, from, to, scope },
    { enabled: !rangeError },
  );

  const updateParams = useCallback(
    (patch: Record<string, string | null>) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          for (const [key, value] of Object.entries(patch)) {
            if (value) next.set(key, value);
            else next.delete(key);
          }
          return next;
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  const selectedLabel =
    picked?.id === partnerId
      ? picked.label
      : ledger && ledger.partnerId === partnerId
        ? `${ledger.partnerCode} - ${ledger.partnerName}`
        : '';

  // S12 exports one, several or all partners, so the button asks for the targets first.
  const exportForm: Tt133ExportForm = {
    code: 'S12-DNN',
    report: 'debt-ledger',
    params: { side, from, to, scope },
    fileBase: tt133FileBase('S12-DNN', from, to),
  };

  const confirmExport = (choice: ExportTargetChoice) => {
    if (!exportRequest) return;
    void download(exportRequest.form, exportRequest.format, 'all' in choice ? { all: true } : { partnerIds: choice.ids });
    setExportRequest(null);
  };

  const selectPartner =(p: DebtPartnerSearchItem) => {
    setPicked({ id: p.id, label: `${p.code} - ${p.name}` });
    updateParams({ partnerId: p.id });
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-semibold">Sổ chi tiết công nợ</h1>
        <Tt133ExportButton
          forms={[exportForm]}
          disabled={!!rangeError}
          onSelect={(form, format) => setExportRequest({ form, format })}
        />
      </div>

      <Card>
        <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
          <div className="space-y-1">
            <Label htmlFor="debt-ledger-side">Phía</Label>
            {/* A partner belongs to one side's list, so switching the side clears it. */}
            <Select value={side} onValueChange={(v) => updateParams({ side: v, partnerId: null })}>
              <SelectTrigger id="debt-ledger-side">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {SIDES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {DEBT_SIDE_LABELS[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-ledger-partner">Đối tượng</Label>
            <ReportPartnerPicker id="debt-ledger-partner" key={side} side={side} selectedLabel={selectedLabel} onSelect={selectPartner} />
          </div>
          <PeriodPresetSelect id="debt-ledger-period" onChange={(range) => updateParams(range)} />
          <div className="space-y-1">
            <Label htmlFor="debt-ledger-from">Từ ngày</Label>
            <Input
              id="debt-ledger-from"
              type="date"
              value={from}
              onChange={(e) => updateParams({ from: e.target.value })}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="debt-ledger-to">Đến ngày</Label>
            <Input id="debt-ledger-to" type="date" value={to} onChange={(e) => updateParams({ to: e.target.value })} />
          </div>
          <ReportScopeSelect id="debt-ledger-scope" value={scope} onChange={setScope} />
        </CardContent>
      </Card>

      {!partnerId ? (
        <p className="text-sm text-muted-foreground">Chọn đối tượng để xem sổ chi tiết công nợ</p>
      ) : rangeError ? (
        <p className="text-sm text-destructive">{rangeError}</p>
      ) : isError ? (
        <p className="text-sm text-destructive">{getErrorMessage(error)}</p>
      ) : ledger ? (
        <Card>
          <CardContent className="overflow-x-auto pt-6">
            <DebtLedgerTable ledger={ledger} />
          </CardContent>
        </Card>
      ) : (
        isLoading && <p className="text-sm text-muted-foreground">Đang tải…</p>
      )}

      <ExportTargetsDialog
        open={exportRequest !== null}
        onOpenChange={(open) => !open && setExportRequest(null)}
        title="Chọn đối tượng xuất sổ S12"
        current={partnerId && selectedLabel ? { id: partnerId, label: selectedLabel } : null}
        renderPicker={(add) => (
          <ReportPartnerPicker
            id="debt-ledger-export-partner"
            side={side}
            selectedLabel=""
            onSelect={(p) => add({ id: p.id, label: `${p.code} - ${p.name}` })}
          />
        )}
        onConfirm={confirmExport}
      />
    </div>
  );
}
