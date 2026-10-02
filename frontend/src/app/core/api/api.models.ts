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
  createdAt: string;
  updatedAt: string | null;
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
  answers: FormAnswer[];
  attachments: AttachmentInfo[];
  comments: CommentInfo[];
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
