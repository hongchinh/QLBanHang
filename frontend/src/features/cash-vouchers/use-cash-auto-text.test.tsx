import { act, renderHook } from '@testing-library/react';
import { useForm } from 'react-hook-form';
import { describe, expect, it } from 'vitest';
import { toFormDefaults } from './payload';
import type { CashLineFormValues, CashVoucherFormParsed, CashVoucherFormValues } from './schema';
import { useCashAutoText } from './use-cash-auto-text';

const REASON_ID = '22222222-2222-2222-2222-222222222222';

function setup() {
  const { result } = renderHook(() => {
    const form = useForm<CashVoucherFormValues, unknown, CashVoucherFormParsed>({
      defaultValues: toFormDefaults(undefined, undefined, 'Receipt'),
    });
    const isDirty = form.formState.isDirty; // read during render so RHF tracks it
    const autoText = useCashAutoText(form);
    return { form, autoText, isDirty };
  });
  const desc = () => result.current.form.getValues('description');
  const line = (i: number) => result.current.form.getValues(`lines.${i}.description`);
  return { result, desc, line0: () => line(0), line };
}

function savedValues(description: string): CashVoucherFormValues {
  const line: CashLineFormValues = { _uiKey: 'k1', id: 'l1', description, amount: 1000, note: '' };
  return {
    ...toFormDefaults(undefined, undefined, 'Receipt'),
    reasonId: REASON_ID,
    partnerName: 'Công ty A',
    description,
    lines: [line],
  };
}

