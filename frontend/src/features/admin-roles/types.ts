export type PermissionModule = 'system' | 'catalog' | 'sales' | 'inventory' | 'report';

export interface PermissionDto {
  code: string;
  name: string;
  module: PermissionModule;
  description?: string | null;
}

export interface RoleListItem {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isSystem: boolean;
  permissionCount: number;
  userCount: number;
}

export interface RoleDetail {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isSystem: boolean;
  permissionCodes: string[];
  userCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateRolePayload {
  code: string;
  name: string;
  description?: string | null;
  permissionCodes: string[];
}

export interface UpdateRolePayload {
  name: string;
  description?: string | null;
}

export interface UpdateRolePermissionsPayload {
  permissionCodes: string[];
}
