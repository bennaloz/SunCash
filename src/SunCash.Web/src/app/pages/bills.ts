import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, Bill, GsePayment, OfferKind, RegulatedCharges, Tariff, errorMessage } from '../api';
import { fmtEur, fmtEurKwh, fmtKwh, longDate, todayIt } from '../format';

/**
 * «Bollette»: l'offerta luce (modulo manuale: la via della prima versione), la parte regolata
 * caricata dal repo, le bollette reali accanto alla stima, i pagamenti GSE.
 */
@Component({
  selector: 'app-bills',
  imports: [FormsModule],
  template: `
    <div class="page">
      <h1>Bollette e offerta luce</h1>
      @if (error()) { <div class="notice error">{{ error() }}</div> }

      <section class="card">
        <h2>Offerta luce</h2>
        <p class="muted">Una riga per ogni contratto, dalla data in cui inizia. Basta aggiornarla quando cambia l'offerta.</p>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Dal</th><th>Tipo</th><th>Prezzi energia</th><th class="num">Perdite</th><th></th></tr></thead>
            <tbody>
              @for (t of tariffs(); track t.validFrom) {
                <tr>
                  <td>{{ long(t.validFrom) }}</td>
                  <td>{{ kindLabel[t.kind] }}</td>
                  <td>{{ prices(t) }}</td>
                  <td class="num">{{ (t.lossesFactor * 100).toFixed(1) }}%</td>
                  <td><button (click)="deleteTariff(t.validFrom)">Elimina</button></td>
                </tr>
              } @empty {
                <tr><td colspan="5" class="muted">Nessuna offerta inserita: il risparmio da autoconsumo non si può calcolare.</td></tr>
              }
            </tbody>
          </table>
        </div>
        <form class="grid" (ngSubmit)="saveTariff()">
          <label>Valida dal <input type="date" name="validFrom" [(ngModel)]="tariff.validFrom" required /></label>
          <label>Tipo di offerta
            <select name="kind" [(ngModel)]="tariff.kind">
              <option value="Monoraria">Prezzo unico</option>
              <option value="Fasce">A fasce F1/F2/F3</option>
              <option value="IndicizzataPun">Legata al PUN</option>
            </select>
          </label>
          @switch (tariff.kind) {
            @case ('Monoraria') {
              <label>Prezzo €/kWh <input type="number" step="0.0001" min="0" name="f1" [(ngModel)]="tariff.energyF1EurKwh" /></label>
            }
            @case ('Fasce') {
              <label>F1 €/kWh <input type="number" step="0.0001" min="0" name="f1" [(ngModel)]="tariff.energyF1EurKwh" /></label>
              <label>F2 €/kWh <input type="number" step="0.0001" min="0" name="f2" [(ngModel)]="tariff.energyF2EurKwh" /></label>
              <label>F3 €/kWh <input type="number" step="0.0001" min="0" name="f3" [(ngModel)]="tariff.energyF3EurKwh" /></label>
            }
            @case ('IndicizzataPun') {
              <label>Spread €/kWh <input type="number" step="0.0001" min="0" name="spread" [(ngModel)]="tariff.punSpreadEurKwh" /></label>
              <label>PUN
                <select name="basis" [(ngModel)]="tariff.punBasis">
                  <option value="Orario">orario</option>
                  <option value="Mensile">media mensile</option>
                </select>
              </label>
            }
          }
          <label>Perdite di rete % <input type="number" step="0.1" min="0" max="100" name="losses" [(ngModel)]="lossesPercent" /></label>
          <button class="primary" type="submit">Salva offerta</button>
        </form>
      </section>

      <section class="card">
        <h2>Parte regolata (ARERA)</h2>
        @if (charges().length === 0) {
          <div class="notice warn">Non ci sono ancora i valori di trasporto, oneri, accise e IVA: il risparmio non si calcola. Si aggiornano nel repository, non da qui.</div>
        } @else {
          <table>
            <thead><tr><th>Dal</th><th class="num">Trasporto e oneri</th><th class="num">Accise</th><th class="num">IVA</th></tr></thead>
            <tbody>
              @for (c of charges(); track c.validFrom) {
                <tr><td>{{ long(c.validFrom) }}</td><td class="num">{{ eurKwh(c.networkEurKwh) }}</td>
                  <td class="num">{{ eurKwh(c.exciseEurKwh) }}</td><td class="num">{{ c.vatRate * 100 }}%</td></tr>
              }
            </tbody>
          </table>
        }
      </section>

      <section class="card">
        <h2>Bollette reali</h2>
        <p class="muted">Accanto a ogni bolletta, i prelievi misurati dall'impianto e il loro costo variabile stimato. Se i kWh coincidono e il costo variabile è plausibile, il modello della tariffa regge.</p>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Periodo</th><th class="num">kWh in bolletta</th><th class="num">kWh misurati</th>
              <th class="num">Importo</th><th class="num">Costo variabile stimato</th><th></th></tr></thead>
            <tbody>
              @for (b of bills(); track b.periodFrom + b.periodTo) {
                <tr>
                  <td>{{ long(b.periodFrom) }} – {{ long(b.periodTo) }}</td>
                  <td class="num">{{ kwh(b.importKwh) }}</td>
                  <td class="num">{{ kwh(b.estimatedImportKwh) }}</td>
                  <td class="num">{{ eur(b.amountEur) }}</td>
                  <td class="num">{{ eur(b.estimatedVariableCostEur) }}</td>
                  <td><button (click)="deleteBill(b)">Elimina</button></td>
                </tr>
              } @empty { <tr><td colspan="6" class="muted">Nessuna bolletta inserita.</td></tr> }
            </tbody>
          </table>
        </div>
        <form class="grid" (ngSubmit)="saveBill()">
          <label>Dal <input type="date" name="billFrom" [(ngModel)]="bill.from" required /></label>
          <label>Al <input type="date" name="billTo" [(ngModel)]="bill.to" required /></label>
          <label>kWh prelevati <input type="number" step="1" min="0" name="billKwh" [(ngModel)]="bill.importKwh" /></label>
          <label>Importo € <input type="number" step="0.01" min="0" name="billAmount" [(ngModel)]="bill.amountEur" required /></label>
          <button class="primary" type="submit">Salva bolletta</button>
        </form>
      </section>

      <section class="card">
        <h2>Pagamenti GSE</h2>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Mese</th><th class="num">Importo</th></tr></thead>
            <tbody>
              @for (p of payments(); track p.month) { <tr><td>{{ p.month }}</td><td class="num">{{ eur(p.amountEur) }}</td></tr> }
              @empty { <tr><td colspan="2" class="muted">Nessun pagamento inserito.</td></tr> }
            </tbody>
          </table>
        </div>
        <form class="grid" (ngSubmit)="savePayment()">
          <label>Mese di competenza <input type="month" name="gseMonth" [(ngModel)]="payment.month" required /></label>
          <label>Importo € <input type="number" step="0.01" min="0" name="gseAmount" [(ngModel)]="payment.amountEur" required /></label>
          <button class="primary" type="submit">Salva pagamento</button>
        </form>
      </section>
    </div>
  `,
})
export class BillsPage {
  private readonly api = inject(Api);
  protected readonly tariffs = signal<Tariff[]>([]);
  protected readonly charges = signal<RegulatedCharges[]>([]);
  protected readonly bills = signal<Bill[]>([]);
  protected readonly payments = signal<GsePayment[]>([]);
  protected readonly error = signal<string | null>(null);

