import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, PeriodTotals, errorMessage } from '../api';
import { fmtEur, todayIt } from '../format';

interface Cell { date: string; day: number; totals: PeriodTotals['totals'] | null; future: boolean; }

/** «Calendario»: il mese a griglia con il beneficio di ogni giorno. */
@Component({
  selector: 'app-calendar',
  imports: [RouterLink],
  template: `
    <div class="page">
      <div class="toolbar">
        <h1>{{ monthLabel() }}</h1>
        <button (click)="shift(-1)" aria-label="Mese prima">‹</button>
        <button (click)="shift(1)" [disabled]="month() >= thisMonth" aria-label="Mese dopo">›</button>
      </div>
      @if (error()) { <div class="notice error">{{ error() }}</div> }
      <div class="card">
        <div class="grid">
          @for (d of weekdays; track d) { <div class="head">{{ d }}</div> }
          @for (blank of blanks(); track $index) { <div></div> }
          @for (c of cells(); track c.date) {
            @if (c.future) {
              <div class="cell future"><span class="n">{{ c.day }}</span></div>
            } @else {
              <a class="cell" [routerLink]="['/giorno', c.date]" [class.incomplete]="c.totals?.incomplete">
                <span class="n">{{ c.day }}</span>
                <span class="v">{{ eur(c.totals?.benefitEur) }}</span>
              </a>
            }
          }
        </div>
      </div>
      <p class="muted">Un giorno sottolineato ha dati mancanti in qualche intervallo.</p>
    </div>
  `,
  styles: `
    .grid { display: grid; grid-template-columns: repeat(7, 1fr); gap: 6px; }
    .head { font-size: var(--fs-small); color: var(--text-muted); text-align: center; }
    .cell { display: grid; gap: 2px; padding: 8px 6px; border-radius: var(--radius-sm); background: var(--surface-2); text-decoration: none; min-height: 64px; }
    .cell .n { font-size: var(--fs-small); color: var(--text-muted); }
    .cell .v { font-weight: 600; font-variant-numeric: tabular-nums; }
    .cell.incomplete .v { text-decoration: underline dotted; }
    .cell.future { opacity: 0.4; }
    @media (max-width: 860px) { .cell .v { font-size: 0.75rem; } .cell { min-height: 52px; padding: 6px 3px; } }
  `,
})
export class CalendarPage {
  private readonly api = inject(Api);
  protected readonly thisMonth = todayIt().slice(0, 7);
  protected readonly month = signal(this.thisMonth);
  protected readonly days = signal<PeriodTotals[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly weekdays = ['lun', 'mar', 'mer', 'gio', 'ven', 'sab', 'dom'];
  protected readonly eur = fmtEur;

  protected readonly monthLabel = computed(() =>
    new Intl.DateTimeFormat('it-IT', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${this.month()}-15T12:00:00Z`)));

  protected readonly blanks = computed(() => {
    const dow = new Date(`${this.month()}-01T12:00:00Z`).getUTCDay(); // 0 = domenica
    return Array.from({ length: (dow + 6) % 7 });
  });

  protected readonly cells = computed<Cell[]>(() => {
    const today = todayIt();
    const byDate = new Map(this.days().map(d => [d.period, d.totals]));
    return this.dates().map(date => ({ date, day: +date.slice(8), totals: byDate.get(date) ?? null, future: date > today }));
  });

  constructor() {
    effect(() => void this.load());
  }

  protected shift(months: number) {
    const d = new Date(`${this.month()}-15T12:00:00Z`);
    d.setUTCMonth(d.getUTCMonth() + months);
    this.month.set(d.toISOString().slice(0, 7));
  }

  private dates(): string[] {
    const [y, m] = this.month().split('-').map(Number);
    const last = new Date(Date.UTC(y, m, 0)).getUTCDate();
    return Array.from({ length: last }, (_, i) => `${this.month()}-${String(i + 1).padStart(2, '0')}`);
  }

  private async load() {
    const dates = this.dates();
    const to = [dates[dates.length - 1], todayIt()].sort()[0];
    this.error.set(null);
    if (to < dates[0]) { this.days.set([]); return; }
    try {
      this.days.set(await this.api.days(dates[0], to));
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
