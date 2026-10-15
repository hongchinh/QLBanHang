import { useNavigate, useParams } from 'react-router-dom';
import { useProduct, useProductGroups, useUnits } from '@/features/products/hooks';
import { useSaveProduct } from '@/features/products/use-save-product';
import { ProductFormFields } from '@/features/products/components/product-form-fields';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';

export function ProductFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const navigate = useNavigate();

  const { data: product, isLoading } = useProduct(isEdit ? id : undefined);
  const groups = useProductGroups();
  const units = useUnits();
  const save = useSaveProduct();

  // Wait for both the (optional) edited entity and the lookups before mounting
  // the form — react-hook-form's defaultValues are captured once on first render.
  const lookupsReady = !groups.isLoading && !units.isLoading;
  if ((isEdit && isLoading) || !lookupsReady) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  return (
    <ProductFormFields
      isEdit={isEdit}
      initial={product}
      groups={groups.data ?? []}
      units={units.data ?? []}
      submitting={save.isPending}
      submitError={getErrorMessage(save.error)}
      hasSubmitError={save.isError}
      onCancel={() => navigate('/products')}
      onSubmit={async (parsed) => {
        try {
          if (isEdit && id) {
            await save.update(id, parsed);
            toast({ variant: 'success', title: 'Đã cập nhật hàng hóa' });
          } else {
            await save.create(parsed);
            toast({ variant: 'success', title: 'Đã tạo hàng hóa' });
          }
          navigate('/products');
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
        }
      }}
    />
  );
}
