import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastService } from '../../core/notifications/toast.service';
import { DesktopShell } from '../../layout/desktop-shell';
import { CreateTicket } from './create-ticket';

@Component({
  selector: 'app-create-ticket-page',
  imports: [DesktopShell, CreateTicket],
  template: `
    <app-desktop-shell>
      <main id="main-content"><app-create-ticket [standalone]="true" (busyChanged)="saving.set($event)"
        (dismissed)="cancel()" (created)="created($event)" /></main>
    </app-desktop-shell>
  `,
})
export class CreateTicketPage {
  readonly saving = signal(false);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly returnPath = inject(ActivatedRoute).snapshot.queryParamMap.get('returnTo') === 'tickets' ? '/tickets' : '/board';

  /** Return only to known app routes; never trust an arbitrary redirect query. */
  protected cancel(): void {
    if (!this.saving()) void this.router.navigate([this.returnPath]);
  }

  /** Acknowledge the created ID and open its detail on the originating view. */
  protected created(ticketId: number): void {
    this.saving.set(false);
    this.toast.show(`Ticket #${ticketId} creado correctamente.`);
    void this.router.navigate([this.returnPath], { queryParams: { ticket: ticketId } });
  }
}
