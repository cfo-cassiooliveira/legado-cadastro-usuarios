import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { AuthStore } from './auth-store';
import { adminGuard, authGuard } from './auth.guards';

describe('auth guards', () => {
  const isAuthenticated = signal(false);
  const isAdmin = signal(false);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { isAuthenticated, isAdmin } },
      ],
    });
  });

  function run(guard: typeof authGuard): unknown {
    return TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );
  }

  function urlOf(result: unknown): string {
    return TestBed.inject(Router).serializeUrl(result as UrlTree);
  }

  it('authGuard redireciona visitante para o login', () => {
    isAuthenticated.set(false);
    expect(urlOf(run(authGuard))).toBe('/login');
  });

  it('authGuard libera usuário autenticado', () => {
    isAuthenticated.set(true);
    expect(run(authGuard)).toBe(true);
  });

  it('adminGuard só libera administradores', () => {
    isAdmin.set(false);
    expect(urlOf(run(adminGuard))).toBe('/profile');
    isAdmin.set(true);
    expect(run(adminGuard)).toBe(true);
  });
});
