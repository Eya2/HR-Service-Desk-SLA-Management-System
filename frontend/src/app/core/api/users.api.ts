import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Paged } from './api.models';

export interface UserSummary {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  isActive: boolean;
  lastLoginAt: string | null;
}

export interface UserDetails {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  managerId: string | null;
  managerName: string | null;
  isActive: boolean;
  lastLoginAt: string | null;
  createdAt: string;
}

/** Roles an HR Admin can grant (SuperAdmin is platform-only). */
export const ASSIGNABLE_ROLES = ['Employee', 'Manager', 'HrOfficer', 'PayrollSpecialist', 'HrAdmin', 'Auditor'] as const;

/** HR Admin: the organisation's accounts. */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(query: { search: string; role: string | null; page: number; pageSize: number }): Observable<Paged<UserSummary>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.search.trim()) params = params.set('search', query.search.trim());
    if (query.role) params = params.set('role', query.role);
    return this.http.get<Paged<UserSummary>>('/api/users', { params });
  }

  get(id: string): Observable<UserDetails> {
    return this.http.get<UserDetails>(`/api/users/${encodeURIComponent(id)}`);
  }

  create(user: { email: string; firstName: string; lastName: string; roles: string[]; managerId: string | null; password: string }): Observable<UserDetails> {
    return this.http.post<UserDetails>('/api/users', user);
  }

  update(id: string, user: { firstName: string; lastName: string; roles: string[]; managerId: string | null; isActive: boolean }): Observable<UserDetails> {
    return this.http.put<UserDetails>(`/api/users/${encodeURIComponent(id)}`, user);
  }

  resetPassword(id: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`/api/users/${encodeURIComponent(id)}/reset-password`, { newPassword });
  }
}
