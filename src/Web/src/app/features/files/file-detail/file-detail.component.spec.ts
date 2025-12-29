import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Location } from '@angular/common';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { FileDetailComponent } from './file-detail.component';
import { IndexedFileService } from '../../../services/indexed-file.service';
import { NotificationService } from '../../../services/notification.service';
import { IndexedFileDto } from '../../../models';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

describe('FileDetailComponent', () => {
  let component: FileDetailComponent;
  let fixture: ComponentFixture<FileDetailComponent>;
  let mockFileService: {
    getById: ReturnType<typeof vi.fn>;
    getThumbnailUrl: ReturnType<typeof vi.fn>;
    getFileUrl: ReturnType<typeof vi.fn>;
  };
  let mockNotificationService: {
    success: ReturnType<typeof vi.fn>;
    error: ReturnType<typeof vi.fn>;
  };
  let mockLocation: { back: ReturnType<typeof vi.fn> };

  const mockFile: IndexedFileDto = {
    id: '123e4567-e89b-12d3-a456-426614174000',
    filePath: '/photos/vacation/beach.jpg',
    fileName: 'beach.jpg',
    fileHash: 'abc123def456',
    fileSize: 2048000,
    width: 1920,
    height: 1080,
    createdAt: '2025-06-15T10:30:00Z',
    modifiedAt: '2025-06-15T10:30:00Z',
    indexedAt: '2025-12-20T14:00:00Z',
    thumbnailPath: '/thumbnails/abc123def456.jpg',
    isDuplicate: false,
    duplicateGroupId: null,
    dateTaken: '2025-06-15T10:25:00Z',
    cameraMake: 'Canon',
    cameraModel: 'EOS R5',
    gpsLatitude: 25.7617,
    gpsLongitude: -80.1918,
    iso: 200,
    aperture: 'f/2.8',
    shutterSpeed: '1/250',
    lastError: null,
    retryCount: 0
  };

  const createComponent = async (fileId: string | null = '123e4567-e89b-12d3-a456-426614174000') => {
    mockFileService = {
      getById: vi.fn(),
      getThumbnailUrl: vi.fn(),
      getFileUrl: vi.fn()
    };
    mockNotificationService = {
      success: vi.fn(),
      error: vi.fn()
    };
    mockLocation = {
      back: vi.fn()
    };

    mockFileService.getThumbnailUrl.mockReturnValue(
      `http://localhost:5000/api/files/${fileId}/thumbnail`
    );
    mockFileService.getFileUrl.mockReturnValue(
      `http://localhost:5000/api/files/${fileId}/download`
    );

    await TestBed.configureTestingModule({
      imports: [FileDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap(fileId ? { id: fileId } : {})
            }
          }
        },
        { provide: IndexedFileService, useValue: mockFileService },
        { provide: NotificationService, useValue: mockNotificationService },
        { provide: Location, useValue: mockLocation }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FileDetailComponent);
    component = fixture.componentInstance;
  };

  // Helper to wait for observable to complete
  const waitForAsync = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

  describe('Component creation', () => {
    beforeEach(async () => {
      await createComponent();
      mockFileService.getById.mockReturnValue(of(mockFile));
    });

    it('should create', () => {
      fixture.detectChanges();
      expect(component).toBeTruthy();
    });
  });

  describe('File loading', () => {
    beforeEach(async () => {
      await createComponent();
    });

    it('should load file on init when ID is provided', async () => {
      mockFileService.getById.mockReturnValue(of(mockFile));

      fixture.detectChanges();
      await waitForAsync();

      expect(mockFileService.getById).toHaveBeenCalledWith('123e4567-e89b-12d3-a456-426614174000');
      expect(component.file()).toEqual(mockFile);
      expect(component.loading()).toBe(false);
      expect(component.error()).toBeNull();
    });

    it('should set error when file loading fails', async () => {
      mockFileService.getById.mockReturnValue(throwError(() => new Error('Not found')));

      fixture.detectChanges();
      await waitForAsync();

      expect(component.file()).toBeNull();
      expect(component.loading()).toBe(false);
      expect(component.error()).toBe('Failed to load file details');
    });
  });

  describe('Missing file ID', () => {
    beforeEach(async () => {
      await createComponent(null);
    });

    it('should set error when file ID is not provided', () => {
      fixture.detectChanges();

      expect(mockFileService.getById).not.toHaveBeenCalled();
      expect(component.error()).toBe('File ID not provided');
      expect(component.loading()).toBe(false);
    });
  });

  describe('Thumbnail URL', () => {
    beforeEach(async () => {
      await createComponent();
      mockFileService.getById.mockReturnValue(of(mockFile));
    });

    it('should return thumbnail URL when file is loaded', async () => {
      fixture.detectChanges();
      await waitForAsync();

      const url = component.getThumbnailUrl();
      expect(url).toContain('/thumbnail');
      expect(mockFileService.getThumbnailUrl).toHaveBeenCalledWith(mockFile.id, mockFile.thumbnailPath, mockFile.fileHash);
    });

    it('should return empty string when file is not loaded', () => {
      expect(component.getThumbnailUrl()).toBe('');
    });
  });

  describe('Copy path functionality', () => {
    beforeEach(async () => {
      await createComponent();
      mockFileService.getById.mockReturnValue(of(mockFile));
    });

    it('should copy path and show success notification', async () => {
      const clipboardWriteText = vi.fn().mockResolvedValue(undefined);
      Object.assign(navigator, {
        clipboard: { writeText: clipboardWriteText }
      });

      fixture.detectChanges();
      await waitForAsync();

      await component.copyPath();

      expect(clipboardWriteText).toHaveBeenCalledWith('/photos/vacation/beach.jpg');
      expect(mockNotificationService.success).toHaveBeenCalledWith('File path copied to clipboard');
    });

    it('should show error notification when copy fails', async () => {
      const clipboardWriteText = vi.fn().mockRejectedValue(new Error('Permission denied'));
      Object.assign(navigator, {
        clipboard: { writeText: clipboardWriteText }
      });

      fixture.detectChanges();
      await waitForAsync();

      await component.copyPath();

      expect(mockNotificationService.error).toHaveBeenCalledWith('Failed to copy path to clipboard');
    });

    it('should not copy when file is not loaded', async () => {
      const clipboardWriteText = vi.fn();
      Object.assign(navigator, {
        clipboard: { writeText: clipboardWriteText }
      });

      await component.copyPath();

      expect(clipboardWriteText).not.toHaveBeenCalled();
    });
  });

  describe('Navigation', () => {
    beforeEach(async () => {
      await createComponent();
      mockFileService.getById.mockReturnValue(of(mockFile));
    });

    it('should navigate back when goBack is called', () => {
      fixture.detectChanges();
      component.goBack();

      expect(mockLocation.back).toHaveBeenCalled();
    });
  });

  describe('Date formatting', () => {
    beforeEach(async () => {
      await createComponent();
      mockFileService.getById.mockReturnValue(of(mockFile));
    });

    it('should format valid date string', () => {
      fixture.detectChanges();

      const formatted = component.formatDate('2025-06-15T10:30:00Z');
      expect(formatted).not.toBe('-');
      expect(formatted.length).toBeGreaterThan(0);
    });

    it('should return dash for null date', () => {
      fixture.detectChanges();

      const formatted = component.formatDate(null);
      expect(formatted).toBe('-');
    });
  });

  describe('Dimensions formatting', () => {
    beforeEach(async () => {
      await createComponent();
    });

    it('should format dimensions when both width and height exist', async () => {
      mockFileService.getById.mockReturnValue(of(mockFile));

      fixture.detectChanges();
      await waitForAsync();

      const formatted = component.formatDimensions();
      expect(formatted).toBe('1920 x 1080');
    });

    it('should return dash when dimensions are missing', async () => {
      const fileWithoutDimensions: IndexedFileDto = {
        ...mockFile,
        width: null as unknown as number,
        height: null as unknown as number
      };
      mockFileService.getById.mockReturnValue(of(fileWithoutDimensions));

      fixture.detectChanges();
      await waitForAsync();

      const formatted = component.formatDimensions();
      expect(formatted).toBe('-');
    });
  });

  describe('GPS data', () => {
    beforeEach(async () => {
      await createComponent();
    });

    it('should detect GPS data when coordinates exist', async () => {
      mockFileService.getById.mockReturnValue(of(mockFile));

      fixture.detectChanges();
      await waitForAsync();

      expect(component.hasGpsData()).toBe(true);
    });

    it('should return false when GPS data is missing', async () => {
      const fileWithoutGps: IndexedFileDto = {
        ...mockFile,
        gpsLatitude: null,
        gpsLongitude: null
      };
      mockFileService.getById.mockReturnValue(of(fileWithoutGps));

      fixture.detectChanges();
      await waitForAsync();

      expect(component.hasGpsData()).toBe(false);
    });

    it('should format GPS coordinates correctly', async () => {
      mockFileService.getById.mockReturnValue(of(mockFile));

      fixture.detectChanges();
      await waitForAsync();

      const coords = component.getGpsCoordinates();
      expect(coords).toBe('25.761700, -80.191800');
    });

    it('should return dash for GPS coordinates when missing', async () => {
      const fileWithoutGps: IndexedFileDto = {
        ...mockFile,
        gpsLatitude: null,
        gpsLongitude: null
      };
      mockFileService.getById.mockReturnValue(of(fileWithoutGps));

      fixture.detectChanges();
      await waitForAsync();

      expect(component.getGpsCoordinates()).toBe('-');
    });

    it('should generate correct Google Maps URL', async () => {
      mockFileService.getById.mockReturnValue(of(mockFile));

      fixture.detectChanges();
      await waitForAsync();

      const url = component.getGoogleMapsUrl();
      expect(url).toContain('google.com/maps');
      expect(url).toContain('25.7617');
      expect(url).toContain('-80.1918');
    });

    it('should return empty string for Maps URL when no GPS', async () => {
      const fileWithoutGps: IndexedFileDto = {
        ...mockFile,
        gpsLatitude: null,
        gpsLongitude: null
      };
      mockFileService.getById.mockReturnValue(of(fileWithoutGps));

      fixture.detectChanges();
      await waitForAsync();

      expect(component.getGoogleMapsUrl()).toBe('');
    });
  });

  describe('EXIF data detection', () => {
    beforeEach(async () => {
      await createComponent();
    });

    it('should detect EXIF data when any field exists', async () => {
      mockFileService.getById.mockReturnValue(of(mockFile));

      fixture.detectChanges();
      await waitForAsync();

      expect(component.hasExifData()).toBe(true);
    });

    it('should return false when no EXIF data exists', async () => {
      const fileWithoutExif: IndexedFileDto = {
        ...mockFile,
        dateTaken: null,
        cameraMake: null,
        cameraModel: null,
        gpsLatitude: null,
        gpsLongitude: null,
        iso: null,
        aperture: null,
        shutterSpeed: null
      };
      mockFileService.getById.mockReturnValue(of(fileWithoutExif));

      fixture.detectChanges();
      await waitForAsync();

      expect(component.hasExifData()).toBe(false);
    });

    it('should detect EXIF data with only camera make', async () => {
      const fileWithCameraMakeOnly: IndexedFileDto = {
        ...mockFile,
        dateTaken: null,
        cameraModel: null,
        gpsLatitude: null,
        gpsLongitude: null,
        iso: null,
        aperture: null,
        shutterSpeed: null
      };
      mockFileService.getById.mockReturnValue(of(fileWithCameraMakeOnly));

      fixture.detectChanges();
      await waitForAsync();

      expect(component.hasExifData()).toBe(true);
    });
  });

  describe('View file', () => {
    it('should open file in new window', async () => {
      await createComponent();
      mockFileService.getById.mockReturnValue(of(mockFile));
      const windowOpenSpy = vi.spyOn(window, 'open').mockImplementation(() => null);

      fixture.detectChanges();
      await waitForAsync();

      component.viewFile();

      expect(mockFileService.getFileUrl).toHaveBeenCalledWith(mockFile.id);
      expect(windowOpenSpy).toHaveBeenCalled();

      windowOpenSpy.mockRestore();
    });

    it('should not open window when file is not loaded', async () => {
      await createComponent();
      // Don't set up getById mock - file won't be loaded
      const windowOpenSpy = vi.spyOn(window, 'open').mockImplementation(() => null);

      // Don't call fixture.detectChanges() - don't trigger ngOnInit

      component.viewFile();

      expect(windowOpenSpy).not.toHaveBeenCalled();

      windowOpenSpy.mockRestore();
    });
  });
});
