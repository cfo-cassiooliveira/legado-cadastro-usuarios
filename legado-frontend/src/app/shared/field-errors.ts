import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { FieldState } from '@angular/forms/signals';

/** Exibe a primeira mensagem de erro de um campo após o usuário interagir com ele. */
@Component({
  selector: 'app-field-errors',
  template: `
    @if (message(); as text) {
      <p class="error-text" role="alert">{{ text }}</p>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FieldErrors {
  readonly state = input.required<FieldState<unknown>>();

  /** Uma mensagem por vez evita poluir o formulário. */
  protected readonly message = computed(() => {
    const state = this.state();
    return state.touched() && state.invalid() ? (state.errors()[0]?.message ?? null) : null;
  });
}

/** Indica se o campo deve ser destacado como inválido (mesma regra de exibição das mensagens). */
export function showInvalid(state: FieldState<unknown>): boolean {
  return state.touched() && state.invalid();
}
