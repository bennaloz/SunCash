# Fixture dei test

Tutte **sintetiche o anonimizzate**: il repo è pubblico (CLAUDE.md, «Repository pubblico»).

- `gme-zonal-prices-pt15-20261025.json`, `gme-zonal-prices-pt60-20261025.json`: prezzi **inventati** nel
  formato di `ME_ZonalPrices` (manuale tecnico GME del 15/10/2025, § 5.52), per il 25 ottobre 2026,
  giorno del ritorno all'ora solare (100 quarti d'ora, 25 ore). Tre zone, per provare che si tiene
  solo quella configurata più il PUN.

I file reali stanno solo in `tests/fixtures.local/`, ignorata da git.
