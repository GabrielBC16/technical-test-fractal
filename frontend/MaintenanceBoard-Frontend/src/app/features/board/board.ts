import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ROLE_LABELS } from '../../core/auth/session.models';
import { SessionService } from '../../core/auth/session.service';

@Component({
  selector: 'app-board',
  template: `
    <main class="min-h-screen bg-slate-50 p-10">
      <header class="flex items-center justify-between border-b border-slate-200 pb-6">
        <span class="text-xl font-bold text-cyan-800">FRACTAL</span>
        @if (session.user(); as user) {
          <div class="flex items-center gap-6">
            <div><p class="font-medium">{{ user.names }} {{ user.lastNames }}</p><p class="text-sm text-slate-500">{{ roles[user.role] }}</p></div>
            <button type="button" class="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm" (click)="logout()">Cerrar sesión</button>
          </div>
        }
      </header>
      <section class="mt-10 rounded-xl border border-slate-200 bg-white p-8" aria-labelledby="board-title">
        <h1 id="board-title" class="text-2xl font-semibold">Tablero de tickets de mantenimiento</h1>
        <p class="mt-3 text-slate-600">Sesión iniciada. El tablero de tickets estará disponible en el siguiente hito.</p>
      </section>
    </main>
  `,
})
export class Board {
  protected readonly session = inject(SessionService);
  protected readonly roles = ROLE_LABELS;
  private readonly router = inject(Router);

  /** Remove the simulated identity before returning to login. */
  protected logout(): void {
    this.session.logout();
    void this.router.navigateByUrl('/login');
  }
}
