import { useState } from 'react';
import { useBranches, useSetPeriodLock } from '@/features/branches/hooks';
import type { Branch } from '@/features/branches/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';

export function PeriodLockPage() {
  const { data, isLoading, isError, error } = useBranches();

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Khóa sổ</h1>
        <p className="text-sm text-muted-foreground">
          Chứng từ có ngày ≤ ngày khóa sổ không được thêm, sửa, xóa, hủy.
        </p>
      </div>

      <Card>
        <CardContent className="p-4">
          {isError && (
            <div className="mb-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {getErrorMessage(error)}
            </div>
          )}

          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Mã</TableHead>
                <TableHead>Chi nhánh</TableHead>
                <TableHead>Khóa sổ đến</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={4} className="h-24 text-center text-muted-foreground">
                    Đang tải...
                  </TableCell>
                </TableRow>
              ) : (
                (data ?? []).map((b) => <PeriodLockRow key={`${b.id}:${b.lockedUntil ?? ''}`} branch={b} />)
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}

function PeriodLockRow({ branch }: { branch: Branch }) {
  const setLock = useSetPeriodLock();
  const [date, setDate] = useState(branch.lockedUntil ?? '');

  const submit = async (lockedUntil: string | null) => {
    try {
      await setLock.mutateAsync({ id: branch.id, lockedUntil });
      if (lockedUntil === null) setDate('');
      toast({
        variant: 'success',
        title: lockedUntil ? 'Đã khóa sổ' : 'Đã bỏ khóa sổ',
        description: `${branch.code} — ${branch.name}`,
      });
    } catch (err) {
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <TableRow>
      <TableCell>{branch.code}</TableCell>
      <TableCell>{branch.name}</TableCell>
      <TableCell>
        <Input
          type="date"
          aria-label="Khóa sổ đến"
          className="max-w-[12rem]"
          value={date}
          onChange={(e) => setDate(e.target.value)}
        />
      </TableCell>
      <TableCell>
        <div className="flex justify-end gap-2">
          <Button size="sm" disabled={!date || setLock.isPending} onClick={() => submit(date)}>
            Lưu
          </Button>
          <Button
            size="sm"
            variant="outline"
            disabled={!branch.lockedUntil || setLock.isPending}
            onClick={() => submit(null)}
          >
            Bỏ khóa
          </Button>
        </div>
      </TableCell>
    </TableRow>
  );
}
