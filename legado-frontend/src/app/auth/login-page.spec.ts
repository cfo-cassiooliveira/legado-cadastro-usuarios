import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { createLoggerSpy, LoggerSpy, provideLoggerSpy } from '../core/app-logger.testing';
import { ExternalNavigation } from '../core/external-navigation';
import { AuthStore } from './auth-store';
import { LoginPage, REDIRECT_DELAY_MS } from './login-page';

describe('LoginPage', () => {
  const login = vi.fn();
  let logger: LoggerSpy;
  const go = vi.fn();
  let fixture: ComponentFixture<LoginPage>;
  let element: HTMLElement;

  beforeEach(async () => {
    login.mockReset();
    logger = createLoggerSpy();
    go.mockReset();
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { login } },
        provideLoggerSpy(logger),
        { provide: ExternalNavigation, useValue: { go } },
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

  // Regressão: o painel lateral também tinha um h1, e a tela ficava com dois títulos principais.
  it('tem um único h1, o do formulário', () => {
    const headings = element.querySelectorAll('h1');

    expect(headings.length).toBe(1);
    expect(headings[0].textContent?.trim()).toBe('Entrar');
  });

  it('não usa estilo inline no botão de envio (CSP sem unsafe-inline)', () => {
    expect(element.querySelector('button[type="submit"]')?.getAttribute('style')).toBeNull();
  });

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

  afterEach(() => vi.useRealTimers());

  function migrated(newSystemUrl?: string): HttpErrorResponse {
    return new HttpErrorResponse({
      status: 401,
      error: { errors: { credentials: ['auth.account_migrated'] }, newSystemUrl },
    });
  }

  it('conta migrada: avisa, mostra o link e redireciona para o Cadastro', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    login.mockRejectedValue(migrated('http://localhost:4300/'));

    await signIn();

    const notice = element.querySelector('.alert--info');
    expect(notice?.getAttribute('role')).toBe('alert');
    expect(notice?.textContent).toContain('Sua conta foi transferida para o novo sistema.');
    expect(notice?.textContent).toContain('em instantes');
    expect(notice?.querySelector('a')?.getAttribute('href')).toBe('http://localhost:4300/');
    expect(element.querySelector('.alert--error')).toBeNull();
    expect(go).not.toHaveBeenCalled();

    vi.advanceTimersByTime(REDIRECT_DELAY_MS);

    expect(go).toHaveBeenCalledWith('http://localhost:4300/');
    expect(logger.warning).toHaveBeenCalledWith('auth.account_migrated', {
      source: 'login-page',
      status: 401,
    });
    expect(logger.error).not.toHaveBeenCalled();
  });

  it('"Ficar nesta página" cancela o redirecionamento e mantém o link', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    login.mockRejectedValue(migrated('http://localhost:4300/'));
    await signIn();

    element.querySelector<HTMLButtonElement>('[data-testid="stay-here"]')?.click();
    await fixture.whenStable();
    vi.advanceTimersByTime(REDIRECT_DELAY_MS);

    expect(go).not.toHaveBeenCalled();
    expect(element.querySelector('[data-testid="stay-here"]')).toBeNull();
    expect(element.querySelector('.alert--info')?.textContent).not.toContain('em instantes');
    expect(element.querySelector('.alert--info a')?.textContent?.trim()).toBe(
      'Ir para o novo sistema',
    );
  });

  it('conta migrada sem endereço do Cadastro: só o aviso, sem link nem redirecionamento', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    login.mockRejectedValue(migrated());

    await signIn();
    vi.advanceTimersByTime(REDIRECT_DELAY_MS);

    expect(element.querySelector('.alert--info')?.textContent).toContain('transferida');
    expect(element.querySelector('.alert--info a')).toBeNull();
    expect(go).not.toHaveBeenCalled();
  });

  it('nova tentativa limpa o aviso e cancela o redirecionamento', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    login.mockRejectedValueOnce(migrated('http://localhost:4300/'));
    await signIn();
    login.mockRejectedValueOnce(
      new HttpErrorResponse({
        status: 401,
        error: { errors: { credentials: ['auth.invalid_credentials'] } },
      }),
    );

    await signIn('errada');
    vi.advanceTimersByTime(REDIRECT_DELAY_MS);

    expect(go).not.toHaveBeenCalled();
    expect(element.querySelector('.alert--info')).toBeNull();
    expect(element.querySelector('.alert--error')?.textContent).toContain(
      'Login ou senha inválidos.',
    );
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
