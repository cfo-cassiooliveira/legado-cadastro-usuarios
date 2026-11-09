/** Funções puras de máscara para campos brasileiros. */

const POSTAL_CODE_DIGITS = 8;
const POSTAL_CODE_PREFIX = 5;
const MOBILE_DIGITS = 11;
const AREA_CODE_DIGITS = 2;
const MOBILE_PREFIX_DIGITS = 7;

export function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

/** Formata como `00000-000` à medida que o usuário digita. */
export function formatPostalCode(value: string): string {
  const digits = digitsOnly(value).slice(0, POSTAL_CODE_DIGITS);
  return digits.length > POSTAL_CODE_PREFIX
    ? `${digits.slice(0, POSTAL_CODE_PREFIX)}-${digits.slice(POSTAL_CODE_PREFIX)}`
    : digits;
}

/** Formata como `(00) 00000-0000` à medida que o usuário digita. */
export function formatMobilePhone(value: string): string {
  const digits = digitsOnly(value).slice(0, MOBILE_DIGITS);
  if (digits.length <= AREA_CODE_DIGITS) {
    return digits.length ? `(${digits}` : '';
  }
  const areaCode = digits.slice(0, AREA_CODE_DIGITS);
  const prefix = digits.slice(AREA_CODE_DIGITS, MOBILE_PREFIX_DIGITS);
  const suffix = digits.slice(MOBILE_PREFIX_DIGITS);
  return suffix ? `(${areaCode}) ${prefix}-${suffix}` : `(${areaCode}) ${prefix}`;
}

export function isCompletePostalCode(value: string): boolean {
  return digitsOnly(value).length === POSTAL_CODE_DIGITS;
}
