export type CustomerStatus = 'Active' | 'Inactive';
export type CustomerGroup = 'Company' | 'Agent' | 'Retail' | 'Project';

export interface Customer {
  id: string;
  code: string;
  name: string;
  taxCode?: string;
  companyAddress?: string;
  defaultShippingAddress?: string;
  contactPerson?: string;
  phoneNumber?: string;
  email?: string;
  group: CustomerGroup;
  note?: string;
  status: CustomerStatus;
  createdAt: string;
  isCustomer: boolean;
  isSupplier: boolean;
  // Days of credit: the default due date of an Increase stock voucher is its date + this (P27).
  creditDays?: number | null;
}

export interface CustomerListItem {
  id: string;
  code: string;
  name: string;
  taxCode?: string;
  phoneNumber?: string;
  contactPerson?: string;
  group: CustomerGroup;
  status: CustomerStatus;
  isCustomer: boolean;
  isSupplier: boolean;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface CustomerListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  group?: CustomerGroup;
  status?: CustomerStatus;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface UpsertCustomerRequest {
  code?: string;
  name: string;
  taxCode?: string;
  companyAddress?: string;
  defaultShippingAddress?: string;
  contactPerson?: string;
  phoneNumber?: string;
  email?: string;
  group: CustomerGroup;
  note?: string;
  status?: CustomerStatus;
  // Partner roles (D2): turning a role on needs that role's permission.
  isCustomer?: boolean;
  isSupplier?: boolean;
  // Omitted (empty input) clears the value.
  creditDays?: number | null;
}

export interface CustomerSearchItem {
  id: string;
  code: string;
  name: string;
  taxCode?: string;
  companyAddress?: string;
  defaultShippingAddress?: string;
  contactPerson?: string;
  phoneNumber?: string;
  status: CustomerStatus;
}

export interface CustomerSearchParams {
  keyword: string;
  activeOnly?: boolean;
  limit?: number;
}
