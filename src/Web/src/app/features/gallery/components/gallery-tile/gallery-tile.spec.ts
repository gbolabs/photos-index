import { ComponentFixture, TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { GalleryTileComponent } from './gallery-tile';
import { IndexedFileDto } from '../../../../models';

describe('GalleryTileComponent', () => {
  let component: GalleryTileComponent;
  let fixture: ComponentFixture<GalleryTileComponent>;

  const mockFile: IndexedFileDto = {
    id: '123e4567-e89b-12d3-a456-426614174000',
    filePath: '/photos/img001.jpg',
    fileName: 'img001.jpg',
    fileHash: 'abc123',
    fileSize: 1024000,
    width: 1920,
    height: 1080,
    createdAt: '2025-12-01T00:00:00Z',
    modifiedAt: '2025-12-01T00:00:00Z',
    indexedAt: '2025-12-20T00:00:00Z',
    thumbnailPath: '/thumbnails/abc123.jpg',
    isDuplicate: false,
    duplicateGroupId: null,
    dateTaken: null,
    cameraMake: null,
    cameraModel: null,
    gpsLatitude: null,
    gpsLongitude: null,
    iso: null,
    aperture: null,
    shutterSpeed: null,
    lastError: null,
    retryCount: 0
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GalleryTileComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(GalleryTileComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('file', mockFile);
    fixture.componentRef.setInput('thumbnailUrl', '/thumbnails/abc123.jpg');

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display file data', () => {
    expect(component.file()).toEqual(mockFile);
    expect(component.thumbnailUrl()).toBe('/thumbnails/abc123.jpg');
  });

  it('should have default size of 180', () => {
    expect(component.size()).toBe(180);
  });

  it('should emit click event on tile click', () => {
    const clickSpy = vi.fn();
    component.click.subscribe(clickSpy);

    const event = new MouseEvent('click');
    component.onTileClick(event);

    expect(clickSpy).toHaveBeenCalledWith(mockFile);
  });

  it('should emit select event on ctrl+click', () => {
    const selectSpy = vi.fn();
    component.select.subscribe(selectSpy);

    const event = new MouseEvent('click', { ctrlKey: true });
    component.onTileClick(event);

    expect(selectSpy).toHaveBeenCalledWith(mockFile);
  });

  it('should emit select event on meta+click (Mac)', () => {
    const selectSpy = vi.fn();
    component.select.subscribe(selectSpy);

    const event = new MouseEvent('click', { metaKey: true });
    component.onTileClick(event);

    expect(selectSpy).toHaveBeenCalledWith(mockFile);
  });

  it('should emit click event on Enter keydown', () => {
    const clickSpy = vi.fn();
    component.click.subscribe(clickSpy);

    const event = new KeyboardEvent('keydown', { key: 'Enter' });
    component.onKeydown(event);

    expect(clickSpy).toHaveBeenCalledWith(mockFile);
  });

  it('should emit click event on Space keydown', () => {
    const clickSpy = vi.fn();
    component.click.subscribe(clickSpy);

    const event = new KeyboardEvent('keydown', { key: ' ' });
    component.onKeydown(event);

    expect(clickSpy).toHaveBeenCalledWith(mockFile);
  });

  it('should compute aspect ratio from file dimensions', () => {
    expect(component.aspectRatio).toBe('1920 / 1080');
  });

  it('should return 1/1 aspect ratio when dimensions not available', () => {
    const fileWithoutDimensions = { ...mockFile, width: null, height: null } as any;
    fixture.componentRef.setInput('file', fileWithoutDimensions);
    fixture.detectChanges();

    expect(component.aspectRatio).toBe('1 / 1');
  });

  it('should not be selected by default', () => {
    expect(component.selected()).toBe(false);
  });

  it('should reflect selected state', () => {
    fixture.componentRef.setInput('selected', true);
    fixture.detectChanges();

    expect(component.selected()).toBe(true);
  });

  it('should render thumbnail image', () => {
    const img = fixture.nativeElement.querySelector('img.thumbnail');
    expect(img).toBeTruthy();
    expect(img.src).toContain('abc123.jpg');
  });

  it('should have correct aria-label', () => {
    const tile = fixture.nativeElement.querySelector('.gallery-tile');
    expect(tile.getAttribute('aria-label')).toBe('img001.jpg');
  });
});
