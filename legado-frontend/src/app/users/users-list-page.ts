import { DatePipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { initialsOf } from '../shared/initials';
import { PagedResult, UserSummary } from './user.model';
import { usersPageUrl } from './users-api';

const PAGE_SIZE = 10;

/** Listagem administrativa paginada (base de usuários a migrar para o Keycloak). */
@Component({
  selector: 'app-users-list-page',
  imports: [DatePipe],
  templateUrl: './users-list-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsersListPage {
  protected readonly initialsOf = initialsOf;
  protected readonly page = signal(1);
  protected readonly users = httpResource<PagedResult<UserSummary>>(() =>
    usersPageUrl(this.page(), PAGE_SIZE),
  );

  protected readonly totalPages = computed(() => {
    const result = this.users.hasValue() ? this.users.value() : undefined;
    return result ? Math.max(Math.ceil(result.totalCount / result.pageSize), 1) : 1;
  });

  protected previousPage(): void {
    this.page.update((current) => Math.max(current - 1, 1));
  }

  protected nextPage(): void {
    this.page.update((current) => Math.min(current + 1, this.totalPages()));
  }
}
