import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Duplicates } from './duplicates';
import { DuplicateService } from '../../services/duplicate.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

describe('Duplicates', () => {
  let component: Duplicates;
  let fixture: ComponentFixture<Duplicates>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Duplicates],
      providers: [DuplicateService, provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(Duplicates);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
