import { useQuery } from '@tanstack/react-query';
import { banksApi } from './api';

export function useBanks() {
  return useQuery({ queryKey: ['banks'], queryFn: banksApi.list });
}
