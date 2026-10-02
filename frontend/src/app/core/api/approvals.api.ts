import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { CalendarInfo, Paged, PendingApproval, SlaPolicyInfo, TeamInfo, TeamStats, TicketSummary, WorkflowInfo } from './api.models';

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

@Injectable({ providedIn: 'root' })
export class TeamsApi {
  private readonly http = inject(HttpClient);

  list(): Observable<TeamInfo[]> {
    return this.http.get<TeamInfo[]>('/api/teams');
  }

  save(id: string | null, team: { name: string; strategy: string; memberIds: string[] }): Observable<TeamInfo> {
    return id
      ? this.http.put<TeamInfo>(`/api/teams/${encodeURIComponent(id)}`, team)
      : this.http.post<TeamInfo>('/api/teams', team);
  }

  setResponsibleTeam(requestTypeId: string, teamId: string | null): Observable<void> {
    return this.http.put<void>(`/api/request-types/${encodeURIComponent(requestTypeId)}/team`, { teamId });
  }

  /** HR staff accounts, for team membership (HR Admin only). */
  staff(): Observable<{ id: string; fullName: string; roles: string[] }[]> {
    return this.http
      .get<Paged<{ id: string; fullName: string; roles: string[]; isActive: boolean }>>('/api/users', { params: { pageSize: 100 } })
      .pipe(
        map((page) =>
          page.items.filter((u) => u.isActive && u.roles.some((r) => ['HrOfficer', 'PayrollSpecialist', 'HrAdmin'].includes(r))),
        ),
      );
  }
}

@Injectable({ providedIn: 'root' })
export class SlaApi {
  private readonly http = inject(HttpClient);

  calendar(): Observable<CalendarInfo> {
    return this.http.get<CalendarInfo>('/api/calendar');
  }

  addHoliday(date: string, name: string): Observable<CalendarInfo> {
    return this.http.post<CalendarInfo>('/api/calendar/holidays', { date, name });
  }

  removeHoliday(date: string): Observable<CalendarInfo> {
    return this.http.delete<CalendarInfo>(`/api/calendar/holidays/${encodeURIComponent(date)}`);
  }

  /** The deadline `minutes` business minutes after `start`, computed by the API on the calendar. */
  deadline(start: string, minutes: number): Observable<string> {
    return this.http.get<string>('/api/calendar/deadline', { params: { start, minutes } });
  }

  policies(): Observable<SlaPolicyInfo[]> {
    return this.http.get<SlaPolicyInfo[]>('/api/sla-policies');
  }

  savePolicy(policy: Omit<SlaPolicyInfo, 'requestTypes'>): Observable<SlaPolicyInfo> {
    return this.http.put<SlaPolicyInfo>(`/api/sla-policies/${encodeURIComponent(policy.id)}`, policy);
  }

  setRequestTypePolicy(requestTypeId: string, policyId: string | null): Observable<void> {
    return this.http.put<void>(`/api/request-types/${encodeURIComponent(requestTypeId)}/sla-policy`, { policyId });
  }
}
