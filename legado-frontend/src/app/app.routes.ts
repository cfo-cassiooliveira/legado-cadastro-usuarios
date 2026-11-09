import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  {
    path: 'login',
    title: $localize`Entrar`,
    loadComponent: () => import('./auth/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    title: $localize`Cadastro`,
    loadComponent: () => import('./users/register-page').then((m) => m.RegisterPage),
  },
  {
    path: 'profile',
    title: $localize`Meu perfil`,
    canActivate: [authGuard],
    loadComponent: () => import('./users/profile-page').then((m) => m.ProfilePage),
  },
  {
    path: 'users',
    title: $localize`Usuários`,
    canActivate: [authGuard, adminGuard],
    loadComponent: () => import('./users/users-list-page').then((m) => m.UsersListPage),
  },
  { path: '**', redirectTo: 'login' },
];
