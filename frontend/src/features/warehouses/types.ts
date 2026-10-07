export interface Warehouse {
  id: string;
  code: string;
  name: string;
  branchId: string;
  branchCode: string;
  branchName: string;
  isActive: boolean;
}

export interface WarehouseListParams {
  // Honoured only for users with branches.access_all; otherwise the working branch.
  branchId?: string;
  isActive?: boolean;
}

export interface CreateWarehouseRequest {
  code: string;
  name: string;
  // Omitted → the working branch.
  branchId?: string;
  isActive: boolean;
}

export interface UpdateWarehouseRequest {
  name: string;
  branchId: string;
  isActive: boolean;
}
