/** Siglas das Unidades Federativas, na ordem alfabética das siglas. */
export const BRAZILIAN_STATES: readonly string[] = [
  'AC',
  'AL',
  'AM',
  'AP',
  'BA',
  'CE',
  'DF',
  'ES',
  'GO',
  'MA',
  'MG',
  'MS',
  'MT',
  'PA',
  'PB',
  'PE',
  'PI',
  'PR',
  'RJ',
  'RN',
  'RO',
  'RR',
  'RS',
  'SC',
  'SE',
  'SP',
  'TO',
];

// DDDs ativos (Anatel) — espelha a regra do backend para feedback imediato.
const VALID_AREA_CODES = new Set([
  11, 12, 13, 14, 15, 16, 17, 18, 19, 21, 22, 24, 27, 28, 31, 32, 33, 34, 35, 37, 38, 41, 42, 43,
  44, 45, 46, 47, 48, 49, 51, 53, 54, 55, 61, 62, 63, 64, 65, 66, 67, 68, 69, 71, 73, 74, 75, 77,
  79, 81, 82, 83, 84, 85, 86, 87, 88, 89, 91, 92, 93, 94, 95, 96, 97, 98, 99,
]);

const MOBILE_PATTERN = /^(\d{2})9\d{8}$/;
const MIN_PASSWORD_LENGTH = 8;

/** Celular com DDD válido e 9 dígitos iniciando em 9 (aceita máscara). */
export function isValidMobilePhone(value: string): boolean {
  const match = MOBILE_PATTERN.exec(value.replace(/\D/g, ''));
  return match !== null && VALID_AREA_CODES.has(Number(match[1]));
}

/** Regra individual da política de senha (exibida como checklist na tela). */
export interface PasswordRule {
  readonly label: string;
  readonly test: (value: string) => boolean;
}

/** Política de senha igual à do backend (Identity): 8+, maiúscula, minúscula, dígito e símbolo. */
export const PASSWORD_RULES: readonly PasswordRule[] = [
  { label: $localize`8+ caracteres`, test: (v) => v.length >= MIN_PASSWORD_LENGTH },
  { label: $localize`Maiúscula`, test: (v) => /[A-Z]/.test(v) },
  { label: $localize`Minúscula`, test: (v) => /[a-z]/.test(v) },
  { label: $localize`Número`, test: (v) => /\d/.test(v) },
  { label: $localize`Símbolo`, test: (v) => /[^A-Za-z0-9]/.test(v) },
];

export function isStrongPassword(value: string): boolean {
  return PASSWORD_RULES.every((rule) => rule.test(value));
}

/** Nome completo com ao menos duas palavras. */
export function hasSurname(value: string): boolean {
  return value.trim().split(/\s+/).length >= 2;
}
