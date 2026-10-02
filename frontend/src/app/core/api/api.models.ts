/** DTOs returned by the HR Service Desk API. */

export type FieldType = 'Text' | 'Textarea' | 'Number' | 'Date' | 'Select' | 'File';

export interface FieldOption {
  value: string;
  label: string;
}

/** One input of a request type's dynamic form (see the API's FormSchema). */
export interface FormFieldDef {
  key: string;
  label: string;
  type: FieldType;
  required?: boolean;
  helpText?: string;
  options?: FieldOption[];
  min?: number;
  max?: number;
  minLength?: number;
  maxLength?: number;
  pattern?: string;
  maxFiles?: number;
}

export interface RequestTypeSummary {
  id: string;
  name: string;
  description: string;
  category: string;
  isConfidential: boolean;
  isActive: boolean;
  defaultPriority: string;
}

export interface RequestType extends RequestTypeSummary {
  fields: FormFieldDef[];
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface TicketSummary {
  id: string;
  reference: string;
  title: string;
  requestTypeId: string;
  requestTypeName: string;
  category: string;
  status: string;
  priority: string;
  isConfidential: boolean;
  requesterId: string;
  requesterName: string;
  teamId: string | null;
  teamName: string | null;
  assigneeId: string | null;
  assigneeName: string | null;
  slaState: SlaStateName;
  resolutionDueAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export type SlaStateName = 'None' | 'OnTrack' | 'AtRisk' | 'Breached';

/** SLA position of a case; due dates are null while the clock is paused. */
export interface TicketSla {
  state: SlaStateName;
  isPaused: boolean;
  firstResponseDueAt: string | null;
  resolutionDueAt: string | null;
  firstRespondedAt: string | null;
  firstResponseBreached: boolean;
  resolutionBreached: boolean;
  firstResponseTargetMinutes: number | null;
  resolutionTargetMinutes: number | null;
}

export interface CalendarInfo {
  id: string;
  name: string;
  timeZoneId: string;
  workingHours: { day: string; start: string; end: string }[];
  holidays: { date: string; name: string }[];
}

export interface SlaPolicyInfo {
  id: string;
  name: string;
  isDefault: boolean;
  atRiskThresholdPercent: number;
  targets: { priority: string; firstResponseMinutes: number; resolutionMinutes: number }[];
  pauseStatuses: string[];
  requestTypes: string[];
}

export interface Person {
  id: string;
  fullName: string;
  email: string;
}

export interface AttachmentInfo {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  fieldKey: string | null;
  uploadedByName: string;
  createdAt: string;
}

export interface CommentInfo {
  id: string;
  authorId: string;
  authorName: string;
  body: string;
  isInternal: boolean;
  createdAt: string;
}

export interface FormAnswer {
  key: string;
  label: string;
  type: FieldType;
  value: unknown;
  /** Human-readable value (e.g. the label of a select option). */
  displayValue: string | null;
  files: AttachmentInfo[];
}

export interface TicketPermissions {
  canComment: boolean;
  canCommentInternally: boolean;
  canEdit: boolean;
  canChangePriority: boolean;
  canAttach: boolean;
  /** Statuses the caller may move the case to, according to the API's status machine. */
  availableTransitions: string[];
  /** The approval step the caller can decide now, if any. */
  decidableApprovalId: string | null;
  canAssign: boolean;
  canClaim: boolean;
}

export interface TeamInfo {
  id: string;
  name: string;
  strategy: 'Manual' | 'RoundRobin' | 'LeastLoaded';
  members: { id: string; fullName: string; activeCases: number }[];
  requestTypes: string[];
}

export const STRATEGIES = ['Manual', 'RoundRobin', 'LeastLoaded'] as const;

export interface ApprovalInfo {
  id: string;
  stepOrder: number;
  stepName: string;
  approverRole: string;
  approverName: string | null;
  decision: 'Pending' | 'Approved' | 'Rejected' | 'Skipped';
  decidedByName: string | null;
  decidedAt: string | null;
  comment: string | null;
}

export interface PendingApproval {
  approvalId: string;
  ticketId: string;
  reference: string;
  title: string;
  requestTypeName: string;
  requesterName: string;
  stepName: string;
  stepOrder: number;
  stepCount: number;
  submittedAt: string;
}

export interface WorkflowStepInfo {
  order: number;
  name: string;
  approverRole: string;
}

export interface WorkflowInfo {
  requestTypeId: string;
  requestTypeName: string;
  requestTypeIsConfidential: boolean;
  responsibleTeamId: string | null;
  isConfigured: boolean;
  isActive: boolean;
  version: number;
  steps: WorkflowStepInfo[];
}

export interface TeamStats {
  teamSize: number;
  openCases: number;
  submittedLast30Days: number;
  pendingMyApproval: number;
  byStatus: { status: string; count: number }[];
}

/** Roles that can be asked to approve; "Manager" means the requester's own manager. */
export const APPROVER_ROLES = ['Manager', 'HrOfficer', 'PayrollSpecialist', 'HrAdmin'] as const;

/** One entry of a case's audit trail. `actorName` is null for system actions. */
export interface TimelineEntry {
  id: string;
  type: string;
  actorName: string | null;
  occurredAt: string;
  data: Record<string, unknown> | null;
}

export interface TicketDetails {
  id: string;
  reference: string;
  title: string;
  description: string;
  requestTypeId: string;
  requestTypeName: string;
  category: string;
  status: string;
  priority: string;
  isConfidential: boolean;
  requester: Person;
  assignee: Person | null;
  team: { id: string; name: string } | null;
  sla: TicketSla;
  answers: FormAnswer[];
  attachments: AttachmentInfo[];
  comments: CommentInfo[];
  timeline: TimelineEntry[];
  approvals: ApprovalInfo[];
  permissions: TicketPermissions;
  createdAt: string;
  updatedAt: string | null;
}

export const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;

export const STATUSES = [
  'New',
  'PendingApproval',
  'Open',
  'InProgress',
  'WaitingOnEmployee',
  'Resolved',
  'Closed',
  'Reopened',
  'Rejected',
  'Cancelled',
] as const;

export interface EscalationRuleInfo {
  id: string;
  name: string;
  trigger: 'AtRisk' | 'Breached' | 'NoResponseFor';
  noResponseHours: number | null;
  action: 'NotifyAssignee' | 'NotifyManager' | 'BumpPriority' | 'ReassignToTeam';
  targetTeamId: string | null;
  requestTypeId: string | null;
  isActive: boolean;
  timesFired: number;
}
