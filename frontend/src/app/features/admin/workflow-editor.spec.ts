import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { WorkflowInfo } from '../../core/api/api.models';
import { WorkflowEditor } from './workflow-editor';

describe('WorkflowEditor', () => {
  let fixture: ComponentFixture<WorkflowEditor>;
  let http: HttpTestingController;

  const salaryAdvance: WorkflowInfo = {
    requestTypeId: 'rt-1',
    requestTypeName: 'Salary advance',
    requestTypeIsConfidential: false,
    responsibleTeamId: null,
    isConfigured: true,
    isActive: true,
    version: 1,
    steps: [
      { order: 1, name: 'Manager approval', approverRole: 'Manager' },
      { order: 2, name: 'Payroll validation', approverRole: 'PayrollSpecialist' },
    ],
  };

  async function render(workflow: WorkflowInfo): Promise<HTMLElement> {
    TestBed.configureTestingModule({
      imports: [WorkflowEditor],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(WorkflowEditor);
    fixture.componentRef.setInput('requestTypeId', workflow.requestTypeId);
    fixture.detectChanges();
    http.expectOne(`/api/workflows/${workflow.requestTypeId}`).flush(workflow);
    http.expectOne('/api/teams').flush([]);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  const editor = () => fixture.componentInstance as unknown as {
    steps: { getRawValue(): { name: string; approverRole: string }[] };
    move(i: number, offset: number): void;
    roles(): readonly string[];
  };

  it('loads the existing chain', async () => {
    const el = await render(salaryAdvance);

    expect(el.querySelectorAll('[data-testid="step"]').length).toBe(2);
    expect(editor().steps.getRawValue()[1]).toEqual({ name: 'Payroll validation', approverRole: 'PayrollSpecialist' });
  });

  it('reorders and adds steps, then saves the chain in order', async () => {
    const el = await render(salaryAdvance);

    editor().move(1, -1);
    (el.querySelector('[data-testid="add-step"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(el.querySelectorAll('[data-testid="step"]').length).toBe(3);

    const last = el.querySelectorAll('[data-testid="step"] input')[2] as HTMLInputElement;
    last.value = 'Budget';
    last.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    (el.querySelector('[data-testid="save-workflow"]') as HTMLButtonElement).click();

    const req = http.expectOne('/api/workflows/rt-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      isActive: true,
      steps: [
        { name: 'Payroll validation', approverRole: 'PayrollSpecialist' },
        { name: 'Manager approval', approverRole: 'Manager' },
        { name: 'Budget', approverRole: 'Manager' },
      ],
    });
    req.flush(salaryAdvance);
  });

  it('offers only HR Admins for confidential request types', async () => {
    await render({ ...salaryAdvance, requestTypeName: 'Harassment report', requestTypeIsConfidential: true, isConfigured: false, steps: [] });

    expect(editor().roles()).toEqual(['HrAdmin']);
    expect(editor().steps.getRawValue()).toEqual([{ name: 'Manager approval', approverRole: 'HrAdmin' }]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('only be approved by HR Admins');
  });
});
