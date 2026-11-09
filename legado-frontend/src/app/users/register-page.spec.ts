import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RegisterPage } from './register-page';
import { VALID_REGISTRATION } from './registration-form.spec';

const CITIES_SP = [
  { ibgeCode: 3509502, name: 'Campinas' },
  { ibgeCode: 3550308, name: 'São Paulo' },
];

describe('RegisterPage (wizard)', () => {
  let fixture: ComponentFixture<RegisterPage>;
  let element: HTMLElement;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [RegisterPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(RegisterPage);
    element = fixture.nativeElement as HTMLElement;
    http = TestBed.inject(HttpTestingController);
    await fixture.whenStable();
  });

  afterEach(() => http.verify());

  function query<T extends Element>(selector: string): T {
    const found = element.querySelector<T>(selector);
    if (!found) throw new Error(`Element ${selector} not found`);
    return found;
  }

  function type(selector: string, value: string): void {
    const target = query<HTMLInputElement | HTMLSelectElement>(selector);
    target.value = value;
    target.dispatchEvent(new Event(target instanceof HTMLSelectElement ? 'change' : 'input'));
  }

  /** Drena microtasks e roda a detecção de mudanças sem esperar requests pendentes (whenStable aguardaria o httpResource). */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    fixture.detectChanges();
  }

  async function clickNext(): Promise<void> {
    query<HTMLButtonElement>('[data-testid="next"]').click();
    await settle();
  }

  function currentStepTitle(): string {
    return query('.step-title h2').textContent?.trim() ?? '';
  }

  async function completePersonalStep(): Promise<void> {
    type('#fullName', VALID_REGISTRATION.fullName);
    type('#email', VALID_REGISTRATION.email);
    type('#mobilePhone', '16991234567');
    await settle();
    await clickNext();
  }

  async function completeAccessStep(): Promise<void> {
    type('#userName', VALID_REGISTRATION.userName);
    type('#password', VALID_REGISTRATION.password);
    type('#passwordConfirmation', VALID_REGISTRATION.password);
    await settle();
    await clickNext();
  }

  async function lookupSe(): Promise<void> {
    type('#postalCode', '01001000');
    http.expectOne('/api/v1/addresses/postal-codes/01001000').flush({
      postalCode: '01001000',
      street: 'Praça da Sé',
      district: 'Sé',
      city: 'São Paulo',
      cityIbgeCode: 3550308,
      state: 'SP',
    });
    await settle();
    http.expectOne('/api/v1/addresses/states/SP/cities').flush(CITIES_SP);
    await settle();
  }

  async function reachReview(): Promise<void> {
    await completePersonalStep();
    await completeAccessStep();
    await lookupSe();
    type('#number', '100');
    await settle();
    await clickNext();
  }

  async function confirmAndRespond(body: object, status: number): Promise<void> {
    query<HTMLButtonElement>('[data-testid="confirm"]').click();
    await settle();
    const request = http.expectOne('/api/v1/users');
    expect(request.request.body).toMatchObject({
      mobilePhone: '16991234567',
      postalCode: '01001000',
      cityIbgeCode: 3550308,
    });
    request.flush(body, { status, statusText: String(status) });
    await settle();
  }

  it('começa no passo de dados pessoais', () => {
    expect(currentStepTitle()).toBe('Dados pessoais');
    expect(element.querySelector('[aria-current="step"]')?.textContent).toContain('1');
  });

  it('não avança com o passo inválido e mostra os erros', async () => {
    await clickNext();

    expect(currentStepTitle()).toBe('Dados pessoais');
    expect(element.textContent).toContain('Informe o nome completo.');
  });

  it('avança passo a passo e permite voltar', async () => {
    await completePersonalStep();
    expect(currentStepTitle()).toBe('Acesso');

    query<HTMLButtonElement>('.btn--ghost').click();
    await settle();

    expect(currentStepTitle()).toBe('Dados pessoais');
    expect(query<HTMLInputElement>('#fullName').value).toBe(VALID_REGISTRATION.fullName);
  });

  it('aplica máscara ao celular', async () => {
    type('#mobilePhone', '16991234567');
    await settle();

    expect(query<HTMLInputElement>('#mobilePhone').value).toBe('(16) 99123-4567');
  });

  it('mostra o checklist da política de senha', async () => {
    await completePersonalStep();
    type('#password', 'Senha1');
    await settle();

    expect(element.querySelectorAll('.password-rules li.ok').length).toBe(3);
  });

  it('preenche o endereço e a cidade a partir do CEP', async () => {
    await completePersonalStep();
    await completeAccessStep();
    await lookupSe();

    expect(query<HTMLInputElement>('#postalCode').value).toBe('01001-000');
    expect(query<HTMLInputElement>('#street').value).toBe('Praça da Sé');
    expect(query<HTMLInputElement>('#district').value).toBe('Sé');
    expect(query<HTMLSelectElement>('#state').value).toBe('SP');
    expect(query<HTMLSelectElement>('#city').value).toBe('3550308');
    expect(element.textContent).toContain('Endereço encontrado');
  });

  it('informa CEP inexistente', async () => {
    await completePersonalStep();
    await completeAccessStep();
    type('#postalCode', '99999-999');
    http
      .expectOne('/api/v1/addresses/postal-codes/99999999')
      .flush({}, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(element.textContent).toContain('CEP não encontrado nos Correios.');
  });

  it('revisão mostra os dados e envia o cadastro sem máscaras', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await reachReview();

    expect(currentStepTitle()).toBe('Revisão');
    expect(element.textContent).toContain('São Paulo/SP');
    expect(element.textContent).toContain(VALID_REGISTRATION.userName);

    await confirmAndRespond({ id: 'new-id' }, 201);

    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { registered: 1 } });
  });

  it('erro de duplicidade da API volta ao passo do campo com a mensagem', async () => {
    await reachReview();

    await confirmAndRespond({ errors: { userName: ['user_name.taken'] } }, 409);

    expect(currentStepTitle()).toBe('Acesso');
    expect(element.textContent).toContain('Este login já está em uso.');
  });

  it('exibe mensagem geral quando o serviço de endereços está indisponível', async () => {
    await reachReview();

    await confirmAndRespond({ code: 'address_service.unavailable' }, 503);

    expect(query('.alert--error').textContent).toContain('Serviço de endereços indisponível');
  });
});
