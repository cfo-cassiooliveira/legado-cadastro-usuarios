import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { RegisterUserRequest } from './user.model';

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  register(request: RegisterUserRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/v1/users', request);
  }
}

/** URL paginada da listagem administrativa (usada com httpResource). */
export function usersPageUrl(page: number, pageSize: number): string {
  return `/api/v1/users?page=${page}&pageSize=${pageSize}`;
}
