# SunCash

Quanto rende un impianto fotovoltaico, quarto d'ora per quarto d'ora: il valore dell'energia
immessa in rete (al prezzo zonale MGP del GME) più il risparmio da autoconsumo (al prezzo
variabile della propria bolletta). Per uso personale: un impianto, un utente.

I numeri sono una stima: il GSE liquida sulle misure del distributore, non su quelle del meter.

## Documentazione

- [`docs/analisi.md`](docs/analisi.md): analisi completa, fonti dati, regole di calcolo,
  modello dati e piano di sviluppo.
- [`CLAUDE.md`](CLAUDE.md): regole operative per chi modifica il codice.

## Sviluppo

Servono l'SDK .NET 10 e Node 24.

```bash
dotnet build
dotnet test
```

Le credenziali (GME, fornitore di energia) non stanno mai nel repository: in sviluppo si usano
`dotnet user-secrets` nel progetto `src/SunCash.Api`, in esecuzione le variabili d'ambiente.
Senza credenziali l'API non parte e dice quali campi mancano.

```bash
dotnet user-secrets --project src/SunCash.Api set "Gme:UserName" "<utente>"
```

Il frontend sta in [`src/SunCash.Web`](src/SunCash.Web/README.md).

## Avvio con Docker

```bash
cp .env.example .env
docker compose up -d --build
```

Prima di avviare, riempire `.env` con le credenziali. L'app risponde su `http://localhost:8080`.

## Su Azure

Container Apps con login Microsoft e backup su Azure Files: guida in
[`docs/deploy-azure.md`](docs/deploy-azure.md), deploy con il workflow manuale «Deploy Azure».

## Licenza

[GNU AGPL-3.0](LICENSE). Chi offre SunCash come servizio in rete deve renderne disponibile il
codice. I dati del GME restano soggetti alle condizioni del GME, che ne vietano l'uso a scopo di
lucro.
