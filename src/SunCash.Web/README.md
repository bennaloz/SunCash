# SunCash.Web

Frontend Angular di SunCash: sei schermate (Oggi, Calendario, Andamento, Prezzo catturato,
Bollette, Stato) che leggono l'API. **Nessuna logica di calcolo e nessuna conversione di fuso**:
gli euro arrivano già calcolati e le etichette orarie già in ora italiana; qui si formatta e
basta (`src/app/format.ts`).

Serve Node 24 (con npm 11: npm 10.9 ha un bug di risoluzione delle peer dependency su questo progetto).

```bash
npm ci
npm start
```

`npm start` apre `http://localhost:4200` e inoltra `/api` all'API su `http://localhost:5224`
(`proxy.conf.json`). In produzione lo fa nginx (`nginx.conf.template`, con l'indirizzo dell'API in `API_UPSTREAM`).

Colori, raggi e dimensioni stanno solo nei token di `src/styles.css`, con tema chiaro e scuro; la
soglia mobile è 860px.
