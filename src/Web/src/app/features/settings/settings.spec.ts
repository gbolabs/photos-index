import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { Settings } from './settings';
import { ApiService } from '../../core/api.service';
import { ScanDirectoryDto } from '../../core/models';

describe('Settings', () => {
  let component: Settings;
  let fixture: ComponentFixture<Settings>;
  let apiService: ApiService;

  const mockDirectories: ScanDirectoryDto[] = [
    {
      id: '1',
      path: '/test/path1',
      isEnabled: true,
      lastScannedAt: '2024-01-15T10:00:00Z',
      createdAt: '2024-01-01T00:00:00Z',
      fileCount: 50,
    },
    {
      id: '2',
      path: '/test/path2',
      isEnabled: false,
      lastScannedAt: null,
      createdAt: '2024-01-02T00:00:00Z',
      fileCount: 0,
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Settings],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(Settings);
    component = fixture.componentInstance;
    apiService = TestBed.inject(ApiService);
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load directories on init', () => {
    vi.spyOn(apiService, 'getDirectories').mockReturnValue(of(mockDirectories));

    component.ngOnInit();

    expect(apiService.getDirectories).toHaveBeenCalled();
    expect(component.directories()).toEqual(mockDirectories);
    expect(component.loading()).toBe(false);
  });

  it('should handle error when loading directories fails', () => {
    vi.spyOn(apiService, 'getDirectories').mockReturnValue(
      throwError(() => new Error('API Error'))
    );

    component.ngOnInit();

    expect(component.error()).toBeTruthy();
    expect(component.loading()).toBe(false);
  });

  it('should refresh directories', () => {
    vi.spyOn(apiService, 'getDirectories').mockReturnValue(of(mockDirectories));

    component.onRefresh();

    expect(apiService.getDirectories).toHaveBeenCalled();
  });

  it('should display directory list when loaded', () => {
    vi.spyOn(apiService, 'getDirectories').mockReturnValue(of(mockDirectories));
    component.ngOnInit();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const directoryList = compiled.querySelector('app-directory-list');
    expect(directoryList).toBeTruthy();
  });

  it('should show loading state initially', async () => {
    // Don't call ngOnInit yet, manually set the loading state
    component.loading.set(true);
    component.directories.set([]);
    fixture.detectChanges();
    await fixture.whenStable();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.settings__loading')).toBeTruthy();
  });
});
