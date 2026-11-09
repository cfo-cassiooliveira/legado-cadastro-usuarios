import { HttpStatusCode } from '@angular/common/http';

/**
 * Status HTTP que representam falha de negócio prevista (dado inválido, sessão recusada, não encontrado,
 * conflito, rate limit). Pela regra de log do workspace eles são registrados como warning; qualquer outro
 * status (5xx, rede = 0) é falha inesperada e vai como error.
 */
const EXPECTED_FAILURES: ReadonlySet<number> = new Set([
  HttpStatusCode.BadRequest,
  HttpStatusCode.Unauthorized,
  HttpStatusCode.NotFound,
  HttpStatusCode.Conflict,
  HttpStatusCode.TooManyRequests,
]);

export function isExpectedFailure(status: number): boolean {
  return EXPECTED_FAILURES.has(status);
}
