import { describeEvent, statusAction } from './status-actions';

describe('status actions', () => {
  it('labels transitions for the people performing them', () => {
    expect(statusAction('New', 'Open').label).toBe('Accept');
    expect(statusAction('Open', 'InProgress').label).toBe('Start work');
    expect(statusAction('WaitingOnEmployee', 'InProgress').label).toBe('Resume');
    expect(statusAction('InProgress', 'Open').label).toBe('Back to queue');
  });

  it('asks for a reason where the employee needs an explanation', () => {
    expect(statusAction('Open', 'Rejected').reason).toBe('required');
    expect(statusAction('Open', 'WaitingOnEmployee').reason).toBe('required');
    expect(statusAction('InProgress', 'Resolved').reason).toBe('optional');
    expect(statusAction('Resolved', 'Closed').reason).toBe('none');
  });

  it('describes audit events in plain words', () => {
    expect(describeEvent('StatusChanged', { from: 'Open', to: 'WaitingOnEmployee' })).toBe(
      'changed the status from Open to Waiting on employee',
    );
    expect(describeEvent('CommentAdded', { isInternal: true })).toBe('added an internal note');
    expect(describeEvent('AttachmentAdded', { fileName: 'timesheet.pdf' })).toBe('added the document timesheet.pdf');
    expect(describeEvent('Created', null)).toBe('submitted the request');
  });
});
