import { HttpErrorResponse } from '@angular/common/http';

/** Erro da API normalizado a partir de um Problem Details (RFC 9457). */
export interface ApiProblem {
  readonly status: number;
  /** Códigos de erro por campo do contrato (ex.: `{ email: ['email.taken'] }`). */
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  /** Código geral do problema, quando a API informa (ex.: `address_service.unavailable`). */
  readonly code: string | null;
  /** Endereço do novo sistema (conta já migrada); só http(s) absoluto, senão `null`. */
  readonly newSystemUrl: string | null;
}

/** Converte qualquer erro de HTTP em {@link ApiProblem}; erros de rede viram status 0. */
export function toApiProblem(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, fieldErrors: {}, code: null, newSystemUrl: null };
  }

  const body: unknown = error.error;
  return {
    status: error.status,
    fieldErrors: readFieldErrors(body),
    code: readCode(body),
    newSystemUrl: readHttpUrl(body),
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

// O valor vira destino de redirecionamento: nada além de http(s) absoluto (javascript:, data: etc. são ignorados).
function readHttpUrl(body: unknown): string | null {
  const value = isRecord(body) ? body['newSystemUrl'] : null;
  if (typeof value !== 'string' || !URL.canParse(value)) {
    return null;
  }
  const { protocol } = new URL(value);
  return protocol === 'https:' || protocol === 'http:' ? value : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
