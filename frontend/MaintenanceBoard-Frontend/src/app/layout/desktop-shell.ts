import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { ROLE_LABELS } from '../core/auth/session.models';
import { SessionService } from '../core/auth/session.service';
import { initials } from '../core/tickets/ticket-presentation';

@Component({
  selector: 'app-desktop-shell',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <div class="desktop-shell">
      <aside class="sidebar" aria-label="Navegación principal">
        <a class="brand" routerLink="/board" aria-label="Fractal, inicio">FRACTAL</a>
        <p class="sidebar-subtitle">Solicitudes y<br />Mantenimientos</p>
        <nav>
          <a class="nav-link" routerLink="/board" routerLinkActive="active-nav" ariaCurrentWhenActive="page"><span aria-hidden="true">▦</span> Tablero</a>
          <a class="nav-link" routerLink="/tickets" routerLinkActive="active-nav" ariaCurrentWhenActive="page"><span aria-hidden="true">≡</span> Listado de tickets</a>
        </nav>
        <p class="workspace-label">TU ESPACIO DE TRABAJO</p>
        <p class="sidebar-description">Gestiona incidencias y consulta cada paso de su atención.</p>
        <div class="sidebar-note"><strong>Cada cambio<br />cuenta</strong><p>El historial conserva los estados, comentarios y asignaciones.</p></div>
      </aside>
      <div class="workspace">
        <header class="topbar">
          <p>Operaciones <span aria-hidden="true">/</span> Mantenimiento</p>
          @if (session.user(); as user) {
            <div class="account">
              <span class="avatar" aria-hidden="true">{{ getInitials(user.names + ' ' + user.lastNames) }}</span>
              <div><p class="account-name">{{ user.names }} {{ user.lastNames }}</p><p class="account-role">{{ roles[user.role] }}</p></div>
              <button class="logout" type="button" (click)="logout()">Cerrar sesión</button>
            </div>
          }
        </header>
        <ng-content />
      </div>
    </div>
  `,
  styleUrl: './desktop-shell.css',
})
export class DesktopShell {
  protected readonly session = inject(SessionService);
  protected readonly roles = ROLE_LABELS;
  protected readonly getInitials = initials;
  private readonly router = inject(Router);

  /** Clear the simulated identity before returning to login. */
  protected logout(): void {
    this.session.logout();
    void this.router.navigateByUrl('/login');
  }
}
