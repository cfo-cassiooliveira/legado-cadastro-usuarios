/** Contrato de `POST /api/v1/auth/login`. */
export interface LoginResponse {
  readonly accessToken: string;
  readonly tokenType: string;
  readonly expiresAt: string;
}

/** Perfil administrativo, com o mesmo nome usado pelo backend. */
export const ADMIN_ROLE = 'Admin';
