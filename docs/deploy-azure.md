# SunCash su Azure

Guida per pubblicare SunCash su **Azure Container Apps** con il workflow `Deploy Azure` del repository.
La configurazione si fa una volta sola, in circa 15 minuti. Dopo, ogni aggiornamento è un clic su
**Actions → Deploy Azure → Run workflow**.

## Com'è fatto

- **Un'app con due container** nella stessa rete: `api` (ASP.NET Core con i job) e `web` (nginx con
  il frontend). **Una sola replica, sempre accesa**: SQLite vuole un solo processo che scrive, e i
  job di import devono girare anche quando nessuno guarda la dashboard.
- **Database**: sul disco locale del container, che **non sopravvive** a un riavvio o a un nuovo
  deploy. Per questo il backup va su **Azure Files** ogni notte e a ogni spegnimento, e l'app,
  quando riparte senza database, ricomincia dall'ultima copia. SQLite non apre mai un file su
  Azure Files: lì sopra il locking non è affidabile.
- **Accesso**: login con account Microsoft (autenticazione integrata di Container Apps). Finché il
  login non è configurato, l'app **non è esposta su internet**.
- **Segreti**: solo nei secret di GitHub, passati all'app come secret di Container Apps. Mai nel repo.

Cosa può andare perso in un riavvio: le modifiche fatte a mano (offerte, bollette, pagamenti) dopo
l'ultimo backup, se lo spegnimento non fa in tempo a farne uno. Energia e prezzi li reimportano i job.

### Costi

Ordine di grandezza **10-20 € al mese**, da verificare sul
[calcolatore prezzi di Azure](https://azure.microsoft.com/it-it/pricing/calculator/): una replica
da 0,5 vCPU e 1 GiB sempre accesa (tariffata in gran parte come «inattiva», perché l'app lavora
pochi secondi l'ora), un registro di immagini Basic, uno storage account con pochi MB di backup,
un'area di lavoro Log Analytics. Per spegnere tutto:

```bash
az group delete --name rg-suncash
```

## Passo 1 — Identità per il deploy

GitHub entra in Azure con una **credenziale federata**: nessuna password Azure nei secret.

1. Portale Azure → **Microsoft Entra ID** → **Registrazioni app** → **Nuova registrazione**.
   Nome `suncash-deploy`, «Solo account in questa directory dell'organizzazione». Registra.
2. Nella registrazione: **Certificati e segreti** → **Credenziali federate** → **Aggiungi credenziale**.
   Scenario «GitHub Actions che distribuiscono risorse di Azure». Organizzazione `bennaloz`,
   repository `SunCash`, tipo di entità **Ambiente**, nome ambiente `azure`. Aggiungi.
3. Portale → **Sottoscrizioni** → la tua sottoscrizione → **Controllo di accesso (IAM)** →
   **Aggiungi assegnazione di ruolo** → ruolo **Collaboratore** → membro `suncash-deploy`.
4. Annota dalla pagina «Panoramica» della registrazione: **ID applicazione (client)** e
   **ID della directory (tenant)**; dalla sottoscrizione: **ID sottoscrizione**.

## Passo 2 — Il codice dell'impianto FusionSolar

Il workflow ha bisogno dello `stationCode` dell'impianto (una stringa come `NE=12345678`). Due modi:

- in FusionSolar web, apri il tuo impianto: l'indirizzo nel browser contiene `NE%3D…`, cioè `NE=…`;
- oppure, con le credenziali Northbound, da un terminale del tuo PC:

  ```bash
  curl -si -X POST "https://<prefisso>.fusionsolar.huawei.com/thirdData/login" -H "Content-Type: application/json" -d '{"userName":"<utente>","systemCode":"<password>"}' | grep -i xsrf-token
  ```

  ```bash
  curl -s -X POST "https://<prefisso>.fusionsolar.huawei.com/thirdData/stations" -H "Content-Type: application/json" -H "XSRF-TOKEN: <token>" -d '{"pageNo":1}'
  ```

  Il codice è quello che inizia con `NE=` (di solito nel campo `plantCode`). Se il login risponde con `"success":false`, le credenziali
  non sono più valide: vanno rifatte dall'installatore.

## Passo 3 — Secret e variabili in GitHub

Repository → **Settings** → **Secrets and variables** → **Actions**.

**Secrets** (scheda «Secrets»):

| Nome | Valore |
| --- | --- |
| `AZURE_CLIENT_ID` | ID applicazione di `suncash-deploy` |
| `AZURE_TENANT_ID` | ID della directory |
| `AZURE_SUBSCRIPTION_ID` | ID della sottoscrizione |
| `FUSIONSOLAR_USERNAME` | Utente Northbound |
| `FUSIONSOLAR_SYSTEMCODE` | Password Northbound |
| `FUSIONSOLAR_STATIONCODE` | Codice dell'impianto (passo 2) |
| `GME_USERNAME`, `GME_PASSWORD` | Facoltativi: quando arrivano. Senza, l'app parte e la pagina Stato lo segnala |

**Variables** (scheda «Variables»):

| Nome | Valore |
| --- | --- |
| `FUSIONSOLAR_BASE_URL` | `https://<prefisso>.fusionsolar.huawei.com/thirdData/` (il prefisso è quello della pagina di login di FusionSolar web) |
| `AZURE_LOCATION` | Facoltativa, predefinita `westeurope` |
| `SYNC_START_DATE` | Facoltativa, predefinita `2026-01-01`: da quando ricostruire lo storico |

## Passo 4 — Primo deploy

**Actions** → **Deploy Azure** → **Run workflow**. Alla fine, il riepilogo dell'esecuzione mostra
l'**indirizzo di ritorno** per il login. L'app esiste ma non è ancora raggiungibile da internet.

## Passo 5 — Login Microsoft

1. **Microsoft Entra ID** → **Registrazioni app** → **Nuova registrazione**. Nome `suncash-login`,
   «Solo account in questa directory dell'organizzazione». URI di reindirizzamento: piattaforma
   **Web**, valore l'indirizzo di ritorno del passo 4. Registra.
2. **Autenticazione** → spunta **Token ID**. Salva.
3. **Certificati e segreti** → **Nuovo segreto client** → copia subito il **Valore**.
4. In GitHub aggiungi i secret `AUTH_CLIENT_ID` (ID applicazione di `suncash-login`) e
   `AUTH_CLIENT_SECRET` (il valore del segreto).
5. Rilancia **Deploy Azure**. Il riepilogo mostra l'indirizzo dell'app: si apre con il login
   Microsoft, e possono entrare solo gli account della tua directory.

Il segreto client scade (di solito dopo 6 o 24 mesi): quando scade, se ne crea uno nuovo, si
aggiorna `AUTH_CLIENT_SECRET` e si rilancia il deploy.

## Dopo

- **Stato** dice se FusionSolar e GME rispondono e fin dove è arrivato lo storico. FusionSolar ha
  limiti di frequenza stretti: ricostruire un anno richiede ore, ed è normale.
- **Bollette**: inserire l'offerta luce. Il risparmio da autoconsumo resta non calcolato finché nel
  repo mancano i valori della parte regolata ARERA (`regulated-charges.json`).
- I log dell'app: portale → `suncash` (Container App) → **Flusso di log**. Al primo import
  FusionSolar scrive i nomi dei campi che riceve davvero: servono a verificare la configurazione
  (`docs/analisi.md`, «Primo fornitore»).
