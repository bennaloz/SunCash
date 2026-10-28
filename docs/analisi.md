# Analisi app guadagni fotovoltaico

Oct 28, 2026 · @Riccardo Beninatto

> **Revisione 2026-10-28.** Rispetto alla prima stesura: database **SQLite** al posto di
> PostgreSQL; lettura dell'energia pensata per **più fornitori** (FusionSolar è il primo, non
> l'unico); il **risparmio da autoconsumo** entra nel perimetro della prima versione.

## Nome dell'app

Nome scelto: **SunCash**. Dice subito cosa mostra l'app, cioè i soldi che porta l'impianto. Repo GitHub `SunCash`, namespace `SunCash.*`.

Scartati: Quartino, SunSpot, SunCashback (troppo lungo, richiama i programmi di rimborso), SolarCash (una sillaba in più, suona da azienda del settore).

## Obiettivo e perimetro

SunCash mostra quanto rende l'impianto, giorno per giorno e ora per ora, sommando due voci:

- **valore dell'energia immessa**: kWh immessi per il prezzo zonale MGP pubblicato dal GME nello stesso intervallo;
- **risparmio da autoconsumo**: kWh prodotti e consumati in casa per il prezzo che la propria offerta luce avrebbe fatto pagare per prelevarli dalla rete.

L'utente è uno solo: il proprietario dell'impianto. L'app gira in casa o su un piccolo hosting e non è multi-tenant.

Cosa fa nella prima versione:

- importa ogni ora energia immessa, prelevata, prodotta e consumata dal fornitore di dati dell'impianto (oggi Huawei FusionSolar);
- importa ogni giorno i prezzi MGP zonali e il PUN Index GME a 15 minuti;
- calcola il valore dell'energia immessa e il risparmio da autoconsumo per quarto d'ora, ora, giorno e mese;
- mostra una dashboard con grafici giornalieri e mensili e il cumulato da inizio anno;
- ricalcola lo storico quando cambia la regola di valorizzazione o la tariffa inserita.

Cosa resta fuori:

- lo scambio sul posto, che segue un'altra logica (contributo in conto scambio);
- la riconciliazione automatica con i pagamenti GSE, che restano un dato inserito a mano;
- più impianti e più utenti.

I numeri sono una stima. Il GSE liquida sulle misure del distributore, non su quelle del meter dell'impianto, quindi va previsto uno scarto, da misurare confrontando i primi mesi con gli estratti GSE. Anche il risparmio è una stima: dipende da quanto fedelmente la tariffa inserita riproduce la bolletta.

## Repository pubblico

Il codice sta in un repository GitHub pubblico, quindi nel repo non finiscono credenziali, dati dell'impianto né dati del GME.

- **Segreti**: solo `dotnet user-secrets` e variabili d'ambiente. Il `.gitignore` esclude `.env`, `appsettings.*.local.json`, i file di database (`*.db`, `*.db-wal`, `*.db-shm`) e i file di backup. Su GitHub vanno attivati secret scanning e push protection.
- **Dati dell'impianto**: niente `stationCode`, nomi impianto, indirizzi, coordinate, numeri di serie o potenza installata in codice, test, issue e screenshot. Le configurazioni di esempio usano solo segnaposto.
- **Dati della bolletta**: le tariffe reali si inseriscono dall'app e vivono nel database; nel repo solo valori di esempio.
- **Fixture dei fornitori di energia**: anonimizzate, con codici sostituiti e valori alterati, ma struttura identica alla risposta reale.
- **Fixture GME**: sintetiche, nello stesso formato dei file reali, perché i dati del GME non si possono ridistribuire.
- **File reali**: solo in `tests/fixtures.local/`, ignorata da git. I test che li usano sono marcati e girano solo in locale.
- **Database e backup**: mai nel repo.

La zona di mercato è un parametro (`Valuation:Zone`): il codice non presuppone una zona specifica.

## Fonti dati

