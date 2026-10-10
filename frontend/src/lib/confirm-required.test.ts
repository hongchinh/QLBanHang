import { describe, expect, it } from 'vitest';
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { toConfirmationRequest, withConfirmed, type ConfirmFlag } from './confirm-required';

function axiosErrorWith(status: number, data: unknown): AxiosError {
  const config = { headers: new AxiosHeaders(), url: '/x' } as InternalAxiosRequestConfig;
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', config, null, {
    status,
    statusText: '',
    headers: {},
    config,
    data,
  });
}

function errorBody(code: string, details?: Record<string, string[]>) {
  return { success: false, error: { code, message: `Server says ${code}`, details }, timestamp: '' };
}

describe('toConfirmationRequest', () => {
  const cases: Array<[string, ConfirmFlag | undefined, boolean, string]> = [
    ['NEGATIVE_STOCK_WARNING', 'acknowledgeNegativeStock', false, 'Xuất quá tồn kho'],
    ['NEGATIVE_STOCK_BLOCKED', undefined, true, 'Không đủ tồn kho'],
    ['DEBT_SETTLEMENTS_WILL_BE_REMOVED', 'confirmRemoveSettlements', false, 'Bỏ đối trừ chứng từ'],
    ['CREDIT_LIMIT_WARNING', 'acknowledgeCreditLimit', false, 'Vượt hạn mức nợ'],
    ['CREDIT_LIMIT_BLOCKED', undefined, true, 'Vượt hạn mức nợ'],
    ['NEGATIVE_FUND_WARNING', 'acknowledgeNegativeFund', false, 'Quỹ bị âm'],
    ['NEGATIVE_FUND_BLOCKED', undefined, true, 'Không đủ tiền trong quỹ'],
  ];

  it.each(cases)('maps each code to flag and blocked state: %s', (code, flag, blocked, title) => {
    const details = { 'KH01': ['Dư nợ 9.000.000 vượt hạn mức 5.000.000'], 'QTM': ['Âm 1.000'] };
    const request = toConfirmationRequest(axiosErrorWith(422, errorBody(code, details)));

    expect(request).toEqual({
      code,
      flag,
      blocked,
      title,
      message: `Server says ${code}`,
      items: [
        { key: 'KH01', messages: ['Dư nợ 9.000.000 vượt hạn mức 5.000.000'] },
        { key: 'QTM', messages: ['Âm 1.000'] },
      ],
    });
  });

  it('maps missing details to an empty items list', () => {
    const request = toConfirmationRequest(axiosErrorWith(422, errorBody('CREDIT_LIMIT_WARNING')));
    expect(request?.items).toEqual([]);
  });

  it('returns null for other errors', () => {
    expect(toConfirmationRequest(axiosErrorWith(409, errorBody('DEBT_HAS_SETTLEMENTS')))).toBeNull();
    expect(toConfirmationRequest(axiosErrorWith(400, errorBody('NEGATIVE_FUND_WARNING')))).toBeNull();
    expect(toConfirmationRequest(axiosErrorWith(422, errorBody('SOMETHING_ELSE')))).toBeNull();
    const config = { headers: new AxiosHeaders(), url: '/x' } as InternalAxiosRequestConfig;
    expect(toConfirmationRequest(new AxiosError('Network Error', 'ERR_NETWORK', config))).toBeNull();
    expect(toConfirmationRequest(new Error('boom'))).toBeNull();
    expect(toConfirmationRequest(undefined)).toBeNull();
  });
});

describe('withConfirmed', () => {
  it('withConfirmed accumulates flags', () => {
    const credit = toConfirmationRequest(axiosErrorWith(422, errorBody('CREDIT_LIMIT_WARNING')))!;
    const fund = toConfirmationRequest(axiosErrorWith(422, errorBody('NEGATIVE_FUND_WARNING')))!;
    const blocked = toConfirmationRequest(axiosErrorWith(422, errorBody('NEGATIVE_FUND_BLOCKED')))!;

    const start = { acknowledgeNegativeStock: true };
    const afterCredit = withConfirmed(start, credit);
    const afterFund = withConfirmed(afterCredit, fund);

    expect(afterCredit).toEqual({ acknowledgeNegativeStock: true, acknowledgeCreditLimit: true });
    expect(afterFund).toEqual({
      acknowledgeNegativeStock: true,
      acknowledgeCreditLimit: true,
      acknowledgeNegativeFund: true,
    });
    expect(start).toEqual({ acknowledgeNegativeStock: true });
    expect(withConfirmed(afterFund, blocked)).toEqual(afterFund);
  });
});
