# Analisi app guadagni fotovoltaico

Oct 28, 2026 · @Riccardo Beninatto

## Nome dell'app

Nome scelto: **SunCash**. Dice subito cosa mostra l'app, cioè i soldi che porta l'energia immessa in rete. Repo GitHub `suncash`, namespace `SunCash.*`.

Scartati: Quartino, SunSpot, SunCashback (troppo lungo, richiama i programmi di rimborso), SolarCash (una sillaba in più, suona da azienda del settore).

## Obiettivo e perimetro

SunCash mostra quanto rende l'energia immessa in rete, giorno per giorno e ora per ora. Il valore si ottiene moltiplicando i kWh immessi, letti da Huawei FusionSolar, per il prezzo zonale Nord dell'MGP pubblicato dal GME nello stesso intervallo.

L'utente è uno solo: il proprietario dell'impianto. L'app gira in casa o su un piccolo hosting e non è multi-tenant.

Cosa fa nella prima versione:

- importa ogni ora energia immessa, prelevata e prodotta da FusionSolar;
- importa ogni giorno i prezzi MGP zonali e il PUN Index GME a 15 minuti;
- calcola il valore dell'energia immessa per quarto d'ora, ora, giorno e mese;
- mostra una dashboard con grafici giornalieri e mensili e il cumulato da inizio anno;
- ricalcola lo storico quando cambia la regola di valorizzazione.

Cosa resta fuori:

- lo scambio sul posto, che segue un'altra logica (contributo in conto scambio);
- la riconciliazione automatica con i pagamenti GSE, che restano un dato inserito a mano;
- il valore dell'autoconsumo, cioè la bolletta evitata: è un'estensione naturale, ma richiede il prezzo della propria offerta luce;
- più impianti e più utenti.

I numeri sono una stima. Il GSE liquida sulle misure del distributore, non su quelle del meter Huawei, quindi va previsto uno scarto, da misurare confrontando i primi mesi con gli estratti GSE.

## Repository pubblico

Il codice sta in un repository GitHub pubblico, quindi nel repo non finiscono credenziali, dati dell'impianto né dati del GME.

- **Segreti**: solo `dotnet user-secrets` e variabili d'ambiente. Il `.gitignore` esclude `.env`, `appsettings.*.local.json` e i file di backup. Su GitHub vanno attivati secret scanning e push protection.
- **Dati dell'impianto**: niente `stationCode`, nomi impianto, indirizzi, coordinate, numeri di serie o potenza installata in codice, test, issue e screenshot. Le configurazioni di esempio usano solo segnaposto.
- **Fixture FusionSolar**: anonimizzate, con codici sostituiti e valori alterati, ma struttura identica alla risposta reale.
- **Fixture GME**: sintetiche, nello stesso formato dei file reali, perché i dati del GME non si possono ridistribuire.
- **File reali**: solo in `tests/fixtures.local/`, ignorata da git. I test che li usano sono marcati e girano solo in locale.
- **Database e backup**: mai nel repo.

La zona di mercato è un parametro (`Valuation:Zone`): il codice non presuppone una zona specifica.

## Fonti dati

Servono due fonti: FusionSolar per l'energia, il GME per i prezzi. Entrambe richiedono credenziali da chiedere prima di iniziare lo sviluppo.

### Energia: Huawei FusionSolar

La lettura dell'energia passa da un'interfaccia `IEnergySource`, con due implementazioni possibili. La prima versione usa l'API cloud; Modbus resta un'implementazione successiva.

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

Nomi dei campi KPI per energia immessa, prelevata e prodotta: `<DA VERIFICARE: nella Northbound Interface Reference e su una risposta reale>`. Senza smart meter sul punto di scambio l'energia immessa non è disponibile e l'app non ha senso.

### Prezzi: API del GME

Dal 15 ottobre 2025 il GME espone i dati di mercato con un'API ufficiale; l'FTPS resta solo per un periodo transitorio.

1. Registrazione al form `https://api.mercatoelettrico.org/users/RegistrationForm/RegistrationRequest`; le credenziali arrivano via email.
2. Login con il metodo `/Auth`, che restituisce un JWT.
3. Richiesta con `RequestData`: i dati arrivano nel campo `contentResponse`, uno zip in Base64 da decomprimere.

Parametri della richiesta per esiti MGP, zona `NORD`, granularità 15 minuti: `<DA VERIFICARE: Manuale tecnico API GME>`.

I dati restano del GME e non si possono usare a scopo di lucro. Per un uso personale va bene; se SunCash diventasse un prodotto, servirebbe un'altra fonte o un accordo.

