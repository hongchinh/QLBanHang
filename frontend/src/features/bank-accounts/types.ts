export interface UserBankAccount {
  id: string;
  bankId: string;
  bankCode: string;
  bankName: string;
  bankBin: string;
  accountNumber: string;
  accountName: string;
  isDefault: boolean;
}

export interface CreateUserBankAccountRequest {
  bankId: string;
  accountNumber: string;
  accountName: string;
  isDefault?: boolean;
}

export interface UpdateUserBankAccountRequest {
  bankId: string;
  accountNumber: string;
  accountName: string;
}
