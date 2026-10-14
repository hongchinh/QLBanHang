// Mirrors backend Organization/Company/Models (CompanyProfileDto, UpdateCompanyProfileRequest).
export interface CompanyProfile {
  name: string | null;
  address: string | null;
  taxCode: string | null;
  chiefAccountantName: string | null;
  legalRepresentativeName: string | null;
  accountingServiceNote: string | null;
  updatedAt?: string;
}

export type UpdateCompanyProfileRequest = Omit<CompanyProfile, 'updatedAt'>;
