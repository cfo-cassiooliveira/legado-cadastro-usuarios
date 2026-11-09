import { HttpStatusCode } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { form, FormField, required, submit } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { AppLogger } from '../core/app-logger';
import { messageForProblem } from '../core/error-messages';
import { toApiProblem } from '../core/problem-details';
import { AuthHero } from '../shared/auth-hero';
import { FieldErrors, showInvalid } from '../shared/field-errors';
import { AuthStore } from './auth-store';

const SOURCE = 'login-page';

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

  /** Preenchido via query param após o cadastro (`?registered=1`). */
  readonly registered = input<string>();

  protected readonly model = signal<LoginModel>({ userName: '', password: '' });
  protected readonly loginForm = form(this.model, (path) => {
    required(path.userName, { message: $localize`Informe o login.` });
    required(path.password, { message: $localize`Informe a senha.` });
  });
  protected readonly showInvalid = showInvalid;
  protected readonly errorMessage = signal<string | null>(null);

  protected async signIn(event: Event): Promise<void> {
    event.preventDefault();
    this.errorMessage.set(null);

    await submit(this.loginForm, async () => {
      try {
        const { userName, password } = this.model();
        await this.auth.login(userName, password);
        this.logger.info('auth.sign_in_succeeded', { source: SOURCE });
        await this.router.navigate(['/profile']);
      } catch (error) {
        const problem = toApiProblem(error);
        this.logFailure(problem.status, error);
        this.errorMessage.set(messageForProblem(problem));
      }
      return undefined;
    });
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
