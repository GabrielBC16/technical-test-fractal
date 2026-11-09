import { DOCUMENT } from '@angular/common';
import { DestroyRef, inject, Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ViewportService {
  private readonly mobile = signal(false);
  readonly isMobile = this.mobile.asReadonly();

  constructor() {
    // Match the CSS breakpoint and release the listener when the application is destroyed.
    const window = inject(DOCUMENT).defaultView;
    const query = window?.matchMedia?.('(max-width: 767px)');
    if (!query) return;
    this.mobile.set(query.matches);
    const update = (event: MediaQueryListEvent) => this.mobile.set(event.matches);
    query.addEventListener('change', update);
    inject(DestroyRef).onDestroy(() => query.removeEventListener('change', update));
  }
}
