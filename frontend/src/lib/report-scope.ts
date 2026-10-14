// Application/Common/ReportScope.cs: the working branch only, or every branch of the company.
export type ReportScope = 'Branch' | 'Company';

export const REPORT_SCOPE_LABELS: Record<ReportScope, string> = {
  Branch: 'Chi nhánh đang làm việc',
  Company: 'Toàn công ty',
};
