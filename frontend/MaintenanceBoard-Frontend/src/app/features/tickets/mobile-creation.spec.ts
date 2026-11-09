import { API_BASE_URL } from '../../core/api/api.config';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { ViewportService } from '../../core/layout/viewport.service';
import { Board } from '../board/board';
import { CreateTicketPage } from './create-ticket-page';
import { pendingCreationGuard } from './pending-creation.guard';

describe('Mobile creation flow', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: API_BASE_URL, useValue: 'http://localhost:5213/api' },
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: ViewportService, useValue: { isMobile: () => true } },
      ],
    });
  });
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('uses mobile state buttons and navigates to a separate creation page', () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(Board);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets/board')
      .flush({ pending: [], inProgress: [], closed: [] });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelectorAll('.mobile-status button').length).toBe(3);
    element.querySelector<HTMLButtonElement>('.new-ticket')!.click();
    expect(navigate).toHaveBeenCalledWith(['/tickets/new'], { queryParams: { returnTo: 'board' } });
    expect(element.querySelector('dialog')).toBeNull();
  });

  it('renders a normal page, protects pending creation, and restricts the return route', () => {
    TestBed.overrideProvider(ActivatedRoute, { useValue: { snapshot: { queryParamMap: new Map([['returnTo', 'https://untrusted.example']]) } } });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(CreateTicketPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('dialog')).toBeNull();
    expect(element.querySelector('h1')?.textContent).toBe('Nuevo ticket');
    for (const [selector, value] of [['#ticket-title', 'Equipo averiado'], ['#ticket-description', 'No enciende']]) {
      const field = element.querySelector<HTMLInputElement>(selector)!;
      field.value = value;
      field.dispatchEvent(new Event('input'));
    }
    element.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    expect(fixture.componentInstance.saving()).toBe(true);
    expect(pendingCreationGuard(fixture.componentInstance, {} as never, {} as never, {} as never)).toBe(false);
    TestBed.inject(HttpTestingController).expectOne('http://localhost:5213/api/tickets')
      .flush({ ticketId: 77 }, { status: 201, statusText: 'Created' });
    expect(fixture.componentInstance.saving()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/board'], { queryParams: { ticket: 77 } });
  });
});
