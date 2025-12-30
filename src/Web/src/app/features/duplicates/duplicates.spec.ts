import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Subject } from 'rxjs';
import { signal } from '@angular/core';
import { Duplicates } from './duplicates';
import { DuplicateService } from '../../services/duplicate.service';
import { CleanerSignalRService, DeleteFileResult } from '../../services/cleaner-signalr.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

// Mock CleanerSignalRService
const mockCleanerSignalRService = {
  connected: signal(false),
  deleteComplete$: new Subject<DeleteFileResult>(),
  jobComplete$: new Subject<{ jobId: string; succeeded: number; failed: number; skipped: number }>(),
  cleanerConnected$: new Subject<{ cleanerId: string; hostname: string }>(),
  cleanerDisconnected$: new Subject<string>(),
  cleanerStatus$: new Subject<any>(),
  deleteProgress$: new Subject<{ jobId: string; fileId: string; status: string }>(),
};

describe('Duplicates', () => {
  let component: Duplicates;
  let fixture: ComponentFixture<Duplicates>;

  const createComponent = async (queryParams: { [key: string]: string } = {}) => {
    await TestBed.configureTestingModule({
      imports: [Duplicates],
      providers: [
        DuplicateService,
        { provide: CleanerSignalRService, useValue: mockCleanerSignalRService },
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: convertToParamMap(queryParams),
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Duplicates);
    component = fixture.componentInstance;
    fixture.detectChanges();
  };

  it('should create', async () => {
    await createComponent();
    expect(component).toBeTruthy();
  });

  it('should start in list view by default', async () => {
    await createComponent();
    expect(component.viewMode()).toBe('list');
    expect(component.selectedGroupId()).toBeNull();
  });

  it('should navigate to detail view when groupId query param is present', async () => {
    const testGroupId = 'test-group-123';
    await createComponent({ groupId: testGroupId });

    expect(component.viewMode()).toBe('detail');
    expect(component.selectedGroupId()).toBe(testGroupId);
  });
});
