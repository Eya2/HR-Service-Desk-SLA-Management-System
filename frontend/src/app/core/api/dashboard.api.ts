import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface NamedCount {
  key: string;
  label: string;
  count: number;
}

export interface ComplianceRow {
  key: string;
  label: string;
  resolved: number;
  met: number;
  compliancePercent: number | null;
}

export interface DashboardData {
  from: string;
  to: string;
  teamId: string | null;
  granularity: 'Day' | 'Week';
  kpis: {
    created: number;
    resolved: number;
    backlog: number;
    atRiskNow: number;
    breachedNow: number;
    slaCompliancePercent: number | null;
    averageFirstResponseHours: number | null;
    averageResolutionHours: number | null;
    reopenRatePercent: number | null;
    averageSatisfaction: number | null;
    ratings: number;
  };
  complianceByRequestType: ComplianceRow[];
  complianceByTeam: ComplianceRow[];
  backlogByStatus: NamedCount[];
  backlogByPriority: NamedCount[];
  topCategories: NamedCount[];
  volume: { date: string; created: number; resolved: number }[];
  workload: { userId: string; name: string; activeCases: number; resolvedInPeriod: number }[];
  teams: { id: string; name: string }[];
}

export interface DashboardQuery {
  from: string;
  to: string;
  teamId: string | null;
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  get(query: DashboardQuery): Observable<DashboardData> {
    return this.http.get<DashboardData>('/api/dashboard', { params: this.params(query) });
  }

  /** Downloads the CSV with the bearer token (a plain link could not send it). */
  export(query: DashboardQuery): void {
    this.http.get('/api/dashboard/export', { params: this.params(query), responseType: 'blob' }).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `hr-cases-${query.from.slice(0, 10)}-${query.to.slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
    });
  }

  private params(query: DashboardQuery): HttpParams {
    let params = new HttpParams().set('from', query.from).set('to', query.to);
    if (query.teamId) params = params.set('teamId', query.teamId);
    return params;
  }
}
