import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthStore } from './auth-store';
import { LoginPage } from './login-page';

describe('LoginPage', () => {
  const login = vi.fn();
  let fixture: ComponentFixture<LoginPage>;
  let element: HTMLElement;

  beforeEach(async () => {
    login.mockReset();
    TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [provideRouter([]), { provide: AuthStore, useValue: { login } }],
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

  async function submitForm(): Promise<void> {
    element.querySelector('form')?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  it('não chama a API com campos vazios e exibe os erros', async () => {
    await submitForm();

    expect(login).not.toHaveBeenCalled();
    expect(element.textContent).toContain('Informe o login.');
  });

  it('autentica e navega para o perfil', async () => {
    login.mockResolvedValue(undefined);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    type('#userName', 'ana.souza');
    type('#password', 'Senha@123');

    await submitForm();

    expect(login).toHaveBeenCalledWith('ana.souza', 'Senha@123');
    expect(navigate).toHaveBeenCalledWith(['/profile']);
  });

  it('mostra mensagem genérica quando as credenciais são inválidas', async () => {
    login.mockRejectedValue(
      new HttpErrorResponse({
        status: 401,
        error: { errors: { credentials: ['auth.invalid_credentials'] } },
      }),
    );
    type('#userName', 'ana.souza');
    type('#password', 'errada');

    await submitForm();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'Login ou senha inválidos.',
    );
  });

  it('exibe confirmação de cadastro quando vem do registro', async () => {
    fixture.componentRef.setInput('registered', '1');
    await fixture.whenStable();

    expect(element.textContent).toContain('Cadastro realizado!');
  });
});
