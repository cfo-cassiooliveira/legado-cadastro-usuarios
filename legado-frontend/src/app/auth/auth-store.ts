import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, OnDestroy, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { UserDetails } from '../users/user.model';
import { ADMIN_ROLE, LoginResponse } from './auth.model';

interface Session {
  readonly accessToken: string;
  readonly expiresAt: Date;
}

/**
 * Sessão do usuário. O token fica **apenas em memória** (nunca em localStorage/sessionStorage) e só é aceito
 * pela API junto com o cookie HttpOnly de fingerprint, que o JavaScript não consegue ler. Recarregar a página
 * encerra a sessão — comportamento aceito no legado (ADR-0001).
 */
@Injectable({ providedIn: 'root' })
export class AuthStore implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly session = signal<Session | null>(null);
  private expiryTimer: ReturnType<typeof setTimeout> | undefined;

  readonly currentUser = signal<UserDetails | null>(null);
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly isAdmin = computed(() => this.currentUser()?.roles.includes(ADMIN_ROLE) ?? false);

  /** Autentica, guarda o token em memória e carrega o perfil (o cookie de fingerprint é gravado pelo navegador). */
  async login(userName: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>('/api/v1/auth/login', { userName, password }),
    );
    this.startSession({
      accessToken: response.accessToken,
      expiresAt: new Date(response.expiresAt),
    });

    try {
      this.currentUser.set(await firstValueFrom(this.http.get<UserDetails>('/api/v1/users/me')));
    } catch (error) {
      // Sem perfil carregado a sessão é inconsistente; desfaz o login.
      this.clearSession();
      throw error;
    }
  }

  /**
   * Logout completo: revoga os tokens no servidor (e apaga o cookie) e limpa a sessão local.
   * A sessão local é limpa mesmo se a chamada falhar, para nunca deixar o usuário "logado" na tela.
   */
  async logout(): Promise<void> {
    try {
      if (this.isAuthenticated()) {
        await firstValueFrom(this.http.post<void>('/api/v1/auth/logout', null));
      }
    } catch {
      // Falha de rede ou token já inválido: o servidor recusará o token de qualquer forma ao expirar.
    } finally {
      this.clearSession();
    }
  }

  /** Limpa somente a sessão local (usado quando a API já recusou o token). */
  clearSession(): void {
    clearTimeout(this.expiryTimer);
    this.session.set(null);
    this.currentUser.set(null);
  }

  ngOnDestroy(): void {
    clearTimeout(this.expiryTimer);
  }

  private startSession(session: Session): void {
    this.session.set(session);
    clearTimeout(this.expiryTimer);
    // Sem refresh token: ao expirar, a sessão é encerrada no cliente.
    const remainingMs = Math.max(session.expiresAt.getTime() - Date.now(), 0);
    this.expiryTimer = setTimeout(() => this.clearSession(), remainingMs);
  }
}
