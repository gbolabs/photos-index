import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { vi } from 'vitest';
import { FilterBarComponent } from './filter-bar';
import { GalleryFilters, TileSize } from '../../services/gallery-state.service';

describe('FilterBarComponent', () => {
  let component: FilterBarComponent;
  let fixture: ComponentFixture<FilterBarComponent>;

  const defaultFilters: GalleryFilters = {
    directory: null,
    search: null,
    minDate: null,
    maxDate: null,
    duplicatesOnly: false
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FilterBarComponent],
      providers: [provideNoopAnimations()]
    }).compileComponents();

    fixture = TestBed.createComponent(FilterBarComponent);
    component = fixture.componentInstance;

    // Set required inputs
    fixture.componentRef.setInput('filters', defaultFilters);
    fixture.componentRef.setInput('tileSize', 'medium');

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display filters', () => {
    expect(component.filters()).toEqual(defaultFilters);
  });

  it('should display tile size', () => {
    expect(component.tileSize()).toBe('medium');
  });

  it('should emit filtersChange on search input', () => {
    const changeSpy = vi.fn();
    component.filtersChange.subscribe(changeSpy);

    const event = { target: { value: 'test search' } } as any;
    component.onSearchChange(event);

    expect(changeSpy).toHaveBeenCalledWith({ search: 'test search' });
  });

  it('should emit filtersChange on directory change', () => {
    const changeSpy = vi.fn();
    component.filtersChange.subscribe(changeSpy);

    component.onDirectoryChange('/photos');

    expect(changeSpy).toHaveBeenCalledWith({ directory: '/photos' });
  });

  it('should emit filtersChange with undefined for empty directory', () => {
    const changeSpy = vi.fn();
    component.filtersChange.subscribe(changeSpy);

    component.onDirectoryChange('');

    expect(changeSpy).toHaveBeenCalledWith({ directory: undefined });
  });

  it('should emit filtersChange on duplicates only toggle', () => {
    const changeSpy = vi.fn();
    component.filtersChange.subscribe(changeSpy);

    component.onDuplicatesOnlyChange(true);

    expect(changeSpy).toHaveBeenCalledWith({ duplicatesOnly: true });
  });

  it('should emit tileSizeChange on tile size change', () => {
    const changeSpy = vi.fn();
    component.tileSizeChange.subscribe(changeSpy);

    component.onTileSizeChange('large');

    expect(changeSpy).toHaveBeenCalledWith('large');
  });

  it('should emit refresh on refresh button click', () => {
    const refreshSpy = vi.fn();
    component.refresh.subscribe(refreshSpy);

    component.onRefresh();

    expect(refreshSpy).toHaveBeenCalled();
  });

  it('should have empty directories by default', () => {
    expect(component.directories()).toEqual([]);
  });

  it('should display provided directories', () => {
    fixture.componentRef.setInput('directories', ['/photos', '/documents']);
    fixture.detectChanges();

    expect(component.directories()).toEqual(['/photos', '/documents']);
  });

  it('should render search input', () => {
    const searchInput = fixture.nativeElement.querySelector('input[type="text"]');
    expect(searchInput).toBeTruthy();
  });

  it('should render tile size toggle group', () => {
    const toggleGroup = fixture.nativeElement.querySelector('mat-button-toggle-group');
    expect(toggleGroup).toBeTruthy();
  });

  it('should render refresh button', () => {
    const refreshButton = fixture.nativeElement.querySelector('button[aria-label="Refresh"]');
    expect(refreshButton).toBeTruthy();
  });
});
