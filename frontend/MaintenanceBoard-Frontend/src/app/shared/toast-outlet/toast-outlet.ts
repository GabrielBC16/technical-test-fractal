import { Component, inject, input } from '@angular/core';
import { ToastService } from '../../core/notifications/toast.service';

@Component({
  selector: 'app-toast-outlet',
  template: `
    @if (toast.activeScope() === scope()) {
      <aside class="toast-stack" aria-label="Notificaciones">
        @for (item of toast.items(); track item.id) {
          <div class="toast" [class.error]="item.kind === 'error'">
            <p [attr.role]="item.kind === 'error' ? 'alert' : 'status'">{{ item.message }}</p>
            <button type="button" aria-label="Cerrar notificación" (click)="toast.dismiss(item.id)">×</button>
          </div>
        }
      </aside>
    }
  `,
  styles: `
    .toast-stack { position: fixed; right: 24px; bottom: 24px; z-index: 1000; display: grid; gap: 10px; width: 380px; max-width: calc(100vw - 48px); }
    .toast { display: flex; align-items: flex-start; gap: 14px; padding: 16px; border: 1px solid #b9dfce; border-radius: 10px; background: #e3f5ec; color: #176949; box-shadow: 0 6px 24px #17202a18; }
    .error { border-color: #edbec9; background: #fbe9ed; color: #a82039; }
    p { flex: 1; margin: 0; font-size: 13px; line-height: 1.5; overflow-wrap: anywhere; }
    button { border: 0; background: transparent; color: inherit; font-size: 20px; line-height: 1; }
  `,
})
export class ToastOutlet {
  readonly scope = input('app');
  protected readonly toast = inject(ToastService);
}
