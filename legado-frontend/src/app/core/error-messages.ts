import { ApiProblem } from './problem-details';

/** Tradução dos códigos estáveis da API para mensagens ao usuário (i18n, Art. I.3). */
const MESSAGES: Readonly<Record<string, string>> = {
  'full_name.required': $localize`Informe o nome completo.`,
  'full_name.too_long': $localize`Nome muito longo.`,
  'full_name.invalid_characters': $localize`Use apenas letras no nome.`,
  'full_name.surname_required': $localize`Informe nome e sobrenome.`,
  'user_name.required': $localize`Informe o login.`,
  'user_name.invalid_length': $localize`O login deve ter entre 4 e 30 caracteres.`,
  'user_name.invalid_characters': $localize`Use letras, números, ponto, hífen ou sublinhado no login.`,
  'user_name.taken': $localize`Este login já está em uso.`,
  'email.required': $localize`Informe o e-mail.`,
  'email.invalid': $localize`E-mail inválido.`,
  'email.taken': $localize`Este e-mail já está cadastrado.`,
  'mobile_phone.required': $localize`Informe o celular.`,
  'mobile_phone.invalid': $localize`Celular inválido. Use DDD + 9 dígitos.`,
  'mobile_phone.invalid_area_code': $localize`DDD inexistente.`,
  'password.required': $localize`Informe a senha.`,
  'password.too_short': $localize`A senha deve ter ao menos 8 caracteres.`,
  'password.requires_digit': $localize`A senha precisa de ao menos um número.`,
  'password.requires_lower': $localize`A senha precisa de uma letra minúscula.`,
  'password.requires_upper': $localize`A senha precisa de uma letra maiúscula.`,
  'password.requires_non_alphanumeric': $localize`A senha precisa de um símbolo.`,
  'postal_code.required': $localize`Informe o CEP.`,
  'postal_code.invalid': $localize`CEP inválido.`,
  'postal_code.not_found': $localize`CEP não encontrado nos Correios.`,
  'postal_code.city_mismatch': $localize`O CEP não pertence à cidade selecionada.`,
  'city.not_in_state': $localize`A cidade não pertence à UF selecionada.`,
  'state.required': $localize`Selecione a UF.`,
  'state.invalid': $localize`UF inválida.`,
  'street.required': $localize`Informe o logradouro.`,
  'number.required': $localize`Informe o número.`,
  'district.required': $localize`Informe o bairro.`,
  'complement.too_long': $localize`Complemento muito longo.`,
  'auth.invalid_credentials': $localize`Login ou senha inválidos.`,
  'address_service.unavailable': $localize`Serviço de endereços indisponível. Tente novamente em instantes.`,
};

const GENERIC_ERROR = $localize`Não foi possível concluir a operação. Tente novamente.`;
const NETWORK_ERROR = $localize`Sem comunicação com o servidor.`;
const RATE_LIMITED = $localize`Muitas tentativas. Aguarde um minuto e tente novamente.`;
const HTTP_STATUS_TOO_MANY_REQUESTS = 429;

export function messageForCode(code: string): string {
  return MESSAGES[code] ?? GENERIC_ERROR;
}

/** Mensagem geral (fora de campo) para um problema da API. */
export function messageForProblem(problem: ApiProblem): string {
  if (problem.status === 0) {
    return NETWORK_ERROR;
  }
  if (problem.status === HTTP_STATUS_TOO_MANY_REQUESTS) {
    return RATE_LIMITED;
  }
  const firstCode = problem.code ?? Object.values(problem.fieldErrors).flat()[0];
  return firstCode ? messageForCode(firstCode) : GENERIC_ERROR;
}