describe('useCashAutoText', () => {
  it('initNew fills the description and line 0 without dirtying the form, once', () => {
    const { result, desc, line0 } = setup();

    act(() => result.current.autoText.initNew('Thu tiền khách hàng'));

    expect(desc()).toBe('Thu tiền khách hàng');
    expect(line0()).toBe('Thu tiền khách hàng');
    expect(result.current.isDirty).toBe(false);

    act(() => result.current.form.setValue('description', ''));
    act(() => result.current.autoText.initNew('Khác'));
    expect(desc()).toBe('');
  });

  it('initNew with an empty reason name does nothing and does not settle', () => {
    const { result, desc } = setup();

    act(() => result.current.autoText.initNew(''));
    expect(desc()).toBe('');

    act(() => result.current.autoText.initNew('Thu khác'));
    expect(desc()).toBe('Thu khác');
  });

  it('initNew keeps a description the user already typed', () => {
    const { result, desc } = setup();
    act(() => result.current.form.setValue('description', 'x'));

    act(() => result.current.autoText.initNew('Thu tiền khách hàng'));

    expect(desc()).toBe('x');
  });

  it('follows the section-01 §3.3 example step by step', () => {
    const { result, desc, line0 } = setup();

    act(() => result.current.autoText.initNew('Thu tiền khách hàng'));
    expect([desc(), line0()]).toEqual(['Thu tiền khách hàng', 'Thu tiền khách hàng']);

    act(() => {
      result.current.form.setValue('partnerName', 'Công ty A');
      result.current.autoText.onInputsChanged({ reasonName: 'Thu tiền khách hàng', partnerName: 'Công ty A' });
    });
    expect([desc(), line0()]).toEqual(['Thu tiền khách hàng - Công ty A', 'Thu tiền khách hàng - Công ty A']);
    expect(result.current.isDirty).toBe(true);

    act(() => {
      result.current.form.setValue('partnerName', 'Công ty B');
      result.current.autoText.onInputsChanged({ reasonName: 'Thu hoàn tiền NCC', partnerName: 'Công ty B' });
    });
    expect([desc(), line0()]).toEqual(['Thu hoàn tiền NCC - Công ty B', 'Thu hoàn tiền NCC - Công ty B']);

    act(() => {
      result.current.form.setValue('description', 'Thu hoàn tiền hàng lỗi');
      result.current.autoText.onDescriptionEdited('Thu hoàn tiền hàng lỗi');
    });
    expect(line0()).toBe('Thu hoàn tiền hàng lỗi');

    act(() => result.current.autoText.onInputsChanged({ reasonName: 'Thu khác', partnerName: 'Công ty B' }));
    expect([desc(), line0()]).toEqual(['Thu hoàn tiền hàng lỗi', 'Thu hoàn tiền hàng lỗi']);

    act(() => result.current.form.setValue('lines.0.description', 'Hàng lỗi lô 05'));
    act(() => {
      result.current.form.setValue('description', 'Khác');
      result.current.autoText.onDescriptionEdited('Khác');
    });
    expect([desc(), line0()]).toEqual(['Khác', 'Hàng lỗi lô 05']);
  });

  it('clearing the partner takes the partner off the auto text', () => {
    const { result, desc, line0 } = setup();
    act(() => result.current.autoText.initNew('Thu tiền khách hàng'));
    act(() => {
      result.current.form.setValue('partnerName', 'Công ty A');
      result.current.autoText.onInputsChanged({ reasonName: 'Thu tiền khách hàng', partnerName: 'Công ty A' });
    });

    act(() => {
      result.current.form.setValue('partnerName', '');
      result.current.autoText.onInputsChanged({ reasonName: 'Thu tiền khách hàng', partnerName: '' });
    });

    expect([desc(), line0()]).toEqual(['Thu tiền khách hàng', 'Thu tiền khách hàng']);
  });

  it('an empty extra line follows the header, an edited one does not', () => {
    const { result, line } = setup();
    act(() => result.current.autoText.initNew('Thu tiền khách hàng'));
    act(() =>
      result.current.form.setValue('lines', [
        { _uiKey: 'a', description: 'Thu tiền khách hàng', amount: 0, note: '' },
        { _uiKey: 'b', description: '', amount: 0, note: '' },
        { _uiKey: 'c', description: 'Tự nhập', amount: 5, note: '' },
      ]),
    );

    act(() => result.current.autoText.onInputsChanged({ reasonName: 'Thu khác', partnerName: '' }));

    expect([line(0), line(1), line(2)]).toEqual(['Thu khác', 'Thu khác', 'Tự nhập']);
  });

  describe('saved voucher', () => {
    it('baseline writes nothing, a later input change follows the auto text', () => {
      const { result, desc, line0 } = setup();
      const auto = 'Thu tiền khách hàng - Công ty A';
      act(() => result.current.form.reset(savedValues(auto)));

      act(() =>
        result.current.autoText.baseline({ reasonName: 'Thu tiền khách hàng', partnerName: 'Công ty A', description: auto }),
      );
      expect([desc(), line0()]).toEqual([auto, auto]);
      expect(result.current.isDirty).toBe(false);

      act(() => result.current.autoText.onInputsChanged({ reasonName: 'Thu khác', partnerName: 'Công ty A' }));
      expect([desc(), line0()]).toEqual(['Thu khác - Công ty A', 'Thu khác - Công ty A']);
    });

    it('a hand-typed description stays', () => {
      const { result, desc, line0 } = setup();
      act(() => result.current.form.reset(savedValues('Tự nhập')));

      act(() =>
        result.current.autoText.baseline({ reasonName: 'Thu tiền khách hàng', partnerName: 'Công ty A', description: 'Tự nhập' }),
      );
      act(() => result.current.autoText.onInputsChanged({ reasonName: 'Thu khác', partnerName: 'Công ty A' }));

      expect([desc(), line0()]).toEqual(['Tự nhập', 'Tự nhập']);
    });

    it('initNew is a no-op after baseline', () => {
      const { result, desc } = setup();

      act(() => result.current.autoText.baseline(undefined));
      act(() => result.current.autoText.initNew('Thu tiền khách hàng'));

      expect(desc()).toBe('');
    });

    it('after form.reset and baseline the next change works from the new baseline', () => {
      const { result, desc, line0 } = setup();
      act(() => result.current.form.reset(savedValues('Tự nhập')));
      act(() =>
        result.current.autoText.baseline({ reasonName: 'Thu tiền khách hàng', partnerName: 'Công ty A', description: 'Tự nhập' }),
      );

      const auto = 'Thu khác - Công ty C';
      act(() => result.current.form.reset({ ...savedValues(auto), partnerName: 'Công ty C' }));
      act(() => result.current.autoText.baseline({ reasonName: 'Thu khác', partnerName: 'Công ty C', description: auto }));
      act(() => result.current.autoText.onInputsChanged({ reasonName: 'Chi khác', partnerName: 'Công ty C' }));

      expect([desc(), line0()]).toEqual(['Chi khác - Công ty C', 'Chi khác - Công ty C']);
    });
  });
});
