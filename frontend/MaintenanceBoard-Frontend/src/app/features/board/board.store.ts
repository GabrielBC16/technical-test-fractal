import { computed, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, Subject, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { TicketBoard } from '../../core/tickets/ticket.models';
import { TicketsApiService } from '../../core/tickets/tickets-api.service';

type BoardState =
  | { status: 'loading'; board: null; error: '' }
  | { status: 'ready'; board: TicketBoard; error: '' }
  | { status: 'error'; board: null; error: string };

@Injectable()
export class BoardStore {
  private readonly api = inject(TicketsApiService);
  private readonly reload = new Subject<void>();
  private readonly currentState = signal<BoardState>({ status: 'loading', board: null, error: '' });
  readonly state = this.currentState.asReadonly();
  readonly board = computed(() => this.state().board);
  readonly total = computed(() => {
    const board = this.board();
    return board ? board.pending.length + board.inProgress.length + board.closed.length : 0;
  });

  constructor() {
    // Switch to the latest request and keep retry available after HTTP errors.
    this.reload.pipe(
      startWith(undefined),
      switchMap(() => this.api.getBoard().pipe(
        map((board): BoardState => ({ status: 'ready', board, error: '' })),
        catchError((error: unknown) => of<BoardState>({ status: 'error', board: null, error: apiErrorMessage(error) })),
        startWith<BoardState>({ status: 'loading', board: null, error: '' }),
      )),
      takeUntilDestroyed(),
    ).subscribe(state => this.currentState.set(state));
  }

  /** Refresh from the server and cancel an older unfinished request. */
  refresh(): void {
    this.reload.next();
  }
}
