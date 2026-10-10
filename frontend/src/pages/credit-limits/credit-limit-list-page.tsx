import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table';
import { Pencil, Plus, Search, Trash2 } from 'lucide-react';
import { useCreditLimits, useDeleteCreditLimit, useUpsertCreditLimit } from '@/features/credit-limits/hooks';
import type { CreditLimit } from '@/features/credit-limits/types';
import { useDebtPartnerSearch } from '@/features/debt/hooks';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Card, CardContent } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { formatMoneyForDisplay, parseMoneyInput } from '@/pages/quotations/utils/money-input';
import { formatApiErrorDetails, getApiError, getErrorMessage } from '@/lib/api-client';
import { applyApiFieldErrors } from '@/lib/apply-api-field-errors';
import { cn } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced-value';
import { useSearchParamNumber, useSearchParamString } from '@/lib/use-search-param-state';
import { toast } from '@/lib/use-toast';

const PAGE_SIZE = 20;

// Route permission: credit_limit.manage (every credit limit endpoint needs it).
export function CreditLimitListPage() {
  const [search, setSearch] = useSearchParamString('q');
  const [page, setPage] = useSearchParamNumber('page', 1);
  const debouncedSearch = useDebouncedValue(search, 300);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<CreditLimit | undefined>();
  const [pendingDelete, setPendingDelete] = useState<CreditLimit | null>(null);

  const { data, isLoading, isError, error } = useCreditLimits({
    search: debouncedSearch || undefined,
    page,
    pageSize: PAGE_SIZE,
  });
  const remove = useDeleteCreditLimit();

  const openCreate = () => {
    setEditTarget(undefined);
    setDialogOpen(true);
  };

  const columns = useMemo<ColumnDef<CreditLimit>[]>(
    () => [
      { header: 'Mã KH', accessorKey: 'customerCode' },
      { header: 'Tên KH', accessorKey: 'customerName' },
      {
        id: 'creditDays',
        header: () => <div className="text-right">Số ngày được nợ</div>,
        cell: ({ row }) => (
          <span className="block text-right tabular-nums">{row.original.creditDays ?? ''}</span>
        ),
      },
      {
        id: 'amount',
        header: () => <div className="text-right">Hạn mức</div>,
        cell: ({ row }) => (
          <span className="block text-right tabular-nums">{formatMoneyForDisplay(row.original.amount)}</span>
        ),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="icon"
              aria-label="Sửa hạn mức"
              onClick={() => {
                setEditTarget(row.original);
                setDialogOpen(true);
              }}
            >
              <Pencil className="h-4 w-4 text-blue-600" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              aria-label="Xóa hạn mức"
              onClick={() => setPendingDelete(row.original)}
            >
              <Trash2 className="h-4 w-4 text-red-600" />
            </Button>
          </div>
        ),
      },
    ],
    [],
  );

  const table = useReactTable({ data: data?.items ?? [], columns, getCoreRowModel: getCoreRowModel() });

  const onConfirmDelete = () => {
    if (!pendingDelete) return;
    const target = pendingDelete;
    remove.mutate(target.customerId, {
      onSuccess: () => {
        toast({ variant: 'success', title: 'Đã xóa hạn mức nợ', description: target.customerName });
        setPendingDelete(null);
      },
      onError: (err) => {
        toast({ variant: 'destructive', title: 'Không thể xóa', description: getErrorMessage(err) });
      },
    });
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-2xl font-bold">Hạn mức nợ</h1>
          <p className="text-sm text-muted-foreground">Hạn mức công nợ của khách hàng tại chi nhánh đang làm việc</p>
        </div>
        <Button onClick={openCreate}>
          <Plus className="mr-2 h-4 w-4 text-cyan-600" /> Thêm hạn mức
        </Button>
      </div>

      <Card>
        <CardContent className="p-4">
          <div className="mb-4 flex items-center gap-2">
            <div className="relative max-w-sm flex-1">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
              <Input
                placeholder="Tìm theo mã / tên khách hàng..."
                aria-label="Tìm hạn mức"
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  if (page !== 1) setPage(1);
                }}
                className="pl-9"
              />
            </div>
          </div>

          {isError && (
            <div className="mb-3 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
              {getErrorMessage(error)}
            </div>
          )}

          <Table>
            <TableHeader>
              {table.getHeaderGroups().map((hg) => (
                <TableRow key={hg.id}>
                  {hg.headers.map((h) => (
                    <TableHead key={h.id}>{flexRender(h.column.columnDef.header, h.getContext())}</TableHead>
                  ))}
                </TableRow>
              ))}
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={columns.length} className="h-24 text-center text-muted-foreground">
                    Đang tải...
                  </TableCell>
                </TableRow>
              ) : table.getRowModel().rows.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={columns.length} className="h-24 text-center text-muted-foreground">
                    Chưa có hạn mức nào.
                  </TableCell>
                </TableRow>
              ) : (
                table.getRowModel().rows.map((row) => (
                  <TableRow key={row.id}>
                    {row.getVisibleCells().map((c) => (
                      <TableCell key={c.id}>{flexRender(c.column.columnDef.cell, c.getContext())}</TableCell>
                    ))}
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          {data && data.totalPages > 1 && (
            <div className="mt-4 flex items-center justify-between text-sm">
              <div className="text-muted-foreground">
                Trang {data.page} / {data.totalPages} • Tổng {data.totalItems}
              </div>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={!data.hasPreviousPage} onClick={() => setPage(page - 1)}>
                  Trước
                </Button>
                <Button variant="outline" size="sm" disabled={!data.hasNextPage} onClick={() => setPage(page + 1)}>
                  Sau
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <CreditLimitFormDialog open={dialogOpen} onOpenChange={setDialogOpen} initial={editTarget} />

      <ConfirmDialog
        open={!!pendingDelete}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title="Xóa hạn mức nợ?"
        description={
          pendingDelete ? (
            <>
              Bạn chắc chắn muốn xóa hạn mức của <strong>{pendingDelete.customerName}</strong>?
            </>
          ) : null
        }
        destructive
        confirmLabel="Xóa"
        loading={remove.isPending}
        onConfirm={onConfirmDelete}
      />
    </div>
  );
}

interface PickedCustomer {
  id: string;
  code: string;
  name: string;
}

const formSchema = z
  .object({
    customer: z.object({ id: z.string(), code: z.string(), name: z.string() }).nullable(),
    amount: z
      .string()
      .refine((s) => s.trim() !== '', 'Nhập hạn mức')
      .refine((s) => s.trim() === '' || (parseMoneyInput(s) ?? -1) >= 0, 'Hạn mức không hợp lệ'),
  })
  .superRefine((values, ctx) => {
    if (!values.customer) ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['customer'], message: 'Chọn khách hàng' });
  });

type FormValues = z.infer<typeof formSchema>;

interface DialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initial?: CreditLimit;
}

