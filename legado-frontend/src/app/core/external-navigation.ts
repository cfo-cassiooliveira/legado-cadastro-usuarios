import { DOCUMENT, inject, Injectable } from '@angular/core';

/** Navegação para fora da SPA (outro sistema); isolada para os testes não trocarem a página do runner. */
@Injectable({ providedIn: 'root' })
export class ExternalNavigation {
  private readonly document = inject(DOCUMENT);

  go(url: string): void {
    this.document.location.assign(url);
  }
}
