import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import { SessionService } from './session.service';

/** Send identity only to this API and expire the matching session on HTTP 401. */
export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  const apiUrl = inject(API_BASE_URL);
  const session = inject(SessionService);
  const router = inject(Router);
  const user = session.user();
  const isApi = request.url === apiUrl || request.url.startsWith(`${apiUrl}/`);
  const isLogin = request.url.split('?')[0] === `${apiUrl}/auth/login`;
  if (!isApi || isLogin || !user) return next(request);

  return next(request.clone({ setHeaders: { 'X-User-Id': String(user.userId) } })).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && session.user() === user) {
        session.logout();
        void router.navigate(['/login'], { queryParams: { expired: '1' } });
      }
      return throwError(() => error);
    }),
  );
};
