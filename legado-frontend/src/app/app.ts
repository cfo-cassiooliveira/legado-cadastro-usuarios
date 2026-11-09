import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from './auth/auth-store';
import { initialsOf } from './shared/initials';

/** Shell da aplicação: cabeçalho com navegação conforme a sessão. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  protected readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly initials = computed(() => initialsOf(this.auth.currentUser()?.fullName ?? ''));

  /** Revoga a sessão no servidor e volta para o login. */
  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigate(['/login']);
  }
}
