import { Routes } from '@angular/router';
import { authenticatedGuard, guestGuard } from './core/auth/session.guards';
import { pendingCreationGuard } from './features/tickets/pending-creation.guard';

export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/login/login').then(m => m.Login) },
  { path: 'board', canActivate: [authenticatedGuard], loadComponent: () => import('./features/board/board').then(m => m.Board) },
  { path: 'tickets/new', canActivate: [authenticatedGuard], canDeactivate: [pendingCreationGuard], loadComponent: () => import('./features/tickets/create-ticket-page').then(m => m.CreateTicketPage) },
  { path: 'tickets', canActivate: [authenticatedGuard], loadComponent: () => import('./features/ticket-list/ticket-list').then(m => m.TicketList) },
  { path: '', pathMatch: 'full', redirectTo: 'board' },
  { path: '**', redirectTo: 'board' },
];
