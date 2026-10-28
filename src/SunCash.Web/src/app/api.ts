import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Totali di un periodo, come li restituisce l'API (kWh ed euro non arrotondati). */
export interface Totals {
  exportKwh: number;
  selfConsumptionKwh: number;
  importKwh: number;
  exportValueEur: number;
  savingsEur: number;
  benefitEur: number;
  importCostEur: number;
  capturedPriceEurMwh: number | null;
  averageZonalEurMwh: number | null;
  incomplete: boolean;
  savingsUnavailable: boolean;
}

/** Una riga di valorizzazione: etichetta italiana già pronta, niente conversioni di fuso qui. */
export interface ValuationRow {
  startUtc: string;
  minutes: number;
  localDate: string;
  localLabel: string;
  resolution: 'Quarter' | 'Hour';
  band: 'F1' | 'F2' | 'F3';
  exportKwh: number | null;
  zonalEurMwh: number | null;
  punEurMwh: number | null;
  exportValueEur: number | null;
  selfConsumptionKwh: number | null;
  selfConsumptionBasis: 'Measured' | 'Balance' | 'Unavailable';
  billPriceEurKwh: number | null;
  savingsEur: number | null;
}

export interface DayValuation { date: string; totals: Totals; rows: ValuationRow[]; }
export interface PeriodTotals { period: string; startUtc: string | null; totals: Totals; }
export interface MonthTotals { month: string; totals: Totals; gsePaymentEur: number | null; }

export interface Summary {
  today: Totals;
  yesterday: Totals;
  month: Totals;
  year: Totals;
  bestDay: PeriodTotals | null;
  regime: 'RidZonale' | 'RidPmg';
  exportValueRecognizedEur: number;
  annualFixedCostsEur: number;
}

export type OfferKind = 'Monoraria' | 'Fasce' | 'IndicizzataPun';

export interface Tariff {
  validFrom: string;
  kind: OfferKind;
  energyF1EurKwh: number | null;
  energyF2EurKwh: number | null;
  energyF3EurKwh: number | null;
  punSpreadEurKwh: number | null;
  punBasis: 'Orario' | 'Mensile' | null;
  lossesFactor: number;
  origin: 'Manual' | 'OfferCode' | 'BillPdf';
  offerCode: string | null;
  notes: string | null;
}

export interface RegulatedCharges { validFrom: string; networkEurKwh: number; exciseEurKwh: number; vatRate: number; }

export interface Bill {
  periodFrom: string;
  periodTo: string;
  importKwh: number | null;
  amountEur: number;
  origin: string;
  notes: string | null;
  estimatedImportKwh: number | null;
  estimatedVariableCostEur: number | null;
}

export interface GsePayment { month: string; amountEur: number; notes: string | null; }

export interface SyncJobStatus {
  job: string;
  source: string | null;
  lastSuccessUtc: string | null;
  cursor: string | null;
  lastError: string | null;
  stale: boolean;
}

export interface SyncStatus {
  activeSource: string;
  jobs: SyncJobStatus[];
  daysWithGaps: string[];
  regulatedChargesMissing: boolean;
}

export interface SyncOutcome { status: 'Done' | 'RetryLater' | 'Failed'; message: string | null; }

/** Unico punto da cui il frontend parla con l'API. */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly http = inject(HttpClient);

  day(date: string) { return this.get<DayValuation>(`/api/valuation/day/${date}`); }
  days(from: string, to: string) { return this.get<PeriodTotals[]>(`/api/valuation/days?from=${from}&to=${to}`); }
  months(year: number) { return this.get<MonthTotals[]>(`/api/valuation/months?year=${year}`); }
  summary() { return this.get<Summary>('/api/summary'); }

  tariffs() { return this.get<Tariff[]>('/api/tariffs'); }
  saveTariff(t: Tariff) { return firstValueFrom(this.http.put(`/api/tariffs/${t.validFrom}`, t)); }
  deleteTariff(validFrom: string) { return firstValueFrom(this.http.delete(`/api/tariffs/${validFrom}`)); }
  regulatedCharges() { return this.get<RegulatedCharges[]>('/api/regulated-charges'); }

  bills() { return this.get<Bill[]>('/api/bills'); }
  saveBill(from: string, to: string, body: { importKwh: number | null; amountEur: number; notes: string | null }) {
    return firstValueFrom(this.http.put(`/api/bills/${from}/${to}`, body));
  }
  deleteBill(from: string, to: string) { return firstValueFrom(this.http.delete(`/api/bills/${from}/${to}`)); }

  gsePayments() { return this.get<GsePayment[]>('/api/gse-payments'); }
  saveGsePayment(month: string, amountEur: number, notes: string | null) {
    return firstValueFrom(this.http.put(`/api/gse-payments/${month}`, { amountEur, notes }));
  }

  syncStatus() { return this.get<SyncStatus>('/api/sync/status'); }
  runJob(job: string) { return firstValueFrom(this.http.post<SyncOutcome>(`/api/sync/${job}/run`, {})); }

  private get<T>(url: string) { return firstValueFrom(this.http.get<T>(url)); }
}

/** Il messaggio da mostrare per un errore dell'API (ProblemDetails o rete). */
export function errorMessage(e: unknown): string {
  const err = e as { error?: { errors?: Record<string, string[]>; title?: string }; message?: string };
  const fields = err.error?.errors ? Object.values(err.error.errors).flat() : [];
  return fields.length > 0 ? fields.join(' ') : err.error?.title ?? 'Il server non risponde. Riprova tra poco.';
}
