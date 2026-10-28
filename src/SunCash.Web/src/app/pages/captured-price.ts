import { Component, computed, effect, inject, signal } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import type { EChartsCoreOption } from 'echarts/core';
import { Api, MonthTotals, errorMessage } from '../api';
import { fmtEurMwh, shortMonth, todayIt, token } from '../format';

/**
 * «Prezzo catturato»: il prezzo medio che l'impianto ha ottenuto (valore / energia immessa) contro la
 * media del prezzo zonale. Se il primo è più basso, l'impianto produce nelle ore in cui il prezzo cala.
 */
@Component({
  selector: 'app-captured-price',
  imports: [NgxEchartsDirective],
  template: `
    <div class="page">
      <div class="toolbar">
        <h1>Prezzo catturato {{ year() }}</h1>
        <button (click)="year.set(year() - 1)" aria-label="Anno prima">‹</button>
        <button (click)="year.set(year() + 1)" [disabled]="year() >= thisYear" aria-label="Anno dopo">›</button>
      </div>
      @if (error()) { <div class="notice error">{{ error() }}</div> }
      <div class="card"><div echarts class="chart" [options]="chart()"></div></div>
      <div class="card table-wrap">
        <table>
          <thead><tr><th>Mese</th><th class="num">Catturato</th><th class="num">Media zonale</th><th class="num">Rapporto</th></tr></thead>
          <tbody>
            @for (m of months(); track m.month) {
              <tr>
                <td>{{ label(m.month) }}</td>
                <td class="num">{{ mwh(m.totals.capturedPriceEurMwh) }}</td>
                <td class="num">{{ mwh(m.totals.averageZonalEurMwh) }}</td>
                <td class="num">{{ ratio(m) }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <p class="muted">Un rapporto sotto il 100% dice quanto pesa produrre nelle ore centrali, quando il prezzo zonale scende.</p>
    </div>
  `,
})
export class CapturedPricePage {
  private readonly api = inject(Api);
  protected readonly thisYear = +todayIt().slice(0, 4);
  protected readonly year = signal(this.thisYear);
  protected readonly months = signal<MonthTotals[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly mwh = fmtEurMwh;
  protected readonly label = shortMonth;

  protected readonly chart = computed<EChartsCoreOption>(() => {
    const months = this.months();
    const muted = token('--text-muted');
    return {
      tooltip: { trigger: 'axis' },
      legend: { top: 0, textStyle: { color: muted } },
      grid: { left: 56, right: 24, top: 64, bottom: 32 },
      xAxis: { type: 'category', data: months.map(m => shortMonth(m.month)), axisLabel: { color: muted } },
      yAxis: { type: 'value', name: '€/MWh', axisLabel: { color: muted }, splitLine: { lineStyle: { color: token('--border') } } },
      series: [
        { name: 'Catturato', type: 'line', data: months.map(m => m.totals.capturedPriceEurMwh), lineStyle: { color: token('--export') }, itemStyle: { color: token('--export') } },
        { name: 'Media zonale', type: 'line', data: months.map(m => m.totals.averageZonalEurMwh), lineStyle: { color: token('--price') }, itemStyle: { color: token('--price') } },
      ],
    };
  });

  constructor() {
    effect(() => void this.load(this.year()));
  }

  protected ratio(m: MonthTotals): string {
    const { capturedPriceEurMwh: c, averageZonalEurMwh: a } = m.totals;
    return c == null || a == null || a === 0 ? '—' : `${Math.round((c / a) * 100)}%`;
  }

  private async load(year: number) {
    this.error.set(null);
    try {
      this.months.set(await this.api.months(year));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
