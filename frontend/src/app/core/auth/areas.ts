import { Role } from './auth.models';

/** A top-level work area and the roles that may open it (mirrors the API's authorization policies). */
export interface Area {
  path: string;
  /** Translation keys. */
  label: string;
  icon: string;
  description: string;
  roles: readonly Role[];
}

export const AREAS: readonly Area[] = [
  {
    path: 'portal',
    label: 'area.portal.label',
    icon: 'inbox',
    description: 'area.portal.description',
    roles: ['Employee'],
  },
  {
    path: 'manager',
    label: 'area.manager.label',
    icon: 'fact_check',
    description: 'area.manager.description',
    roles: ['Manager', 'HrOfficer', 'PayrollSpecialist', 'HrAdmin'],
  },
  {
    path: 'agent',
    label: 'area.agent.label',
    icon: 'support_agent',
    description: 'area.agent.description',
    roles: ['HrOfficer', 'PayrollSpecialist', 'HrAdmin', 'Auditor'],
  },
  {
    path: 'dashboard',
    label: 'area.dashboard.label',
    icon: 'monitoring',
    description: 'area.dashboard.description',
    roles: ['HrAdmin', 'Auditor', 'HrOfficer', 'PayrollSpecialist'],
  },
  {
    path: 'audit',
    label: 'area.audit.label',
    icon: 'policy',
    description: 'area.audit.description',
    roles: ['HrAdmin', 'Auditor'],
  },
  {
    path: 'admin',
    label: 'area.admin.label',
    icon: 'admin_panel_settings',
    description: 'area.admin.description',
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