Servono due tipi di fonte: un **fornitore di energia** (i dati dell'impianto) e il **GME** per i prezzi. Entrambe richiedono credenziali da chiedere prima di iniziare lo sviluppo.

### Energia: fornitori intercambiabili

La lettura dell'energia passa da un'interfaccia `IEnergySource`. Ogni fornitore (FusionSolar oggi; domani SolarEdge, Fronius, SMA, Modbus locale…) è un'implementazione a sé, in una propria cartella di `SunCash.Infrastructure`, con le proprie opzioni di configurazione. Il resto dell'app — job, database, calcolo, API — non sa quale fornitore è attivo.

Il contratto, in sostanza:

- **identità**: un id stabile (`fusionsolar-cloud`, `modbus`, …) che finisce nella colonna `source` delle misure;
- **capacità dichiarate**: granularità disponibili (5, 15, 60 minuti), profondità dello storico recuperabile, grandezze fornite (immessa, prelevata, prodotta, consumata, batteria);
- **lettura**: dato un giorno in ora italiana, restituisce le misure di quel giorno nel formato comune (`EnergyReading`), con l'energia in Wh;
- **limiti propri**: sessione, rate limit e codici d'errore sono affare dell'implementazione; verso il job il fornitore espone solo tre esiti — *fatto*, *riprova al prossimo ciclo*, *errore*.

Un impianto ha **un fornitore attivo alla volta** (`Energy:Provider`). Il cambio di fornitore è un evento raro: il nuovo riparte da dove si è fermato il vecchio, e le misure di entrambi restano distinte per `source`.

Aggiungere un fornitore vuol dire: una nuova implementazione di `IEnergySource`, la sua sezione di configurazione, le sue fixture anonimizzate, la registrazione nel contenitore. Nessuna modifica a job, schema o view.

#### Primo fornitore: Huawei FusionSolar

La prima versione usa l'API cloud Northbound; Modbus TCP resta un fornitore successivo.

|  | Northbound API (cloud) | Modbus TCP (locale) |
| --- | --- | --- |
| Accesso | Account Northbound creato dall'installatore o dall'admin cliente in **System > Company Management** | Porta Modbus abilitata su SDongle o inverter, rete di casa |
| Granularità | Oraria (`getKpiStationHour`); 5 minuti dallo storico dispositivo (`getDevHistoryKpi`) | Lettura istantanea, campionabile a piacere |
| Limiti | Rate limit stretti per endpoint, una sola sessione attiva | Nessuno, ma una sola connessione Modbus alla volta sul dongle |
| Storico | Recuperabile a ritroso | Solo da quando l'app è in funzione |
| Dipende da | Cloud Huawei | App sempre accesa in rete locale |

Flusso Northbound:

1. `POST /thirdData/login` con `userName` e `systemCode`; il token torna nell'header `xsrf-token` e va rimandato come `XSRF-TOKEN`.
2. Elenco impianti per ricavare lo `stationCode`. `getStationList` è deprecato: usare l'endpoint che lo sostituisce nella Northbound Interface Reference.
3. `POST /thirdData/getKpiStationHour` con `stationCodes` e `collectTime` (epoch in millisecondi del giorno): restituisce le righe orarie del giorno.
4. Per il dettaglio a 15 minuti: `getDevHistoryKpi` sul dispositivo meter, poi aggregazione.

Dominio base: il prefisso della pagina di login FusionSolar dell'utente, ad esempio `https://<prefisso>.fusionsolar.huawei.com/thirdData/`. `<DA VERIFICARE: prefisso reale dell'account>`

Nomi dei campi KPI per energia immessa, prelevata, prodotta e consumata: `<DA VERIFICARE: nella Northbound Interface Reference e su una risposta reale>`. Senza smart meter sul punto di scambio l'energia immessa non è disponibile e l'app non ha senso.

### Prezzi: API del GME

Dal 15 ottobre 2025 il GME espone i dati di mercato con un'API ufficiale; l'FTPS resta solo per un periodo transitorio.

1. Registrazione al form `https://api.mercatoelettrico.org/users/RegistrationForm/RegistrationRequest`; le credenziali arrivano via email.
2. Login con il metodo `/Auth`, che restituisce un JWT.
3. Richiesta con `RequestData`: i dati arrivano nel campo `contentResponse`, uno zip in Base64 da decomprimere.

Parametri della richiesta per esiti MGP, zona `NORD`, granularità 15 minuti: `<DA VERIFICARE: Manuale tecnico API GME>`.

I dati restano del GME e non si possono usare a scopo di lucro. Per un uso personale va bene; se SunCash diventasse un prodotto, servirebbe un'altra fonte o un accordo.

Fonti: [Huawei, openAPI account](https://docs.rs/crate/huawei_client/latest/source/docs/Communicate%20with%20FusionSolar%20through%20an%20openAPI%20account.pdf) · [HomeAssistant-FusionSolar](https://github.com/tijsverkoyen/homeassistant-fusionsolar) · [meteocontrol, setup Northbound](https://zammad.meteocontrol.de/help/en-us/6-datalogger-import-configuration/179-vcom-import-huawei-fusionsolar-api) · [GME, servizio API](https://gme.mercatoelettrico.org/en-US/Home/APIService) · [GME, manuale utente API](https://gme.mercatoelettrico.org/Portals/0/Documents/it-IT/20251015Manuale_Utente_API.pdf)

## Regole di calcolo

Il beneficio di ogni quarto d'ora è la somma di due voci; ore, giorni e mesi sono somme di quarti d'ora.

```latex
B_q = V_q + S_q
```

### Valore dell'energia immessa

```latex
V_q = \frac{E_q}{10^6} \cdot P_{z,q}
```

`E_q` è l'energia immessa nel quarto d'ora in Wh, `P_{z,q}` il prezzo zonale MGP in €/MWh, `V_q` il valore in euro.

#### Quale prezzo

Con il Ritiro Dedicato il GSE paga il prezzo zonale orario della zona in cui si trova l'impianto; nella configurazione di esempio è la zona Nord. Il PUN Index GME si importa comunque: nei grafici è il riferimento del prezzo lato acquisto, e serve al calcolo del risparmio quando l'offerta luce è indicizzata al PUN. Non entra mai nel valore dell'immesso.

#### Regime di valorizzazione

Il regime è un parametro di configurazione (`Valuation:Regime`), così lo storico si ricalcola se cambia.

| Regime | Regola | Note |
| --- | --- | --- |
| `RidZonale` | `V_q` come sopra | Caso base |
| `RidPmg` | Si calcolano sia la serie a prezzo zonale sia quella a prezzo minimo garantito; il GSE riconosce la più favorevole con il conguaglio annuale | PMG 2026: 47,5 €/MWh. `<DA VERIFICARE: meccanismo esatto di confronto nella convenzione GSE>` |

Il PMG va in una tabella `price_floors(year, eur_mwh)`, perché ARERA lo aggiorna ogni anno.

### Risparmio da autoconsumo

Il risparmio è l'energia prodotta e consumata in casa, valorizzata al prezzo che la bolletta avrebbe applicato prelevandola dalla rete **in quel quarto d'ora**.

```latex
S_q = \frac{A_q}{1000} \cdot T_q
```

`A_q` è l'energia autoconsumata in Wh, `T_q` il prezzo variabile della bolletta in €/kWh, `S_q` il risparmio in euro.

#### Energia autoconsumata

```latex
A_q = C_q - I_q
```

`C_q` è il consumo della casa, `I_q` l'energia prelevata dalla rete. Così il risparmio si conta nel momento in cui l'energia si consuma, e il conto resta giusto anche con una batteria: l'energia accumulata a mezzogiorno e consumata la sera vale al prezzo della sera.

L'impianto **ha una batteria** (deciso il 2026-10-28), quindi la scorciatoia `A_q = Pr_q - E_q` (prodotta meno immessa) **non va usata**: sposterebbe il risparmio nell'ora di carica, a un prezzo diverso, e ci metterebbe dentro le perdite della batteria. Se il fornitore non dà il consumo, lo si ricava dal bilancio dell'energia:

```latex
C_q = Pr_q + I_q - E_q - Ch_q + Dis_q
```

`Ch_q` e `Dis_q` sono l'energia caricata e scaricata dalla batteria. La riga dichiara da dove viene il consumo (`SelfConsumptionBasis = Measured | Balance`) e la dashboard lo segnala. Se il fornitore non dà né il consumo né i flussi della batteria, il risparmio non si calcola e la dashboard lo dice, invece di mostrare un numero sbagliato.

#### Prezzo della bolletta

Conta solo la parte di bolletta che **scala con i kWh prelevati**: le quote fisse e la quota potenza si pagano comunque e l'autoconsumo non le riduce.

```latex
T_q = \left( p^{en}_q \cdot (1 + \lambda) + p^{rete} + p^{acc} \right) \cdot (1 + iva)
```

| Termine | Cosa è | Da dove |
| --- | --- | --- |
| `p^{en}_q` | Prezzo dell'energia dell'offerta nel quarto d'ora | Dipende dal tipo di offerta, sotto |
| `λ` | Perdite di rete applicate all'energia | Bolletta; spesso 10,2% sulle offerte indicizzate, già incluse in quelle a prezzo fisso |
| `p^{rete}` | Trasporto e oneri di sistema, quota variabile in €/kWh | Bolletta |
| `p^{acc}` | Accise in €/kWh | Bolletta |
| `iva` | Aliquota IVA | Bolletta (10% per uso domestico) |

Tipo di offerta (`kind`):

| Tipo | `p^{en}_q` |
| --- | --- |
| `Monoraria` | Un prezzo fisso in €/kWh |
| `Fasce` | Prezzo F1, F2 o F3 secondo la fascia ARERA del quarto d'ora (festivi compresi) |
| `IndicizzataPun` | PUN Index GME / 1000 + spread in €/kWh; il PUN è orario o media mensile secondo il contratto |

Tutti i valori della bolletta stanno nella tabella `tariffs` con una data di inizio validità: quando cambia l'offerta si aggiunge una riga e lo storico continua a usare la tariffa del suo periodo. Tipo di offerta in essere e voci variabili della propria bolletta: `<DA VERIFICARE: ultima bolletta>`. Indicizzazione al PUN orario o mensile, se l'offerta è indicizzata: `<DA VERIFICARE: condizioni economiche del contratto>`.

### Granularità

I prezzi MGP sono a 15 minuti dal 1° ottobre 2025 e il GME li pubblica anche aggregati all'ora. Il calcolo segue questa regola:

- se ci sono i dati a 5 minuti del meter, si aggregano a 15 minuti e si usa il prezzo a 15 minuti;
- se c'è solo il dato orario, si usa il prezzo orario aggregato e ogni ora vale come un blocco unico.

Ogni riga di valore registra quale delle due vie ha usato (`Resolution = Quarter | Hour`), così la dashboard lo può segnalare. La stessa regola vale per il risparmio: con il solo dato orario, il prezzo della bolletta è quello dell'ora. Con quale granularità il GSE valorizzi oggi l'energia ritirata: `<DA VERIFICARE: convenzione GSE e delibere ARERA dopo il passaggio ai 15 minuti>`.

### Tempo e cambio d'ora

Tutti i timestamp si salvano in UTC. Il GME ragiona in ora italiana: il giorno del passaggio all'ora legale ha 23 ore (92 quarti d'ora), quello del ritorno all'ora solare 25 (100 quarti d'ora). La conversione usa `TimeZoneInfo` con l'id `Europe/Rome`, mai offset fissi. Servono test dedicati sulle due date di fine marzo e fine ottobre.

