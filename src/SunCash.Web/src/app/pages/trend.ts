import { Component, computed, effect, inject, signal } from '@angular/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import type { EChartsCoreOption } from 'echarts/core';
import { Api, MonthTotals, errorMessage } from '../api';
import { fmtEur, fmtKwh, shortMonth, todayIt, token } from '../format';

/** «Andamento»: beneficio mensile dell'anno, cumulato e confronto con i pagamenti GSE. */
@Component({
  selector: 'app-trend',
  imports: [NgxEchartsDirective],
  template: `
    <div class="page">
      <div class="toolbar">
        <h1>Andamento {{ year() }}</h1>
        <button (click)="year.set(year() - 1)" aria-label="Anno prima">‹</button>
        <button (click)="year.set(year() + 1)" [disabled]="year() >= thisYear" aria-label="Anno dopo">›</button>
      </div>
      @if (error()) { <div class="notice error">{{ error() }}</div> }
      <div class="card"><div echarts class="chart" [options]="chart()"></div></div>
      <div class="card table-wrap">
        <table>
          <thead><tr><th>Mese</th><th class="num">Immessa</th><th class="num">Valore immesso</th><th class="num">Pagato GSE</th>
            <th class="num">Autoconsumo</th><th class="num">Risparmio</th><th class="num">Totale</th></tr></thead>
          <tbody>
            @for (m of months(); track m.month) {
              <tr>
                <td>{{ label(m.month) }}</td>
                <td class="num">{{ kwh(m.totals.exportKwh) }}</td>
                <td class="num">{{ eur(m.totals.exportValueEur) }}</td>
                <td class="num">{{ eur(m.gsePaymentEur) }}</td>
                <td class="num">{{ kwh(m.totals.selfConsumptionKwh) }}</td>
                <td class="num">{{ eur(m.totals.savingsEur) }}</td>
                <td class="num"><strong>{{ eur(m.totals.benefitEur) }}</strong></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
      <p class="muted">«Pagato GSE» è l'importo inserito a mano in «Bollette»: confrontato con il valore stimato misura lo scarto fra meter e distributore.</p>
    </div>
  `,
})
export class TrendPage {
  private readonly api = inject(Api);
  protected readonly thisYear = +todayIt().slice(0, 4);
  protected readonly year = signal(this.thisYear);
  protected readonly months = signal<MonthTotals[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly eur = fmtEur;
  protected readonly kwh = fmtKwh;
  protected readonly label = shortMonth;

  protected readonly chart = computed<EChartsCoreOption>(() => {
    const months = this.months();
    let running = 0;
    const cumulative = months.map(m => (running += m.totals.benefitEur));
    const muted = token('--text-muted');
    return {
      tooltip: { trigger: 'axis' },
      legend: { top: 0, textStyle: { color: muted } },
      grid: { left: 56, right: 64, top: 64, bottom: 32 },
      xAxis: { type: 'category', data: months.map(m => shortMonth(m.month)), axisLabel: { color: muted } },
      yAxis: [
        { type: 'value', name: '€ / mese', axisLabel: { color: muted }, splitLine: { lineStyle: { color: token('--border') } } },
        { type: 'value', name: '€ cumulati', axisLabel: { color: muted }, splitLine: { show: false } },
      ],
      series: [
        { name: 'Immesso', type: 'bar', stack: 'eur', data: months.map(m => m.totals.exportValueEur), itemStyle: { color: token('--export') } },
        { name: 'Risparmio', type: 'bar', stack: 'eur', data: months.map(m => m.totals.savingsEur), itemStyle: { color: token('--self') } },
        { name: 'Cumulato', type: 'line', yAxisIndex: 1, data: cumulative, lineStyle: { color: token('--text') }, itemStyle: { color: token('--text') } },
        { name: 'Pagato GSE', type: 'line', data: months.map(m => m.gsePaymentEur), showSymbol: true, lineStyle: { type: 'dashed', color: token('--price') }, itemStyle: { color: token('--price') } },
      ],
    };
  });

  constructor() {
    effect(() => void this.load(this.year()));
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
