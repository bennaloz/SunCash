import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** Guscio dell'app: navigazione in alto su desktop, in basso sul telefono. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="bar">
      <a class="brand" routerLink="/">☀ SunCash</a>
      <nav>
        @for (item of nav; track item.path) {
          <a [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: item.path === '/' }">{{ item.label }}</a>
        }
      </nav>
    </header>
    <main><router-outlet /></main>
  `,
  styles: `
    .bar { display: flex; align-items: center; gap: 24px; padding: 10px 16px; background: var(--surface); border-bottom: 1px solid var(--border); position: sticky; top: 0; z-index: 10; }
    .brand { font-weight: 700; text-decoration: none; }
    nav { display: flex; gap: 4px; }
    nav a { text-decoration: none; padding: 6px 10px; border-radius: var(--radius-sm); color: var(--text-muted); }
    nav a.active { background: var(--surface-2); color: var(--text); font-weight: 600; }
    @media (max-width: 860px) {
      nav { position: fixed; bottom: 0; left: 0; right: 0; justify-content: space-around; overflow-x: auto; background: var(--surface); border-top: 1px solid var(--border); padding: 6px 4px; }
      nav a { font-size: var(--fs-small); padding: 6px 4px; white-space: nowrap; }
    }
  `,
})
export class App {
  protected readonly nav = [
    { path: '/', label: 'Oggi' },
    { path: '/calendario', label: 'Calendario' },
    { path: '/andamento', label: 'Andamento' },
    { path: '/prezzo', label: 'Prezzo' },
    { path: '/bollette', label: 'Bollette' },
    { path: '/stato', label: 'Stato' },
  ];
}
