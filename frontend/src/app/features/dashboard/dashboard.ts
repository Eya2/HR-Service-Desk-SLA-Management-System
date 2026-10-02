import { DecimalPipe, PercentPipe } from '@angular/common';
import { Component, LOCALE_ID, computed, inject, signal } from '@angular/core';
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
import { ChartText, complianceChart, countChart, formatBucket, priorityChart, volumeChart, workloadChart } from './dashboard-charts';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { EnumLabelPipe, enumLabel } from '../../shared/ui/enum-label';
import { formatNumber } from '@angular/common';

type Preset = '7' | '30' | '90' | '365' | 'custom';

const DAY = 86_400_000;

/** HR leadership dashboard: KPIs, charts with table views, period and team filters, CSV export. */
@Component({
  selector: 'app-dashboard',
  imports: [
    DecimalPipe,
    PercentPipe,
    TranslatePipe,
    EnumLabelPipe,
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
        <h1>{{ 'dashboard.title' | translate }}</h1>
        <p>{{ 'dashboard.lead' | translate }}</p>
      </div>
      <button mat-stroked-button type="button" (click)="export()" [disabled]="!data.value()" data-testid="export">
        <mat-icon fontSet="material-symbols-outlined">download</mat-icon> {{ 'dashboard.export' | translate }}
      </button>
    </div>

    <div class="filters dense-controls" role="group" [attr.aria-label]="'dashboard.filters' | translate">
      <mat-button-toggle-group [value]="preset()" (change)="preset.set($event.value)" [attr.aria-label]="'dashboard.period' | translate" hideSingleSelectionIndicator>
        <mat-button-toggle value="7">{{ 'dashboard.days7' | translate }}</mat-button-toggle>
        <mat-button-toggle value="30">{{ 'dashboard.days30' | translate }}</mat-button-toggle>
        <mat-button-toggle value="90">{{ 'dashboard.days90' | translate }}</mat-button-toggle>
        <mat-button-toggle value="365">{{ 'dashboard.months12' | translate }}</mat-button-toggle>
        <mat-button-toggle value="custom">{{ 'dashboard.custom' | translate }}</mat-button-toggle>
      </mat-button-toggle-group>
      @if (preset() === 'custom') {
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="date">
          <mat-label>{{ 'dashboard.from' | translate }}</mat-label>
          <input matInput type="date" [value]="customFrom()" (change)="customFrom.set($any($event.target).value)" />
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="date">
          <mat-label>{{ 'dashboard.to' | translate }}</mat-label>
          <input matInput type="date" [value]="customTo()" (change)="customTo.set($any($event.target).value)" />
        </mat-form-field>
      }
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="team">
        <mat-label>{{ 'dashboard.team' | translate }}</mat-label>
        <mat-select [value]="teamId()" (selectionChange)="teamId.set($event.value)" data-testid="team-filter">
          <mat-option [value]="null">{{ 'dashboard.allTeams' | translate }}</mat-option>
          @for (team of teams(); track team.id) {
            <mat-option [value]="team.id">{{ team.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <span class="spacer"></span>
      <mat-slide-toggle [checked]="tables()" (change)="tables.set($event.checked)" data-testid="tables-toggle">{{ 'dashboard.tables' | translate }}</mat-slide-toggle>
    </div>

    @if (data.isLoading()) {
      <mat-progress-bar mode="indeterminate" class="loading" />
    }

    @if (data.value(); as d) {
      <section class="kpis" [attr.aria-label]="'dashboard.keyFigures' | translate">
        <div class="kpi" data-testid="kpi-compliance">
          <span class="label">{{ 'dashboard.compliance' | translate }}</span>
          <span class="value">{{ d.kpis.slaCompliancePercent === null ? '—' : (d.kpis.slaCompliancePercent / 100 | percent: '1.0-1') }}</span>
          <span class="sub">{{ 'dashboard.resolvedOnTime' | translate }}</span>
        </div>
        <div class="kpi" data-testid="kpi-created">
          <span class="label">{{ 'dashboard.created' | translate }}</span>
          <span class="value">{{ d.kpis.created }}</span>
          <span class="sub">{{ 'dashboard.resolvedCount' | translate: { count: d.kpis.resolved } }}</span>
        </div>
        <div class="kpi" data-testid="kpi-backlog">
          <span class="label">{{ 'dashboard.backlog' | translate }}</span>
          <span class="value">{{ d.kpis.backlog }}</span>
          <span class="sub status">
            <span class="dot warning"></span>{{ 'dashboard.atRiskLate' | translate: { risk: d.kpis.atRiskNow } }} <span class="dot critical"></span>{{ 'dashboard.late' | translate: { late: d.kpis.breachedNow } }}
          </span>
        </div>
        <div class="kpi">
          <span class="label">{{ 'dashboard.firstResponse' | translate }}</span>
          <span class="value">{{ hours(d.kpis.averageFirstResponseHours) }}</span>
          <span class="sub">{{ 'dashboard.average' | translate }}</span>
        </div>
        <div class="kpi">
          <span class="label">{{ 'dashboard.resolution' | translate }}</span>
          <span class="value">{{ hours(d.kpis.averageResolutionHours) }}</span>
          <span class="sub">{{ 'dashboard.average' | translate }}</span>
        </div>
        <div class="kpi">
          <span class="label">{{ 'dashboard.reopened' | translate }}</span>
          <span class="value">{{ d.kpis.reopenRatePercent === null ? '—' : (d.kpis.reopenRatePercent / 100 | percent: '1.0-1') }}</span>
          <span class="sub">{{ 'dashboard.ofResolved' | translate }}</span>
        </div>
        <div class="kpi" data-testid="kpi-csat">
          <span class="label">{{ 'dashboard.csat' | translate }}</span>
          <span class="value">
            @if (d.kpis.averageSatisfaction === null) {
              —
            } @else {
              {{ d.kpis.averageSatisfaction | number: '1.1-1' }}<mat-icon class="kpi-star" fontSet="material-symbols-outlined" aria-hidden="true">star</mat-icon>
            }
          </span>
          <span class="sub">{{ 'dashboard.csatSub' | translate: { count: d.kpis.ratings } }}</span>
        </div>
      </section>

      <div class="grid">
        <article class="card wide">
          <header><h2>{{ 'dashboard.volume' | translate }}</h2><p>{{ (d.granularity === 'Week' ? 'dashboard.volumeSubWeek' : 'dashboard.volumeSubDay') | translate }}</p></header>
          @if (tables()) {
            <table class="data" [attr.aria-label]="'dashboard.volume' | translate">
              <thead><tr><th>{{ (d.granularity === 'Week' ? 'dashboard.week' : 'dashboard.day') | translate }}</th><th>{{ 'dashboard.created' | translate }}</th><th>{{ 'dashboard.resolved' | translate }}</th></tr></thead>
              <tbody>
                @for (p of d.volume; track p.date) {
                  <tr><td>{{ bucket(p.date, d.granularity, text, locale) }}</td><td>{{ p.created }}</td><td>{{ p.resolved }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().volume" [height]="280" [label]="'dashboard.volume' | translate" />
          }
        </article>

        <article class="card">
          <header><h2>{{ 'dashboard.byType' | translate }}</h2><p>{{ 'dashboard.byTypeSub' | translate }}</p></header>
          @if (d.complianceByRequestType.length === 0) {
            <p class="empty">{{ 'dashboard.noResolved' | translate }}</p>
          } @else if (tables()) {
            <table class="data" [attr.aria-label]="'dashboard.byType' | translate">
              <thead><tr><th>{{ 'dashboard.requestType' | translate }}</th><th>{{ 'dashboard.onTime' | translate }}</th><th>{{ 'dashboard.resolved' | translate }}</th><th>%</th></tr></thead>
              <tbody>
                @for (r of d.complianceByRequestType; track r.key) {
                  <tr><td>{{ r.label }}</td><td>{{ r.met }}</td><td>{{ r.resolved }}</td><td>{{ r.compliancePercent }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().byType" [height]="chartHeight(d.complianceByRequestType.length)" [label]="'dashboard.byType' | translate" />
          }
        </article>

        <article class="card">
          <header><h2>{{ 'dashboard.byTeam' | translate }}</h2><p>{{ 'dashboard.byTeamSub' | translate }}</p></header>
          @if (d.complianceByTeam.length === 0) {
            <p class="empty">{{ 'dashboard.noResolved' | translate }}</p>
          } @else if (tables()) {
            <table class="data" [attr.aria-label]="'dashboard.byTeam' | translate">
              <thead><tr><th>{{ 'dashboard.team' | translate }}</th><th>{{ 'dashboard.onTime' | translate }}</th><th>{{ 'dashboard.resolved' | translate }}</th><th>%</th></tr></thead>
              <tbody>
                @for (r of d.complianceByTeam; track r.key) {
                  <tr><td>{{ r.label }}</td><td>{{ r.met }}</td><td>{{ r.resolved }}</td><td>{{ r.compliancePercent }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().byTeam" [height]="chartHeight(d.complianceByTeam.length)" [label]="'dashboard.byTeam' | translate" />
          }
        </article>

        <article class="card">
          <header><h2>{{ 'dashboard.byStatus' | translate }}</h2><p>{{ 'dashboard.openNow' | translate }}</p></header>
          @if (tables()) {
            <table class="data" [attr.aria-label]="'dashboard.byStatus' | translate">
              <thead><tr><th>{{ 'dashboard.status' | translate }}</th><th>{{ 'dashboard.cases' | translate }}</th></tr></thead>
              <tbody>
                @for (r of d.backlogByStatus; track r.key) {
                  <tr><td>{{ r.key | enumLabel: 'status' }}</td><td>{{ r.count }}</td></tr>
                }
              </tbody>
            </table>
          } @else if (d.backlogByStatus.length === 0) {
            <p class="empty">{{ 'dashboard.noOpen' | translate }}</p>
          } @else {
            <app-chart [config]="charts().status" [height]="chartHeight(d.backlogByStatus.length)" [label]="'dashboard.byStatus' | translate" />
          }
        </article>

        <article class="card">
          <header><h2>{{ 'dashboard.byPriority' | translate }}</h2><p>{{ 'dashboard.byPrioritySub' | translate }}</p></header>
          @if (tables()) {
            <table class="data" [attr.aria-label]="'dashboard.byPriority' | translate">
              <thead><tr><th>{{ 'dashboard.priority' | translate }}</th><th>{{ 'dashboard.cases' | translate }}</th></tr></thead>
              <tbody>
                @for (r of d.backlogByPriority; track r.key) {
                  <tr><td>{{ r.key | enumLabel: 'priority' }}</td><td>{{ r.count }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().priority" [height]="240" [label]="'dashboard.byPriority' | translate" />
          }
        </article>

        <article class="card">
          <header><h2>{{ 'dashboard.categories' | translate }}</h2><p>{{ 'dashboard.categoriesSub' | translate }}</p></header>
          @if (d.topCategories.length === 0) {
            <p class="empty">{{ 'dashboard.noCreated' | translate }}</p>
          } @else if (tables()) {
            <table class="data" [attr.aria-label]="'dashboard.categories' | translate">
              <thead><tr><th>{{ 'dashboard.category' | translate }}</th><th>{{ 'dashboard.cases' | translate }}</th></tr></thead>
              <tbody>
                @for (r of d.topCategories; track r.key) {
                  <tr><td>{{ r.key | enumLabel: 'category' }}</td><td>{{ r.count }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().categories" [height]="chartHeight(d.topCategories.length)" [label]="'dashboard.categories' | translate" />
          }
        </article>

        <article class="card">
          <header><h2>{{ 'dashboard.workload' | translate }}</h2><p>{{ 'dashboard.workloadSub' | translate }}</p></header>
          @if (d.workload.length === 0) {
            <p class="empty">{{ 'dashboard.noAssigned' | translate }}</p>
          } @else if (tables()) {
            <table class="data" [attr.aria-label]="'dashboard.workload' | translate">
              <thead><tr><th>{{ 'dashboard.agent' | translate }}</th><th>{{ 'dashboard.active' | translate }}</th><th>{{ 'dashboard.resolved' | translate }}</th></tr></thead>
              <tbody>
                @for (w of d.workload; track w.userId) {
                  <tr><td>{{ w.name }}</td><td>{{ w.activeCases }}</td><td>{{ w.resolvedInPeriod }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <app-chart [config]="charts().workload" [height]="chartHeight(d.workload.length, 44)" [label]="'dashboard.workload' | translate" />
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
      grid-template-columns: repeat(auto-fit, minmax(132px, 1fr));
      gap: 14px;
      margin-bottom: 20px;
    }
    .kpi-star {
      font-size: 22px;
      width: 22px;
      height: 22px;
      vertical-align: -2px;
      margin-inline-start: 4px;
      color: #f5a524;
      font-variation-settings: 'FILL' 1;
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
      font: 700 1.9rem/1.1 Inter, 'IBM Plex Sans Arabic', sans-serif;
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
      font: 600 1rem Inter, 'IBM Plex Sans Arabic', sans-serif;
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

  private readonly translate = inject(TranslateService);
  protected readonly locale = inject(LOCALE_ID);
  protected readonly text: ChartText = (key, params) => this.translate.instant(key, params) as string;
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
      volume: volumeChart(d, this.text, this.locale),
      byType: complianceChart(d.complianceByRequestType, this.text),
      byTeam: complianceChart(d.complianceByTeam, this.text),
      status: countChart(d.backlogByStatus, this.text('dashboard.openNow'), (k) => enumLabel(this.translate, 'status', k)),
      priority: priorityChart(d.backlogByPriority, this.text, (k) => enumLabel(this.translate, 'priority', k)),
      categories: countChart(d.topCategories, this.text('dashboard.cases'), (k) => enumLabel(this.translate, 'category', k)),
      workload: workloadChart(d, this.text),
    };
  });

  protected hours(value: number | null): string {
    return value === null ? '—' : this.text('dashboard.hours', { value: formatNumber(value, this.locale, '1.1-1') });
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
