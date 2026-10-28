# SunCash su Azure App Service

Stesso schema del progetto idrosistemi: l'App Service lo crei tu nel portale, il deploy lo fa il
workflow **Deploy** a ogni push su `main` (o a mano da Actions) con il *publish profile*.

> Decisione del 2026-10-28: App Service al posto di Azure Container Apps, che era stato preparato
> poco prima e chiedeva più configurazione (identità federata, due registrazioni app, Bicep).
> Quella strada è stata tolta dal repo e **non va ripresa**: due modi di fare il deploy sono due
> configurazioni che divergono.

## Com'è fatto

- **Una sola app**: l'API ASP.NET Core serve anche il frontend Angular, pubblicato in `wwwroot`.
- **Sta nel piano App Service già pagato** (`asp-bb-project`, Linux B1, Italy North, gruppo
  `rg-bb-project`), insieme a idrosistemi: nessun costo in più. App: **`suncash-bennaloz`**, creata
  il 2026-10-28 (`https://suncash-bennaloz.azurewebsites.net`).
- **Database sul disco locale** dell'istanza (`/tmp`), che **non sopravvive** a un riavvio: SQLite
  non va sul disco condiviso `/home`, che è uno storage di rete. Il **backup** va invece in
  `/home` ogni notte e a ogni spegnimento, e all'avvio l'app, se non trova il database, riparte
  dall'ultima copia. Energia e prezzi li reimportano comunque i job.
- **Accesso**: autenticazione integrata di App Service con account Microsoft.
- **Segreti**: impostazioni dell'App Service, mai nel repo né su GitHub.

## Passo 1 — Creare l'App Service

Portale Azure → **Crea una risorsa** → **App Web**:

| Campo | Valore |
| --- | --- |
| Pubblica | Codice |
| Stack di runtime | .NET 10 (LTS) |
| Sistema operativo | Linux (quello del piano `asp-bb-project`) |
| Piano | `asp-bb-project`, oppure un nuovo piano **Basic B1** o superiore: serve Always On, che il piano gratuito non ha |

Dopo la creazione, in **Impostazioni → Configurazione → Impostazioni generali**:

- **Always On**: Attivato. Senza, l'app si ferma quando nessuno la usa e i job non girano.
- **Credenziali di pubblicazione di base SCM**: Attivato. Serve per scaricare il publish profile.

E in **Scalabilità orizzontale**: **una sola istanza**. SQLite vuole un solo processo che scrive.

## Passo 2 — Impostazioni dell'app

**Impostazioni → Variabili d'ambiente → Impostazioni app**, una riga per voce:

| Nome | Valore |
| --- | --- |
| `ConnectionStrings__SunCash` | `Data Source=/tmp/suncash/suncash.db` |
| `Backup__Directory` | `/home/data/suncash/backups` |
| `Sync__StartDate` | `2026-01-01` (da quando ricostruire lo storico) |
| `Valuation__Zone` | `NORD` |
| `Energy__Provider` | `FusionSolar` |
| `Energy__FusionSolar__BaseUrl` | `https://<cluster>.fusionsolar.huawei.com/thirdData/`: il cluster è quello dell'indirizzo di FusionSolar web (`uni002eu5…` → `eu5`) |
| `Energy__FusionSolar__UserName` | utente **Northbound** (non quello di FusionSolar web) |
| `Energy__FusionSolar__SystemCode` | **password** dell'utente Northbound: Huawei la chiama «systemCode» |
| `Energy__FusionSolar__StationCode` | facoltativo: se manca, l'app usa l'unico impianto dell'account e scrive il codice nel log (passo 5) |
| `Gme__UserName`, `Gme__Password` | quando arrivano: senza, l'app parte e la pagina Stato lo segnala |

Su un App Service **Windows** i percorsi diventano `Data Source=%TEMP%\suncash\suncash.db` e
`%HOME%\data\suncash\backups` (i percorsi accettano variabili d'ambiente).

## Passo 3 — Login Microsoft

**Impostazioni → Autenticazione → Aggiungi provider di identità** → **Microsoft**:

- Registrazione app: **Crea una nuova registrazione**; tipi di account: **solo tenant corrente**.
- Accesso all'app: **Richiedi autenticazione**; richieste non autenticate: **reindirizzamento HTTP 302**.

Fatto: entrano solo gli account della tua directory. Non serve altro, nemmeno nel codice.

## Passo 4 — GitHub

1. App Service → **Panoramica** → **Scarica profilo di pubblicazione**.
2. Repository SunCash → **Settings → Secrets and variables → Actions**:
   - scheda **Secrets**: `AZURE_WEBAPP_PUBLISH_PROFILE` = tutto il contenuto del file scaricato;
   - scheda **Variables**: `AZURE_WEBAPP_NAME` = nome dell'App Service.
3. **Actions → Deploy → Run workflow** (dopo, parte da solo a ogni push su `main`).

Il workflow gira sui runner di GitHub: il repo è pubblico, e il runner locale di idrosistemi è
registrato per quell'altro repo. Per usare un runner locale anche qui: registrarlo per SunCash e
impostare la variabile `DEPLOY_RUNNER` = `self-hosted`.

## Passo 5 — Il codice dell'impianto FusionSolar (di solito non serve)

Lo `stationCode` è una stringa come `NE=12345678`. Se l'account Northbound vede un solo impianto,
l'app lo trova da sola al primo import e lo scrive nel log. Serve impostarlo solo se l'account vede
più impianti (la pagina Stato lo dice). Per trovarlo a mano, due modi:

- in FusionSolar web, apri il tuo impianto: l'indirizzo nel browser contiene `NE%3D…`, cioè `NE=…`;
- oppure, con le credenziali Northbound, da un terminale del tuo PC:

  ```bash
  curl -si -X POST "https://<prefisso>.fusionsolar.huawei.com/thirdData/login" -H "Content-Type: application/json" -d '{"userName":"<utente>","systemCode":"<password>"}' | grep -i xsrf-token
  ```

  ```bash
  curl -s -X POST "https://<prefisso>.fusionsolar.huawei.com/thirdData/stations" -H "Content-Type: application/json" -H "XSRF-TOKEN: <token>" -d '{"pageNo":1}'
  ```

  Il codice è quello che inizia con `NE=` (di solito nel campo `plantCode`). Se il login risponde
  con `"success":false`, le credenziali non sono più valide: vanno rifatte dall'installatore.

## Dopo

- **Stato** dice se FusionSolar e GME rispondono e fin dove è arrivato lo storico. FusionSolar ha
  limiti di frequenza stretti: ricostruire un anno richiede ore, ed è normale.
- **Bollette**: inserire l'offerta luce. Il risparmio da autoconsumo resta non calcolato finché nel
  repo mancano i valori della parte regolata ARERA (`regulated-charges.json`).
- **Log**: App Service → **Flusso di log**. Al primo import FusionSolar scrive i nomi dei campi che
  riceve davvero: servono a verificare la configurazione (`docs/analisi.md`, «Primo fornitore»).
