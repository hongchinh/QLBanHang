import { useNavigate, useParams } from 'react-router-dom';
import { useCreateSupplier, useSupplier, useUpdateSupplier } from '@/features/suppliers/hooks';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { CustomerFormFields } from '@/pages/customers/customer-form-fields';

export function SupplierFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const navigate = useNavigate();

  const { data: supplier, isLoading } = useSupplier(isEdit ? id : undefined);
  const create = useCreateSupplier();
  const update = useUpdateSupplier();

  if (isEdit && isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <CustomerFormFields
      // `role` is the partner role prop of CustomerFormFields, not an ARIA role.
      // eslint-disable-next-line jsx-a11y/aria-role
      role="supplier"
      isEdit={isEdit}
      initial={supplier}
      submitting={create.isPending || update.isPending}
      submitError={getErrorMessage(create.error ?? update.error)}
      hasSubmitError={create.isError || update.isError}
      onCancel={() => navigate('/suppliers')}
      onSubmit={async (parsed) => {
        try {
          if (isEdit && id) {
            await update.mutateAsync({ id, data: parsed });
            toast({ variant: 'success', title: 'Đã cập nhật nhà cung cấp' });
          } else {
            await create.mutateAsync(parsed);
            toast({ variant: 'success', title: 'Đã tạo nhà cung cấp' });
          }
          navigate('/suppliers');
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
        }
      }}
    />
  );
}
