/** Contrato de `GET /api/v1/users/me`. */
export interface UserDetails {
  readonly id: string;
  readonly userName: string;
  readonly fullName: string;
  readonly email: string;
  readonly mobilePhone: string;
  readonly address: UserAddress;
  readonly roles: readonly string[];
  readonly createdAt: string;
}

export interface UserAddress {
  readonly postalCode: string;
  readonly street: string;
  readonly number: string;
  readonly complement: string | null;
  readonly district: string;
  readonly city: string;
  readonly cityIbgeCode: number;
  readonly state: string;
}

/** Linha de `GET /api/v1/users`. */
export interface UserSummary {
  readonly id: string;
  readonly userName: string;
  readonly fullName: string;
  readonly email: string;
  readonly createdAt: string;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

/** Corpo de `POST /api/v1/users`. */
export interface RegisterUserRequest {
  readonly fullName: string;
  readonly userName: string;
  readonly password: string;
  readonly email: string;
  readonly mobilePhone: string;
  readonly postalCode: string;
  readonly street: string;
  readonly number: string;
  readonly complement: string | null;
  readonly district: string;
  readonly cityIbgeCode: number;
  readonly state: string;
}
