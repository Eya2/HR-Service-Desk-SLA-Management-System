import { Role, Session, UserProfile } from '../core/auth/auth.models';

export function profile(roles: Role[] = ['Employee']): UserProfile {
  return {
    id: 'u-1',
    email: 'amira.bensalah@acme.example',
    firstName: 'Amira',
    lastName: 'Ben Salah',
    fullName: 'Amira Ben Salah',
    roles,
    tenantId: 't-1',
    tenantName: 'Acme Tunisie',
  };
}

export function session(token = 'token-1', roles: Role[] = ['Employee']): Session {
  return { accessToken: token, expiresAt: '2026-10-02T10:15:00Z', user: profile(roles) };
}
