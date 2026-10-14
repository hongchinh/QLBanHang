import { Link } from 'react-router-dom';
import { TileCard } from '@/features/dashboard/components/tile-card';
import { formatNumber } from '@/features/dashboard/format';
import { useDebtBreakdown, useDebtSummary } from '@/features/executive-dashboard/hooks';
import { codeName } from '@/features/executive-dashboard/labels';
import { executiveLinks, reportLinkTitle } from '@/features/executive-dashboard/links';
import type {
  CreditLimitBreach,
  DebtEmployeeRow,
  DebtPartnerRow,
  ExecutiveParams,
  PayableDueRow,
} from '@/features/executive-dashboard/types';
import { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { useCanOpenLink } from '@/hooks/useCanOpenLink';
import { formatDateOnly } from '@/lib/date-only';
import { cn } from '@/lib/utils';
import { todayYmd } from '@/lib/vn-datetime';
import { BranchDocumentLink } from '../components/branch-document-link';
import { DataTable } from '../components/data-table';
import type { DataColumn } from '../components/data-table';
import { DebtTermBar } from '../components/debt-term-bar';

interface DebtTabProps {
  params: ExecutiveParams;
}

const EMPLOYEE_COLUMNS: DataColumn<DebtEmployeeRow>[] = [
  { key: 'owner', header: 'Nhân viên', cell: (row) => row.ownerName ?? 'Không xác định' },
  { key: 'open', header: 'Còn nợ', align: 'right', cell: (row) => formatNumber(row.open) },
  { key: 'overdue', header: 'Quá hạn', align: 'right', cell: (row) => formatNumber(row.overdue) },
];

// The Công nợ tab: the receivable and payable as at today by term, the largest debtors, the customers over
// their credit limit, the receivable by employee and the payables due within 30 days.
export function DebtTab({ params }: DebtTabProps) {
  const scope = useExecutiveScope();
  const canOpenLink = useCanOpenLink();
  const { from, to, branchId } = params;
  const companyView = scope.canSwitch && scope.mode === 'company';
  const linkTitle = reportLinkTitle(scope.mode, scope.branches.length, scope.workingBranch?.code);
  const workingBranchCode = scope.workingBranch?.code;
  const today = todayYmd();

  const summary = useDebtSummary({ branchId });
  const breakdown = useDebtBreakdown({ branchId });
  const topDebtors = breakdown.data?.topDebtors ?? [];
  const overLimit = breakdown.data?.overLimit ?? [];
  const byEmployee = breakdown.data?.byEmployee ?? [];
  const payablesDue = breakdown.data?.payablesDue ?? [];

  const debtorColumns: DataColumn<DebtPartnerRow>[] = [
    {
      key: 'partner',
      header: 'Khách hàng',
      cell: (row) => {
        const name = codeName(row.partnerCode, row.partnerName);
        const to = executiveLinks.debtOverdue('Receivable', today, row.partnerId);
        if (!canOpenLink(to)) return name;
        return (
          <Link to={to} title={linkTitle} className="text-primary hover:underline">
            {name}
          </Link>
        );
      },
    },
    { key: 'balance', header: 'Còn nợ', align: 'right', cell: (row) => formatNumber(row.balance) },
    {
      key: 'overdue',
      header: 'Quá hạn',
      align: 'right',
      cell: (row) => <span className={cn(row.overdue > 0 && 'text-rose-700')}>{formatNumber(row.overdue)}</span>,
    },
  ];
  const overLimitColumns: DataColumn<CreditLimitBreach>[] = [
    { key: 'partner', header: 'Khách hàng', cell: (row) => codeName(row.partnerCode, row.partnerName) },
    ...(companyView ? [{ key: 'branch', header: 'Chi nhánh', cell: (row: CreditLimitBreach) => row.branchCode }] : []),
    { key: 'balance', header: 'Dư nợ', align: 'right', cell: (row) => formatNumber(row.balance) },
    { key: 'limit', header: 'Hạn mức', align: 'right', cell: (row) => formatNumber(row.limit) },
    { key: 'over', header: 'Vượt', align: 'right', cell: (row) => formatNumber(row.over) },
  ];
  const payableColumns: DataColumn<PayableDueRow>[] = [
    { key: 'partner', header: 'Nhà cung cấp', cell: (row) => codeName(row.partnerCode, row.partnerName) },
    {
      key: 'document',
      header: 'Chứng từ',
      className: 'whitespace-nowrap',
      cell: (row) => (
        <BranchDocumentLink
          sourceType={row.sourceType}
          sourceId={row.sourceId}
          code={row.docCode}
          branchCode={row.branchCode}
          workingBranchCode={workingBranchCode}
        />
      ),
    },
    { key: 'due', header: 'Hạn TT', className: 'whitespace-nowrap', cell: (row) => formatDateOnly(row.dueDate) },
    { key: 'open', header: 'Còn phải trả', align: 'right', cell: (row) => formatNumber(row.open) },
  ];

  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <TileCard
        title="Cơ cấu nợ phải thu"
        query={summary}
        action={{ label: 'Tuổi nợ →', to: executiveLinks.debtAging('Receivable', today), title: linkTitle }}
      >
        {summary.data && <DebtTermBar side={summary.data.receivable} buckets={summary.data.overdueBuckets} />}
      </TileCard>
      <TileCard
        title="Cơ cấu nợ phải trả"
        query={summary}
        action={{ label: 'Tuổi nợ →', to: executiveLinks.debtAging('Payable', today), title: linkTitle }}
      >
        {summary.data && <DebtTermBar side={summary.data.payable} buckets={summary.data.overdueBuckets} />}
      </TileCard>
      <TileCard
        title="Khách nợ nhiều nhất"
        query={breakdown}
        isEmpty={topDebtors.length === 0}
        action={{ label: 'Số dư công nợ →', to: executiveLinks.debtBalance('Receivable', from, to), title: linkTitle }}
      >
        <DataTable columns={debtorColumns} rows={topDebtors} rowKey={(row) => row.partnerId} />
      </TileCard>
      <TileCard
        title="Khách vượt hạn mức"
        query={breakdown}
        isEmpty={overLimit.length === 0}
        // The credit limits are no report: they are managed per branch, the branch hint does not apply.
        action={{ label: 'Hạn mức nợ →', to: executiveLinks.creditLimits() }}
      >
        <DataTable columns={overLimitColumns} rows={overLimit} rowKey={(row) => `${row.partnerId}-${row.branchId}`} />
      </TileCard>
      <TileCard
        title="Công nợ theo nhân viên"
        query={breakdown}
        isEmpty={byEmployee.length === 0}
        action={{ label: 'Công nợ theo nhân viên →', to: executiveLinks.debtBySalesperson(from, to), title: linkTitle }}
      >
        <DataTable columns={EMPLOYEE_COLUMNS} rows={byEmployee} rowKey={(row) => row.ownerUserId ?? 'none'} />
      </TileCard>
      <TileCard
        title="Phải trả đến hạn 30 ngày"
        query={breakdown}
        isEmpty={payablesDue.length === 0}
        action={{ label: 'Số dư công nợ →', to: executiveLinks.debtBalance('Payable', from, to), title: linkTitle }}
      >
        <DataTable columns={payableColumns} rows={payablesDue} rowKey={(row) => `${row.sourceType}-${row.sourceId}`} />
      </TileCard>
    </div>
  );
}
