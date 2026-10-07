import { useState } from 'react';
import { useNumbering, useUpdateNumbering } from '@/features/inventory-settings/hooks';
import type {
  DocumentNumbering,
  DocumentType,
  NumberingResetPolicy,
} from '@/features/inventory-settings/types';
import { formatDocumentNumber } from '@/features/inventory-settings/utils';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { formatApiErrorDetails, getApiError, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';

const DOC_TYPE_LABELS: Record<DocumentType, string> = {
  StockIn: 'Phiếu nhập kho',
  StockOut: 'Phiếu xuất kho',
};

const RESET_POLICIES: { value: NumberingResetPolicy; label: string }[] = [
  { value: 'None', label: 'Không' },
  { value: 'Monthly', label: 'Theo tháng' },
  { value: 'Yearly', label: 'Theo năm' },
];

export function NumberingSettingsPage() {
  const { data, isLoading, isError, error } = useNumbering();

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Đánh số chứng từ</h1>
        <p className="text-sm text-muted-foreground">Mẫu số phiếu nhập, xuất kho của chi nhánh làm việc</p>
      </div>

      {isError && (
        <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          {getErrorMessage(error)}
        </div>
      )}
      {isLoading && <div className="text-sm text-muted-foreground">Đang tải...</div>}

      <div className="grid gap-4 lg:grid-cols-2">
        {(data ?? []).map((n) => (
          <NumberingCard key={n.docType} initial={n} />
        ))}
      </div>
    </div>
  );
}

function NumberingCard({ initial }: { initial: DocumentNumbering }) {
  const update = useUpdateNumbering();
  const [prefix, setPrefix] = useState(initial.prefix);
  const [length, setLength] = useState(String(initial.length));
  const [resetPolicy, setResetPolicy] = useState<NumberingResetPolicy>(initial.resetPolicy);
  const [pattern, setPattern] = useState(initial.pattern);
  const [patternError, setPatternError] = useState<string>();

  const id = (field: string) => `${initial.docType}-${field}`;
  const title = DOC_TYPE_LABELS[initial.docType];
  // Local date getters = the VN date (D14: the browser runs in VN time).
  const preview = formatDocumentNumber(pattern, prefix, Number(length) || 0, 1, new Date());

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setPatternError(undefined);
    try {
      await update.mutateAsync({
        docType: initial.docType,
        data: { prefix: prefix.trim(), length: Number(length), resetPolicy, pattern: pattern.trim() },
      });
      toast({ variant: 'success', title: 'Đã lưu đánh số chứng từ', description: title });
    } catch (err) {
      setPatternError(getApiError(err)?.details?.pattern?.[0]);
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <section aria-label={title}>
      <Card className="h-full">
        <CardHeader>
          <CardTitle>{title}</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor={id('prefix')}>Ký hiệu</Label>
              <Input id={id('prefix')} value={prefix} onChange={(e) => setPrefix(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor={id('length')}>Độ dài số</Label>
              <Input
                id={id('length')}
                type="number"
                min={1}
                max={10}
                value={length}
                onChange={(e) => setLength(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor={id('resetPolicy')}>Đặt lại số</Label>
              <Select value={resetPolicy} onValueChange={(v) => setResetPolicy(v as NumberingResetPolicy)}>
                <SelectTrigger id={id('resetPolicy')} aria-label="Đặt lại số">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {RESET_POLICIES.map((p) => (
                    <SelectItem key={p.value} value={p.value}>
                      {p.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor={id('pattern')}>Mẫu</Label>
              <Input id={id('pattern')} value={pattern} onChange={(e) => setPattern(e.target.value)} />
              {patternError ? (
                <p className="text-sm text-destructive">{patternError}</p>
              ) : (
                <p className="text-xs text-muted-foreground">Ký hiệu dùng được: {'{KH} {STT} {THANG} {NAM}'}</p>
              )}
            </div>
            <div className="flex items-center justify-between gap-2 sm:col-span-2">
              <span className="font-mono text-sm">Ví dụ: {preview}</span>
              <Button type="submit" disabled={update.isPending}>
                {update.isPending ? 'Đang lưu...' : 'Lưu'}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </section>
  );
}
