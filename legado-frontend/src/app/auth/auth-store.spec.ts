import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { UserDetails } from '../users/user.model';
import { AuthStore } from './auth-store';

export const SAMPLE_USER: UserDetails = {
  id: '0199a1b2-0000-7000-8000-00000000abcd',
  userName: 'ana.souza',
  fullName: 'Ana Souza',
  email: 'ana@exemplo.com',
  mobilePhone: '+5516991234567',
  address: {
    postalCode: '01001000',
    street: 'Praça da Sé',
    number: '1',
    complement: null,
    district: 'Sé',
    city: 'São Paulo',
    cityIbgeCode: 3550308,
    state: 'SP',
  },
  roles: ['User'],
  createdAt: '2026-11-08T12:00:00Z',
};

describe('AuthStore', () => {
  let store: AuthStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(AuthStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  async function loginAs(user: UserDetails, expiresInMs = 60_000): Promise<void> {
    const login = store.login('ana.souza', 'Senha@123');
    http.expectOne('/api/v1/auth/login').flush({
      accessToken: 'jwt',
      tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    });
    await Promise.resolve();
    http.expectOne('/api/v1/users/me').flush(user);
    await login;
  }

  it('login guarda o token em memória e carrega o perfil', async () => {
    await loginAs(SAMPLE_USER);

    expect(store.isAuthenticated()).toBe(true);
    expect(store.accessToken()).toBe('jwt');
    expect(store.currentUser()?.userName).toBe('ana.souza');
    expect(store.isAdmin()).toBe(false);
    // O token nunca deve ir para o storage do navegador.
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('identifica perfil administrativo', async () => {
    await loginAs({ ...SAMPLE_USER, roles: ['Admin', 'User'] });

    expect(store.isAdmin()).toBe(true);
  });

  it('falha no login não cria sessão', async () => {
    const login = store.login('ana.souza', 'errada');
    http
      .expectOne('/api/v1/auth/login')
      .flush({ errors: {} }, { status: 401, statusText: 'Unauthorized' });

    await expect(login).rejects.toBeTruthy();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('desfaz a sessão se o perfil não puder ser carregado', async () => {
    const login = store.login('ana.souza', 'Senha@123');
    http.expectOne('/api/v1/auth/login').flush({
      accessToken: 'jwt',
      tokenType: 'Bearer',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
    });
    await Promise.resolve();
    http.expectOne('/api/v1/users/me').flush(null, { status: 500, statusText: 'Error' });

    await expect(login).rejects.toBeTruthy();
    expect(store.isAuthenticated()).toBe(false);
  });

  it('encerra a sessão quando o token expira', async () => {
    vi.useFakeTimers();
    await loginAs(SAMPLE_USER, 1_000);

    vi.advanceTimersByTime(1_001);

    expect(store.isAuthenticated()).toBe(false);
    expect(store.currentUser()).toBeNull();
  });

  it('logout revoga a sessão no servidor e limpa a sessão local', async () => {
    await loginAs(SAMPLE_USER);

    const logout = store.logout();
    http.expectOne({ method: 'POST', url: '/api/v1/auth/logout' }).flush(null, {
      status: 204,
      statusText: 'No Content',
    });
    await logout;

    expect(store.isAuthenticated()).toBe(false);
    expect(store.accessToken()).toBeNull();
  });

  it('logout limpa a sessão local mesmo se o servidor falhar', async () => {
    await loginAs(SAMPLE_USER);

    const logout = store.logout();
    http.expectOne('/api/v1/auth/logout').flush(null, { status: 500, statusText: 'Server Error' });
    await logout;

    expect(store.isAuthenticated()).toBe(false);
  });

  it('logout sem sessão não chama a API', async () => {
    await store.logout();

    http.expectNone('/api/v1/auth/logout');
    expect(store.isAuthenticated()).toBe(false);
  });
});
