import { digitsOnly, formatMobilePhone, formatPostalCode, isCompletePostalCode } from './masks';

describe('masks', () => {
  it('formatPostalCode aplica a máscara progressivamente', () => {
    expect(formatPostalCode('0100')).toBe('0100');
    expect(formatPostalCode('010010')).toBe('01001-0');
    expect(formatPostalCode('01001000999')).toBe('01001-000');
    expect(formatPostalCode('01.001-000')).toBe('01001-000');
  });

  it('formatMobilePhone aplica a máscara progressivamente', () => {
    expect(formatMobilePhone('')).toBe('');
    expect(formatMobilePhone('1')).toBe('(1');
    expect(formatMobilePhone('169')).toBe('(16) 9');
    expect(formatMobilePhone('16991234567')).toBe('(16) 99123-4567');
    expect(formatMobilePhone('(16) 99123-45678')).toBe('(16) 99123-4567');
  });

  it('digitsOnly e isCompletePostalCode', () => {
    expect(digitsOnly('a1-2.3')).toBe('123');
    expect(isCompletePostalCode('01001-000')).toBe(true);
    expect(isCompletePostalCode('01001-00')).toBe(false);
  });
});
