import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FieldTree, form, FormField, submit, TreeValidationResult } from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AddressApi, citiesUrl } from '../addresses/address-api';
import { City, PostalCodeAddress } from '../addresses/address.model';
import { messageForCode, messageForProblem } from '../core/error-messages';
import { ApiProblem, toApiProblem } from '../core/problem-details';
import { AuthHero } from '../shared/auth-hero';
import { BRAZILIAN_STATES, PASSWORD_RULES } from '../shared/brazil';
import { FieldErrors, showInvalid } from '../shared/field-errors';
import { formatMobilePhone, formatPostalCode, isCompletePostalCode } from '../shared/masks';
import {
  EMPTY_REGISTRATION,
  RegistrationModel,
  registrationSchema,
  toRegisterRequest,
} from './registration-form';
import { REGISTRATION_STEPS, stepOfField } from './registration-steps';
import { UsersApi } from './users-api';

type PostalCodeStatus = 'idle' | 'loading' | 'found' | 'not-found' | 'error';

const HTTP_NOT_FOUND = 404;
const REVIEW_STEP = REGISTRATION_STEPS.length - 1;

/** Cadastro em assistente (wizard): cada passo valida seus campos antes de avançar. */
@Component({
  selector: 'app-register-page',
  imports: [FormField, RouterLink, FieldErrors, AuthHero],
  templateUrl: './register-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPage {
  private readonly usersApi = inject(UsersApi);
  private readonly addressApi = inject(AddressApi);
  private readonly router = inject(Router);

  protected readonly steps = REGISTRATION_STEPS;
  protected readonly reviewStep = REVIEW_STEP;
  protected readonly states = BRAZILIAN_STATES;
  protected readonly showInvalid = showInvalid;

  protected readonly model = signal<RegistrationModel>({ ...EMPTY_REGISTRATION });
  protected readonly registrationForm = form(this.model, registrationSchema);
  protected readonly currentStep = signal(0);
  protected readonly postalCodeStatus = signal<PostalCodeStatus>('idle');
  protected readonly formError = signal<string | null>(null);

  protected readonly step = computed(() => this.steps[this.currentStep()]);

  protected readonly passwordRules = computed(() => {
    const password = this.model().password;
    return PASSWORD_RULES.map((rule) => ({ label: rule.label, ok: rule.test(password) }));
  });
  protected readonly passwordStrength = computed(() => {
    const rules = this.passwordRules();
    return Math.round((rules.filter((rule) => rule.ok).length / rules.length) * 100);
  });

  private readonly selectedState = computed(() => this.model().state);
  protected readonly cities = httpResource<City[]>(() => {
    const state = this.selectedState();
    return state ? citiesUrl(state) : undefined;
  });
  protected readonly cityOptions = computed<readonly City[]>(() =>
    this.cities.hasValue() ? this.cities.value() : [],
  );
  protected readonly cityName = computed(
    () =>
      this.cityOptions().find((city) => String(city.ibgeCode) === this.model().cityIbgeCode)
        ?.name ?? '',
  );

  /** Valida os campos do passo atual; só avança se todos estiverem válidos. */
  protected nextStep(): void {
    const fields = this.step().fields.map((name) => this.registrationForm[name]());
    fields.forEach((field) => field.markAsTouched());
    if (fields.every((field) => field.valid())) {
      this.currentStep.update((index) => Math.min(index + 1, REVIEW_STEP));
    }
  }

  protected previousStep(): void {
    this.currentStep.update((index) => Math.max(index - 1, 0));
  }

  /** Permite voltar a um passo já visitado (ex.: "Editar" na revisão). */
  protected goToStep(index: number): void {
    if (index < this.currentStep()) {
      this.currentStep.set(index);
    }
  }

  protected applyMobileMask(): void {
    const field = this.registrationForm.mobilePhone().value;
    field.set(formatMobilePhone(field()));
  }

  /** Aplica a máscara e, com 8 dígitos, consulta o CEP para preencher o endereço. */
  protected async lookupPostalCode(): Promise<void> {
    const field = this.registrationForm.postalCode().value;
    field.set(formatPostalCode(field()));
    if (!isCompletePostalCode(field())) {
      this.postalCodeStatus.set('idle');
      return;
    }

    this.postalCodeStatus.set('loading');
    try {
      const address = await firstValueFrom(this.addressApi.lookupPostalCode(field()));
      this.fillAddress(address);
      this.postalCodeStatus.set('found');
    } catch (error) {
      this.postalCodeStatus.set(
        toApiProblem(error).status === HTTP_NOT_FOUND ? 'not-found' : 'error',
      );
    }
  }

  /** Ao trocar de UF manualmente, a cidade anterior deixa de ser válida. */
  protected resetCity(): void {
    this.registrationForm.cityIbgeCode().value.set('');
  }

  protected async register(event: Event): Promise<void> {
    event.preventDefault();
    if (this.currentStep() !== REVIEW_STEP) {
      // Enter em um campo intermediário equivale a "Continuar".
      this.nextStep();
      return;
    }
    this.formError.set(null);

    await submit(this.registrationForm, async (fields) => {
      try {
        await firstValueFrom(this.usersApi.register(toRegisterRequest(this.model())));
        await this.router.navigate(['/login'], { queryParams: { registered: 1 } });
        return undefined;
      } catch (error) {
        return this.toFieldErrors(toApiProblem(error), fields);
      }
    });
  }

  private fillAddress(address: PostalCodeAddress): void {
    // Logradouro/bairro vazios (CEP geral de cidade) mantêm o que o usuário digitou.
    this.model.update((current) => ({
      ...current,
      street: address.street || current.street,
      district: address.district || current.district,
      state: address.state,
      cityIbgeCode: String(address.cityIbgeCode),
    }));
  }

  /** Erros por campo voltam ao formulário (e ao passo do campo); os demais viram mensagem geral. */
  private toFieldErrors(
    problem: ApiProblem,
    fields: FieldTree<RegistrationModel>,
  ): TreeValidationResult {
    // FieldTree é um Proxy (o operador `in` não o enxerga); usamos os campos conhecidos do modelo.
    const entries = Object.entries(problem.fieldErrors).filter(([name]) =>
      Object.hasOwn(EMPTY_REGISTRATION, name),
    );
    const errors = entries.flatMap(([name, codes]) => {
      const fieldTree = fields[name as keyof RegistrationModel];
      return codes.map((code) => ({ kind: 'server', message: messageForCode(code), fieldTree }));
    });

    if (errors.length === 0) {
      this.formError.set(messageForProblem(problem));
      return [];
    }

    const firstStep = Math.min(...entries.map(([name]) => stepOfField(name)));
    this.currentStep.set(firstStep);
    return errors;
  }
}
