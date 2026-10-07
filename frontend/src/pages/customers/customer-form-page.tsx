import { useNavigate, useParams } from 'react-router-dom';
import { useCreateCustomer, useCustomer, useUpdateCustomer } from '@/features/customers/hooks';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { toast } from '@/lib/use-toast';
import { CustomerFormFields } from './customer-form-fields';
import { CustomerQuotationsSection } from './customer-quotations-section';

export function CustomerFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = !!id && id !== 'new';
  const navigate = useNavigate();

  const { data: customer, isLoading } = useCustomer(isEdit ? id : undefined);
  const create = useCreateCustomer();
  const update = useUpdateCustomer();

  if (isEdit && isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải...</div>;
  }

  const form = (
    <CustomerFormFields
      isEdit={isEdit}
      initial={customer}
      submitting={create.isPending || update.isPending}
      submitError={getErrorMessage(create.error ?? update.error)}
      hasSubmitError={create.isError || update.isError}
      onCancel={() => navigate('/customers')}
      onSubmit={async (parsed) => {
        try {
          if (isEdit && id) {
            await update.mutateAsync({ id, data: parsed });
            toast({ variant: 'success', title: 'Đã cập nhật khách hàng' });
          } else {
            await create.mutateAsync(parsed);
            toast({ variant: 'success', title: 'Đã tạo khách hàng' });
          }
          navigate('/customers');
        } catch (err) {
          toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
        }
      }}
    />
  );

  if (!isEdit || !id) {
    return form;
  }

  return (
    <Tabs defaultValue="general" className="space-y-4">
      <TabsList>
        <TabsTrigger value="general">Thông tin chung</TabsTrigger>
        <TabsTrigger value="quotations">Báo giá</TabsTrigger>
      </TabsList>
      <TabsContent value="general">{form}</TabsContent>
      <TabsContent value="quotations">
        <CustomerQuotationsSection customerId={id} />
      </TabsContent>
    </Tabs>
  );
}
