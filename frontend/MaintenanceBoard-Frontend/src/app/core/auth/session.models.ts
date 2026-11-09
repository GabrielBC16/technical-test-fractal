export type UserRole = 'Requester' | 'Operator' | 'Supervisor';

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface UserSession {
  userId: number;
  userName: string;
  names: string;
  lastNames: string;
  role: UserRole;
}

export const ROLE_LABELS: Record<UserRole, string> = {
  Requester: 'Solicitante',
  Operator: 'Operador',
  Supervisor: 'Supervisor',
};

/** Reject malformed stored identities before restoring the simulated session. */
export function isUserSession(value: unknown): value is UserSession {
  if (!value || typeof value !== 'object') return false;
  const user = value as Partial<UserSession>;
  return typeof user.userId === 'number' && Number.isSafeInteger(user.userId) && user.userId > 0
    && typeof user.userName === 'string' && user.userName.length > 0
    && typeof user.names === 'string' && typeof user.lastNames === 'string'
    && (user.role === 'Requester' || user.role === 'Operator' || user.role === 'Supervisor');
}
