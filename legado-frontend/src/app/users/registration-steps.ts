import { RegistrationModel } from './registration-form';

/** Passo do assistente de cadastro e os campos que ele valida antes de avançar. */
export interface RegistrationStep {
  readonly title: string;
  readonly description: string;
  readonly fields: readonly (keyof RegistrationModel)[];
}

export const REGISTRATION_STEPS: readonly RegistrationStep[] = [
  {
    title: $localize`Dados pessoais`,
    description: $localize`Como podemos identificar e falar com você.`,
    fields: ['fullName', 'email', 'mobilePhone'],
  },
  {
    title: $localize`Acesso`,
    description: $localize`Crie o login e a senha para entrar no sistema.`,
    fields: ['userName', 'password', 'passwordConfirmation'],
  },
  {
    title: $localize`Endereço`,
    description: $localize`Digite o CEP e completamos o restante para você.`,
    fields: ['postalCode', 'street', 'number', 'complement', 'district', 'state', 'cityIbgeCode'],
  },
  {
    title: $localize`Revisão`,
    description: $localize`Confira seus dados antes de concluir.`,
    fields: [],
  },
];

/** Índice do passo que contém o campo (para levar o usuário ao erro devolvido pela API). */
export function stepOfField(field: string): number {
  const index = REGISTRATION_STEPS.findIndex((step) =>
    step.fields.includes(field as keyof RegistrationModel),
  );
  return index;
}
