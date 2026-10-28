import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./pages/today').then(m => m.TodayPage) },
  { path: 'giorno/:date', loadComponent: () => import('./pages/today').then(m => m.TodayPage) },
  { path: 'calendario', loadComponent: () => import('./pages/calendar').then(m => m.CalendarPage) },
  { path: 'andamento', loadComponent: () => import('./pages/trend').then(m => m.TrendPage) },
  { path: 'prezzo', loadComponent: () => import('./pages/captured-price').then(m => m.CapturedPricePage) },
  { path: 'bollette', loadComponent: () => import('./pages/bills').then(m => m.BillsPage) },
  { path: 'stato', loadComponent: () => import('./pages/status').then(m => m.StatusPage) },
  { path: '**', redirectTo: '' },
];
