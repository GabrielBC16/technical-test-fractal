import { afterNextRender, Component, DestroyRef, ElementRef, inject, input, output, viewChild } from '@angular/core';
import { ToastService } from '../../core/notifications/toast.service';
import { ToastOutlet } from '../toast-outlet/toast-outlet';

@Component({
  selector: 'app-side-panel',
  imports: [ToastOutlet],
  template: `
    <dialog #dialog [class.wide]="wide()" [class.centered]="centered()" [attr.aria-labelledby]="titleId" [attr.aria-describedby]="subtitleId"
      (cancel)="cancel($event)">
      <header class="panel-header">
        <div><h2 [id]="titleId">{{ title() }}</h2><p [id]="subtitleId">{{ subtitle() }}</p></div>
        <button class="close-button" type="button" aria-label="Cerrar panel" (click)="dismissed.emit()" [disabled]="busy()">×</button>
      </header>
      <ng-content />
      <app-toast-outlet [scope]="titleId" />
    </dialog>
  `,
  styleUrl: './side-panel.css',
})
export class SidePanel {
  private static nextId = 0;
  protected readonly titleId = `panel-title-${SidePanel.nextId++}`;
  protected readonly subtitleId = `${this.titleId}-subtitle`;
  readonly title = input.required<string>();
  readonly subtitle = input('');
  readonly wide = input(false);
  readonly centered = input(false);
  readonly busy = input(false);
  readonly dismissed = output<void>();
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    // Native modal dialogs isolate background controls and restore the opener's focus.
    let element: HTMLDialogElement | undefined;
    let releaseScope: (() => void) | undefined;
    const toast = inject(ToastService);
    afterNextRender(() => {
      element = this.dialog().nativeElement;
      element.showModal();
      releaseScope = toast.activate(this.titleId);
    });
    inject(DestroyRef).onDestroy(() => { releaseScope?.(); element?.close(); });
  }

  /** Prevent Escape from closing the panel while a mutation is in flight. */
  protected cancel(event: Event): void {
    event.preventDefault();
    if (!this.busy()) this.dismissed.emit();
  }
}
