import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { digitsOnly } from '../shared/masks';
import { PostalCodeAddress } from './address.model';

/** Consulta de CEP (ViaCEP/Correios) via backend. */
@Injectable({ providedIn: 'root' })
export class AddressApi {
  private readonly http = inject(HttpClient);

  lookupPostalCode(postalCode: string): Observable<PostalCodeAddress> {
    return this.http.get<PostalCodeAddress>(
      `/api/v1/addresses/postal-codes/${digitsOnly(postalCode)}`,
    );
  }
}

/** URL da lista de municípios de uma UF (usada com httpResource). */
export function citiesUrl(state: string): string {
  return `/api/v1/addresses/states/${encodeURIComponent(state)}/cities`;
}
