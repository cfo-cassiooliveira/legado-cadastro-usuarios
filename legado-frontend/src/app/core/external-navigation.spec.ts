import { DOCUMENT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ExternalNavigation } from './external-navigation';

describe('ExternalNavigation', () => {
  it('troca a página do navegador pelo endereço informado', () => {
    const assign = vi.fn();
    TestBed.configureTestingModule({
      providers: [{ provide: DOCUMENT, useValue: { location: { assign } } }],
    });

    TestBed.inject(ExternalNavigation).go('http://localhost:4300/');

    expect(assign).toHaveBeenCalledWith('http://localhost:4300/');
  });
});