SQLite non conosce i fusi orari, quindi le view non possono convertire da sole. La conversione la fa **un solo punto**, il dominio, che scrive il calendario `time_slots`: per ogni quarto d'ora UTC la data italiana, l'etichetta locale e la fascia ARERA. Le view raggruppano e scelgono la fascia leggendo da lì. Le ore non hanno bisogno del calendario: l'Italia è sempre a un numero intero di ore da UTC, quindi l'ora UTC e l'ora italiana coincidono come intervallo.

### Costi

Il corrispettivo di gestione GSE e gli eventuali costi fissi si inseriscono come voci annue in configurazione e si mostrano solo nel cumulato, non nel valore orario.

Fonti: [GME, report MGP settimana 40/2025](<https://gme.mercatoelettrico.org/Portals/0/Documents/en-US/\\20251006MGPreportsettimanale40En.pdf>) · [GME, 15 minuti su MGP](<https://mercatoelettrico.org/Portals/0/Documenti/Presentazioni/216\\20250326_intro15mMGP.pdf>) · [Solare Industriale, Ritiro Dedicato](https://www.solareindustriale.it/incentivi/ritiro-dedicato) · [Enpal, Ritiro Dedicato 2026](https://www.enpal.com/it/fotovoltaico/ritiro-dedicato)

## Architettura

Un solo backend .NET ospita sia l'API sia i due job di import; il calcolo vive nelle view del database e in una libreria di dominio senza dipendenze, testabile da sola.

```text
 Fornitore energia ──► job energy-import ──┐
 (FusionSolar, …)                          ├──► SQLite ──► view di valorizzazione ──► API ──► Web (Angular)
 GME (API MGP/PUN) ──► job gme-prices ─────┘   (dati grezzi,  (euro calcolati,
                                                calendario,     mai salvati)
                                                tariffe)
```

I job scrivono solo dati grezzi con upsert; l'API non scrive misure né prezzi, legge le view di valutazione e scrive solo i dati inseriti a mano (tariffe, pagamenti GSE).

Struttura della solution (.NET 10, LTS):

```text
SunCash/
├── src/
│   ├── SunCash.Domain/          # tipi, calcolatore, fusi orari, fasce ARERA; nessuna dipendenza
│   ├── SunCash.Infrastructure/  # EF Core (SQLite), client GME, parser
│   │   └── Energy/
│   │       └── FusionSolar/     # un fornitore = una cartella che implementa IEnergySource
│   ├── SunCash.Api/             # host ASP.NET Core: endpoint + BackgroundService dei job
│   └── SunCash.Web/             # Angular
├── tests/
│   ├── SunCash.Domain.Tests/
│   ├── SunCash.Infrastructure.Tests/   # fixture anonimizzate e sintetiche, WireMock.Net, SQLite su file temporaneo
│   ├── SunCash.Api.Tests/
│   └── fixtures.local/                 # ignorata da git: file reali, solo in locale
├── docs/analisi.md
├── docker-compose.yml
├── .gitignore
├── LICENSE
└── README.md
```

I job stanno nello stesso host dell'API per semplicità di deploy, e con SQLite è anche la scelta giusta: un solo processo che scrive sul file. Se un giorno serve separarli, `SunCash.Infrastructure` è già condiviso, ma andrebbe rivista la scelta del database.

## Modello dati

Il database è **SQLite** con EF Core (`Microsoft.EntityFrameworkCore.Sqlite`): un file solo, niente server, backup con una copia. Energia, prezzi e tariffe si salvano grezzi; il valore in euro è sempre calcolato da una view, mai salvato, così un cambio di regola non richiede migrazioni di dati.

Tre scelte discendono da SQLite:

- **tempo come intero**: `ts_utc` è l'epoch UTC in secondi. Si confronta, si ordina e si unisce senza ambiguità, mentre EF Core su SQLite non traduce bene i confronti fra `DateTimeOffset`;
- **energia come intero in Wh**: somme esatte, nessun decimale da arrotondare. Prezzi e tariffe sono `REAL`: SQLite non ha un tipo decimale e il valore è comunque una stima. Si arrotonda al centesimo solo in presentazione;
- **WAL attivo** (`PRAGMA journal_mode=WAL`), così l'API legge mentre un job scrive.

```sql
-- Calendario: un quarto d'ora per riga, scritto dal dominio (unico punto che conosce Europe/Rome)
CREATE TABLE time_slots (
    ts_utc      INTEGER PRIMARY KEY,   -- inizio quarto d'ora, epoch UTC in secondi
    local_date  TEXT    NOT NULL,      -- 'yyyy-MM-dd' in ora italiana
    local_label TEXT    NOT NULL,      -- 'HH:mm' in ora italiana (nel ritorno all'ora solare compare due volte)
    tariff_band TEXT    NOT NULL       -- fascia ARERA: 'F1' | 'F2' | 'F3'
);

-- Prezzi di mercato, una riga per intervallo
CREATE TABLE market_prices (
    ts_utc      INTEGER NOT NULL,      -- inizio intervallo
    resolution  INTEGER NOT NULL,      -- minuti: 15 o 60
    market      TEXT    NOT NULL,      -- 'MGP'
    zone        TEXT    NOT NULL,      -- 'NORD', 'PUN'
    eur_mwh     REAL    NOT NULL,
    imported_at INTEGER NOT NULL,
    PRIMARY KEY (ts_utc, resolution, market, zone)
);

-- Misure di energia, una riga per intervallo e fornitore
CREATE TABLE energy_readings (
    ts_utc          INTEGER NOT NULL,
    resolution      INTEGER NOT NULL,  -- 5, 15 o 60
    source          TEXT    NOT NULL,  -- id del fornitore: 'fusionsolar-cloud' | 'modbus' | …
    export_wh       INTEGER,           -- immessa in rete
    import_wh       INTEGER,           -- prelevata dalla rete
    production_wh   INTEGER,
    consumption_wh  INTEGER,           -- consumo della casa, se il fornitore lo dà
    battery_charge_wh    INTEGER,      -- energia caricata nella batteria
    battery_discharge_wh INTEGER,      -- energia scaricata dalla batteria
    imported_at     INTEGER NOT NULL,
    PRIMARY KEY (ts_utc, resolution, source)
);

-- Offerta luce, una riga per periodo di validità
CREATE TABLE tariffs (
    valid_from          TEXT PRIMARY KEY,  -- 'yyyy-MM-dd' ora italiana; vale fino alla riga successiva
    kind                TEXT NOT NULL,     -- 'Monoraria' | 'Fasce' | 'IndicizzataPun'
    energy_f1_eur_kwh   REAL,              -- Monoraria: unico prezzo; Fasce: F1
    energy_f2_eur_kwh   REAL,              -- Fasce
    energy_f3_eur_kwh   REAL,              -- Fasce
    pun_spread_eur_kwh  REAL,              -- IndicizzataPun
    pun_basis           TEXT,              -- IndicizzataPun: 'Orario' | 'Mensile'
    losses_factor       REAL NOT NULL DEFAULT 0,   -- λ, es. 0.102
    network_eur_kwh     REAL NOT NULL DEFAULT 0,   -- trasporto e oneri, quota variabile
    excise_eur_kwh      REAL NOT NULL DEFAULT 0,
    vat_rate            REAL NOT NULL,             -- es. 0.10
    notes               TEXT
);

-- Prezzo minimo garantito per anno
CREATE TABLE price_floors (
    year     INTEGER PRIMARY KEY,
    eur_mwh  REAL    NOT NULL
);

-- Stato dei job di sincronizzazione
CREATE TABLE sync_state (
    job               TEXT PRIMARY KEY,  -- 'gme-prices' | 'energy-import'
    source            TEXT,              -- per energy-import: id del fornitore attivo
    last_success_utc  INTEGER,
    cursor_utc        INTEGER,           -- fin dove è completo lo storico
    last_error        TEXT,
    updated_at        INTEGER NOT NULL
);

-- Pagamenti GSE inseriti a mano, per il confronto stima/reale
CREATE TABLE gse_payments (
    month       TEXT PRIMARY KEY,  -- 'yyyy-MM', mese di competenza
    amount_eur  REAL NOT NULL,
    notes       TEXT
);
```

La view `v_valuation_quarter` parte da `time_slots`, unisce `energy_readings` aggregate a 15 minuti con `market_prices` (zona configurata, `resolution = 15`) e ricade sul prezzo orario dove manca il dato a 15 minuti. Accanto al valore dell'immesso calcola autoconsumo e risparmio, prendendo la tariffa valida in `local_date` e la fascia da `tariff_band`. Da lì derivano `v_valuation_hour`, `v_valuation_day` e `v_valuation_month`, raggruppate sulle date italiane del calendario.

Gli import sono idempotenti: ogni scrittura è un upsert sulla chiave primaria (`INSERT … ON CONFLICT DO UPDATE`), quindi rilanciare un job su un intervallo già importato non crea duplicati.

## Job di sincronizzazione

Due job in un unico processo, ognuno un `BackgroundService` con `PeriodicTimer` e orari espressi come cron (libreria Cronos), valutati in ora italiana.

| Job | Quando | Cosa importa | Finestra |
| --- | --- | --- | --- |
| `gme-prices` | Ogni giorno nel primo pomeriggio, con retry ogni 30 minuti fino a successo | Esiti MGP della zona configurata e PUN Index GME, a 15 e 60 minuti | Il giorno successivo, più gli ultimi 7 giorni per eventuali rettifiche |
| `energy-import` | Ogni ora al minuto 10 | Le misure del fornitore attivo, alla granularità più fine che dichiara | Oggi e ieri, perché i cloud dei fornitori consolidano i dati in ritardo |

`energy-import` non conosce il fornitore: chiede a `IEnergySource` un giorno alla volta e scrive quello che riceve. Il calendario `time_slots` lo estende il dominio, prima di ogni import, fino a fine anno successivo.

Orario di pubblicazione degli esiti MGP del giorno dopo: `<DA VERIFICARE: DTF GME sulle tempistiche dei mercati>`.

### Backfill

Al primo avvio ogni job riparte da `Sync:StartDate` e avanza un giorno alla volta, aggiornando `sync_state.cursor_utc`. Il backfill del fornitore di energia rispetta lo stesso throttling del job ordinario: per FusionSolar un anno di storico richiede ore, ed è accettabile.

### FusionSolar: sessione e rate limit

- Il client tiene un solo token in memoria e rifà il login solo quando la risposta indica sessione scaduta.
- Ogni endpoint ha un proprio limitatore (`System.Threading.RateLimiting`) configurabile da `Energy:FusionSolar:MinIntervalPerEndpoint`.
- Le risposte hanno `success` e `failCode`: il client li controlla sempre, anche con HTTP 200. Codici per sessione scaduta e frequenza eccessiva: `<DA VERIFICARE: tabella failCode della Northbound Interface Reference>`.
- Su frequenza eccessiva il fornitore risponde *riprova al prossimo ciclo* e il job si ferma, invece di riprovare subito, perché i retry ravvicinati allungano il blocco.

### GME

- Il JWT si rinnova al login di ogni esecuzione: il job gira poche volte al giorno, non serve una cache.
- Lo zip in Base64 si decomprime in memoria e si interpreta con un parser dedicato e testato su una fixture sintetica nello stesso formato dei file reali.

### Errori

Ogni esecuzione scrive in `sync_state` esito ed eventuale errore. La dashboard mostra un avviso quando un job non ha successo da più di 24 ore o quando mancano intervalli nello storico.

## API REST e frontend Angular

L'API ASP.NET Core espone solo letture sulle view di valorizzazione, più le poche scritture manuali. Il frontend Angular la consuma e non contiene logica di calcolo.

| Endpoint | Restituisce |
| --- | --- |
| `GET /api/valuation/day/{date}` | 96 righe a 15 minuti (92 o 100 nei giorni del cambio d'ora): kWh immessi, prezzo zonale, PUN, euro dell'immesso, kWh autoconsumati, prezzo bolletta, euro risparmiati, risoluzione e base dell'autoconsumo usate |
| `GET /api/valuation/hours?from&to` | Righe orarie aggregate |
| `GET /api/valuation/days?from&to` | Totali giornalieri: kWh immessi e autoconsumati, euro dell'immesso, euro risparmiati, prezzo medio ponderato |
| `GET /api/valuation/months?year` | Totali mensili con confronto `gse_payments` |
| `GET /api/summary` | Oggi, ieri, mese corrente, anno corrente, miglior giorno, con le due voci e il totale |
| `GET /api/sync/status` | Stato dei job da `sync_state`, fornitore attivo e intervalli mancanti |
| `POST /api/sync/{job}/run` | Avvio manuale di un job, utile durante il backfill |
| `GET /api/tariffs` · `PUT /api/tariffs/{validFrom}` | Elenco e inserimento o correzione di una tariffa |
| `PUT /api/gse-payments/{month}` | Inserimento o correzione di un pagamento GSE |

Le date nei parametri sono in ora italiana (`yyyy-MM-dd`); l'API le converte in intervalli UTC. Le risposte riportano il timestamp in UTC e l'etichetta locale già calcolata, così il frontend non fa conversioni.

### Schermate

1. **Oggi**: barre per quarto d'ora o per ora dei kWh immessi e autoconsumati, linea del prezzo zonale sull'asse secondario, totale in euro del giorno diviso fra immesso e risparmio.
2. **Calendario**: mese a griglia con il beneficio di ogni giorno; un clic apre il dettaglio del giorno.
3. **Andamento**: beneficio mensile dell'anno e cumulato, con le due voci separate e i pagamenti GSE affiancati quando presenti.
4. **Prezzo catturato**: prezzo medio ponderato ottenuto dall'impianto contro media del prezzo zonale, per capire quanto pesa produrre nelle ore centrali.
5. **Tariffe**: elenco delle offerte luce inserite, con i periodi di validità, e modulo per aggiungerne una.
6. **Stato**: esito dei job, fornitore attivo, intervalli mancanti, pulsante di sincronizzazione manuale.

Grafici con una libreria già nota nello stack Angular (ad esempio ECharts tramite `ngx-echarts`). Il layout deve funzionare anche da telefono.

## Configurazione, segreti, deploy

La configurazione usa le opzioni tipizzate di .NET (`IOptions<T>` con validazione all'avvio). Nessuna credenziale va nel repository: in sviluppo stanno in `dotnet user-secrets`, in esecuzione nelle variabili d'ambiente del container. Le tariffe non sono configurazione: cambiano nel tempo e si inseriscono dall'app.

```json
{
  "Sync": { "StartDate": "2026-01-01" },
  "Valuation": { "Regime": "RidZonale", "Zone": "NORD", "AnnualFixedCostsEur": 0 },
  "Energy": {
    "Provider": "FusionSolar",
    "FusionSolar": {
      "BaseUrl": "https://<prefisso>.fusionsolar.huawei.com/thirdData/",
      "UserName": "<segreto>",
      "SystemCode": "<segreto>",
      "StationCode": "<da ricavare al primo login>",
      "MinIntervalPerEndpoint": "00:05:00"
    }
  },
  "Gme": {
    "BaseUrl": "https://api.mercatoelettrico.org/",
    "UserName": "<segreto>",
    "Password": "<segreto>"
  },
  "ConnectionStrings": { "SunCash": "Data Source=/data/suncash.db" }
}
```

Ogni fornitore ha la sua sezione sotto `Energy`, validata solo se è quello attivo. Il valore di `MinIntervalPerEndpoint` è prudente e va tarato sui limiti reali dell'account.

### Deploy

Un `docker-compose.yml` con due servizi: `suncash-api` (che ospita anche i job e monta un volume `/data` con il file SQLite) e `suncash-web` (Angular servito da nginx, che inoltra `/api` all'API). Va bene su un mini PC o un NAS di casa. Per vederla da fuori casa basta una VPN come Tailscale, senza esporre porte.

Il backup giornaliero è una copia coerente del file fatta da SQLite stesso (`VACUUM INTO` o l'API di backup), mai una copia del file a database aperto.

In alternativa: una piccola VM in cloud con il volume su disco locale. SQLite non va messo su un file system di rete (Azure Files, SMB, NFS): il locking non è affidabile e il database si corrompe. Per un uso personale l'hosting di casa basta.

L'app non ha login nella prima versione: la protezione è la rete. Se in futuro si espone su internet, serve un'autenticazione davanti (ad esempio Entra ID o un reverse proxy con autenticazione).

## Decisioni aperte e prerequisiti

I prerequisiti si chiedono subito, perché le credenziali GME e Northbound richiedono giorni. Lo sviluppo può partire in parallelo con dati di esempio.

Prerequisiti:

- [ ] Chiedere all'installatore l'account Northbound FusionSolar, con i permessi di lettura su impianto e dispositivi
- [ ] Registrarsi al servizio API del GME e ricevere le credenziali
- [ ] Verificare che ci sia lo smart meter sul punto di scambio e che FusionSolar mostri l'energia immessa, il consumo della casa e i flussi della batteria
- [ ] Recuperare dalla convenzione GSE il regime (prezzo zonale o PMG) e la granularità di valorizzazione
- [ ] Recuperare dall'ultima bolletta tipo di offerta, prezzi dell'energia, perdite, quota variabile di trasporto e oneri, accise e IVA
- [ ] Salvare una risposta reale di `getKpiStationHour` e uno zip reale del GME nella cartella locale tests/fixtures.local/, da cui ricavare le fixture anonimizzate e sintetiche

Decisioni prese:

- [x] Database: SQLite (2026-10-28)
- [x] Energia letta da fornitori intercambiabili dietro `IEnergySource`; FusionSolar è il primo (2026-10-28)
- [x] Valore dell'autoconsumo incluso nella prima versione (2026-10-28)
- [x] L'impianto ha una batteria: consumo misurato o da bilancio, mai prodotta meno immessa (2026-10-28)

Decisioni aperte:

- [ ] Licenza del repository (ad esempio MIT)
- [ ] Modbus locale come secondo fornitore nella prima versione o rinviato
- [ ] Hosting: mini PC/NAS di casa o VM in cloud
- [ ] Confronto del risparmio stimato con le bollette reali (tabella di bollette inserite a mano, come `gse_payments`) sì o no

## Piano di sviluppo per Claude Code

Il lavoro si divide in nove milestone, ognuna chiusa da un criterio verificabile. Questo documento è `docs/analisi.md`; il `CLAUDE.md` alla radice lo riassume e lo cita.

1. **Scheletro.** Solution .NET con i progetti descritti in Architettura, `docker-compose.yml` con il volume per SQLite, build e test in CI (GitHub Actions). Fatto quando `dotnet test` passa in CI su una solution vuota e il repo ha .gitignore, LICENSE, secret scanning e push protection.
2. **Dominio e calcolo.** Tipi `Interval`, `EnergyReading`, `MarketPrice`, `Tariff`, il calcolatore di valore e risparmio (con il bilancio della batteria), la conversione ora italiana/UTC, il calendario con le fasce ARERA e i festivi. Nessuna dipendenza esterna. Fatto quando passano i test sui giorni da 92, 96 e 100 quarti d'ora, sul ricadere al prezzo orario, sulle fasce nei festivi e sui tre tipi di offerta.
3. **Client GME.** Login, `RequestData`, decompressione dello zip, parser, job `gme-prices` con upsert. Fatto quando il parser passa sulla fixture sintetica (e, in locale, su un file reale) e il job importa un giorno su un database SQLite di test.
4. **Fornitori di energia e FusionSolar.** Contratto `IEnergySource`, job `energy-import` indipendente dal fornitore, backfill; poi l'implementazione FusionSolar con login e token in memoria, limitatore per endpoint e gestione di `failCode`. Fatto quando il job passa con un fornitore finto in memoria e i test FusionSolar con un server finto (WireMock.Net) coprono sessione scaduta e frequenza eccessiva.
5. **Persistenza e view.** Migrazioni EF Core, calendario, view di valorizzazione per quarto d'ora, ora, giorno e mese, con valore dell'immesso e risparmio. Fatto quando i totali delle view coincidono al centesimo con quelli del calcolatore del punto 2 su un dataset di prova che copre i tre tipi di offerta.
6. **API.** Endpoint della sezione API REST, con OpenAPI. Fatto quando i test di integrazione coprono un giorno normale, uno del cambio d'ora e un cambio di tariffa a metà mese.
7. **Frontend.** Le sei schermate in Angular. Fatto quando la dashboard mostra dati reali importati da entrambe le fonti e una tariffa reale.
8. **Deploy.** Immagini Docker, compose di produzione, backup giornaliero del file SQLite. Fatto quando l'app gira sul server di casa e i job girano da soli per una settimana senza errori.
9. **Confronto con la realtà.** Primo confronto fra stima e pagamenti GSE (e bollette, se la decisione aperta è presa). Fatto quando lo scarto misurato è scritto in questo documento.

Modbus locale come secondo fornitore si pianifica dopo la prima settimana di dati reali.
