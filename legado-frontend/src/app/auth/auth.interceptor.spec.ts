import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import {
  CORRELATION_ID_HEADER,
  correlationIdInterceptor,
} from '../core/correlation-id.interceptor';
import { AuthStore } from './auth-store';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  const token = signal<string | null>('jwt');
  const clearSession = vi.fn();
  const navigate = vi.fn().mockResolvedValue(true);
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    token.set('jwt');
    clearSession.mockClear();
    navigate.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([correlationIdInterceptor, authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: { accessToken: token, clearSession } },
        { provide: Router, useValue: { navigate } },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('anexa Bearer e correlation id nas chamadas da API', () => {
    void firstValueFrom(http.get('/api/v1/users/me'));

    const request = controller.expectOne('/api/v1/users/me').request;
    expect(request.headers.get('Authorization')).toBe('Bearer jwt');
    expect(request.headers.get(CORRELATION_ID_HEADER)).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('não envia o token para outras origens', () => {
    void firstValueFrom(http.get('https://viacep.com.br/ws/01001000/json/'));

    expect(controller.expectOne(() => true).request.headers.has('Authorization')).toBe(false);
  });

  it('sem sessão não envia Authorization', () => {
    token.set(null);
    void firstValueFrom(http.get('/api/v1/users/me'));

    expect(controller.expectOne('/api/v1/users/me').request.headers.has('Authorization')).toBe(
      false,
    );
  });

  it('401 encerra a sessão e volta ao login', async () => {
    const call = firstValueFrom(http.get('/api/v1/users/me'));
    controller
      .expectOne('/api/v1/users/me')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(call).rejects.toBeTruthy();
    expect(clearSession).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
