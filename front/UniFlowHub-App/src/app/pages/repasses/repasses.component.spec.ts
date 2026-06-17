import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { RepassesService } from '../../core/repasses.service';
import { UnidadesService } from '../../core/unidades.service';
import { RepassesComponent } from './repasses.component';

describe('RepassesComponent', () => {
  let component: RepassesComponent;
  let fixture: ComponentFixture<RepassesComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RepassesComponent],
      providers: [
        {
          provide: RepassesService,
          useValue: {
            dashboard: () => of({ veiculos: [], topDiasEstoque: [] }),
          },
        },
        {
          provide: UnidadesService,
          useValue: {
            listEmpresas: () => of([]),
            list: () => of([]),
          },
        },
        {
          provide: ToastrService,
          useValue: {
            error: () => undefined,
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(RepassesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
