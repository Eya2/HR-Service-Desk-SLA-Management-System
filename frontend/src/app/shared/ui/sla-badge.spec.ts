import { TestBed } from '@angular/core/testing';
import { SlaStateName } from '../../core/api/api.models';
import { SlaBadge } from './sla-badge';

describe('SlaBadge', () => {
  async function render(state: SlaStateName, dueAt: string | null, paused = false): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(SlaBadge);
    fixture.componentRef.setInput('state', state);
    fixture.componentRef.setInput('dueAt', dueAt);
    fixture.componentRef.setInput('paused', paused);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the state and the deadline', async () => {
    const el = await render('AtRisk', '2026-03-23T09:30:00Z');

    expect(el.querySelector('.badge')?.getAttribute('data-state')).toBe('AtRisk');
    expect(el.textContent).toContain('At risk');
    expect(el.querySelector('.due')).not.toBeNull();
  });

  it('shows "Paused" without a deadline while the clock is stopped', async () => {
    const el = await render('OnTrack', null, true);

    expect(el.textContent).toContain('Paused');
    expect(el.querySelector('.due')).toBeNull();
  });
});
