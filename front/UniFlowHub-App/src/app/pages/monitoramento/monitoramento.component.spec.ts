import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { MonitoramentoComponent } from './monitoramento.component';
import { AuthService } from '../../core/auth.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { ToastrService } from 'ngx-toastr';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';

describe('MonitoramentoComponent', () => {
  let component: MonitoramentoComponent;
  let fixture: ComponentFixture<MonitoramentoComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MonitoramentoComponent],
      providers: [
        {
          provide: AuthService,
          useValue: {
            user: signal(null),
            logout: jasmine.createSpy('logout'),
          },
        },
        {
          provide: ProfileFlowService,
          useValue: {
            editProfile: jasmine.createSpy('editProfile'),
            changePassword: jasmine.createSpy('changePassword'),
          },
        },
        {
          provide: ThemeService,
          useValue: {
            current: signal('light'),
            options: [{ value: 'light', label: 'Claro' }],
            setTheme: jasmine.createSpy('setTheme'),
          },
        },
        {
          provide: ToastrService,
          useValue: {
            warning: jasmine.createSpy('warning'),
          },
        },
        {
          provide: Router,
          useValue: {
            navigateByUrl: jasmine.createSpy('navigateByUrl'),
          },
        },
        {
          provide: HttpClient,
          useValue: {
            post: jasmine.createSpy('post'),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(MonitoramentoComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
