import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AttachmentInfo, CommentInfo, Paged, TicketDetails, TicketSummary } from './api.models';

export interface SubmitTicket {
  requestTypeId: string;
  title: string;
  description: string;
  /** Answers to non-file fields. */
  values: Record<string, unknown>;
  /** Files keyed by the form field they answer. */
  files: Record<string, File[]>;
}

export interface TicketListQuery {
  status?: string | null;
  search?: string | null;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class TicketsApi {
  private readonly http = inject(HttpClient);

  /** One multipart call: a JSON `payload` part plus one part per file, named after its field key. */
  submit(request: SubmitTicket): Observable<{ id: string; reference: string }> {
    const body = new FormData();
    const { files, ...payload } = request;
    body.append('payload', JSON.stringify(payload));
    for (const [field, list] of Object.entries(files)) {
      for (const file of list) {
        body.append(field, file, file.name);
      }
    }
    return this.http.post<{ id: string; reference: string }>('/api/tickets', body);
  }

  mine(query: TicketListQuery): Observable<Paged<TicketSummary>> {
    let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    if (query.status) params = params.set('status', query.status);
    if (query.search) params = params.set('search', query.search);
    return this.http.get<Paged<TicketSummary>>('/api/tickets/mine', { params });
  }

  get(id: string): Observable<TicketDetails> {
    return this.http.get<TicketDetails>(`/api/tickets/${encodeURIComponent(id)}`);
  }

  update(id: string, change: { title: string; description: string; priority: string | null }): Observable<void> {
    return this.http.put<void>(`/api/tickets/${encodeURIComponent(id)}`, change);
  }

  changeStatus(id: string, status: string, reason: string | null): Observable<void> {
    return this.http.post<void>(`/api/tickets/${encodeURIComponent(id)}/status`, { status, reason });
  }

  addComment(id: string, body: string, isInternal: boolean): Observable<CommentInfo> {
    return this.http.post<CommentInfo>(`/api/tickets/${encodeURIComponent(id)}/comments`, { body, isInternal });
  }

  addAttachments(id: string, files: File[]): Observable<AttachmentInfo[]> {
    const body = new FormData();
    files.forEach((file) => body.append('files', file, file.name));
    return this.http.post<AttachmentInfo[]>(`/api/tickets/${encodeURIComponent(id)}/attachments`, body);
  }

  /** Downloads with the bearer token (a plain link could not send it) and saves the file. */
  download(ticketId: string, attachment: AttachmentInfo): void {
    this.http
      .get(`/api/tickets/${encodeURIComponent(ticketId)}/attachments/${encodeURIComponent(attachment.id)}`, {
        responseType: 'blob',
      })
      .subscribe((blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = attachment.fileName;
        link.click();
        URL.revokeObjectURL(url);
      });
  }
}
