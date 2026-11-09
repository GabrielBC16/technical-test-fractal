import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, Observable, Subscription } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { SessionService } from '../../core/auth/session.service';
import { AssignableUser, TicketListItem, TransitionTicketRequest } from '../../core/tickets/ticket.models';
import { TicketsApiService } from '../../core/tickets/tickets-api.service';
import { SidePanel } from '../../shared/side-panel/side-panel';
import { ToastService } from '../../core/notifications/toast.service';

type TransitionTarget = TransitionTicketRequest['newStatus'];

@Component({
  selector: 'app-ticket-actions',
  imports: [ReactiveFormsModule, SidePanel],
  templateUrl: './ticket-actions.html',
  styleUrl: './ticket-actions.css',
})
export class TicketActions {
  readonly ticket = input.required<TicketListItem>();
  readonly changed = output<string>();
  readonly refreshRequested = output<void>();
  readonly busyChanged = output<boolean>();
  protected readonly session = inject(SessionService);
  protected readonly open = computed(() => this.ticket().status === 'Pending' || this.ticket().status === 'InProgress');
  protected readonly canComment = computed(() => this.session.canManageTickets()
    || (this.session.user()?.role === 'Requester' && this.ticket().reportedByUserId === this.session.user()?.userId));
  protected readonly mode = signal<'assignment' | TransitionTarget | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly conflict = signal(false);
  protected readonly users = signal<AssignableUser[]>([]);
  protected readonly usersLoading = signal(false);
  protected readonly usersError = signal('');
  protected readonly availableUsers = computed(() => this.users().filter(user =>
    user.isActive && (user.role === 'Operator' || user.role === 'Supervisor')
    && !this.ticket().assignees.some(assigned => assigned.userId === user.userId)));
  protected readonly additional = computed(() => this.ticket().assignees.length > 0);
  protected readonly dialogTitle = computed(() => {
    switch (this.mode()) {
      case 'assignment': return this.additional() ? 'Agregar operador' : 'Asignar operador';
      case 'Closed': return 'Cerrar ticket';
      case 'Rejected': return 'Rechazar ticket';
      default: return 'Iniciar atención';
    }
  });
  private readonly api = inject(TicketsApiService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly builder = inject(FormBuilder);
  private usersRequest?: Subscription;

  protected readonly assignmentForm = this.builder.nonNullable.group({
    assignedUserId: ['', Validators.required],
    justification: [''],
  });
  protected readonly transitionForm = this.builder.nonNullable.group({ text: [''] });
  protected readonly commentForm = this.builder.nonNullable.group({
    comment: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(4000)]],
  });

  /** Open one registration at a time; each additional assignee has its own required reason. */
  protected openAssignment(): void {
    if (!this.session.canManageTickets() || !this.open() || this.ticket().assignees.length >= 3 || this.busy() || this.conflict()) return;
    this.assignmentForm.reset();
    const reason = this.assignmentForm.controls.justification;
    reason.setValidators(this.additional()
      ? [Validators.required, Validators.pattern(/\S/), Validators.maxLength(2000)]
      : [Validators.maxLength(2000)]);
    reason.updateValueAndValidity();
    this.error.set('');
    this.mode.set('assignment');
    this.loadUsers();
  }

  /** Fetch eligible users only when an authorized manager opens the assignment form. */
  protected loadUsers(): void {
    if (!this.session.canManageTickets() || this.busy()) return;
    this.usersRequest?.unsubscribe();
    this.users.set([]);
    this.usersLoading.set(true);
    this.usersError.set('');
    this.usersRequest = this.api.getAssignableUsers().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.usersLoading.set(false)),
    ).subscribe({
      next: users => this.users.set(users),
      error: (error: unknown) => this.usersError.set(apiErrorMessage(error)),
    });
  }

  /** Save exactly one eligible user and never resend the existing assignments. */
  protected assign(): void {
    if (this.busy() || this.conflict() || !this.open() || !this.session.canManageTickets()) return;
    this.assignmentForm.markAllAsTouched();
    if (this.assignmentForm.invalid || this.ticket().assignees.length >= 3) return;
    const value = this.assignmentForm.getRawValue();
    const assignedUserId = Number(value.assignedUserId);
    if (!this.availableUsers().some(user => user.userId === assignedUserId)) return;
    const justification = value.justification.trim();
    this.execute(this.api.assign(this.ticket().ticketId, {
      assignedUserId, ...(justification ? { justification } : {}),
    }), 'Operador asignado correctamente.');
  }

  /** Offer only valid forward transitions and require the corresponding conditional field. */
  protected openTransition(target: TransitionTarget): void {
    if (!this.session.canManageTickets() || !this.open() || this.busy() || this.conflict()) return;
    if (target === 'InProgress' && (this.ticket().status !== 'Pending' || this.ticket().assignees.length === 0)) return;
    if (target === 'Closed' && this.ticket().status !== 'InProgress') return;
    this.transitionForm.reset();
    const text = this.transitionForm.controls.text;
    text.setValidators(target === 'InProgress' ? [] : [Validators.required, Validators.pattern(/\S/), Validators.maxLength(4000)]);
    text.updateValueAndValidity();
    this.error.set('');
    this.mode.set(target);
  }

  /** Send expectedStatus from the displayed snapshot, with cause or rejection comment as required. */
  protected transition(): void {
    const target = this.mode();
    if (!target || target === 'assignment' || this.busy() || this.conflict() || !this.session.canManageTickets() || !this.open()) return;
    this.transitionForm.markAllAsTouched();
    if (this.transitionForm.invalid) return;
    const text = this.transitionForm.getRawValue().text.trim();
    this.execute(this.api.transition(this.ticket().ticketId, {
      expectedStatus: this.ticket().status, newStatus: target,
      ...(target === 'Closed' ? { cause: text } : {}),
      ...(target === 'Rejected' ? { comment: text } : {}),
    }), target === 'Closed' ? 'Ticket cerrado correctamente.'
      : target === 'Rejected' ? 'Ticket rechazado correctamente.' : 'Atención iniciada correctamente.');
  }

  /** Requesters may comment only on their own tickets, including terminal tickets. */
  protected addComment(): void {
    if (this.busy() || this.conflict() || !this.canComment()) return;
    this.commentForm.markAllAsTouched();
    if (this.commentForm.invalid) return;
    this.execute(this.api.addComment(this.ticket().ticketId, this.commentForm.getRawValue().comment.trim()),
      'Comentario publicado correctamente.');
  }

  /** Keep the active form while a mutation is pending and clear transient errors on dismissal. */
  protected closeAction(): void {
    if (this.busy()) return;
    this.usersRequest?.unsubscribe();
    this.mode.set(null);
    if (!this.conflict()) this.error.set('');
  }

  /** Serialize mutations; preserve failed drafts and require refresh after a concurrency conflict. */
  private execute(request: Observable<void>, message: string): void {
    this.busy.set(true);
    this.busyChanged.emit(true);
    this.error.set('');
    request.pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => { this.busy.set(false); this.busyChanged.emit(false); }),
    ).subscribe({
      next: () => {
        this.commentForm.reset();
        this.mode.set(null);
        this.changed.emit(message);
      },
      error: (error: unknown) => {
        this.conflict.set(error instanceof HttpErrorResponse && error.status === 409);
        this.error.set(apiErrorMessage(error));
        this.toast.show(apiErrorMessage(error), 'error');
      },
    });
  }
}
