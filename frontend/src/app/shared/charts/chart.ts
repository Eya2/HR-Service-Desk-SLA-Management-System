import { Component, ElementRef, OnDestroy, effect, inject, input, viewChild } from '@angular/core';
import {
  BarController,
  BarElement,
  CategoryScale,
  Chart,
  ChartConfiguration,
  Filler,
  Legend,
  LineController,
  LineElement,
  LinearScale,
  PointElement,
  Tooltip,
} from 'chart.js';
import { ThemeService } from '../../core/layout/theme.service';

Chart.register(BarController, BarElement, CategoryScale, Filler, Legend, LineController, LineElement, LinearScale, PointElement, Tooltip);

/** Chart colours, read from the --viz-* tokens so light and dark each use their own validated steps. */
export interface VizColors {
  surface: string;
  ink: string;
  inkSecondary: string;
  muted: string;
  grid: string;
  axis: string;
  series: [string, string];
  ordinal: [string, string, string, string];
}

export function readVizColors(): VizColors {
  const style = getComputedStyle(document.documentElement);
  const v = (name: string) => style.getPropertyValue(name).trim();
  return {
    surface: v('--viz-surface'),
    ink: v('--viz-ink'),
    inkSecondary: v('--viz-ink-secondary'),
    muted: v('--viz-muted'),
    grid: v('--viz-grid'),
    axis: v('--viz-axis'),
    series: [v('--viz-series-1'), v('--viz-series-2')],
    ordinal: [v('--viz-ordinal-1'), v('--viz-ordinal-2'), v('--viz-ordinal-3'), v('--viz-ordinal-4')],
  };
}

/** Common chrome: recessive grid and axes, text in ink colours (never the series colour), Inter. */
export function baseOptions(c: VizColors): ChartConfiguration['options'] {
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    font: { family: 'Inter, system-ui, sans-serif' },
    plugins: {
      legend: { display: false, labels: { color: c.inkSecondary, usePointStyle: true, pointStyle: 'rectRounded', boxWidth: 10 } },
      tooltip: {
        backgroundColor: c.surface,
        titleColor: c.ink,
        bodyColor: c.inkSecondary,
        borderColor: c.axis,
        borderWidth: 1,
        padding: 10,
        cornerRadius: 8,
        boxPadding: 4,
      },
    },
  } as ChartConfiguration['options'];
}

/**
 * A Chart.js canvas. <c>config</c> is a function of the current colours, so the chart is rebuilt in the right
 * palette when the theme changes.
 */
@Component({
  selector: 'app-chart',
  template: `<div class="frame" [style.height.px]="height()"><canvas #canvas [attr.aria-label]="label()" role="img"></canvas></div>`,
  styles: `
    .frame {
      position: relative;
      width: 100%;
    }
  `,
})
export class ChartView implements OnDestroy {
  readonly config = input.required<(colors: VizColors) => ChartConfiguration>();
  readonly height = input(260);
  /** Text alternative for the canvas; the card's table view carries the full data. */
  readonly label = input('Chart');

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly theme = inject(ThemeService);
  private chart: Chart | null = null;

  constructor() {
    effect(() => {
      this.theme.version(); // re-read colours after a theme change
      const build = this.config();
      // Let the browser apply the new colour scheme before reading the tokens.
      requestAnimationFrame(() => this.render(build));
    });
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private render(build: (colors: VizColors) => ChartConfiguration): void {
    this.chart?.destroy();
    this.chart = new Chart(this.canvas().nativeElement, build(readVizColors()));
  }
}
