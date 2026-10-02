/** Business roles, as issued by the API in the access token and profile. */
export type Role = 'Employee' | 'Manager' | 'HrOfficer' | 'PayrollSpecialist' | 'HrAdmin' | 'Auditor' | 'SuperAdmin';

export interface UserProfile {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  roles: Role[];
  tenantId: string;
  tenantName: string;
}

/** Response of `POST /api/auth/login` and `/refresh`. The refresh token itself stays in an HttpOnly cookie. */
export interface Session {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
}
