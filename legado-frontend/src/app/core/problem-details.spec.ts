import { HttpErrorResponse } from '@angular/common/http';
import { messageForCode, messageForProblem } from './error-messages';
import { toApiProblem } from './problem-details';

describe('toApiProblem', () => {
  it('lê os códigos por campo de um ValidationProblem', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { title: 'x', errors: { email: ['email.taken', 42], postalCode: 'invalid-shape' } },
    });

    const problem = toApiProblem(error);

    expect(problem.status).toBe(400);
    expect(problem.fieldErrors).toEqual({ email: ['email.taken'] });
    expect(problem.code).toBeNull();
  });

  it('lê o código geral do problema', () => {
    const error = new HttpErrorResponse({
      status: 503,
      error: { code: 'address_service.unavailable' },
    });

    expect(toApiProblem(error).code).toBe('address_service.unavailable');
  });

  it('lê o endereço do novo sistema da conta migrada', () => {
    const error = new HttpErrorResponse({
      status: 401,
      error: { newSystemUrl: 'http://localhost:4300/' },
    });

    expect(toApiProblem(error).newSystemUrl).toBe('http://localhost:4300/');
  });

  // Destino de redirecionamento: qualquer coisa além de http(s) absoluto é descartada.
  it.each(['javascript:alert(1)', 'data:text/html,oi', '/relativo', 42])(
    'descarta endereço inseguro ou inválido (%s)',
    (newSystemUrl) => {
      const error = new HttpErrorResponse({ status: 401, error: { newSystemUrl } });

      expect(toApiProblem(error).newSystemUrl).toBeNull();
    },
  );

  it('trata erro que não é HTTP como falha de rede', () => {
    expect(toApiProblem(new Error('boom'))).toEqual({
      status: 0,
      fieldErrors: {},
      code: null,
      newSystemUrl: null,
    });
  });
});

describe('error messages', () => {
  it('traduz código conhecido e usa mensagem genérica para o desconhecido', () => {
    expect(messageForCode('email.taken')).toContain('e-mail');
    expect(messageForCode('nao.existe')).toContain('Não foi possível');
  });

  it('prioriza rede, rate limit e depois o primeiro código', () => {
    expect(
      messageForProblem({ status: 0, fieldErrors: {}, code: null, newSystemUrl: null }),
    ).toContain('servidor');
    expect(
      messageForProblem({ status: 429, fieldErrors: {}, code: null, newSystemUrl: null }),
    ).toContain('Muitas tentativas');
    expect(
      messageForProblem({
        status: 401,
        fieldErrors: { credentials: ['auth.invalid_credentials'] },
        code: null,
        newSystemUrl: null,
      }),
    ).toBe('Login ou senha inválidos.');
  });
});
