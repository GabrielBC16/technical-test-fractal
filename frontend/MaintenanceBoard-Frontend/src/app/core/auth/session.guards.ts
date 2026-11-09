import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from './session.service';

/** Protect navigation while leaving effective authorization to the API. */
export const authenticatedGuard: CanActivateFn = () =>
  inject(SessionService).isAuthenticated() || inject(Router).createUrlTree(['/login']);

/** Avoid showing the login form to an existing local session. */
export const guestGuard: CanActivateFn = () =>
  !inject(SessionService).isAuthenticated() || inject(Router).createUrlTree(['/board']);

/** Reserve management routes for the roles accepted by the backend. */
export const managementGuard: CanActivateFn = () =>
  inject(SessionService).canManageTickets() || inject(Router).createUrlTree(['/board']);
