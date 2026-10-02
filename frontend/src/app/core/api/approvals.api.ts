import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Paged, PendingApproval, TeamStats, TicketSummary, WorkflowInfo } from './api.models';

@Injectable({ providedIn: 'root' })
export class ApprovalsApi {
  private readonly http = inject(HttpClient);

  pending(): Observable<PendingApproval[]> {
    return this.http.get<PendingApproval[]>('/api/approvals/pending');
  }

  decide(ticketId: string, approvalId: string, approve: boolean, comment: string | null): Observable<void> {
    return this.http.post<void>(
      `/api/tickets/${encodeURIComponent(ticketId)}/approvals/${encodeURIComponent(approvalId)}/decision`,
      { approve, comment },
    );
  }

  teamStats(): Observable<TeamStats> {
    return this.http.get<TeamStats>('/api/team/stats');
  }

  teamTickets(page: number, pageSize: number, status: string | null): Observable<Paged<TicketSummary>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    return this.http.get<Paged<TicketSummary>>('/api/team/tickets', { params });
  }
}

@Injectable({ providedIn: 'root' })
export class WorkflowsApi {
  private readonly http = inject(HttpClient);

  list(): Observable<WorkflowInfo[]> {
    return this.http.get<WorkflowInfo[]>('/api/workflows');
  }

  get(requestTypeId: string): Observable<WorkflowInfo> {
    return this.http.get<WorkflowInfo>(`/api/workflows/${encodeURIComponent(requestTypeId)}`);
  }

  save(requestTypeId: string, isActive: boolean, steps: { name: string; approverRole: string }[]): Observable<WorkflowInfo> {
    return this.http.put<WorkflowInfo>(`/api/workflows/${encodeURIComponent(requestTypeId)}`, { isActive, steps });
  }
}
