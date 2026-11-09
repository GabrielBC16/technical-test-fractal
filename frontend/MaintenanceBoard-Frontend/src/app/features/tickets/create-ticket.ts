import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { SessionService } from '../../core/auth/session.service';
import { TicketsApiService } from '../../core/tickets/tickets-api.service';
import { SidePanel } from '../../shared/side-panel/side-panel';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-create-ticket',
  imports: [ReactiveFormsModule, SidePanel],
  templateUrl: './create-ticket.html',
  styleUrl: './create-ticket.css',
})
export class CreateTicket {
  readonly standalone = input(false);
  readonly busyChanged = output<boolean>();
  readonly dismissed = output<void>();
  readonly created = output<number>();
  protected readonly session = inject(SessionService);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  private readonly api = inject(TicketsApiService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    title: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(4000)]],
  });

  /** Validate and create once; keep the input available when the server rejects it. */
  protected submit(): void {
    if (this.busy()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    this.busy.set(true);
    this.busyChanged.emit(true);
    this.error.set('');
    this.form.disable();
    this.api.create({ title: value.title.trim(), description: value.description.trim() }).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => {
        this.busy.set(false);
        this.form.enable();
        if (!this.destroyRef.destroyed) this.busyChanged.emit(false);
      }),
    ).subscribe({
      next: response => this.created.emit(response.ticketId),
      error: (error: unknown) => {
        const message = apiErrorMessage(error);
        this.error.set(message);
        this.toast.show(message, 'error');
      },
    });
  }
}
