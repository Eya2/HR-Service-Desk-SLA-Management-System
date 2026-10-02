import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Paged } from './api.models';

export interface ApiKeyInfo {
  id: string;
  name: string;
  prefix: string;
  scopes: string[];
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

export interface WebhookInfo {
  id: string;
  name: string;
  url: string;
  events: string[];
  isActive: boolean;
  createdAt: string;
  pendingDeliveries: number;
  failedDeliveries: number;
  lastDeliveredAt: string | null;
}

export interface WebhookDelivery {
  id: string;
  eventType: string;
  ticketId: string | null;
  status: 'Pending' | 'Succeeded' | 'Failed';
  attempts: number;
  createdAt: string;
  lastAttemptAt: string | null;
  nextAttemptAt: string | null;
  lastStatusCode: number | null;
  lastError: string | null;
  payload: string;
}

export interface SaveWebhook {
  name: string;
  url: string;
  events: string[];
  isActive: boolean;
  rotateSecret: boolean;
}

/** HR Admin: API keys and webhook subscriptions. */
@Injectable({ providedIn: 'root' })
export class IntegrationsApi {
  private readonly http = inject(HttpClient);

  catalog(): Observable<{ scopes: string[]; events: string[] }> {
    return this.http.get<{ scopes: string[]; events: string[] }>('/api/integrations/catalog');
  }

  keys(): Observable<ApiKeyInfo[]> {
    return this.http.get<ApiKeyInfo[]>('/api/integrations/api-keys');
  }

  createKey(name: string, scopes: string[]): Observable<{ key: ApiKeyInfo; secret: string }> {
    return this.http.post<{ key: ApiKeyInfo; secret: string }>('/api/integrations/api-keys', { name, scopes });
  }

  revokeKey(id: string): Observable<void> {
    return this.http.delete<void>(`/api/integrations/api-keys/${encodeURIComponent(id)}`);
  }

  webhooks(): Observable<WebhookInfo[]> {
    return this.http.get<WebhookInfo[]>('/api/integrations/webhooks');
  }

  saveWebhook(id: string | null, webhook: SaveWebhook): Observable<{ webhook: WebhookInfo; secret: string | null }> {
    return id
      ? this.http.put<{ webhook: WebhookInfo; secret: string | null }>(`/api/integrations/webhooks/${encodeURIComponent(id)}`, webhook)
      : this.http.post<{ webhook: WebhookInfo; secret: string | null }>('/api/integrations/webhooks', webhook);
  }

  deleteWebhook(id: string): Observable<void> {
    return this.http.delete<void>(`/api/integrations/webhooks/${encodeURIComponent(id)}`);
  }

  deliveries(id: string, page = 1): Observable<Paged<WebhookDelivery>> {
    return this.http.get<Paged<WebhookDelivery>>(`/api/integrations/webhooks/${encodeURIComponent(id)}/deliveries`, {
      params: new HttpParams().set('page', page).set('pageSize', 20),
    });
  }

  ping(id: string): Observable<void> {
    return this.http.post<void>(`/api/integrations/webhooks/${encodeURIComponent(id)}/ping`, null);
  }

  redeliver(deliveryId: string): Observable<void> {
    return this.http.post<void>(`/api/integrations/deliveries/${encodeURIComponent(deliveryId)}/redeliver`, null);
  }
}
