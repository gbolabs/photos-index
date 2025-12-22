import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Duplicates } from './duplicates';
import { DuplicateService } from '../../services/duplicate.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

describe('Duplicates', () => {
  let component: Duplicates;
  let fixture: ComponentFixture<Duplicates>;

  const createComponent = async (queryParams: { [key: string]: string } = {}) => {
    await TestBed.configureTestingModule({
      imports: [Duplicates],
      providers: [
        DuplicateService,
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
