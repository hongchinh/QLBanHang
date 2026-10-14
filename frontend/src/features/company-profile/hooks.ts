import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { companyProfileApi } from './api';
import { companyProfileKeys } from './keys';
import type { UpdateCompanyProfileRequest } from './types';

export function useCompanyProfile() {
  return useQuery({
    queryKey: companyProfileKeys.all,
    queryFn: () => companyProfileApi.get(),
  });
}

export function useUpdateCompanyProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateCompanyProfileRequest) => companyProfileApi.update(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: companyProfileKeys.all }),
  });
}
