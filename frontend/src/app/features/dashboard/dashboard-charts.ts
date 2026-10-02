import { ChartConfiguration } from 'chart.js';
import { ComplianceRow, DashboardData, NamedCount } from '../../core/api/dashboard.api';
import { VizColors, baseOptions } from '../../shared/charts/chart';
import { humanize } from '../../shared/ui/labels';

/** Translates a key ("dashboard.created") with optional parameters. */
export type ChartText = (key: string, params?: Record<string, unknown>) => string;

const rtl = () => typeof document !== 'undefined' && document.documentElement.dir === 'rtl';

/** Thin bars with 4px rounded data ends anchored on the baseline and a 2px surface gap between bars. */
function bar(_c: VizColors) {
  return { borderRadius: 4, borderSkipped: 'start' as const, maxBarThickness: 20, borderWidth: 0, categoryPercentage: 0.8, barPercentage: 0.9 };
}

function axes(c: VizColors, horizontal: boolean, valueMax?: number, valueSuffix = '') {
  const value = {
    beginAtZero: true,
    max: valueMax,
    grid: { color: c.grid, drawTicks: false },
    border: { display: false },
    ticks: { color: c.muted, precision: 0, padding: 6, callback: (v: number | string) => `${v}${valueSuffix}` },
  };
  const category = {
    grid: { display: false },
    border: { color: c.axis },
    // Horizontal labels only: skip some rather than rotate them.
    ticks: { color: c.inkSecondary, padding: 6, maxRotation: 0, autoSkip: true, autoSkipPadding: 12, maxTicksLimit: horizontal ? undefined : 8 },
  };
  // Right-to-left: bars grow from the right and the time axis runs right to left.
  if (rtl()) {
    return horizontal ? { x: { ...value, reverse: true }, y: { ...category, position: 'right' } } : { x: { ...category, reverse: true }, y: { ...value, position: 'right' } };
  }
  return horizontal ? { x: value, y: category } : { x: category, y: value };
}

/** Created vs resolved over time: two series, a legend and a crosshair-style tooltip on the whole column. */
export function volumeChart(data: DashboardData, t: ChartText, locale?: string): (c: VizColors) => ChartConfiguration {
  return (c) => {
    const base = baseOptions(c)!;
    const labels = data.volume.map((p) => formatBucket(p.date, data.granularity, t, locale));
    return {
      type: 'line',
      data: {
        labels,
        datasets: [
          { label: t('dashboard.created'), data: data.volume.map((p) => p.created), borderColor: c.series[0], backgroundColor: c.series[0] },
          { label: t('dashboard.resolved'), data: data.volume.map((p) => p.resolved), borderColor: c.series[1], backgroundColor: c.series[1] },
        ].map((d) => ({ ...d, borderWidth: 2, tension: 0.25, pointRadius: 0, pointHoverRadius: 5, pointHoverBorderColor: c.surface, pointHoverBorderWidth: 2 })),
      },
      options: {
        ...base,
        interaction: { mode: 'index', intersect: false },
        plugins: { ...base.plugins, legend: { ...base.plugins!.legend, display: true, position: 'top', align: 'end' } },
        scales: axes(c, false),
      },
    } as ChartConfiguration;
  };
}

/** SLA compliance per row, worst first; the tooltip states "x of y resolved on time". */
export function complianceChart(rows: ComplianceRow[], t: ChartText): (c: VizColors) => ChartConfiguration {
  return (c) => {
    const base = baseOptions(c)!;
    return {
      type: 'bar',
      data: {
        labels: rows.map((r) => r.label),
        datasets: [{ label: t('dashboard.onTime'), data: rows.map((r) => r.compliancePercent ?? 0), backgroundColor: c.series[0], ...bar(c) }],
      },
      options: {
        ...base,
        indexAxis: 'y',
        plugins: {
          ...base.plugins,
          tooltip: {
            ...base.plugins!.tooltip,
            callbacks: {
              label: (ctx) => t('dashboard.onTimeTooltip', { percent: ctx.parsed.x, met: rows[ctx.dataIndex].met, resolved: rows[ctx.dataIndex].resolved }),
            },
          },
        },
        scales: axes(c, true, 100, '%'),
      },
    } as ChartConfiguration;
  };
}

/** Counts per category as horizontal bars (one hue: identity is in the labels). */
export function countChart(rows: NamedCount[], label: string, translate = humanize): (c: VizColors) => ChartConfiguration {
  return (c) => ({
    type: 'bar',
    data: {
      labels: rows.map((r) => translate(r.key)),
      datasets: [{ label, data: rows.map((r) => r.count), backgroundColor: c.series[0], ...bar(c) }],
    },
    options: { ...baseOptions(c), indexAxis: 'y', scales: axes(c, true) },
  }) as ChartConfiguration;
}

/** Backlog per priority: an ordinal ramp, light (Low) to dark (Critical). */
export function priorityChart(rows: NamedCount[], t: ChartText, translate = humanize): (c: VizColors) => ChartConfiguration {
  return (c) => ({
    type: 'bar',
    data: {
      labels: rows.map((r) => translate(r.key)),
      datasets: [{ label: t('dashboard.openNow'), data: rows.map((r) => r.count), backgroundColor: rows.map((_, i) => c.ordinal[Math.min(i, 3)]), ...bar(c), maxBarThickness: 40 }],
    },
    options: { ...baseOptions(c), scales: axes(c, false) },
  }) as ChartConfiguration;
}

/** Active cases and cases resolved in the period, per agent (two series, legend). */
export function workloadChart(data: DashboardData, t: ChartText): (c: VizColors) => ChartConfiguration {
  return (c) => {
    const base = baseOptions(c)!;
    return {
      type: 'bar',
      data: {
        labels: data.workload.map((w) => w.name),
        datasets: [
          { label: t('dashboard.activeNow'), data: data.workload.map((w) => w.activeCases), backgroundColor: c.series[0], ...bar(c) },
          { label: t('dashboard.resolvedInPeriod'), data: data.workload.map((w) => w.resolvedInPeriod), backgroundColor: c.series[1], ...bar(c) },
        ],
      },
      options: {
        ...base,
        indexAxis: 'y',
        plugins: { ...base.plugins, legend: { ...base.plugins!.legend, display: true, position: 'top', align: 'end' } },
        scales: axes(c, true),
      },
    } as ChartConfiguration;
  };
}

export function formatBucket(isoDate: string, granularity: 'Day' | 'Week', t?: ChartText, locale?: string): string {
  const date = new Date(`${isoDate}T00:00:00`);
  const text = date.toLocaleDateString(locale, { day: 'numeric', month: 'short' });
  if (granularity !== 'Week') return text;
  return t ? t('dashboard.weekOf', { date: text }) : `Week of ${text}`;
}
