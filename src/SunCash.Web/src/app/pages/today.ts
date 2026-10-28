import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NgxEchartsDirective } from 'ngx-echarts';
import type { EChartsCoreOption } from 'echarts/core';
import { Api, DayValuation, Summary, errorMessage } from '../api';
import { addDays, fmtEur, fmtEurMwh, fmtKwh, longDate, todayIt, token } from '../format';

/** «Oggi»: il giorno quarto d'ora per quarto d'ora, più il riepilogo di mese e anno. */
@Component({
  selector: 'app-today',
  imports: [NgxEchartsDirective],
  template: `
    <div class="page">
      <div class="toolbar">
        <h1>{{ title() }}</h1>
        <button (click)="go(-1)" aria-label="Giorno prima">‹</button>
        <input type="date" class="day-picker" [value]="day()" (change)="goTo($any($event.target).value)" aria-label="Scegli il giorno" />
        <button (click)="go(1)" [disabled]="day() >= today" aria-label="Giorno dopo">›</button>
      </div>

      @if (error()) { <div class="notice error">{{ error() }}</div> }

      @if (data(); as d) {
        <div class="tiles">
          <div class="card tile"><h2>Beneficio del giorno</h2><div class="figure">{{ eur(d.totals.benefitEur) }}</div>
            <div class="sub">immesso + risparmio</div></div>
          <div class="card tile export"><h2>Energia immessa</h2><div class="figure">{{ eur(d.totals.exportValueEur) }}</div>
            <div class="sub">{{ kwh(d.totals.exportKwh) }} · {{ mwh(d.totals.capturedPriceEurMwh) }}</div></div>
          <div class="card tile self"><h2>Risparmio da autoconsumo</h2><div class="figure">{{ eur(d.totals.savingsEur) }}</div>
            <div class="sub">{{ kwh(d.totals.selfConsumptionKwh) }} consumati in casa</div></div>
        </div>
        @if (missingPrices()) {
          <div class="notice warn">Mancano i prezzi GME in alcuni intervalli: lì l'energia immessa non ha un valore. Vedi «Stato».</div>
        }
        @if (d.totals.incomplete) { <div class="notice warn">Mancano dati in alcuni intervalli del giorno: il totale è parziale.</div> }
        @if (d.totals.savingsUnavailable) {
          <div class="notice warn">Il risparmio non è calcolato in alcuni intervalli: manca l'offerta luce, la parte regolata o il consumo della casa. Vedi «Bollette» e «Stato».</div>
        }
        @if (hasHourly()) { <div class="notice warn">Alcune ore hanno solo il dato orario: valgono come blocco unico al prezzo dell'ora.</div> }
        <div class="card"><div echarts class="chart" [options]="chart()"></div></div>
      }

      @if (summary(); as s) {
        <div class="tiles">
          <div class="card tile"><h2>Questo mese</h2><div class="figure">{{ eur(s.month.benefitEur) }}</div>
            <div class="sub">{{ eur(s.month.exportValueEur) }} immesso · {{ eur(s.month.savingsEur) }} risparmio</div></div>
          <div class="card tile"><h2>Da inizio anno</h2><div class="figure">{{ eur(s.year.benefitEur - s.annualFixedCostsEur) }}</div>
            <div class="sub">{{ eur(s.year.exportValueEur) }} immesso · {{ eur(s.year.savingsEur) }} risparmio
              @if (s.annualFixedCostsEur > 0) { · − {{ eur(s.annualFixedCostsEur) }} costi fissi }</div></div>
          @if (s.bestDay; as b) {
            <div class="card tile"><h2>Giorno migliore</h2><div class="figure">{{ eur(b.totals.benefitEur) }}</div>
              <div class="sub">{{ long(b.period) }}</div></div>
          }
        </div>
      }
      <p class="muted">Stime: il GSE liquida sulle misure del distributore, non su quelle dell'impianto.</p>
    </div>
  `,
})
export class TodayPage {
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  /** Dalla rotta <code>giorno/:date</code>; vuoto = oggi. */
  readonly date = input<string>();

  protected readonly today = todayIt();
  protected readonly day = computed(() => this.date() ?? this.today);
  protected readonly data = signal<DayValuation | null>(null);
  protected readonly summary = signal<Summary | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly title = computed(() => (this.day() === this.today ? 'Oggi' : longDate(this.day())));
  protected readonly missingPrices = computed(() => this.data()?.rows.some(r => r.exportKwh != null && r.zonalEurMwh == null) ?? false);
  protected readonly hasHourly = computed(() => this.data()?.rows.some(r => r.resolution === 'Hour') ?? false);

  protected readonly eur = fmtEur;
  protected readonly kwh = fmtKwh;
  protected readonly mwh = fmtEurMwh;
  protected readonly long = longDate;

  protected readonly chart = computed<EChartsCoreOption>(() => {
    const rows = this.data()?.rows ?? [];
    return {
      tooltip: { trigger: 'axis' },
      legend: { top: 0, textStyle: { color: token('--text-muted') } },
      grid: { left: 48, right: 56, top: 64, bottom: 32 },
      xAxis: { type: 'category', data: rows.map(r => r.localLabel), axisLabel: { color: token('--text-muted') } },
      yAxis: [
        { type: 'value', name: 'kWh', axisLabel: { color: token('--text-muted') }, splitLine: { lineStyle: { color: token('--border') } } },
        { type: 'value', name: '€/MWh', axisLabel: { color: token('--text-muted') }, splitLine: { show: false } },
      ],
      series: [
        { name: 'Immessa', type: 'bar', stack: 'kwh', data: rows.map(r => r.exportKwh), itemStyle: { color: token('--export') } },
        { name: 'Autoconsumo', type: 'bar', stack: 'kwh', data: rows.map(r => r.selfConsumptionKwh), itemStyle: { color: token('--self') } },
        { name: 'Prezzo zonale', type: 'line', yAxisIndex: 1, step: 'end', showSymbol: false, data: rows.map(r => r.zonalEurMwh), lineStyle: { color: token('--price') }, itemStyle: { color: token('--price') } },
      ],
    };
  });

  constructor() {
    effect(() => void this.load(this.day()));
    this.api.summary().then(s => this.summary.set(s), () => undefined);
  }

  protected go(days: number) { this.goTo(addDays(this.day(), days)); }

  protected goTo(date: string) {
    if (date) void this.router.navigate(date === this.today ? ['/'] : ['/giorno', date]);
  }

  private async load(date: string) {
    this.error.set(null);
    try {
      this.data.set(await this.api.day(date));
    } catch (e) {
      this.data.set(null);
      this.error.set(errorMessage(e));
    }
  }
}
