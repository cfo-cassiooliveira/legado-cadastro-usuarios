import { isExpectedFailure } from './expected-failures';

describe('isExpectedFailure', () => {
  it.each([400, 401, 404, 409, 429])('%i é falha prevista (warning)', (status) => {
    expect(isExpectedFailure(status)).toBe(true);
  });

  it.each([0, 403, 500, 502, 503])('%i é falha inesperada (error)', (status) => {
    expect(isExpectedFailure(status)).toBe(false);
  });
});
