import { TestBed } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('Toast feedback', () => {
  afterEach(() => vi.useRealTimers());

  it('follows the topmost dialog and keeps errors available until dismissed', () => {
    vi.useFakeTimers();
    const toast = TestBed.inject(ToastService);
    const closeDetail = toast.activate('detail');
    const closeAction = toast.activate('action');
    expect(toast.activeScope()).toBe('action');
    closeAction();
    expect(toast.activeScope()).toBe('detail');
    toast.show('Ticket guardado.');
    toast.show('No se pudo guardar.', 'error');
    vi.advanceTimersByTime(7000);
    expect(toast.items().map(item => item.message)).toEqual(['No se pudo guardar.']);
    toast.dismiss(toast.items()[0].id);
    expect(toast.items()).toEqual([]);
    closeDetail();
    expect(toast.activeScope()).toBe('app');
  });
});