  protected readonly kindLabel: Record<OfferKind, string> = { Monoraria: 'Prezzo unico', Fasce: 'A fasce', IndicizzataPun: 'Legata al PUN' };
  protected readonly eur = fmtEur;
  protected readonly kwh = fmtKwh;
  protected readonly eurKwh = fmtEurKwh;
  protected readonly long = longDate;

  protected tariff: Tariff = this.emptyTariff();
  protected lossesPercent = 0;
  protected bill = { from: '', to: '', importKwh: null as number | null, amountEur: null as number | null };
  protected payment = { month: todayIt().slice(0, 7), amountEur: null as number | null };

  constructor() {
    void this.load();
  }

  protected prices(t: Tariff): string {
    switch (t.kind) {
      case 'Monoraria': return fmtEurKwh(t.energyF1EurKwh);
      case 'Fasce': return `F1 ${fmtEurKwh(t.energyF1EurKwh)} · F2 ${fmtEurKwh(t.energyF2EurKwh)} · F3 ${fmtEurKwh(t.energyF3EurKwh)}`;
      default: return `PUN ${t.punBasis === 'Mensile' ? 'mensile' : 'orario'} + ${fmtEurKwh(t.punSpreadEurKwh)}`;
    }
  }

  protected saveTariff() {
    return this.run(() => this.api.saveTariff({ ...this.tariff, lossesFactor: (this.lossesPercent ?? 0) / 100 }), () => {
      this.tariff = this.emptyTariff();
      this.lossesPercent = 0;
    });
  }

  protected deleteTariff(validFrom: string) { return this.run(() => this.api.deleteTariff(validFrom)); }

  protected saveBill() {
    const b = this.bill;
    return this.run(() => this.api.saveBill(b.from, b.to, { importKwh: b.importKwh, amountEur: b.amountEur ?? 0, notes: null }),
      () => (this.bill = { from: '', to: '', importKwh: null, amountEur: null }));
  }

  protected deleteBill(b: Bill) { return this.run(() => this.api.deleteBill(b.periodFrom, b.periodTo)); }

  protected savePayment() {
    return this.run(() => this.api.saveGsePayment(this.payment.month, this.payment.amountEur ?? 0, null),
      () => (this.payment.amountEur = null));
  }

  private async run(action: () => Promise<unknown>, then?: () => void) {
    this.error.set(null);
    try {
      await action();
      then?.();
      await this.load();
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  private async load() {
    try {
      const [t, c, b, p] = await Promise.all([this.api.tariffs(), this.api.regulatedCharges(), this.api.bills(), this.api.gsePayments()]);
      this.tariffs.set(t);
      this.charges.set(c);
      this.bills.set(b);
      this.payments.set(p);
    } catch (e) {
      this.error.set(errorMessage(e));
    }
  }

  private emptyTariff(): Tariff {
    return {
      validFrom: todayIt(), kind: 'Monoraria', energyF1EurKwh: null, energyF2EurKwh: null, energyF3EurKwh: null,
      punSpreadEurKwh: null, punBasis: 'Orario', lossesFactor: 0, origin: 'Manual', offerCode: null, notes: null,
    };
  }
}
