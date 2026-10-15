import { useEffect, useRef, useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { ProductFormFields } from '@/features/products/components/product-form-fields';
import { useProductGroups, useUnits } from '@/features/products/hooks';
import { useSaveProduct } from '@/features/products/use-save-product';
import { splitTypedProductText } from '@/features/products/split-typed-text';
import { toProductSuggestion } from '@/features/products/to-product-suggestion';
import type { ProductFormParsed } from '@/features/products/schema';
import type { ProductSuggestion } from '@/features/products/types';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { focusById } from '@/lib/enter-focus';
import { toast } from '@/lib/use-toast';

export interface ProductQuickAddDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Text typed in the product cell; split by splitTypedProductText. */
  seed: string;
  onCreated: (created: ProductSuggestion) => void;
  onCloseAutoFocus?: (event: Event) => void;
}

// Does not wrap itself in StopFormEvents: the caller wraps the whole <Dialog> so the wrapper sits outside
// DialogContent (Radix's Tab loop runs and the close button is covered).
export function ProductQuickAddDialog({
  open,
  onOpenChange,
  seed,
  onCreated,
  onCloseAutoFocus,
}: ProductQuickAddDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-w-4xl max-h-[90vh] overflow-y-auto"
        // The form renders after the lookups load, so QuickAddBody focuses the first field itself.
        onOpenAutoFocus={(e) => e.preventDefault()}
        aria-describedby={undefined}
        onCloseAutoFocus={onCloseAutoFocus}
      >
        <DialogHeader>
          <DialogTitle>Thêm nhanh hàng hóa</DialogTitle>
        </DialogHeader>
        <QuickAddBody onOpenChange={onOpenChange} seed={seed} onCreated={onCreated} />
      </DialogContent>
    </Dialog>
  );
}

// Mounted only while the dialog is open: the lookup and save hooks live here, so a closed dialog needs no query client.
function QuickAddBody({ onOpenChange, seed, onCreated }: Pick<ProductQuickAddDialogProps, 'onOpenChange' | 'seed' | 'onCreated'>) {
  const groups = useProductGroups();
  const units = useUnits();
  const save = useSaveProduct();
  // Local: a unit get-or-create failure happens before the mutation and never reaches save.error.
  const [error, setError] = useState('');
  // useSaveProduct hands a repeat call the same promise; this keeps the toast and onCreated to one.
  const submitting = useRef(false);

  const { code, name } = splitTypedProductText(seed);
  const loading = groups.isLoading || units.isLoading;
  const failed = groups.isError || units.isError;
  const ready = !loading && !failed;

  useEffect(() => {
    if (ready) focusById(code ? 'name' : 'code', 'end');
  }, [ready, code]);

  async function handleSubmit(parsed: ProductFormParsed) {
    if (submitting.current) return;
    submitting.current = true;
    save.reset();
    setError('');
    try {
      const created = await save.create(parsed);
      toast({ variant: 'success', title: 'Đã tạo hàng hóa', description: created.code });
      onCreated(toProductSuggestion(created));
      onOpenChange(false);
    } catch (err) {
      setError(getErrorMessage(err));
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    } finally {
      submitting.current = false;
    }
  }

  if (loading) return <p className="text-sm text-muted-foreground">Đang tải...</p>;
  if (failed) {
    return (
      <div className="space-y-3">
        <p className="text-sm text-destructive">Không tải được danh mục</p>
        <Button
          type="button"
          variant="outline"
          onClick={() => {
            void groups.refetch();
            void units.refetch();
          }}
        >
          Thử lại
        </Button>
      </div>
    );
  }

  return (
    <ProductFormFields
      isEdit={false}
      showHeader={false}
      enterNavigation
      initialCode={code}
      initialName={name}
      submitLabel="Lưu và chọn"
      groups={groups.data ?? []}
      units={units.data ?? []}
      submitting={save.isPending}
      submitError={error}
      hasSubmitError={error !== ''}
      onCancel={() => onOpenChange(false)}
      onSubmit={handleSubmit}
    />
  );
}
