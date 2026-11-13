import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Painel de apresentação exibido ao lado dos formulários de login e cadastro.
 * O destaque é um parágrafo, não um título: o único h1 da página é o do formulário (o assunto da tela).
 */
@Component({
  selector: 'app-auth-hero',
  template: `
    <aside class="auth-hero">
      <p class="auth-hero__headline">{{ headline() }}</p>
      <p>{{ description() }}</p>
      <ul>
        <li i18n>Endereço validado na base de CEPs dos Correios</li>
        <li i18n>Cidades oficiais do IBGE</li>
        <li i18n>Senha protegida com PBKDF2-SHA512</li>
      </ul>
    </aside>
  `,
  styles: `
    :host {
      display: contents;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthHero {
  readonly headline = input.required<string>();
  readonly description = input.required<string>();
}
