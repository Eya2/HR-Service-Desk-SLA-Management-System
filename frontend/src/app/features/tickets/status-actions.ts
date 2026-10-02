import { TranslateService } from '@ngx-translate/core';
import { enumLabel } from '../../shared/ui/enum-label';

/** How a transition is presented: button label (translation key), and whether a message to the employee is asked for. */
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
  Open: { label: 'action.Open', icon: 'inbox', reason: 'none', primary: true },
  InProgress: { label: 'action.InProgress', icon: 'play_arrow', reason: 'none', primary: true },
  WaitingOnEmployee: {
    label: 'action.WaitingOnEmployee',
    icon: 'contact_support',
    reason: 'required',
    reasonLabel: 'action.WaitingOnEmployeeReason',
  },
  Resolved: { label: 'action.Resolved', icon: 'task_alt', reason: 'optional', reasonLabel: 'action.ResolvedReason', primary: true },
  Closed: { label: 'action.Closed', icon: 'done_all', reason: 'none' },
  Reopened: { label: 'action.Reopened', icon: 'replay', reason: 'optional', reasonLabel: 'action.ReopenedReason' },
  Rejected: { label: 'action.Rejected', icon: 'block', reason: 'required', reasonLabel: 'action.reasonToEmployee' },
  Cancelled: { label: 'action.Cancelled', icon: 'cancel', reason: 'optional', reasonLabel: 'action.optionalReason' },
};

export function statusAction(from: string, to: string): StatusAction {
  const action = ACTIONS[to] ?? { label: `status.${to}`, icon: 'arrow_forward', reason: 'none' };
  // Same target, different meaning depending on where the case is.
  if (from === 'WaitingOnEmployee' && to === 'InProgress') return { ...action, status: to, label: 'action.resume' };
  if (from === 'InProgress' && to === 'Open') return { ...action, status: to, label: 'action.backToQueue', icon: 'undo', primary: false };
  return { ...action, status: to };
}

/** One sentence per audit event, for the case timeline. */
export function describeEvent(translate: TranslateService, type: string, data: Record<string, unknown> | null): string {
  const d = data ?? {};
  const t = (key: string, params?: Record<string, unknown>) => translate.instant(`event.${key}`, params) as string;
  switch (type) {
    case 'StatusChanged':
      return t(type, { from: enumLabel(translate, 'status', `${d['from']}`), to: enumLabel(translate, 'status', `${d['to']}`) });
    case 'PriorityChanged':
      return t(type, { from: enumLabel(translate, 'priority', `${d['from']}`), to: enumLabel(translate, 'priority', `${d['to']}`) });
    case 'CommentAdded':
      return t(d['isInternal'] ? 'CommentInternal' : 'CommentAdded');
    case 'AttachmentAdded':
      return t(type, { name: d['fileName'] });
    case 'Escalated':
      return t(type, { rule: d['rule'] });
    case 'Assigned':
      return t(d['to'] ? 'Assigned' : 'Unassigned');
    case 'SlaStateChanged':
      return t(type, { state: enumLabel(translate, 'sla', `${d['to']}`) });
    case 'ApprovalRequested':
      return t(type, { step: d['step'] });
    case 'ApprovalDecided':
      return t(type, { decision: translate.instant(`decision.${`${d['decision']}`.toLowerCase()}Verb`), step: d['step'] });
    case 'Rated':
      return t(type, { score: d['score'] });
    default:
      return enumLabel(translate, 'event', type);
  }
}
