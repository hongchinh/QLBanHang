import { useEffect, useState } from 'react';
import type { Branch, DemoBranchDeletionResult } from '@/features/branches/types';
import { useDeleteDemoBranch, useDemoBranchDeletionPreview } from '@/features/branches/hooks';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';

interface DemoBranchDeleteDialogProps {
  branch: Branch | null;
  onClose: () => void;
  onDeleted: (id: string) => void;
}

export function DemoBranchDeleteDialog({ branch, onClose, onDeleted }: DemoBranchDeleteDialogProps) {
  const [code, setCode] = useState('');
  const preview = useDemoBranchDeletionPreview(branch?.id ?? null);
  const deleteBranch = useDeleteDemoBranch();

  // A fresh dialog (another branch, or reopened) starts with an empty confirmation.
  useEffect(() => {
    setCode('');
  }, [branch?.id]);

  const data = preview.data;
  // A cached preview is not trusted until the refetch lands, and a failed refetch blocks it.
  const canConfirm =
    !!branch && !!data && !preview.isFetching && !preview.isError && code.trim() === branch.code;

  async function handleDelete() {
    if (!branch || !canConfirm || deleteBranch.isPending) return;
    let result: DemoBranchDeletionResult;
    try {
      result = await deleteBranch.mutateAsync(branch.id);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Không thể xóa', description: formatApiErrorDetails(err) });
      return;
    }
    const kept = result.keptSampleProductCount + result.keptSamplePartnerCount;
    toast({
      variant: 'success',
      title: 'Đã xóa chi nhánh demo',
      ...(kept > 0 && {
        description: `Giữ lại ${result.keptSampleProductCount} hàng hóa và ${result.keptSamplePartnerCount} đối tượng mẫu vì đang được dùng ở nơi khác.`,
      }),
    });
    onClose();
    onDeleted(branch.id);
  }

  return (
    <Dialog open={!!branch} onOpenChange={(open) => !open && !deleteBranch.isPending && onClose()}>
      <DialogContent showClose={false} className="max-w-md">
        <DialogHeader>
          <DialogTitle>Xóa chi nhánh demo?</DialogTitle>
          <DialogDescription asChild>
            <div className="space-y-2" aria-live="polite">
              {preview.isLoading && <p>Đang kiểm tra dữ liệu...</p>}
              {preview.isError && (
                <div className="rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
                  {getErrorMessage(preview.error)}
                </div>
              )}
              {data && branch && (
                <>
                  <p>
                    Toàn bộ dữ liệu của chi nhánh{' '}
                    <strong>
                      {branch.code} — {branch.name}
                    </strong>{' '}
                    sẽ bị xóa vĩnh viễn: {data.documentCount} chứng từ, tồn kho và công nợ đầu kỳ, kho, quỹ. Chứng từ
                    do người dùng tự nhập vào chi nhánh này cũng bị xóa.
                  </p>
                  {data.reassignedUserCount > 0 && (
                    <p>
                      {data.reassignedUserCount} người dùng đang có chi nhánh mặc định là chi nhánh này sẽ được chuyển
                      về Chi nhánh chính.
                    </p>
                  )}
                </>
              )}
            </div>
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            void handleDelete();
          }}
        >
          {branch && (
            <div className="space-y-2">
              <Label htmlFor="demo-delete-code">Nhập mã chi nhánh ({branch.code}) để xác nhận</Label>
              <Input
                id="demo-delete-code"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                readOnly={deleteBranch.isPending}
                autoComplete="off"
                autoCapitalize="none"
                spellCheck={false}
              />
            </div>
          )}
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={deleteBranch.isPending}>
              Hủy
            </Button>
            <Button type="submit" variant="destructive" disabled={!canConfirm || deleteBranch.isPending}>
              {deleteBranch.isPending ? 'Đang xóa...' : 'Xóa vĩnh viễn'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
