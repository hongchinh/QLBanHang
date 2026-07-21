import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ButtonLoader } from '@/components/ui/button-loader';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { toast } from '@/lib/use-toast';
import { getErrorMessage } from '@/lib/api-client';
import { useBanks } from '@/features/banks/hooks';
import {
  useCreateBankAccount,
  useDeleteBankAccount,
  useMyBankAccounts,
  useSetDefaultBankAccount,
  useUpdateBankAccount,
} from './hooks';
import { bankAccountFormSchema, type BankAccountFormValues } from './schema';
import type { UserBankAccount } from './types';

export function BankAccountsTab() {
  const { data: accounts, isLoading } = useMyBankAccounts();
  const { data: banks } = useBanks();
  const createAccount = useCreateBankAccount();
  const updateAccount = useUpdateBankAccount();
  const deleteAccount = useDeleteBankAccount();
  const setDefaultAccount = useSetDefaultBankAccount();

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<UserBankAccount | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<UserBankAccount | null>(null);

  const form = useForm<BankAccountFormValues>({
    resolver: zodResolver(bankAccountFormSchema),
    defaultValues: { bankId: '', accountNumber: '', accountName: '' },
  });

  const openCreate = () => {
    setEditing(null);
    form.reset({ bankId: '', accountNumber: '', accountName: '' });
    setFormOpen(true);
  };

  const openEdit = (account: UserBankAccount) => {
    setEditing(account);
    form.reset({
      bankId: account.bankId,
      accountNumber: account.accountNumber,
      accountName: account.accountName,
    });
    setFormOpen(true);
  };

  const onSubmit = form.handleSubmit(async (values) => {
    try {
      if (editing) {
        await updateAccount.mutateAsync({ id: editing.id, data: values });
        toast({ title: 'Đã cập nhật tài khoản' });
      } else {
        await createAccount.mutateAsync(values);
        toast({ title: 'Đã thêm tài khoản' });
      }
      setFormOpen(false);
    } catch (err) {
      toast({ title: 'Lưu thất bại', description: getErrorMessage(err), variant: 'destructive' });
    }
  });

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await deleteAccount.mutateAsync(deleteTarget.id);
      toast({ title: 'Đã xoá tài khoản' });
    } catch (err) {
      toast({ title: 'Xoá thất bại', description: getErrorMessage(err), variant: 'destructive' });
    } finally {
      setDeleteTarget(null);
    }
  };

  const handleSetDefault = async (id: string) => {
    try {
      await setDefaultAccount.mutateAsync(id);
      toast({ title: 'Đã đặt làm mặc định' });
    } catch (err) {
      toast({ title: 'Thao tác thất bại', description: getErrorMessage(err), variant: 'destructive' });
    }
  };

  const isSaving = createAccount.isPending || updateAccount.isPending;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Quản lý các tài khoản ngân hàng dùng để nhận chuyển khoản qua mã QR.
        </p>
        <Button onClick={openCreate}>Thêm tài khoản</Button>
      </div>

      {isLoading ? (
        <div>Đang tải…</div>
      ) : accounts && accounts.length > 0 ? (
        <div className="space-y-3">
          {accounts.map((account) => (
            <Card key={account.id}>
              <CardContent className="flex items-center justify-between gap-4 pt-6">
                <div>
                  <p className="font-medium">
                    {account.bankName}
                    {account.isDefault && <Badge className="ml-2">Mặc định</Badge>}
                  </p>
                  <p className="text-sm text-muted-foreground">{account.accountNumber}</p>
                  <p className="text-sm text-muted-foreground">{account.accountName}</p>
                </div>
                <div className="flex shrink-0 gap-2">
                  {!account.isDefault && (
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handleSetDefault(account.id)}
                      disabled={setDefaultAccount.isPending}
                    >
                      Đặt mặc định
                    </Button>
                  )}
                  <Button variant="outline" size="sm" onClick={() => openEdit(account)}>
                    Sửa
                  </Button>
                  <Button variant="destructive" size="sm" onClick={() => setDeleteTarget(account)}>
                    Xoá
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Chưa có tài khoản nhận tiền nào được lưu.</p>
      )}

      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'Sửa tài khoản' : 'Thêm tài khoản'}</DialogTitle>
          </DialogHeader>
          <form className="space-y-4" onSubmit={onSubmit}>
            <div className="space-y-2">
              <Label htmlFor="bankId">Ngân hàng</Label>
              <Controller
                control={form.control}
                name="bankId"
                render={({ field }) => (
                  <Select value={field.value} onValueChange={field.onChange}>
                    <SelectTrigger id="bankId" aria-label="Ngân hàng">
                      <SelectValue placeholder="Chọn ngân hàng" />
                    </SelectTrigger>
                    <SelectContent>
                      {banks?.map((bank) => (
                        <SelectItem key={bank.id} value={bank.id}>{bank.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                )}
              />
              {form.formState.errors.bankId && (
                <p className="text-sm text-destructive">{form.formState.errors.bankId.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="accountNumber">Số tài khoản</Label>
              <Input id="accountNumber" {...form.register('accountNumber')} />
              {form.formState.errors.accountNumber && (
                <p className="text-sm text-destructive">{form.formState.errors.accountNumber.message}</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="accountName">Chủ tài khoản</Label>
              <Input id="accountName" {...form.register('accountName')} />
              {form.formState.errors.accountName && (
                <p className="text-sm text-destructive">{form.formState.errors.accountName.message}</p>
              )}
            </div>

            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={() => setFormOpen(false)}>
                Hủy
              </Button>
              <Button type="submit" disabled={isSaving}>
                {isSaving && <ButtonLoader className="mr-2" />}
                Lưu
              </Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Xoá tài khoản ngân hàng?"
        description={deleteTarget ? `Tài khoản ${deleteTarget.accountNumber} sẽ bị xoá.` : undefined}
        confirmLabel="Xoá"
        destructive
        loading={deleteAccount.isPending}
        onConfirm={handleDelete}
      />
    </div>
  );
}
