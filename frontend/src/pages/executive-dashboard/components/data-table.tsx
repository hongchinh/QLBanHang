import type { ReactNode } from 'react';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { cn } from '@/lib/utils';

export interface DataColumn<T> {
  key: string;
  header: string;
  // 'right' adds text-right and tabular-nums to the header and the cells (numbers and money).
  align?: 'left' | 'right';
  // More classes on the header and the cells (e.g. whitespace-nowrap).
  className?: string;
  cell: (row: T) => ReactNode;
}

interface DataTableProps<T> {
  columns: DataColumn<T>[];
  rows: T[];
  rowKey: (row: T, index: number) => string;
  // The cells of a last row (e.g. "Tổng cộng").
  footer?: ReactNode;
}

const columnClass = (column: Pick<DataColumn<unknown>, 'align' | 'className'>) =>
  cn(column.align === 'right' && 'text-right tabular-nums', column.className) || undefined;

// A small table of a dashboard tile, on the shared table look.
export function DataTable<T>({ columns, rows, rowKey, footer }: DataTableProps<T>) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          {columns.map((column) => (
            <TableHead key={column.key} className={columnClass(column)}>
              {column.header}
            </TableHead>
          ))}
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((row, index) => (
          <TableRow key={rowKey(row, index)}>
            {columns.map((column) => (
              <TableCell key={column.key} className={columnClass(column)}>
                {column.cell(row)}
              </TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
      {footer != null && (
        <TableFooter>
          <TableRow>{footer}</TableRow>
        </TableFooter>
      )}
    </Table>
  );
}
