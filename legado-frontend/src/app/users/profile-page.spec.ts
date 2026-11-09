import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthStore } from '../auth/auth-store';
import { SAMPLE_USER } from '../auth/auth-store.spec';
import { ProfilePage } from './profile-page';

describe('ProfilePage', () => {
  it('exibe os dados do usuário com máscaras', async () => {
    TestBed.configureTestingModule({
      imports: [ProfilePage],
      providers: [{ provide: AuthStore, useValue: { currentUser: signal(SAMPLE_USER) } }],
    });
    const fixture = TestBed.createComponent(ProfilePage);
    await fixture.whenStable();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Ana Souza');
    expect(text).toContain('(16) 99123-4567');
    expect(text).toContain('01001-000');
    expect(text).toContain('São Paulo/SP');
  });
});