// Add and edit share this dialog: the customer is chosen on add and fixed on edit (the limit is keyed by it).
function CreditLimitFormDialog({ open, onOpenChange, initial }: DialogProps) {
  const isEdit = !!initial;
  const upsert = useUpsertCreditLimit();

  const form = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: toDefaults(initial),
  });

  useEffect(() => {
    if (open) form.reset(toDefaults(initial));
  }, [open, initial]); // eslint-disable-line react-hooks/exhaustive-deps

  const errors = form.formState.errors;

  const onSubmit = async (values: FormValues) => {
    if (!values.customer) return;
    try {
      await upsert.mutateAsync({ customerId: values.customer.id, amount: parseMoneyInput(values.amount) ?? 0 });
      toast({ variant: 'success', title: isEdit ? 'Đã cập nhật hạn mức nợ' : 'Đã thêm hạn mức nợ' });
      onOpenChange(false);
    } catch (err) {
      applyApiFieldErrors(err, form.setError, ['amount']);
      // The server reports a partner that is not a customer under customerId.
      const customerError = getApiError(err)?.details?.customerId?.[0];
      if (customerError) form.setError('customer', { type: 'server', message: customerError });
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>{isEdit ? 'Sửa hạn mức nợ' : 'Thêm hạn mức nợ'}</DialogTitle>
        </DialogHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4 pt-2">
          <div className="space-y-2">
            <Label htmlFor="credit-limit-customer">Khách hàng *</Label>
            {isEdit ? (
              <Input
                id="credit-limit-customer"
                value={`${initial.customerCode} - ${initial.customerName}`}
                disabled
                readOnly
              />
            ) : (
              <Controller
                control={form.control}
                name="customer"
                render={({ field }) => (
                  <CustomerPicker
                    inputId="credit-limit-customer"
                    value={field.value}
                    onSelect={field.onChange}
                    invalid={!!errors.customer}
                  />
                )}
              />
            )}
            {errors.customer && <p className="text-sm text-destructive">{errors.customer.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="credit-limit-amount">Hạn mức *</Label>
            <Controller
              control={form.control}
              name="amount"
              render={({ field }) => (
                <Input
                  id="credit-limit-amount"
                  inputMode="decimal"
                  className="text-right tabular-nums"
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={() => {
                    const parsed = parseMoneyInput(field.value);
                    if (parsed !== undefined && parsed >= 0) field.onChange(formatMoneyForDisplay(parsed));
                    field.onBlur();
                  }}
                />
              )}
            />
            {errors.amount && <p className="text-sm text-destructive">{errors.amount.message}</p>}
          </div>

          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={upsert.isPending}>
              Hủy
            </Button>
            <Button type="submit" disabled={upsert.isPending}>
              {upsert.isPending ? 'Đang lưu...' : 'Lưu'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function toDefaults(item?: CreditLimit): FormValues {
  return {
    customer: item ? { id: item.customerId, code: item.customerCode, name: item.customerName } : null,
    amount: item ? formatMoneyForDisplay(item.amount) : '',
  };
}

interface CustomerPickerProps {
  inputId: string;
  value: PickedCustomer | null;
  onSelect: (customer: PickedCustomer | null) => void;
  invalid?: boolean;
}

// Customers of GET /api/debt/partners?side=Receivable. Typing clears the selection and searches again;
// an empty keyword lists the first customers.
function CustomerPicker({ inputId, value, onSelect, invalid }: CustomerPickerProps) {
  const [keyword, setKeyword] = useState(value ? `${value.code} - ${value.name}` : '');
  const [isOpen, setIsOpen] = useState(false);
  const [highlighted, setHighlighted] = useState(0);
  const debouncedKeyword = useDebouncedValue(keyword, 250);
  const { data, isLoading } = useDebtPartnerSearch('Receivable', value ? '' : debouncedKeyword);
  const results = useMemo(() => data ?? [], [data]);

  const containerRef = useRef<HTMLDivElement>(null);
  const listboxId = `credit-limit-customers-${useId()}`;

  useEffect(() => setHighlighted(0), [results]);

  useEffect(() => {
    function onDocMouseDown(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) setIsOpen(false);
    }
    document.addEventListener('mousedown', onDocMouseDown);
    return () => document.removeEventListener('mousedown', onDocMouseDown);
  }, []);

  const showList = isOpen && !value;

  function pick(customer: PickedCustomer) {
    onSelect({ id: customer.id, code: customer.code, name: customer.name });
    setKeyword(`${customer.code} - ${customer.name}`);
    setIsOpen(false);
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (!showList || results.length === 0) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlighted((i) => (i + 1) % results.length);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlighted((i) => (i - 1 + results.length) % results.length);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const choice = results[highlighted];
      if (choice) pick(choice);
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <Input
        id={inputId}
        role="combobox"
        aria-expanded={showList}
        aria-controls={listboxId}
        aria-autocomplete="list"
        aria-activedescendant={showList && results[highlighted] ? `${listboxId}-${highlighted}` : undefined}
        aria-invalid={invalid || undefined}
        autoComplete="off"
        placeholder="Nhập mã / tên / MST khách hàng..."
        value={keyword}
        onFocus={(e) => {
          e.currentTarget.select();
          setIsOpen(true);
        }}
        onChange={(e) => {
          setKeyword(e.target.value);
          setIsOpen(true);
          if (value) onSelect(null);
        }}
        onKeyDown={onKeyDown}
      />
      {showList && (
        <ul
          id={listboxId}
          role="listbox"
          className="absolute z-50 mt-1 max-h-60 w-full overflow-auto rounded-md border bg-popover p-1 text-sm shadow-md"
        >
          {results.length === 0 ? (
            <li className="px-2 py-1.5 text-muted-foreground">
              {isLoading ? 'Đang tìm...' : 'Không tìm thấy khách hàng'}
            </li>
          ) : (
            results.map((c, index) => (
              <li
                key={c.id}
                id={`${listboxId}-${index}`}
                role="option"
                aria-selected={index === highlighted}
                className={cn(
                  'cursor-pointer rounded px-2 py-1.5',
                  index === highlighted && 'bg-accent text-accent-foreground',
                )}
                onMouseEnter={() => setHighlighted(index)}
                onMouseDown={(e) => {
                  // Select before the input blurs (keyboard selection is handled on the input).
                  e.preventDefault();
                  pick(c);
                }}
              >
                <span className="font-medium">{c.code}</span> - {c.name}
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
