import { humanize } from '../../shared/ui/labels';

/** How a transition is presented: button label, and whether a message to the employee is asked for. */
export interface StatusAction {
  status: string;
  label: string;
  icon: string;
  /** 'required': a reason must be given; 'optional': offered; 'none': applied directly. */
  reason: 'required' | 'optional' | 'none';
  reasonLabel?: string;
  primary?: boolean;
}

const ACTIONS: Record<string, Omit<StatusAction, 'status'>> = {
  Open: { label: 'Accept', icon: 'inbox', reason: 'none', primary: true },
  InProgress: { label: 'Start work', icon: 'play_arrow', reason: 'none', primary: true },
  WaitingOnEmployee: {
    label: 'Ask the employee',
    icon: 'contact_support',
    reason: 'required',
    reasonLabel: 'What do you need from the employee?',
  },
  Resolved: { label: 'Resolve', icon: 'task_alt', reason: 'optional', reasonLabel: 'Resolution (sent to the employee)', primary: true },
  Closed: { label: 'Close', icon: 'done_all', reason: 'none' },
  Reopened: { label: 'Reopen', icon: 'replay', reason: 'optional', reasonLabel: 'Why is the case not solved?' },
  Rejected: { label: 'Reject', icon: 'block', reason: 'required', reasonLabel: 'Reason (sent to the employee)' },
  Cancelled: { label: 'Cancel case', icon: 'cancel', reason: 'optional', reasonLabel: 'Reason (optional)' },
};

export function statusAction(from: string, to: string): StatusAction {
  const action = ACTIONS[to] ?? { label: humanize(to), icon: 'arrow_forward', reason: 'none' };
  // Same target, different meaning depending on where the case is.
  if (from === 'WaitingOnEmployee' && to === 'InProgress') return { ...action, status: to, label: 'Resume' };
  if (from === 'InProgress' && to === 'Open') return { ...action, status: to, label: 'Back to queue', icon: 'undo', primary: false };
  return { ...action, status: to };
}

/** One sentence per audit event, for the case timeline. */
export function describeEvent(type: string, data: Record<string, unknown> | null): string {
  const d = data ?? {};
  switch (type) {
    case 'Created':
      return 'submitted the request';
    case 'StatusChanged':
      return `changed the status from ${humanize(`${d['from']}`)} to ${humanize(`${d['to']}`)}`;
    case 'PriorityChanged':
      return `changed the priority from ${d['from']} to ${d['to']}`;
    case 'DetailsUpdated':
      return 'updated the request details';
    case 'CommentAdded':
      return d['isInternal'] ? 'added an internal note' : 'replied';
    case 'AttachmentAdded':
      return `added the document ${d['fileName']}`;
    case 'SlaStateChanged':
      return `SLA ${humanize(`${d['to']}`).toLowerCase()}`;
    case 'ApprovalRequested':
      return `requested approval: ${d['step']}`;
    case 'ApprovalDecided':
      return `${`${d['decision']}`.toLowerCase()} the step ${d['step']}`;
    default:
      return humanize(type).toLowerCase();
  }
}
