import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UsersListPage } from './users-list-page';

describe('UsersListPage', () => {
  let fixture: ComponentFixture<UsersListPage>;
  let element: HTMLElement;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [UsersListPage],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    fixture = TestBed.createComponent(UsersListPage);
    element = fixture.nativeElement as HTMLElement;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  async function respond(url: string, body: object, status = 200): Promise<void> {
    http.expectOne(url).flush(body, { status, statusText: String(status) });
    await settle();
  }

  /** Drena microtasks e roda a detecção de mudanças sem esperar requests pendentes. */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    fixture.detectChanges();
  }

  it('mostra carregando e depois a primeira página', async () => {
    expect(element.textContent).toContain('Carregando');

    await respond('/api/v1/users?page=1&pageSize=10', {
      items: [
        {
          id: '1',
          userName: 'ana.souza',
          fullName: 'Ana Souza',
          email: 'ana@exemplo.com',
          createdAt: '2026-11-08T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 10,
      totalCount: 11,
    });

    expect(element.querySelectorAll('tbody tr').length).toBe(1);
    expect(element.textContent).toContain('Página 1 de 2');
  });

  it('navega para a próxima página', async () => {
    await respond('/api/v1/users?page=1&pageSize=10', {
      items: [],
      page: 1,
      pageSize: 10,
      totalCount: 11,
    });
    element.querySelectorAll<HTMLButtonElement>('button')[1].click();
    await settle();

    await respond('/api/v1/users?page=2&pageSize=10', {
      items: [],
      page: 2,
      pageSize: 10,
      totalCount: 11,
    });

    expect(element.textContent).toContain('Página 2 de 2');
  });

  it('mostra estado vazio', async () => {
    await respond('/api/v1/users?page=1&pageSize=10', {
      items: [],
      page: 1,
      pageSize: 10,
      totalCount: 0,
    });

    expect(element.textContent).toContain('Nenhum usuário cadastrado.');
  });

  it('mostra erro', async () => {
    await respond('/api/v1/users?page=1&pageSize=10', {}, 500);

    expect(element.textContent).toContain('Não foi possível carregar os usuários.');
  });
});
