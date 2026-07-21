export interface GenerateQrRequest {
  bankId: string;
  accountNumber: string;
  accountName: string;
  amount: number;
  content?: string;
}

export interface GenerateQrResponse {
  payload: string;
}
