import { format } from 'date-fns';
import { TileCard } from '@/features/dashboard/components/tile-card';
import { formatNumber } from '@/features/dashboard/format';
import { useCashBreakdown, useCashSeries, useCashSummary } from '@/features/executive-dashboard/hooks';
import { executiveLinks, reportLinkTitle } from '@/features/executive-dashboard/links';
import { isDayGranularityAllowed } from '@/features/executive-dashboard/period';
import { isCashSeriesEmpty, usePeriodGranularity } from '@/features/executive-dashboard/series';
import type {
  CashDocument,
  CashPoint,
  CashReasonTotal,
  ExecutiveParams,
  FundBalance,
} from '@/features/executive-dashboard/types';
import { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { BranchDocumentLink } from '../components/branch-document-link';
import { CashFlowChart } from '../components/cash-flow-chart';
import { DataTable } from '../components/data-table';
import type { DataColumn } from '../components/data-table';
import { FundBalanceTable } from '../components/fund-balance-table';

interface CashTabProps {
  params: ExecutiveParams;
}

const NO_FUNDS: FundBalance[] = [];
const NO_POINTS: CashPoint[] = [];
const NO_REASONS: CashReasonTotal[] = [];
const NO_DOCUMENTS: CashDocument[] = [];

const REASON_COLUMNS: DataColumn<CashReasonTotal>[] = [
  { key: 'reason', header: 'Lý do', cell: (row) => row.reasonName ?? row.reasonCode ?? '' },
  { key: 'count', header: 'Số phiếu', align: 'right', cell: (row) => formatNumber(row.count) },
  { key: 'total', header: 'Số tiền', align: 'right', cell: (row) => formatNumber(row.total) },
];

// The Tiền tab: the balance of every fund, receipts and payments over the period, their split by reason and
// the largest documents.
export function CashTab({ params }: CashTabProps) {
  const scope = useExecutiveScope();
  const { from, to } = params;
  const [granularity, setGranularity] = usePeriodGranularity(from, to);
  const companyView = scope.canSwitch && scope.mode === 'company';
  const linkTitle = reportLinkTitle(scope.mode, scope.branches.length, scope.workingBranch?.code);
  const workingBranchCode = scope.workingBranch?.code;

  const summary = useCashSummary(params);
  const series = useCashSeries({ ...params, granularity });
  const breakdown = useCashBreakdown(params);

  const funds = summary.data?.funds ?? NO_FUNDS;
  const points = series.data?.points ?? NO_POINTS;
  const reasons = breakdown.data?.byReason ?? NO_REASONS;
  const receiptReasons = reasons.filter((row) => row.type === 'Receipt');
  const paymentReasons = reasons.filter((row) => row.type === 'Payment');
  const largest = breakdown.data?.largest ?? NO_DOCUMENTS;

  const documentColumns: DataColumn<CashDocument>[] = [
    {
      key: 'code',
      header: 'Số phiếu',
      className: 'whitespace-nowrap',
      cell: (doc) => (
        <BranchDocumentLink
          sourceType={doc.type}
          sourceId={doc.id}
          code={doc.code}
          branchCode={doc.branchCode}
          workingBranchCode={workingBranchCode}
        />
      ),
    },
    ...(companyView ? [{ key: 'branch', header: 'Chi nhánh', cell: (doc: CashDocument) => doc.branchCode }] : []),
    // The browser runs in VN time (lib/vn-datetime.ts).
    { key: 'at', header: 'Thời gian', className: 'whitespace-nowrap', cell: (doc) => format(new Date(doc.at), 'HH:mm dd/MM') },
    { key: 'partner', header: 'Đối tượng', cell: (doc) => doc.partnerOrPerson ?? '' },
    {
      key: 'description',
      header: 'Diễn giải',
      cell: (doc) =>
        doc.description ? (
          <span className="block max-w-xs truncate" title={doc.description}>
            {doc.description}
          </span>
        ) : null,
    },
    { key: 'receipt', header: 'Thu', align: 'right', cell: (doc) => (doc.type === 'Receipt' ? formatNumber(doc.total) : '') },
    { key: 'payment', header: 'Chi', align: 'right', cell: (doc) => (doc.type === 'Payment' ? formatNumber(doc.total) : '') },
  ];

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <TileCard
        title="Số dư các quỹ"
        query={summary}
        isEmpty={funds.length === 0}
        action={{ label: 'Sổ quỹ', to: executiveLinks.cashBook(), title: linkTitle }}
      >
        <FundBalanceTable funds={funds} showBranch={companyView} total={summary.data?.balance} />
      </TileCard>
      <TileCard title="Dòng tiền thu – chi" query={series} isEmpty={isCashSeriesEmpty(points)}>
        <CashFlowChart
          points={points}
          granularity={granularity}
          labelGranularity={series.data?.granularity}
          onGranularityChange={setGranularity}
          dayDisabled={!isDayGranularityAllowed(from, to)}
          height={280}
        />
      </TileCard>
      <TileCard title="Thu theo lý do" query={breakdown} isEmpty={receiptReasons.length === 0}>
        <DataTable columns={REASON_COLUMNS} rows={receiptReasons} rowKey={(row) => row.reasonId} />
      </TileCard>
      <TileCard title="Chi theo lý do" query={breakdown} isEmpty={paymentReasons.length === 0}>
        <DataTable columns={REASON_COLUMNS} rows={paymentReasons} rowKey={(row) => row.reasonId} />
      </TileCard>
      <TileCard title="Khoản thu chi lớn nhất" className="lg:col-span-2" query={breakdown} isEmpty={largest.length === 0}>
        <DataTable columns={documentColumns} rows={largest} rowKey={(doc) => `${doc.type}-${doc.id}`} />
      </TileCard>
    </div>
  );
}
