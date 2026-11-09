import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { form } from '@angular/forms/signals';
import {
  EMPTY_REGISTRATION,
  RegistrationModel,
  registrationSchema,
  toRegisterRequest,
} from './registration-form';

export const VALID_REGISTRATION: RegistrationModel = {
  fullName: 'Ana Paula Souza',
  userName: 'ana.souza',
  password: 'Senha@123',
  passwordConfirmation: 'Senha@123',
  email: 'ana@exemplo.com',
  mobilePhone: '(16) 99123-4567',
  postalCode: '01001-000',
  street: 'Praça da Sé',
  number: '100',
  complement: ' ',
  district: 'Sé',
  state: 'SP',
  cityIbgeCode: '3550308',
};

describe('registration form', () => {
  function createForm(model: RegistrationModel) {
    return TestBed.runInInjectionContext(() => form(signal(model), registrationSchema));
  }

  it('aceita um cadastro válido', () => {
    expect(createForm(VALID_REGISTRATION)().valid()).toBe(true);
  });

  it('formulário vazio é inválido', () => {
    expect(createForm({ ...EMPTY_REGISTRATION })().valid()).toBe(false);
  });

  it.each<[keyof RegistrationModel, string, string]>([
    ['fullName', 'Ana', 'surname'],
    ['password', 'fraca', 'weakPassword'],
    ['passwordConfirmation', 'Outra@123', 'passwordMismatch'],
    ['mobilePhone', '(16) 89123-4567', 'mobilePhone'],
    ['postalCode', '0100', 'pattern'],
    ['userName', 'a b', 'pattern'],
    ['email', 'invalido', 'email'],
  ])('rejeita %s = "%s"', (field, value, kind) => {
    const registration = createForm({ ...VALID_REGISTRATION, [field]: value });

    expect(
      registration[field]()
        .errors()
        .map((e) => e.kind),
    ).toContain(kind);
  });

  it('toRegisterRequest remove máscaras e confirmação de senha', () => {
    const request = toRegisterRequest(VALID_REGISTRATION);

    expect(request).toEqual({
      fullName: 'Ana Paula Souza',
      userName: 'ana.souza',
      password: 'Senha@123',
      email: 'ana@exemplo.com',
      mobilePhone: '16991234567',
      postalCode: '01001000',
      street: 'Praça da Sé',
      number: '100',
      complement: null,
      district: 'Sé',
      cityIbgeCode: 3550308,
      state: 'SP',
    });
    expect('passwordConfirmation' in request).toBe(false);
  });
});
