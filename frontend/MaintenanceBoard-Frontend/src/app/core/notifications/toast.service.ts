import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';

export interface ToastMessage { id: number; message: string; kind: 'success' | 'error'; }

@Injectable({ providedIn: 'root' })
export class ToastService {
  private sequence = 0;
  private readonly messages = signal<ToastMessage[]>([]);
  private readonly scopes = signal<string[]>(['app']);
  private readonly timers = new Map<number, ReturnType<typeof setTimeout>>();
  readonly items = this.messages.asReadonly();
  readonly activeScope = computed(() => this.scopes().at(-1));

  constructor() {
    inject(DestroyRef).onDestroy(() => { for (const timer of this.timers.values()) clearTimeout(timer); });
  }

  /** Keep feedback in the topmost native dialog so it remains visible and accessible. */
  activate(scope: string): () => void {
    this.scopes.update(scopes => [...scopes, scope]);
    return () => this.scopes.update(scopes => scopes.filter(value => value !== scope));
  }

  /** Bound the stack; successes expire while errors remain until dismissed. */
  show(message: string, kind: ToastMessage['kind'] = 'success'): void {
    if (this.items().length >= 3) this.dismiss(this.items()[0].id);
    const id = ++this.sequence;
    this.messages.update(items => [...items, { id, message, kind }]);
    if (kind === 'success') this.timers.set(id, setTimeout(() => this.dismiss(id), 7000));
  }

  /** Remove a notification and release its timer when the user dismisses it. */
  dismiss(id: number): void {
    clearTimeout(this.timers.get(id));
    this.timers.delete(id);
    this.messages.update(items => items.filter(item => item.id !== id));
  }
}
