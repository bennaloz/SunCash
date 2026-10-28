# Istruzioni di progetto — SunCash

SunCash mostra quanto rende l'impianto fotovoltaico, per quarto d'ora, ora, giorno e mese,
sommando due voci: il **valore dell'energia immessa** (kWh immessi × prezzo zonale MGP del GME)
e il **risparmio da autoconsumo** (kWh autoconsumati × prezzo variabile della bolletta). I dati
dell'impianto arrivano da un fornitore intercambiabile (oggi Huawei FusionSolar). Un solo utente
(il proprietario dell'impianto), un solo impianto, niente multi-tenant.

**Source of truth: [`docs/analisi.md`](docs/analisi.md).** Questo file ne riassume le regole che
valgono a ogni modifica e non le ripete per esteso: per formule, schema, endpoint e flussi delle
API esterne si legge l'analisi. Se una decisione cambia, si aggiorna **prima** l'analisi e poi,
se serve, questo riassunto — mai il contrario, così i due testi non si contraddicono.

## ⛔ Repository pubblico: cosa non entra mai

- **Segreti**: solo `dotnet user-secrets` (sviluppo) e variabili d'ambiente (container). Mai in
  `appsettings*.json`, test, issue, commit.
- **Dati dell'impianto**: niente `stationCode`, nome impianto, indirizzo, coordinate, numeri di
  serie, potenza installata — né in codice, né in test, né in screenshot. Gli esempi usano
  segnaposto (`<segreto>`, `<prefisso>`).
- **Offerta luce e bollette reali**: vivono nel database, inserite dall'app; nel repo solo esempi.
  La parte regolata ARERA (`regulated_charges`) invece è pubblica e sta nel repo.
- **Fixture dei fornitori di energia**: anonimizzate (codici sostituiti, valori alterati, struttura identica).
- **Fixture GME**: sintetiche, nel formato reale. I dati GME non si ridistribuiscono.
- **File reali**: solo in `tests/fixtures.local/` (ignorata da git); i test che li usano sono
  marcati e girano solo in locale.
- **Database e backup** (`*.db`, `*.db-wal`, `*.db-shm`): mai nel repo.

Prima di ogni commit: `git diff --cached` e controllare che non ci sia nulla di quanto sopra.

## Regole di dominio che non si negoziano

- **Il valore in euro non si salva mai**: si salvano energia, prezzi e tariffe grezzi, valore e
  risparmio li calcolano le view (`v_valuation_quarter` → `hour` → `day` → `month`). Un cambio di regola non
  richiede migrazioni di dati.
- **Prezzo del calcolo = prezzo zonale MGP** della zona configurata (`Valuation:Zone`, nessuna
  zona cablata nel codice). Il **PUN** si importa solo come riferimento nei grafici, non entra
  nel valore dell'immesso; serve al risparmio solo se l'offerta luce è indicizzata al PUN.
- **Regime** (`RidZonale` | `RidPmg`) da configurazione; il PMG sta nella tabella
  `price_floors(year, eur_mwh)`, non in costanti.
- **Autoconsumo** = consumo − prelievo. C'è una **batteria**: se il fornitore non dà il consumo
  lo si ricava dal bilancio (prodotta + prelevata − immessa − carica + scarica), **mai** come
  prodotta − immessa; la riga dichiara la base usata. Il prezzo è solo la parte **variabile** della bolletta
  (energia, perdite, trasporto e oneri variabili, accise, IVA): le quote fisse non si evitano.
  Due tabelle con data di validità, mai in configurazione: `tariffs` (offerta dell'utente; oggi
  modulo manuale, domani codice offerta ARERA o PDF — tutte le vie scrivono lì, colonna `origin`)
  e `regulated_charges` (trasporto, oneri, accise, IVA: dati ARERA nel repo).
- **Granularità**: dati a 5 minuti del meter → aggregati a 15 minuti con prezzo a 15 minuti;
  solo dato orario → prezzo orario, ora come blocco unico. Ogni riga dichiara la sua
  `Resolution` (`Quarter` | `Hour`).
- **Tempo**: tutto in UTC nel database (epoch in secondi, `INTEGER`); conversioni con
  `TimeZoneInfo` `Europe/Rome`, **mai offset fissi**. SQLite non conosce i fusi: data italiana,
  etichetta locale e fascia ARERA le scrive **solo il dominio** nel calendario `time_slots`, e le
  view leggono da lì. I giorni del cambio d'ora hanno 92 e 100 quarti d'ora e vanno testati.
- **Costi fissi e corrispettivo GSE**: solo nel cumulato, mai nel valore orario.
- **Import idempotenti**: ogni scrittura è un upsert sulla chiave primaria.
- **I numeri sono una stima**: il GSE liquida sulle misure del distributore, non del meter.

## Architettura in breve

```text
src/SunCash.Domain/          tipi, calcolatore, fusi orari, fasce ARERA — NESSUNA dipendenza esterna
src/SunCash.Infrastructure/  EF Core (SQLite), client GME, parser
  Energy/<Fornitore>/        un fornitore di energia = una cartella che implementa IEnergySource
src/SunCash.Api/             ASP.NET Core: endpoint + BackgroundService dei due job
src/SunCash.Web/             Angular — nessuna logica di calcolo, nessuna conversione di fuso
tests/…                      Domain / Infrastructure (WireMock.Net, SQLite su file) / Api
```

- .NET 10, SQLite (un file, WAL attivo, energia in Wh `INTEGER`, prezzi `REAL`, arrotondamento
  al centesimo solo in presentazione), configurazione con `IOptions<T>` validate all'avvio.
- I job scrivono solo dati grezzi; l'API legge le view e scrive solo i dati manuali
  (`tariffs`, `bills`, `gse_payments`).
- SQLite vuole **un solo processo che scrive** e **mai un file system di rete**: i job restano
  nell'host dell'API, il volume è su disco locale.
- Le date in ingresso all'API sono in ora italiana (`yyyy-MM-dd`); le risposte portano il
  timestamp UTC **e** l'etichetta locale già calcolata.
- **Fornitori di energia intercambiabili**: tutto ciò che è specifico di un fornitore (login,
  rate limit, codici d'errore, nomi dei campi) sta nella sua implementazione di `IEnergySource`.
  Job `energy-import`, schema, view e API **non nominano mai un fornitore**: se serve farlo, il
  contratto è sbagliato. Un fornitore attivo alla volta (`Energy:Provider`), opzioni sotto
  `Energy:<Fornitore>`, misure distinte per `source`.

### Client esterni — vincoli che costano se ignorati

- **FusionSolar**: una sola sessione attiva, token in memoria, re-login solo a sessione scaduta.
  Controllare sempre `success`/`failCode` anche con HTTP 200. Un limitatore per endpoint
  (`FusionSolar:MinIntervalPerEndpoint`). Su «frequenza eccessiva» il job **si ferma fino al
  ciclo successivo**: i retry ravvicinati allungano il blocco.
- **GME**: login `/Auth` (JWT) a ogni esecuzione, `RequestData`, `contentResponse` = zip in
  Base64 decompresso in memoria.

## `<DA VERIFICARE>`: non inventare

L'analisi marca come `<DA VERIFICARE>` ciò che non è ancora noto (prefisso FusionSolar, nomi dei
campi KPI, codici `failCode`, parametri `RequestData` GME, orario di pubblicazione MGP,
meccanismo PMG, granularità GSE). **Non si riempiono a intuito**: si lasciano configurabili o
come costanti nominate con un commento `// DA VERIFICARE: …` che rimanda all'analisi, e quando
il valore è verificato si aggiorna l'analisi.

## Piano di lavoro

Nove milestone, ognuna con il suo criterio di «fatto» in
[`docs/analisi.md` § Piano di sviluppo](docs/analisi.md#piano-di-sviluppo-per-claude-code):

1. Scheletro · 2. Dominio e calcolo · 3. Client GME · 4. Fornitori di energia e FusionSolar ·
5. Persistenza e view · 6. API · 7. Frontend · 8. Deploy · 9. Confronto con la realtà

Una milestone si chiude solo quando il suo criterio è verificato, non quando compila. Modbus come
secondo fornitore è fuori dalle milestone.

## Test

- Dominio testato senza I/O: cambio d'ora (92/96/100 quarti), ricaduta al prezzo orario,
  regimi di valorizzazione, fasce ARERA nei festivi, i tre tipi di offerta luce.
- Client esterni contro server finti (WireMock.Net), mai contro i servizi reali in CI.
- Persistenza su SQLite vero (file temporaneo), mai sul provider InMemory di EF Core.
- Il job `energy-import` si testa con un fornitore finto: prova che il contratto basta.
- I totali delle view devono coincidere con quelli del calcolatore di dominio.

## Git

- Rami di lavoro dedicati, mai commit diretti sul ramo principale.
- Messaggi in stile Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:`, …).
- La CI (GitHub Actions) esegue build e `dotnet test`; si guarda che sia verde prima di
  mergiare.
