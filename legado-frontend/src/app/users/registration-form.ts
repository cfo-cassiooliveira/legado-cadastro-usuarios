import { email, maxLength, pattern, required, schema, validate } from '@angular/forms/signals';
import { hasSurname, isStrongPassword, isValidMobilePhone } from '../shared/brazil';
import { digitsOnly } from '../shared/masks';
import { RegisterUserRequest } from './user.model';

/** Modelo do formulário de cadastro (valores como digitados, com máscara). */
export interface RegistrationModel {
  fullName: string;
  userName: string;
  password: string;
  passwordConfirmation: string;
  email: string;
  mobilePhone: string;
  postalCode: string;
  street: string;
  number: string;
  complement: string;
  district: string;
  state: string;
  /** Código IBGE do município como texto (valor do `<select>`). */
  cityIbgeCode: string;
}

export const EMPTY_REGISTRATION: RegistrationModel = {
  fullName: '',
  userName: '',
  password: '',
  passwordConfirmation: '',
  email: '',
  mobilePhone: '',
  postalCode: '',
  street: '',
  number: '',
  complement: '',
  district: '',
  state: '',
  cityIbgeCode: '',
};

const FULL_NAME_MAX = 150;
const COMPLEMENT_MAX = 100;

/** Validações de cliente espelhando as regras do backend (o backend continua sendo a fonte da verdade). */
export const registrationSchema = schema<RegistrationModel>((path) => {
  required(path.fullName, { message: $localize`Informe o nome completo.` });
  maxLength(path.fullName, FULL_NAME_MAX, { message: $localize`Nome muito longo.` });
  validate(path.fullName, ({ value }) =>
    !value() || hasSurname(value())
      ? null
      : { kind: 'surname', message: $localize`Informe nome e sobrenome.` },
  );

  required(path.userName, { message: $localize`Informe o login.` });
  pattern(path.userName, /^[A-Za-z0-9][A-Za-z0-9._-]{3,29}$/, {
    message: $localize`De 4 a 30 caracteres: letras, números, ponto, hífen ou sublinhado.`,
  });

  required(path.password, { message: $localize`Informe a senha.` });
  validate(path.password, ({ value }) =>
    !value() || isStrongPassword(value())
      ? null
      : {
          kind: 'weakPassword',
          message: $localize`Mínimo 8 caracteres com maiúscula, minúscula, número e símbolo.`,
        },
  );
  validate(path.passwordConfirmation, ({ value, valueOf }) =>
    value() === valueOf(path.password)
      ? null
      : { kind: 'passwordMismatch', message: $localize`As senhas não conferem.` },
  );

  required(path.email, { message: $localize`Informe o e-mail.` });
  email(path.email, { message: $localize`E-mail inválido.` });

  required(path.mobilePhone, { message: $localize`Informe o celular.` });
  validate(path.mobilePhone, ({ value }) =>
    !value() || isValidMobilePhone(value())
      ? null
      : { kind: 'mobilePhone', message: $localize`Celular inválido. Use DDD + 9 dígitos.` },
  );

  required(path.postalCode, { message: $localize`Informe o CEP.` });
  pattern(path.postalCode, /^\d{5}-?\d{3}$/, { message: $localize`CEP inválido.` });
  required(path.street, { message: $localize`Informe o logradouro.` });
  required(path.number, { message: $localize`Informe o número.` });
  maxLength(path.complement, COMPLEMENT_MAX, { message: $localize`Complemento muito longo.` });
  required(path.district, { message: $localize`Informe o bairro.` });
  required(path.state, { message: $localize`Selecione a UF.` });
  required(path.cityIbgeCode, { message: $localize`Selecione a cidade.` });
});

/** Converte o modelo do formulário no contrato da API (sem máscaras, sem confirmação de senha). */
export function toRegisterRequest(model: RegistrationModel): RegisterUserRequest {
  return {
    fullName: model.fullName.trim(),
    userName: model.userName.trim(),
    password: model.password,
    email: model.email.trim(),
    mobilePhone: digitsOnly(model.mobilePhone),
    postalCode: digitsOnly(model.postalCode),
    street: model.street.trim(),
    number: model.number.trim(),
    complement: model.complement.trim() || null,
    district: model.district.trim(),
    cityIbgeCode: Number(model.cityIbgeCode),
    state: model.state,
  };
}
