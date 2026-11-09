import { hasSurname, isStrongPassword, isValidMobilePhone } from './brazil';

describe('brazil validators', () => {
  it.each([
    ['(16) 99123-4567', true],
    ['11987654321', true],
    ['16891234567', false],
    ['10991234567', false],
    ['1699123456', false],
  ])('isValidMobilePhone(%s) = %s', (value, expected) => {
    expect(isValidMobilePhone(value)).toBe(expected);
  });

  it.each([
    ['Senha@123', true],
    ['senha@123', false],
    ['SENHA@123', false],
    ['Senha1234', false],
    ['Se@1', false],
  ])('isStrongPassword(%s) = %s', (value, expected) => {
    expect(isStrongPassword(value)).toBe(expected);
  });

  it('hasSurname exige ao menos duas palavras', () => {
    expect(hasSurname('Ana Souza')).toBe(true);
    expect(hasSurname('  Ana  ')).toBe(false);
  });
});
