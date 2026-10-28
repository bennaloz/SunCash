/** Formattazione italiana di numeri e date: l'unico posto dove si arrotonda. */
const eur = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' });
const kwh = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 1 });
const price = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 2 });
const eurKwh = new Intl.NumberFormat('it-IT', { minimumFractionDigits: 4, maximumFractionDigits: 4 });

export const fmtEur = (v: number | null | undefined) => (v == null ? '—' : eur.format(v));
export const fmtKwh = (v: number | null | undefined) => (v == null ? '—' : `${kwh.format(v)} kWh`);
export const fmtEurMwh = (v: number | null | undefined) => (v == null ? '—' : `${price.format(v)} €/MWh`);
export const fmtEurKwh = (v: number | null | undefined) => (v == null ? '—' : `${eurKwh.format(v)} €/kWh`);

/** Oggi in ora italiana, yyyy-MM-dd. */
export function todayIt(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Rome' }).format(new Date());
}

/** Aggiunge giorni a una data yyyy-MM-dd (aritmetica di calendario, senza fusi). */
export function addDays(date: string, days: number): string {
  const d = new Date(`${date}T12:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}

/** «28 ottobre 2026». */
export function longDate(date: string): string {
  return new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' })
    .format(new Date(`${date}T12:00:00Z`));
}

/** «ott 2026» da yyyy-MM. */
export function shortMonth(month: string): string {
  return new Intl.DateTimeFormat('it-IT', { month: 'short', timeZone: 'UTC' }).format(new Date(`${month}-15T12:00:00Z`));
}

/** Colore di un token CSS, per i grafici (che non leggono le variabili da soli). */
export function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}
