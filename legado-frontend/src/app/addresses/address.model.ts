/** Contrato de `GET /api/v1/addresses/postal-codes/{cep}`. */
export interface PostalCodeAddress {
  readonly postalCode: string;
  readonly street: string;
  readonly district: string;
  readonly city: string;
  readonly cityIbgeCode: number;
  readonly state: string;
}

/** Contrato de `GET /api/v1/addresses/states/{uf}/cities`. */
export interface City {
  readonly ibgeCode: number;
  readonly name: string;
}
