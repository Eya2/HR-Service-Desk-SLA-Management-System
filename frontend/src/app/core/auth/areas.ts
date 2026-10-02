import { Role } from './auth.models';

/** A top-level work area and the roles that may open it (mirrors the API's authorization policies). */
export interface Area {
  path: string;
  label: string;
  icon: string;
  description: string;
  roles: readonly Role[];
}

export const AREAS: readonly Area[] = [
  {
    path: 'portal',
    label: 'My requests',
    icon: 'inbox',
    description: 'Browse the HR catalog, submit requests and follow them.',
    roles: ['Employee'],
  },
  {
    path: 'manager',
    label: 'Approvals',
    icon: 'fact_check',
    description: 'Decide on requests waiting for your approval and follow your team.',
    roles: ['Manager', 'HrOfficer', 'PayrollSpecialist', 'HrAdmin'],
  },
  {
    path: 'agent',
    label: 'HR queue',
    icon: 'support_agent',
    description: 'Work on assigned cases and keep SLAs on track.',
    roles: ['HrOfficer', 'PayrollSpecialist', 'HrAdmin', 'Auditor'],
  },
  {
    path: 'dashboard',
    label: 'Dashboards',
    icon: 'monitoring',
    description: 'SLA compliance, backlog and workload indicators.',
    roles: ['HrAdmin', 'Auditor', 'HrOfficer', 'PayrollSpecialist'],
  },
  {
    path: 'admin',
    label: 'Administration',
    icon: 'admin_panel_settings',
    description: 'Users, request types, workflows and SLA policies.',
    roles: ['HrAdmin'],
  },
];

export function areaByPath(path: string): Area {
  const area = AREAS.find((a) => a.path === path);
  if (!area) {
    throw new Error(`Unknown area: ${path}`);
  }
  return area;
}
