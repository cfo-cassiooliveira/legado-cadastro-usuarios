import { HttpErrorResponse, HttpInterceptorFn, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from './auth-store';

const API_PREFIX = '/api/';

/** Anexa o Bearer token às chamadas da própria API e encerra a sessão quando o token é rejeitado. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  const token = auth.accessToken();

  // Só envia o token para a nossa API (mesma origem), nunca para terceiros.
  if (!token || !request.url.startsWith(API_PREFIX)) {
    return next(request);
  }

  const authorized = request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === HttpStatusCode.Unauthorized) {
        // Token já recusado pela API (expirado, revogado ou sem fingerprint): só limpa a sessão local.
        auth.clearSession();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};
