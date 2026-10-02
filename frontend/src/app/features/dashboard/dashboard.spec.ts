import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DashboardData } from '../../core/api/dashboard.api';
import { VizColors } from '../../shared/charts/chart';
import { Dashboard } from './dashboard';
import { complianceChart, priorityChart, volumeChart } from './dashboard-charts';
import { text } from '../../testing/i18n';

const data: DashboardData = {
  from: '2026-03-01T00:00:00Z',
  to: '2026-03-03T00:00:00Z',
  teamId: null,
  granularity: 'Day',
  kpis: {
    created: 12,
    resolved: 9,
    backlog: 5,
    atRiskNow: 1,
    breachedNow: 2,
    slaCompliancePercent: 77.8,
    averageFirstResponseHours: 1.5,
    averageResolutionHours: 6.25,
    reopenRatePercent: 11.1,
    averageSatisfaction: 4.5,
    ratings: 2,
  },
  complianceByRequestType: [{ key: 'a', label: 'Payslip correction', resolved: 4, met: 2, compliancePercent: 50 }],
  complianceByTeam: [{ key: 't', label: 'Payroll', resolved: 4, met: 2, compliancePercent: 50 }],
  backlogByStatus: [{ key: 'Open', label: 'Open', count: 3 }],
  backlogByPriority: [
    { key: 'Low', label: 'Low', count: 1 },
    { key: 'Medium', label: 'Medium', count: 2 },
    { key: 'High', label: 'High', count: 1 },
    { key: 'Critical', label: 'Critical', count: 1 },
  ],
  topCategories: [{ key: 'Payroll', label: 'Payroll', count: 6 }],
  volume: [
    { date: '2026-03-01', created: 5, resolved: 4 },
    { date: '2026-03-02', created: 7, resolved: 5 },
  ],
  workload: [{ userId: 'u', name: 'Sami Gharbi', activeCases: 3, resolvedInPeriod: 4 }],
  teams: [{ id: 't', name: 'Payroll' }],
};

const colors: VizColors = {
  surface: '#fcfcfb',
  ink: '#0b0b0b',
  inkSecondary: '#52514e',
  muted: '#898781',
  grid: '#e1e0d9',
  axis: '#c3c2b7',
  series: ['#2a78d6', '#eb6834'],
  ordinal: ['#86b6ef', '#3987e5', '#256abf', '#184f95'],
};

describe('dashboard charts', () => {
  it('draws created and resolved in the first two categorical slots with a legend', () => {
    const config = volumeChart(data, text)(colors);
    const sets = config.data.datasets as unknown as { label: string; borderColor: string }[];

    expect(sets.map((s) => [s.label, s.borderColor])).toEqual([
      ['Created', '#2a78d6'],
      ['Resolved', '#eb6834'],
    ]);
    expect((config.options as { plugins: { legend: { display: boolean } } }).plugins.legend.display).toBeTrue();
  });

  it('uses the ordinal ramp for priorities, light to dark', () => {
    const config = priorityChart(data.backlogByPriority, text)(colors);

    expect((config.data.datasets[0] as unknown as { backgroundColor: string[] }).backgroundColor).toEqual(colors.ordinal);
  });

  it('caps compliance at 100%', () => {
    const config = complianceChart(data.complianceByRequestType, text)(colors);

    expect((config.options as { scales: { x: { max: number } } }).scales.x.max).toBe(100);
  });
});

describe('Dashboard', () => {
  let fixture: ComponentFixture<Dashboard>;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({ imports: [Dashboard], providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    const req = http.expectOne((r) => r.url === '/api/dashboard');
    expect(req.request.params.has('from')).toBeTrue();
    req.flush(data);
    await fixture.whenStable();
  });

  const el = () => fixture.nativeElement as HTMLElement;

  it('shows the key figures', () => {
    expect(el().querySelector('[data-testid="kpi-compliance"]')?.textContent).toContain('77.8%');
    expect(el().querySelector('[data-testid="kpi-backlog"]')?.textContent).toContain('2 late');
    expect(el().textContent).toContain('6.3 h');
  });

  it('draws charts, and offers every chart as a table', async () => {
    expect(el().querySelectorAll('app-chart canvas').length).toBe(7);

    (el().querySelector('[data-testid="tables-toggle"] button') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(el().querySelectorAll('app-chart').length).toBe(0);
    expect(el().querySelectorAll('table.data').length).toBe(7);
    expect(el().textContent).toContain('Sami Gharbi');
  });

  it('reloads for another period', async () => {
    const toggle = Array.from(el().querySelectorAll('mat-button-toggle button')).find((b) => b.textContent?.includes('7 days')) as HTMLButtonElement;
    toggle.click();
    TestBed.tick(); // starts the reload; whenStable() would wait for it

    const req = http.expectOne((r) => r.url === '/api/dashboard');
    const days = (new Date(req.request.params.get('to')!).getTime() - new Date(req.request.params.get('from')!).getTime()) / 86_400_000;
    expect(Math.round(days)).toBe(7);
    req.flush(data);
  });
});
