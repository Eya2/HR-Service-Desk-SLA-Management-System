import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Classification, DraftReply } from './api.models';

/** The request assistant (Claude when configured on the server, a local classifier otherwise). */
@Injectable({ providedIn: 'root' })
export class AssistantApi {
  private readonly http = inject(HttpClient);

  /** The request type, title and answers that fit what the employee wrote. */
  classify(text: string): Observable<Classification> {
    return this.http.post<Classification>('/api/assistant/classify', { text });
  }

  /** A reply for HR to review; nothing is posted. */
  draftReply(ticketId: string): Observable<DraftReply> {
    return this.http.post<DraftReply>(`/api/tickets/${encodeURIComponent(ticketId)}/draft-reply`, null);
  }
}

/** What the assistant prepared for the request form, handed over when the employee continues. */
export interface RequestPrefill {
  requestTypeId: string;
  title: string;
  description: string;
  values: Record<string, unknown>;
}

/** Carries the assistant's pre-filled answers from the catalog to the request form (once). */
@Injectable({ providedIn: 'root' })
export class RequestPrefillStore {
  private pending: RequestPrefill | null = null;

  set(prefill: RequestPrefill): void {
    this.pending = prefill;
  }

  /** The prefill for this request type, if any; it is consumed. */
  take(requestTypeId: string): RequestPrefill | null {
    const prefill = this.pending?.requestTypeId === requestTypeId ? this.pending : null;
    this.pending = null;
    return prefill;
  }
}
