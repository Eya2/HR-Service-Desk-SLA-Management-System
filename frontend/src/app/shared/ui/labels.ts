/** Display helpers for API enum values. */

const CATEGORY_LABELS: Record<string, string> = {
  Payroll: 'Payroll',
  LeaveAndAbsence: 'Leave & absence',
  Contracts: 'Contracts',
  Benefits: 'Benefits',
  OnboardingOffboarding: 'Onboarding & offboarding',
  Training: 'Training',
  Expenses: 'Expenses',
  Certificates: 'Certificates',
  Confidential: 'Confidential',
};

const CATEGORY_ICONS: Record<string, string> = {
  Payroll: 'payments',
  LeaveAndAbsence: 'beach_access',
  Contracts: 'contract',
  Benefits: 'health_and_safety',
  OnboardingOffboarding: 'badge',
  Training: 'school',
  Expenses: 'receipt_long',
  Certificates: 'workspace_premium',
  Confidential: 'lock',
};

export const categoryLabel = (category: string): string => CATEGORY_LABELS[category] ?? category;

export const categoryIcon = (category: string): string => CATEGORY_ICONS[category] ?? 'description';

/** "WaitingOnEmployee" → "Waiting on employee". */
export function humanize(value: string): string {
  const words = value.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

export function fileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
