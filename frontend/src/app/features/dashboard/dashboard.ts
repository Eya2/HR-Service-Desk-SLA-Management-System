import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { DashboardApi, DashboardData, DashboardQuery } from '../../core/api/dashboard.api';
import { ChartView } from '../../shared/charts/chart';
import { categoryLabel, humanize } from '../../shared/ui/labels';
import { complianceChart, countChart, formatBucket, priorityChart, volumeChart, workloadChart } from './dashboard-charts';

type Preset = '7' | '30' | '90' | '365' | 'custom';

const DAY = 86_400_000;

/** HR leadership dashboard: KPIs, charts with table views, period and team filters, CSV export. */
@Component({
  selector: 'app-dashboard',
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSlideToggleModule,
    ChartView,
  ],
  template: `
    <div class="page-header">
      <div>
        <h1>Dashboard</h1>
        <p>Service levels, workload and demand. Times are business hours, pauses excluded.</p>
      </div>
      <button mat-stroked-button type="button" (click)="export()" [disabled]="!data.value()" data-testid="export">
        <mat-icon fontSet="material-symbols-outlined">download</mat-icon> Export CSV
      </button>
    </div>

    <div class="filters dense-controls" role="group" aria-label="Filters">
      <mat-button-toggle-group [value]="preset()" (change)="preset.set($event.value)" aria-label="Period" hideSingleSelectionIndicator>
        <mat-button-toggle value="7">7 days</mat-button-toggle>
        <mat-button-toggle value="30">30 days</mat-button-toggle>
        <mat-button-toggle value="90">90 days</mat-button-toggle>
        <mat-button-toggle value="365">12 months</mat-button-toggle>
        <mat-button-toggle value="custom">Custom</mat-button-toggle>
      </mat-button-toggle-group>
      @if (preset() === 'custom') {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="date">
          <mat-label>From</mat-label>
          <input matInput type="date" [value]="customFrom()" (change)="customFrom.set($any($event.target).value)" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="date">
          <mat-label>To</mat-label>
          <input matInput type="date" [value]="customTo()" (change)="customTo.set($any($event.target).value)" />
        </mat-form-field>
      }
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="team">
        <mat-label>Team</mat-label>
        <mat-select [value]="teamId()" (selectionChange)="teamId.set($event.value)" data-testid="team-filter">
          <mat-option [value]="null">All teams</mat-option>
          @for (team of teams(); track team.id) {
            <mat-option [value]="team.id">{{ team.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <span class="spacer"></span>
      <mat-slide-toggle [checked]="tables()" (change)="tables.set($event.checked)" data-testid="tables-toggle">Tables</mat-slide-toggle>
    </div>

    @if (data.isLoading()) {
      <mat-progress-bar mode="indeterminate" class="loading" />
    }

    @if (data.value(); as d) {
      <section class="kpis" aria-label="Key figures">
        <div class="kpi" data-testid="kpi-compliance">
          <span class="label">SLA compliance</span>
          <span class="value">{{ d.kpis.slaCompliancePercent === null ? '—' : (d.kpis.slaCompliancePercent | number: '1.0-1') + '%' }}</span>
          <span class="sub">resolved on time</span>
        </div>
        <div class="kpi" data-testid="kpi-created">
          <span class="label">Created</span>
          <span class="value">{{ d.kpis.created }}</span>
          <span class="sub">{{ d.kpis.resolved }} resolved</span>
        </div>
        <div class="kpi" data-testid="kpi-backlog">
          <span class="label">Backlog</span>
          <span class="value">{{ d.kpis.backlog }}</span>
          <span class="sub status">
            <span class="dot warning"></span>{{ d.kpis.atRiskNow }} at risk <span class="dot critical"></span>{{ d.kpis.breachedNow }} late
          </span>
        </div>
        <div class="kpi">
          <span class="label">First response</span>
          <span class="value">{{ hours(d.kpis.averageFirstResponseHours) }}</span>
          <span class="sub">average</span>
        </div>
        <div class="kpi">
          <span class="label">Resolution</span>
          <span class="value">{{ hours(d.kpis.averageResolutionHours) }}</span>
          <span class="sub">average</span>
        </div>
        <div class="kpi">
          <span class="label">Reopened</span>
          <span class="value">{{ d.kpis.reopenRatePercent === null ? '—' : (d.kpis.reopenRatePercent | number: '1.0-1') + '%' }}</span>
          <span class="sub">of resolved</span>
        </div>
      </section>

      <div class="grid">
        <article class="card wide">
          <header><h2>Volume over time</h2><p>Cases created and resolved per {{ d.granularity === 'Week' ? 'week' : 'day' }}</p></header>
          @if (tables()) {
            <table class="data" aria-label="Volume over time">
              <thead><tr><th>{{ d.granularity }}</th><th>Created</th><th>Resolved</th></tr></thead>
              <tbody>
                @for (p of d.volume; track p.date) {
                  <tr><td>{{ bucket(p.date, d.granularity) }}</td><td>{{ p.created }}</td><td>{{ p.resolved }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().volume" [height]="280" label="Cases created and resolved over time" />
          }
        </article>

        <article class="card">
          <header><h2>SLA compliance by request type</h2><p>Share resolved on time, worst first</p></header>
          @if (d.complianceByRequestType.length === 0) {
            <p class="empty">No case resolved in this period.</p>
          } @else if (tables()) {
            <table class="data" aria-label="SLA compliance by request type">
              <thead><tr><th>Request type</th><th>On time</th><th>Resolved</th><th>%</th></tr></thead>
              <tbody>
                @for (r of d.complianceByRequestType; track r.key) {
                  <tr><td>{{ r.label }}</td><td>{{ r.met }}</td><td>{{ r.resolved }}</td><td>{{ r.compliancePercent }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().byType" [height]="chartHeight(d.complianceByRequestType.length)" label="SLA compliance by request type" />
          }
        </article>

        <article class="card">
          <header><h2>SLA compliance by team</h2><p>Share resolved on time</p></header>
          @if (d.complianceByTeam.length === 0) {
            <p class="empty">No case resolved in this period.</p>
          } @else if (tables()) {
            <table class="data" aria-label="SLA compliance by team">
              <thead><tr><th>Team</th><th>On time</th><th>Resolved</th><th>%</th></tr></thead>
              <tbody>
                @for (r of d.complianceByTeam; track r.key) {
                  <tr><td>{{ r.label }}</td><td>{{ r.met }}</td><td>{{ r.resolved }}</td><td>{{ r.compliancePercent }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().byTeam" [height]="chartHeight(d.complianceByTeam.length)" label="SLA compliance by team" />
          }
        </article>

        <article class="card">
          <header><h2>Backlog by status</h2><p>Open cases now</p></header>
          @if (tables()) {
            <table class="data" aria-label="Backlog by status">
              <thead><tr><th>Status</th><th>Cases</th></tr></thead>
              <tbody>
                @for (r of d.backlogByStatus; track r.key) {
                  <tr><td>{{ humanize(r.label) }}</td><td>{{ r.count }}</td></tr>
                }
              </tbody>
            </table>
          } @else if (d.backlogByStatus.length === 0) {
            <p class="empty">No open case.</p>
          } @else {
            <app-chart [config]="charts().status" [height]="chartHeight(d.backlogByStatus.length)" label="Backlog by status" />
          }
        </article>

        <article class="card">
          <header><h2>Backlog by priority</h2><p>Open cases now, Low to Critical</p></header>
          @if (tables()) {
            <table class="data" aria-label="Backlog by priority">
              <thead><tr><th>Priority</th><th>Cases</th></tr></thead>
              <tbody>
                @for (r of d.backlogByPriority; track r.key) {
                  <tr><td>{{ r.label }}</td><td>{{ r.count }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().priority" [height]="240" label="Backlog by priority" />
          }
        </article>

        <article class="card">
          <header><h2>Top request categories</h2><p>Cases created in the period</p></header>
          @if (d.topCategories.length === 0) {
            <p class="empty">No case created in this period.</p>
          } @else if (tables()) {
            <table class="data" aria-label="Top request categories">
              <thead><tr><th>Category</th><th>Cases</th></tr></thead>
              <tbody>
                @for (r of d.topCategories; track r.key) {
                  <tr><td>{{ category(r.label) }}</td><td>{{ r.count }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().categories" [height]="chartHeight(d.topCategories.length)" label="Top request categories" />
          }
        </article>

        <article class="card">
          <header><h2>Agent workload</h2><p>Active cases now and cases resolved in the period</p></header>
          @if (d.workload.length === 0) {
            <p class="empty">No case assigned.</p>
          } @else if (tables()) {
            <table class="data" aria-label="Agent workload">
              <thead><tr><th>Agent</th><th>Active</th><th>Resolved</th></tr></thead>
              <tbody>
                @for (w of d.workload; track w.userId) {
                  <tr><td>{{ w.name }}</td><td>{{ w.activeCases }}</td><td>{{ w.resolvedInPeriod }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().workload" [height]="chartHeight(d.workload.length, 44)" label="Agent workload" />
          }
        </article>
      </div>
    }
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      margin-bottom: 20px;
    }
    .filters .date {
      width: 160px;
    }
    .filters .team {
      width: 220px;
    }
    .spacer {
      flex: 1;
    }
    .loading {
      margin-bottom: 12px;
    }
    .kpis {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
      gap: 14px;
      margin-bottom: 20px;
    }
    .kpi {
      display: grid;
      gap: 4px;
      padding: 16px 18px;
      border-radius: var(--app-radius);
      background: var(--app-card-bg);
      border: 1px solid var(--app-border);
      box-shadow: var(--app-shadow);
    }
    .kpi .label {
      font-size: 0.8rem;
      font-weight: 500;
      color: var(--app-muted);
    }
    .kpi .value {
      font: 700 1.9rem/1.1 Inter, sans-serif;
      letter-spacing: -0.02em;
    }
    .kpi .sub {
      font-size: 0.8rem;
      color: var(--app-muted);
    }
    .status {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      display: inline-block;
    }
    .dot.warning {
      background: #fab219;
    }
    .dot.critical {
      background: #d03b3b;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 16px;
    }
    .card {
      padding: 18px 20px 16px;
      border-radius: var(--app-radius);
      background: var(--viz-surface);
      border: 1px solid var(--app-border);
      box-shadow: var(--app-shadow);
      color: var(--viz-ink);
    }
    .card.wide {
      grid-column: 1 / -1;
    }
    .card header {
      margin-bottom: 12px;
    }
    .card h2 {
      font: 600 1rem Inter, sans-serif;
      margin: 0;
    }
    .card header p {
      margin: 2px 0 0;
      font-size: 0.82rem;
      color: var(--viz-ink-secondary);
    }
    .empty {
      color: var(--viz-ink-secondary);
      padding: 24px 0;
      text-align: center;
    }
    .data {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.88rem;
      font-variant-numeric: tabular-nums;
    }
    .data th,
    .data td {
      text-align: left;
      padding: 6px 8px;
      border-bottom: 1px solid var(--viz-grid);
    }
    .data th {
      color: var(--viz-ink-secondary);
      font-weight: 500;
    }
    @media (max-width: 959px) {
      .grid {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class Dashboard {
  private readonly api = inject(DashboardApi);

  protected readonly humanize = humanize;
  protected readonly category = categoryLabel;
  protected readonly bucket = formatBucket;
  protected readonly preset = signal<Preset>('30');
  protected readonly customFrom = signal(this.isoDate(Date.now() - 30 * DAY));
  protected readonly customTo = signal(this.isoDate(Date.now()));
  protected readonly teamId = signal<string | null>(null);
  protected readonly tables = signal(false);

  protected readonly query = computed<DashboardQuery>(() => {
    if (this.preset() === 'custom') {
      // Whole local days; "to" includes the chosen day.
      return {
        from: new Date(`${this.customFrom()}T00:00:00`).toISOString(),
        to: new Date(new Date(`${this.customTo()}T00:00:00`).getTime() + DAY).toISOString(),
        teamId: this.teamId(),
      };
    }
    const to = new Date();
    return { from: new Date(to.getTime() - Number(this.preset()) * DAY).toISOString(), to: to.toISOString(), teamId: this.teamId() };
  });

  protected readonly data = rxResource({ params: () => this.query(), stream: ({ params }) => this.api.get(params) });
  protected readonly teams = computed(() => this.data.value()?.teams ?? []);

  protected readonly charts = computed(() => {
    const d = this.data.value() as DashboardData;
    return {
      volume: volumeChart(d),
      byType: complianceChart(d.complianceByRequestType),
      byTeam: complianceChart(d.complianceByTeam),
      status: countChart(d.backlogByStatus, 'Open cases'),
      priority: priorityChart(d.backlogByPriority),
      categories: countChart(d.topCategories, 'Cases', categoryLabel),
      workload: workloadChart(d),
    };
  });

  protected hours(value: number | null): string {
    return value === null ? '—' : `${value.toFixed(1)} h`;
  }

  /** Height grows with the number of bars so they keep a constant thickness. */
  protected chartHeight(rows: number, perRow = 34): number {
    return Math.max(160, rows * perRow + 40);
  }

  protected export(): void {
    this.api.export(this.query());
  }

  private isoDate(ms: number): string {
    const d = new Date(ms);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
