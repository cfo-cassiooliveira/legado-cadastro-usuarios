import { Provider } from '@angular/core';
import { AppLogger } from './app-logger';

/** Espião do AppLogger para os testes verificarem o nível de cada evento (regra de log). */
export interface LoggerSpy {
  readonly error: ReturnType<typeof vi.fn>;
  readonly warning: ReturnType<typeof vi.fn>;
  readonly info: ReturnType<typeof vi.fn>;
}

/** Cria o espião com os três níveis. */
export function createLoggerSpy(): LoggerSpy {
  return { error: vi.fn(), warning: vi.fn(), info: vi.fn() };
}

/** Substitui o {@link AppLogger} real pelo espião no TestBed. */
export function provideLoggerSpy(spy: LoggerSpy): Provider {
  return { provide: AppLogger, useValue: spy };
}
