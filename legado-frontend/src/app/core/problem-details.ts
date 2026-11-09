import { HttpErrorResponse } from '@angular/common/http';

/** Erro da API normalizado a partir de um Problem Details (RFC 9457). */
export interface ApiProblem {
  readonly status: number;
  /** Códigos de erro por campo do contrato (ex.: `{ email: ['email.taken'] }`). */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  /** Código geral do problema, quando a API informa (ex.: `address_service.unavailable`). */
  readonly code: string | null;
}

/** Converte qualquer erro de HTTP em {@link ApiProblem}; erros de rede viram status 0. */
export function toApiProblem(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, fieldErrors: {}, code: null };
  }

  const body: unknown = error.error;
  return {
    status: error.status,
    fieldErrors: readFieldErrors(body),
    code: readCode(body),
  };
}

function readFieldErrors(body: unknown): Record<string, readonly string[]> {
  if (!isRecord(body) || !isRecord(body['errors'])) {
    return {};
  }

  const result: Record<string, readonly string[]> = {};
  for (const [field, codes] of Object.entries(body['errors'])) {
    if (Array.isArray(codes)) {
      result[field] = codes.filter((code): code is string => typeof code === 'string');
    }
  }
  return result;
}

function readCode(body: unknown): string | null {
  return isRecord(body) && typeof body['code'] === 'string' ? body['code'] : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
