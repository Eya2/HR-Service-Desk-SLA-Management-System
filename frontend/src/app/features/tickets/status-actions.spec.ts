import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { text } from '../../testing/i18n';
import { describeEvent, statusAction } from './status-actions';

const label = (from: string, to: string) => text(statusAction(from, to).label);
const describe_ = (type: string, data: Record<string, unknown> | null) => describeEvent(TestBed.inject(TranslateService), type, data);

describe('status actions', () => {
  it('labels transitions for the people performing them', () => {
    expect(label('New', 'Open')).toBe('Accept');
    expect(label('Open', 'InProgress')).toBe('Start work');
    expect(label('WaitingOnEmployee', 'InProgress')).toBe('Resume');
    expect(label('InProgress', 'Open')).toBe('Back to queue');
  });

  it('asks for a reason where the employee needs an explanation', () => {
    expect(statusAction('Open', 'Rejected').reason).toBe('required');
    expect(statusAction('Open', 'WaitingOnEmployee').reason).toBe('required');
    expect(statusAction('InProgress', 'Resolved').reason).toBe('optional');
    expect(statusAction('Resolved', 'Closed').reason).toBe('none');
  });

  it('describes audit events in plain words', () => {
    expect(describe_('StatusChanged', { from: 'Open', to: 'WaitingOnEmployee' })).toBe(
      'changed the status from Open to Waiting on employee',
    );
    expect(describe_('CommentAdded', { isInternal: true })).toBe('added an internal note');
    expect(describe_('AttachmentAdded', { fileName: 'timesheet.pdf' })).toBe('added the document timesheet.pdf');
    expect(describe_('Created', null)).toBe('submitted the request');
    expect(describe_('Rated', { score: 4 })).toBe('rated the service 4/5');
  });
});
