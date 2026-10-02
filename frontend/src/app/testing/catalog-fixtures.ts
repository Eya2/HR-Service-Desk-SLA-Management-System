import { RequestType, RequestTypeSummary, TicketDetails } from '../core/api/api.models';

export const payslipType: RequestType = {
  id: 'type-payslip',
  name: 'Payslip correction',
  description: 'Report an error on a payslip.',
  category: 'Payroll',
  isConfidential: false,
  isActive: true,
  defaultPriority: 'High',
  fields: [
    { key: 'payPeriod', label: 'Pay period', type: 'Text', required: true, pattern: '[0-9]{4}-(0[1-9]|1[0-2])', helpText: 'YYYY-MM' },
    {
      key: 'issue',
      label: 'Issue',
      type: 'Select',
      required: true,
      options: [
        { value: 'missing_overtime', label: 'Missing overtime' },
        { value: 'other', label: 'Other' },
      ],
    },
    { key: 'expectedAmount', label: 'Expected amount', type: 'Number', min: 0, max: 100000 },
    { key: 'neededBy', label: 'Needed by', type: 'Date' },
    { key: 'notes', label: 'Notes', type: 'Textarea', maxLength: 20 },
    { key: 'payslip', label: 'Payslip', type: 'File', required: true, maxFiles: 1 },
  ],
};

export const catalog: RequestTypeSummary[] = [
  { ...payslipType },
  {
    id: 'type-cert',
    name: 'Work certificate',
    description: 'An official certificate of employment.',
    category: 'Certificates',
    isConfidential: false,
    isActive: true,
    defaultPriority: 'Low',
  },
  {
    id: 'type-harassment',
    name: 'Harassment report',
    description: 'Report harassment in confidence.',
    category: 'Confidential',
    isConfidential: true,
    isActive: true,
    defaultPriority: 'High',
  },
];

export function ticketDetails(overrides: Partial<TicketDetails> = {}): TicketDetails {
  return {
    id: 't-1',
    reference: 'HR-2026-000042',
    title: 'March payslip',
    description: 'Overtime missing',
    requestTypeId: 'type-payslip',
    requestTypeName: 'Payslip correction',
    category: 'Payroll',
    status: 'New',
    priority: 'High',
    isConfidential: false,
    requester: { id: 'u-1', fullName: 'Amira Ben Salah', email: 'amira.bensalah@acme.example' },
    assignee: null,
    team: null,
    answers: [{ key: 'payPeriod', label: 'Pay period', type: 'Text', value: '2026-03', displayValue: '2026-03', files: [] }],
    attachments: [],
    comments: [],
    timeline: [
      { id: 'e1', type: 'Created', actorName: 'Amira Ben Salah', occurredAt: '2026-03-02T09:00:00Z', data: { reference: 'HR-2026-000042' } },
    ],
    permissions: {
      canComment: true,
      canCommentInternally: false,
      canEdit: true,
      canChangePriority: false,
      canAttach: true,
      availableTransitions: ['Cancelled'],
      decidableApprovalId: null,
      canAssign: false,
      canClaim: false,
    },
    approvals: [],
    createdAt: '2026-03-02T09:00:00Z',
    updatedAt: null,
    ...overrides,
  };
}
