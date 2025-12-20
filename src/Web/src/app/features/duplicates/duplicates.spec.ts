import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Duplicates } from './duplicates';
import { DuplicatesService } from '../../shared/services/duplicates.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

describe('Duplicates', () => {
  let component: Duplicates;
  let fixture: ComponentFixture<Duplicates>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Duplicates],
      providers: [DuplicatesService, provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Duplicates);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
