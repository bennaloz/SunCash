import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, SyncStatus, errorMessage } from '../api';
import { longDate } from '../format';

/** «Stato»: esito dei job, intervalli mancanti, avvio manuale. */
@Component({
  selector: 'app-status',
  imports: [RouterLink],
  template: `
    <div class="page">
      <h1>Stato</h1>
      @if (error()) { <div class="notice error">{{ error() }}</div> }
      @if (status(); as s) {
        @if (s.regulatedChargesMissing) {
          <div class="notice warn">Manca la parte regolata ARERA: il risparmio da autoconsumo non si calcola.</div>
        }
        <section class="card table-wrap">
          <h2>Sincronizzazione · fornitore di energia: {{ s.activeSource }}</h2>
          <table>
            <thead><tr><th>Job</th><th>Ultimo successo</th><th>Storico completo fino a</th><th>Ultimo messaggio</th><th></th></tr></thead>
            <tbody>
              @for (j of s.jobs; track j.job) {
                <tr>
                  <td>{{ jobLabel[j.job] ?? j.job }} @if (j.stale) { <span class="notice warn">da più di 24 ore</span> }</td>
                  <td>{{ j.lastSuccessUtc ? when(j.lastSuccessUtc) : 'mai' }}</td>
                  <td>{{ j.cursor ? long(j.cursor) : '—' }}</td>
                  <td>{{ j.lastError ?? '—' }}</td>
                  <td><button (click)="run(j.job)" [disabled]="running() === j.job">{{ running() === j.job ? 'In corso…' : 'Avvia ora' }}</button></td>
                </tr>
              }
            </tbody>
          </table>
          @if (lastRun()) { <p class="muted">{{ lastRun() }}</p> }
        </section>
        <section class="card">
          <h2>Giorni con dati mancanti (ultimi 30)</h2>
          @for (d of s.daysWithGaps; track d) {
            <a class="button gap" [routerLink]="['/giorno', d]">{{ long(d) }}</a>
          } @empty { <p class="muted">Nessun buco negli ultimi 30 giorni.</p> }
        </section>
      }
    </div>
  `,
  styles: `.gap { display: inline-block; margin: 0 6px 6px 0; text-decoration: none; }`,
})
export class StatusPage {
  private readonly api = inject(Api);
  protected readonly status = signal<SyncStatus | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly running = signal<string | null>(null);
  protected readonly lastRun = signal<string | null>(null);
  protected readonly long = longDate;
  protected readonly jobLabel: Record<string, string | undefined> = { 'gme-prices': 'Prezzi GME', 'energy-import': 'Energia dell’impianto', 'db-backup': 'Backup del database' };

  constructor() {
    void this.load();
  }

  protected when(utc: string): string {
    return new Intl.DateTimeFormat('it-IT', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Europe/Rome' }).format(new Date(utc));
  }

  protected async run(job: string) {
    this.running.set(job);
    this.lastRun.set(null);
    try {
      const outcome = await this.api.runJob(job);
      this.lastRun.set(outcome.status === 'Done' ? 'Completato.' : outcome.message ?? outcome.status);
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    } finally {
      this.running.set(null);
    }
  }

  private async load() {
    try {
      this.status.set(await this.api.syncStatus());
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }
}
