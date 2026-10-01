import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface SystemInfo {
  name: string;
  version: string;
  environment: string;
}

/** Reads service metadata from the backend (`GET /api/system/info`). */
@Injectable({ providedIn: 'root' })
export class SystemApi {
  private readonly http = inject(HttpClient);

  getInfo(): Observable<SystemInfo> {
    return this.http.get<SystemInfo>('/api/system/info');
  }
}
