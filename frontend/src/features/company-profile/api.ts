import { apiGet, apiPut } from '@/lib/api-client';
import type { CompanyProfile, UpdateCompanyProfileRequest } from './types';

export const companyProfileApi = {
  get: () => apiGet<CompanyProfile>('/settings/company'),
  update: (data: UpdateCompanyProfileRequest) => apiPut<CompanyProfile>('/settings/company', data),
};