Fonti: [Huawei, openAPI account](https://docs.rs/crate/huawei_client/latest/source/docs/Communicate%20with%20FusionSolar%20through%20an%20openAPI%20account.pdf) · [HomeAssistant-FusionSolar](https://github.com/tijsverkoyen/homeassistant-fusionsolar) · [meteocontrol, setup Northbound](https://zammad.meteocontrol.de/help/en-us/6-datalogger-import-configuration/179-vcom-import-huawei-fusionsolar-api) · [GME, servizio API](https://gme.mercatoelettrico.org/en-US/Home/APIService) · [GME, manuale utente API](https://gme.mercatoelettrico.org/Portals/0/Documents/it-IT/20251015Manuale_Utente_API.pdf)

## Regole di calcolo

Il valore di ogni quarto d'ora è l'energia immessa per il prezzo zonale Nord dello stesso quarto d'ora; ore, giorni e mesi sono somme di quarti d'ora.

```latex
V_q = \frac{E_q}{1000} \cdot P_{z,q}
```

`E_q` è l'energia immessa nel quarto d'ora in kWh, `P_{z,q}` il prezzo zonale MGP in €/MWh, `V_q` il valore in euro.

### Quale prezzo

Con il Ritiro Dedicato il GSE paga il prezzo zonale orario della zona in cui si trova l'impianto; nella configurazione di esempio è la zona Nord. Il PUN Index GME si importa lo stesso, ma solo come riferimento nei grafici: è il prezzo lato acquisto e non entra nel calcolo del guadagno.

### Regime di valorizzazione

Il regime è un parametro di configurazione (`Valuation:Regime`), così lo storico si ricalcola se cambia.

| Regime | Regola | Note |
| --- | --- | --- |
| `RidZonale` | `V_q` come sopra | Caso base |
| `RidPmg` | Si calcolano sia la serie a prezzo zonale sia quella a prezzo minimo garantito; il GSE riconosce la più favorevole con il conguaglio annuale | PMG 2026: 47,5 €/MWh. `<DA VERIFICARE: meccanismo esatto di confronto nella convenzione GSE>` |

Il PMG va in una tabella `price_floors(year, eur_mwh)`, perché ARERA lo aggiorna ogni anno.

### Granularità

I prezzi MGP sono a 15 minuti dal 1° ottobre 2025 e il GME li pubblica anche aggregati all'ora. Il calcolo segue questa regola:

- se ci sono i dati a 5 minuti del meter, si aggregano a 15 minuti e si usa il prezzo a 15 minuti;
- se c'è solo il dato orario, si usa il prezzo orario aggregato e ogni ora vale come un blocco unico.

Ogni riga di valore registra quale delle due vie ha usato (`Resolution = Quarter | Hour`), così la dashboard lo può segnalare. Con quale granularità il GSE valorizzi oggi l'energia ritirata: `<DA VERIFICARE: convenzione GSE e delibere ARERA dopo il passaggio ai 15 minuti>`.

### Tempo e cambio d'ora

Tutti i timestamp si salvano in UTC. Il GME ragiona in ora italiana: il giorno del passaggio all'ora legale ha 23 ore (92 quarti d'ora), quello del ritorno all'ora solare 25 (100 quarti d'ora). La conversione usa `TimeZoneInfo` con l'id `Europe/Rome`, mai offset fissi. Servono test dedicati sulle due date di fine marzo e fine ottobre.

### Costi

Il corrispettivo di gestione GSE e gli eventuali costi fissi si inseriscono come voci annue in configurazione e si mostrano solo nel cumulato, non nel valore orario.

