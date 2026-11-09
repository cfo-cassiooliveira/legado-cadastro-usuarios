import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { createLoggerSpy, LoggerSpy, provideLoggerSpy } from '../core/app-logger.testing';
import { AuthStore } from './auth-store';
import { LoginPage } from './login-page';

describe('LoginPage', () => {
  const login = vi.fn();
  let logger: LoggerSpy;
  let fixture: ComponentFixture<LoginPage>;
  let element: HTMLElement;

  beforeEach(async () => {
    login.mockReset();
    logger = createLoggerSpy();
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { login } },
        provideLoggerSpy(logger),
      ],
    });
    fixture = TestBed.createComponent(LoginPage);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  function type(selector: string, value: string): void {
    const input = element.querySelector<HTMLInputElement>(selector);
    if (!input) throw new Error(`Input ${selector} not found`);
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function signIn(password = 'Senha@123'): Promise<void> {
    type('#userName', 'ana.souza');
    type('#password', password);
    element.querySelector('form')?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('não chama a API com campos vazios e exibe os erros', async () => {
    element.querySelector('form')?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();

    expect(login).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Informe o login.');
  });

  it('autentica, registra info e navega para o perfil', async () => {
    login.mockResolvedValue(undefined);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    await signIn();

    expect(login).toHaveBeenCalledWith('ana.souza', 'Senha@123');
    expect(navigate).toHaveBeenCalledWith(['/profile']);
    expect(logger.info).toHaveBeenCalledWith('auth.sign_in_succeeded', { source: 'login-page' });
  });

  it('credenciais inválidas: mensagem genérica e warning (não error)', async () => {
    login.mockRejectedValue(
      new HttpErrorResponse({
        status: 401,
        error: { errors: { credentials: ['auth.invalid_credentials'] } },
      }),
    );

    await signIn('errada');

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Login ou senha inválidos.',
    );
    expect(logger.warning).toHaveBeenCalledWith('auth.sign_in_rejected', {
      source: 'login-page',
      status: 401,
    });
    expect(logger.error).not.toHaveBeenCalled();
  });

  it('rate limit é warning', async () => {
    login.mockRejectedValue(new HttpErrorResponse({ status: 429 }));

    await signIn();

    expect(logger.warning).toHaveBeenCalledWith('auth.rate_limited', {
      source: 'login-page',
      status: 429,
    });
  });

  it('falha inesperada (servidor/rede) é error com a exceção', async () => {
    const failure = new HttpErrorResponse({ status: 500 });
    login.mockRejectedValue(failure);

    await signIn();

    expect(logger.error).toHaveBeenCalledWith(
      'auth.sign_in_failed',
      { source: 'login-page', status: 500 },
      failure,
    );
    expect(logger.warning).not.toHaveBeenCalled();
  });

  it('exibe confirmação de cadastro quando vem do registro', async () => {
    fixture.componentRef.setInput('registered', '1');
    await fixture.whenStable();

    expect(element.textContent).toContain('Cadastro realizado!');
  });
});
