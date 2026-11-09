import { HttpErrorResponse } from '@angular/common/http';

export interface ApiProblem {
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

/** Keep raw ProblemDetails on the HTTP error and show a safe Spanish message. */
export function apiErrorMessage(error: unknown, login = false): string {
  if (!(error instanceof HttpErrorResponse)) return 'No se pudo completar la operación. Inténtalo nuevamente.';
  switch (error.status) {
    case 0: return 'No se pudo conectar con el servidor. Verifica tu conexión e inténtalo nuevamente.';
    case 400: return 'Revisa los campos ingresados.';
    case 401: return login ? 'Usuario o contraseña incorrectos.' : 'Tu sesión terminó. Inicia sesión nuevamente.';
    case 403: return 'No tienes permisos para realizar esta operación.';
    case 404: return 'No se encontró el recurso solicitado.';
    case 409: return 'El ticket cambió o la operación genera un conflicto. Actualiza los datos.';
    case 422: return 'No se cumplen los requisitos para realizar esta operación.';
    default: return 'El servidor no pudo completar la operación. Inténtalo nuevamente.';
  }
}