Fonti: [GME, report MGP settimana 40/2025](<https://gme.mercatoelettrico.org/Portals/0/Documents/en-US/\\20251006MGPreportsettimanale40En.pdf>) · [GME, 15 minuti su MGP](<https://mercatoelettrico.org/Portals/0/Documenti/Presentazioni/216\\20250326_intro15mMGP.pdf>) · [Solare Industriale, Ritiro Dedicato](https://www.solareindustriale.it/incentivi/ritiro-dedicato) · [Enpal, Ritiro Dedicato 2026](https://www.enpal.com/it/fotovoltaico/ritiro-dedicato)

## Architettura

Un solo backend .NET ospita sia l'API sia i due job di import; il calcolo vive nelle view del database e in una libreria di dominio senza dipendenze, testabile da sola.

```text
 Huawei FusionSolar ──► job fusionsolar-energy ──┐
                                                 ├──► PostgreSQL ──► view di valorizzazione ──► API ──► Web (Angular)
 GME (API MGP/PUN)  ──► job gme-prices ──────────┘    (dati grezzi)   (euro calcolati, mai salvati)
```

I job scrivono solo dati grezzi con upsert; l'API non scrive misure né prezzi e legge le view di valutazione.

Struttura della solution (.NET 10, LTS):

```text
suncash/
├── src/
│   ├── SunCash.Domain/          # tipi, calcolatore, fusi orari; nessuna dipendenza
│   ├── SunCash.Infrastructure/  # EF Core, client GME e FusionSolar, parser
│   ├── SunCash.Api/             # host ASP.NET Core: endpoint + BackgroundService dei job
│   └── SunCash.Web/             # Angular
├── tests/
│   ├── SunCash.Domain.Tests/
│   ├── SunCash.Infrastructure.Tests/   # fixture anonimizzate e sintetiche, WireMock.Net, Testcontainers
│   ├── SunCash.Api.Tests/
│   └── fixtures.local/                 # ignorata da git: file reali, solo in locale
├── docs/analisi.md
├── docker-compose.yml
├── .gitignore
├── LICENSE
└── README.md
```

I job stanno nello stesso host dell'API per semplicità di deploy. Se un giorno serve separarli, `SunCash.Infrastructure` è già condiviso e basta un secondo host Worker Service.

## Modello dati

Il database è PostgreSQL con EF Core (Npgsql). Energia e prezzi si salvano grezzi; il valore in euro è sempre calcolato da una view, mai salvato, così un cambio di regola non richiede migrazioni di dati.

```sql
-- Prezzi di mercato, una riga per intervallo
CREATE TABLE market_prices (
    ts_utc        timestamptz   NOT NULL,  -- inizio intervallo
    resolution    smallint      NOT NULL,  -- minuti: 15 o 60
    market        text          NOT NULL,  -- 'MGP'
    zone          text          NOT NULL,  -- 'NORD', 'PUN'
    eur_mwh       numeric(10,4) NOT NULL,
    imported_at   timestamptz   NOT NULL DEFAULT now(),
    PRIMARY KEY (ts_utc, resolution, market, zone)
);

-- Misure di energia, una riga per intervallo e sorgente
CREATE TABLE energy_readings (
    ts_utc          timestamptz  NOT NULL,
    resolution      smallint     NOT NULL,  -- 5, 15 o 60
    source          text         NOT NULL,  -- 'fusionsolar-cloud' | 'modbus'
    export_kwh      numeric(10,4),          -- immessa in rete
    import_kwh      numeric(10,4),          -- prelevata dalla rete
    production_kwh  numeric(10,4),
    imported_at     timestamptz  NOT NULL DEFAULT now(),
    PRIMARY KEY (ts_utc, resolution, source)
);

-- Prezzo minimo garantito per anno
CREATE TABLE price_floors (
    year     smallint      PRIMARY KEY,
    eur_mwh  numeric(10,4) NOT NULL
);

-- Stato dei job di sincronizzazione
CREATE TABLE sync_state (
    job               text PRIMARY KEY,     -- 'gme-prices' | 'fusionsolar-energy'
    last_success_utc  timestamptz,
    cursor_utc        timestamptz,          -- fin dove è completo lo storico
    last_error        text,
    updated_at        timestamptz NOT NULL DEFAULT now()
);

-- Pagamenti GSE inseriti a mano, per il confronto stima/reale
CREATE TABLE gse_payments (
    month       date          PRIMARY KEY,  -- primo giorno del mese di competenza
    amount_eur  numeric(10,2) NOT NULL,
    notes       text
);
```

La view `v_valuation_quarter` unisce `energy_readings` aggregate a 15 minuti con `market_prices` (`zone = 'NORD'`, `resolution = 15`) e ricade sul prezzo orario dove manca il dato a 15 minuti. Da lì derivano `v_valuation_hour`, `v_valuation_day` e `v_valuation_month`, con le date raggruppate in ora italiana.

Gli import sono idempotenti: ogni scrittura è un upsert sulla chiave primaria, quindi rilanciare un job su un intervallo già importato non crea duplicati.

## Job di sincronizzazione

Due job in un unico processo worker, ognuno un `BackgroundService` con `PeriodicTimer` e orari espressi come cron (libreria Cronos), valutati in ora italiana.

| Job | Quando | Cosa importa | Finestra |
| --- | --- | --- | --- |
| `gme-prices` | Ogni giorno nel primo pomeriggio, con retry ogni 30 minuti fino a successo | Esiti MGP zona Nord e PUN Index GME, a 15 e 60 minuti | Il giorno successivo, più gli ultimi 7 giorni per eventuali rettifiche |
| `fusionsolar-energy` | Ogni ora al minuto 10 | Righe orarie e, se disponibili, dati a 5 minuti del meter | Oggi e ieri, perché il cloud consolida i dati in ritardo |

Orario di pubblicazione degli esiti MGP del giorno dopo: `<DA VERIFICARE: DTF GME sulle tempistiche dei mercati>`.

### Backfill

Al primo avvio ogni job riparte da `Sync:StartDate` e avanza un giorno alla volta, aggiornando `sync_state.cursor_utc`. Il backfill FusionSolar rispetta lo stesso throttling del job ordinario: per un anno di storico ci vogliono ore, ed è accettabile.

### FusionSolar: sessione e rate limit

- Il client tiene un solo token in memoria e rifà il login solo quando la risposta indica sessione scaduta.
- Ogni endpoint ha un proprio limitatore (`System.Threading.RateLimiting`) configurabile da `FusionSolar:MinIntervalPerEndpoint`.
- Le risposte hanno `success` e `failCode`: il client li controlla sempre, anche con HTTP 200. Codici per sessione scaduta e frequenza eccessiva: `<DA VERIFICARE: tabella failCode della Northbound Interface Reference>`.
- Su frequenza eccessiva il job si ferma fino al ciclo successivo invece di riprovare subito, perché i retry ravvicinati allungano il blocco.

### GME

- Il JWT si rinnova al login di ogni esecuzione: il job gira poche volte al giorno, non serve una cache.
- Lo zip in Base64 si decomprime in memoria e si interpreta con un parser dedicato e testato su una fixture sintetica nello stesso formato dei file reali.

### Errori

Ogni esecuzione scrive in `sync_state` esito ed eventuale errore. La dashboard mostra un avviso quando un job non ha successo da più di 24 ore o quando mancano intervalli nello storico.

## API REST e frontend Angular

L'API ASP.NET Core espone solo letture sulle view di valorizzazione, più le poche scritture manuali. Il frontend Angular la consuma e non contiene logica di calcolo.

| Endpoint | Restituisce |
| --- | --- |
| `GET /api/valuation/day/{date}` | 96 righe a 15 minuti (92 o 100 nei giorni del cambio d'ora): kWh immessi, prezzo zonale, PUN, euro, risoluzione usata |
| `GET /api/valuation/hours?from&to` | Righe orarie aggregate |
| `GET /api/valuation/days?from&to` | Totali giornalieri: kWh immessi, euro, prezzo medio ponderato |
| `GET /api/valuation/months?year` | Totali mensili con confronto `gse_payments` |
| `GET /api/summary` | Oggi, ieri, mese corrente, anno corrente, miglior giorno |
| `GET /api/sync/status` | Stato dei job da `sync_state` e intervalli mancanti |
| `POST /api/sync/{job}/run` | Avvio manuale di un job, utile durante il backfill |
| `PUT /api/gse-payments/{month}` | Inserimento o correzione di un pagamento GSE |

Le date nei parametri sono in ora italiana (`yyyy-MM-dd`); l'API le converte in intervalli UTC. Le risposte riportano il timestamp in UTC e l'etichetta locale già calcolata, così il frontend non fa conversioni.

### Schermate

1. **Oggi**: barre per quarto d'ora o per ora dei kWh immessi, linea del prezzo zonale sull'asse secondario, totale in euro del giorno.
2. **Calendario**: mese a griglia con il guadagno di ogni giorno; un clic apre il dettaglio del giorno.
3. **Andamento**: guadagno mensile dell'anno e cumulato, con i pagamenti GSE affiancati quando presenti.
4. **Prezzo catturato**: prezzo medio ponderato ottenuto dall'impianto contro media del prezzo zonale, per capire quanto pesa produrre nelle ore centrali.
5. **Stato**: esito dei job, intervalli mancanti, pulsante di sincronizzazione manuale.

Grafici con una libreria già nota nello stack Angular (ad esempio ECharts tramite `ngx-echarts`). Il layout deve funzionare anche da telefono.

## Configurazione, segreti, deploy

La configurazione usa le opzioni tipizzate di .NET (`IOptions<T>` con validazione all'avvio). Nessuna credenziale va nel repository: in sviluppo stanno in `dotnet user-secrets`, in esecuzione nelle variabili d'ambiente del container.

```json
{
  "Sync": { "StartDate": "2026-01-01" },
  "Valuation": { "Regime": "RidZonale", "Zone": "NORD", "AnnualFixedCostsEur": 0 },
  "FusionSolar": {
    "BaseUrl": "https://<prefisso>.fusionsolar.huawei.com/thirdData/",
    "UserName": "<segreto>",
    "SystemCode": "<segreto>",
    "StationCode": "<da ricavare al primo login>",
    "MinIntervalPerEndpoint": "00:05:00"
  },
  "Gme": {
    "BaseUrl": "https://api.mercatoelettrico.org/",
    "UserName": "<segreto>",
    "Password": "<segreto>"
  },
  "ConnectionStrings": { "SunCash": "<segreto>" }
}
```

Il valore di `MinIntervalPerEndpoint` è prudente e va tarato sui limiti reali dell'account.

### Deploy

Un `docker-compose.yml` con tre servizi: `postgres`, `suncash-api` (che ospita anche i worker) e `suncash-web` (Angular servito da nginx, che inoltra `/api` all'API). Va bene su un mini PC o un NAS di casa. Per vederla da fuori casa basta una VPN come Tailscale, senza esporre porte.

In alternativa: Azure Container Apps più Azure Database for PostgreSQL Flexible Server, con i segreti in Key Vault. Costa di più e per un uso personale non serve.

L'app non ha login nella prima versione: la protezione è la rete. Se in futuro si espone su internet, serve un'autenticazione davanti (ad esempio Entra ID o un reverse proxy con autenticazione).

## Decisioni aperte e prerequisiti

I prerequisiti si chiedono subito, perché le credenziali GME e Northbound richiedono giorni. Lo sviluppo può partire in parallelo con dati di esempio.

Prerequisiti:

- [ ] Chiedere all'installatore l'account Northbound FusionSolar, con i permessi di lettura su impianto e dispositivi
- [ ] Registrarsi al servizio API del GME e ricevere le credenziali
- [ ] Verificare che ci sia lo smart meter sul punto di scambio e che FusionSolar mostri l'energia immessa
- [ ] Recuperare dalla convenzione GSE il regime (prezzo zonale o PMG) e la granularità di valorizzazione
- [ ] Salvare una risposta reale di `getKpiStationHour` e uno zip reale del GME nella cartella locale tests/fixtures.local/, da cui ricavare le fixture anonimizzate e sintetiche

Decisioni aperte:

- [ ] Licenza del repository (ad esempio MIT)
- [ ] Modbus locale nella prima versione o rinviato
- [ ] Hosting: mini PC/NAS di casa o Azure
- [ ] Includere o no il valore dell'autoconsumo (richiede il prezzo dell'offerta luce)

## Piano di sviluppo per Claude Code

Il lavoro si divide in otto milestone, ognuna chiusa da un criterio verificabile. In Claude Code conviene esportare questo documento come Markdown in `docs/analisi.md` e chiedere, come primo passo, di generare un `CLAUDE.md` che lo riassuma e lo citi.

1. **Scheletro.** Solution .NET con i progetti descritti in Architettura, `docker-compose.yml` con Postgres, build e test in CI (GitHub Actions). Fatto quando `dotnet test` passa in CI su una solution vuota e il repo ha .gitignore, LICENSE, secret scanning e push protection.
2. **Dominio e calcolo.** Tipi `Interval`, `EnergyReading`, `MarketPrice`, il calcolatore di valorizzazione e la conversione ora italiana/UTC. Nessuna dipendenza esterna. Fatto quando passano i test sui giorni da 92, 96 e 100 quarti d'ora e sul ricadere al prezzo orario.
3. **Client GME.** Login, `RequestData`, decompressione dello zip, parser, job `gme-prices` con upsert. Fatto quando il parser passa sulla fixture sintetica (e, in locale, su un file reale) e il job importa un giorno su un database di test (Testcontainers).
4. **Client FusionSolar.** Login con token in memoria, limitatore per endpoint, gestione di `failCode`, job `fusionsolar-energy` e backfill. Fatto quando i test con un server finto (WireMock.Net) coprono sessione scaduta e frequenza eccessiva.
5. **Persistenza e view.** Migrazioni EF Core, view di valorizzazione per quarto d'ora, ora, giorno e mese. Fatto quando i totali della view coincidono con quelli del calcolatore del punto 2 su un dataset di prova.
6. **API.** Endpoint della sezione API REST, con OpenAPI. Fatto quando i test di integrazione coprono un giorno normale e uno del cambio d'ora.
7. **Frontend.** Le cinque schermate in Angular. Fatto quando la dashboard mostra dati reali importati da entrambe le fonti.
8. **Deploy.** Immagini Docker, compose di produzione, backup giornaliero del database. Fatto quando l'app gira sul server di casa e i job girano da soli per una settimana senza errori.

Modbus locale e valore dell'autoconsumo restano fuori da queste milestone e si pianificano dopo la prima settimana di dati reali.
