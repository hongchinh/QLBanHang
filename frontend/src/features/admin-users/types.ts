export type UserStatus = 'Active' | 'Disabled';

export interface AdminUserListItem {
  id: string;
  username: string;
  fullName: string;
  roleCode: string | null;
  isActive: boolean;
  lastLoginAt: string | null;
  defaultBranchId: string;
  defaultBranchName: string | null;
}

export interface AdminUserListParams {
  search?: string;
  activeOnly?: boolean;
}

export interface AdminUserDetail {
  id: string;
  username: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  roleCode: string | null;
  status: UserStatus;
  isDeleted: boolean;
  lastLoginAt: string | null;
  createdAt: string;
  updatedAt: string | null;
  defaultBranchId: string;
  defaultBranchName: string | null;
}

export interface CreateUserPayload {
  username: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  roleCode: string;
  password: string;
  status: UserStatus;
  // Omitted → the backend assigns the main branch.
  defaultBranchId?: string;
}

export interface UpdateUserPayload {
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  roleCode: string;
  status: UserStatus;
  defaultBranchId?: string;
}

export interface ResetPasswordPayload {
  newPassword: string;
}

export interface SetUserStatusPayload {
  status: UserStatus;
}
