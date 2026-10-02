import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RequestType, RequestTypeSummary } from './api.models';

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);

  list(): Observable<RequestTypeSummary[]> {
    return this.http.get<RequestTypeSummary[]>('/api/request-types');
  }

  get(id: string): Observable<RequestType> {
    return this.http.get<RequestType>(`/api/request-types/${encodeURIComponent(id)}`);
  }
}
