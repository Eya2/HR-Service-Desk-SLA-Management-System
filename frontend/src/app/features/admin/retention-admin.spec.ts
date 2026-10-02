import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RetentionAdmin } from './retention-admin';

describe('RetentionAdmin', () => {
  it('shows the current retention, saves a new one and runs the job', async () => {
    TestBed.configureTestingModule({ imports: [RetentionAdmin], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RetentionAdmin);
    fixture.detectChanges();
    http.expectOne('/api/settings/retention').flush({ retentionMonths: 36 });
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const input = el.querySelector('[data-testid="retention-months"]') as HTMLInputElement;
    expect(input.value).toBe('36');

    input.value = '12';
    input.dispatchEvent(new Event('input'));
    (el.querySelector('[data-testid="save-retention"]') as HTMLButtonElement).click();
    const save = http.expectOne('/api/settings/retention');
    expect(save.request.body).toEqual({ retentionMonths: 12 });
    save.flush({ retentionMonths: 12 });

    (el.querySelector('[data-testid="run-retention"]') as HTMLButtonElement).click();
    http.expectOne('/api/settings/retention/run').flush({ anonymized: 3 });
    await fixture.whenStable();
    expect(el.querySelector('[data-testid="run-result"]')?.textContent).toContain('3 case(s) anonymized');
  });
});
