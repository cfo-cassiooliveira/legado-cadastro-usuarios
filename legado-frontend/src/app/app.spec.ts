import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { SAMPLE_USER } from './auth/auth-store.spec';
import { AuthStore } from './auth/auth-store';
import { UserDetails } from './users/user.model';

describe('App (shell)', () => {
  const currentUser = signal<UserDetails | null>(null);
  const isAuthenticated = signal(false);
  const isAdmin = signal(false);
  const logout = vi.fn().mockResolvedValue(undefined);
  let fixture: ComponentFixture<App>;
  let element: HTMLElement;

  beforeEach(async () => {
    currentUser.set(null);
    isAuthenticated.set(false);
    isAdmin.set(false);
    logout.mockClear();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { currentUser, isAuthenticated, isAdmin, logout } },
      ],
    });
    fixture = TestBed.createComponent(App);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  function navText(): string {
    return element.querySelector('nav')?.textContent ?? '';
  }

  async function signIn(user: UserDetails, admin: boolean): Promise<void> {
    currentUser.set(user);
    isAuthenticated.set(true);
    isAdmin.set(admin);
    await fixture.whenStable();
  }

  it('visitante vê Entrar e Criar conta', () => {
    expect(navText()).toContain('Entrar');
    expect(navText()).toContain('Criar conta');
    expect(navText()).not.toContain('Sair');
  });

  it('usuário comum vê perfil, nome e iniciais, sem o menu de administração', async () => {
    await signIn(SAMPLE_USER, false);

    expect(navText()).toContain('Meu perfil');
    expect(navText()).toContain('Ana Souza');
    expect(element.querySelector('.avatar')?.textContent?.trim()).toBe('AS');
    expect(navText()).not.toContain('Usuários');
  });

  it('administrador vê o menu Usuários', async () => {
    await signIn({ ...SAMPLE_USER, roles: ['Admin', 'User'] }, true);

    expect(navText()).toContain('Usuários');
  });

  it('Sair revoga a sessão e navega para o login', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await signIn(SAMPLE_USER, false);

    element.querySelector<HTMLButtonElement>('nav button')?.click();
    await fixture.whenStable();

    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
