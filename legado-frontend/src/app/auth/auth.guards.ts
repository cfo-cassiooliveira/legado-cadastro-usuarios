import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth-store';

/** Exige sessão ativa; sem ela, redireciona para o login. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  return auth.isAuthenticated() ? true : inject(Router).createUrlTree(['/login']);
};

/** Exige perfil administrativo (a API também valida — o guard só melhora a navegação). */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  return auth.isAdmin() ? true : inject(Router).createUrlTree(['/profile']);
};
