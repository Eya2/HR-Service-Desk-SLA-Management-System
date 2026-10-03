import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RequestType, RequestTypeSummary, SaveRequestType } from './api.models';

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);

  list(): Observable<RequestTypeSummary[]> {
    return this.http.get<RequestTypeSummary[]>('/api/request-types');
  }

  /** HR Admin: the whole catalog, retired types included. */
  listAll(): Observable<RequestTypeSummary[]> {
    return this.http.get<RequestTypeSummary[]>('/api/request-types', { params: { includeInactive: true } });
  }

  save(id: string | null, type: SaveRequestType): Observable<RequestType> {
    return id
      ? this.http.put<RequestType>(`/api/request-types/${encodeURIComponent(id)}`, type)
      : this.http.post<RequestType>('/api/request-types', type);
  }

  setActive(id: string, isActive: boolean): Observable<void> {
    return this.http.put<void>(`/api/request-types/${encodeURIComponent(id)}/active`, { isActive });
  }

  get(id: string): Observable<RequestType> {
    return this.http.get<RequestType>(`/api/request-types/${encodeURIComponent(id)}`);
  }
}
