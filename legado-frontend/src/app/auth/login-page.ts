import { HttpStatusCode } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { form, FormField, required, submit } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { AppLogger } from '../core/app-logger';
import { ExternalNavigation } from '../core/external-navigation';
import { messageForProblem } from '../core/error-messages';
import { ApiProblem, toApiProblem } from '../core/problem-details';
import { AuthHero } from '../shared/auth-hero';
import { FieldErrors, showInvalid } from '../shared/field-errors';
import { AuthStore } from './auth-store';

const SOURCE = 'login-page';
const ACCOUNT_MIGRATED = 'auth.account_migrated';
/** Tempo para ler o aviso antes de ir ao novo sistema (com "Ficar nesta página" para cancelar, WCAG 2.2.1). */
export const REDIRECT_DELAY_MS = 5000;

interface LoginModel {
  userName: string;
  password: string;
}

@Component({
  selector: 'app-login-page',
  imports: [FormField, RouterLink, FieldErrors, AuthHero],
  templateUrl: './login-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly logger = inject(AppLogger);
  private readonly externalNavigation = inject(ExternalNavigation);
  private readonly destroyRef = inject(DestroyRef);

  /** Preenchido via query param após o cadastro (`?registered=1`). */
  readonly registered = input<string>();

  protected readonly model = signal<LoginModel>({ userName: '', password: '' });
  protected readonly loginForm = form(this.model, (path) => {
    required(path.userName, { message: $localize`Informe o login.` });
    required(path.password, { message: $localize`Informe a senha.` });
  });
  protected readonly showInvalid = showInvalid;
  protected readonly errorMessage = signal<string | null>(null);
  /** Senha correta de conta já migrada: aviso e, havendo endereço, redirecionamento para o Cadastro. */
  protected readonly accountMigrated = signal(false);
  protected readonly newSystemUrl = signal<string | null>(null);
  protected readonly redirecting = signal(false);
  protected readonly migratedMessage = computed(() =>
    this.redirecting()
      ? $localize`Sua conta foi transferida para o novo sistema. Levando você para lá em instantes…`
      : $localize`Sua conta foi transferida para o novo sistema.`,
  );
  private redirectTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    this.destroyRef.onDestroy(() => this.cancelRedirect());
  }

  protected async signIn(event: Event): Promise<void> {
    event.preventDefault();
    this.errorMessage.set(null);
    this.accountMigrated.set(false);
    this.cancelRedirect();

    await submit(this.loginForm, async () => {
      try {
        const { userName, password } = this.model();
        await this.auth.login(userName, password);
        this.logger.info('auth.sign_in_succeeded', { source: SOURCE });
        await this.router.navigate(['/profile']);
      } catch (error) {
        const problem = toApiProblem(error);
        if (isAccountMigrated(problem)) {
          this.logger.warning('auth.account_migrated', { source: SOURCE, status: problem.status });
          this.showAccountMigrated(problem.newSystemUrl);
          return undefined;
        }
        this.logFailure(problem.status, error);
        this.errorMessage.set(messageForProblem(problem));
      }
      return undefined;
    });
  }

  /** Interrompe o redirecionamento automático; o link para o novo sistema continua na tela. */
  protected stayHere(): void {
    this.cancelRedirect();
  }

  private showAccountMigrated(url: string | null): void {
    this.accountMigrated.set(true);
    this.newSystemUrl.set(url);
    if (url) {
      this.redirecting.set(true);
      this.redirectTimer = setTimeout(() => this.externalNavigation.go(url), REDIRECT_DELAY_MS);
    }
  }

  private cancelRedirect(): void {
    clearTimeout(this.redirectTimer);
    this.redirectTimer = undefined;
    this.redirecting.set(false);
  }

  /** Credenciais inválidas/bloqueio e rate limit são avisos esperados; o resto é erro capturado. */
  private logFailure(status: number, error: unknown): void {
    if (status === HttpStatusCode.Unauthorized) {
      this.logger.warning('auth.sign_in_rejected', { source: SOURCE, status });
    } else if (status === HttpStatusCode.TooManyRequests) {
      this.logger.warning('auth.rate_limited', { source: SOURCE, status });
    } else {
      this.logger.error('auth.sign_in_failed', { source: SOURCE, status }, error);
    }
  }
}

function isAccountMigrated(problem: ApiProblem): boolean {
  return Object.values(problem.fieldErrors).some((codes) => codes.includes(ACCOUNT_MIGRATED));
}
