import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../core/auth.service';
import { Role } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly theme = inject(ThemeService);

  readonly loading = signal(false);
  readonly creating = signal(false);
  readonly setupMode = signal(false);
  readonly canCreateFirstAdmin = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    senha: ['', [Validators.required]],
  });

  readonly setupForm = this.fb.nonNullable.group({
    nome: ['', Validators.required],
    cpf: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    senha: ['', [Validators.required, Validators.minLength(6)]],
    departamento: ['RH', Validators.required],
    cargo: ['Administrador', Validators.required],
    dataNascimento: ['1990-01-01', Validators.required],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    if (this.auth.isLoggedIn()) {
      this.router.navigateByUrl(this.auth.landingRoute());
      return;
    }

    this.auth.setupStatus().subscribe({
      next: (status) => {
        this.canCreateFirstAdmin.set(status.canCreateFirstAdmin);
        if (!status.canCreateFirstAdmin) {
          this.setupMode.set(false);
        }
      },
      error: () => this.toastr.error('Não foi possível verificar o primeiro acesso.', 'Erro'),
    });
  }

  submit(): void {
    if (this.form.invalid || this.loading()) {
      this.form.markAllAsTouched();
      if (this.form.invalid) {
        this.toastr.warning('Informe email e senha para entrar.', 'Atenção');
      }
      return;
    }

    this.loading.set(true);

    const { email, senha } = this.form.getRawValue();
    this.auth.login(email, senha).subscribe({
      next: () => this.router.navigateByUrl(this.auth.landingRoute()),
      error: (error) => {
        this.toastr.error(this.getErrorMessage(error), 'Acesso negado');
        this.loading.set(false);
      },
    });
  }

  createFirstAdmin(): void {
    if (this.setupForm.invalid || this.creating()) {
      this.setupForm.markAllAsTouched();
      if (this.setupForm.invalid) {
        this.toastr.warning('Preencha todos os dados obrigatórios do administrador.', 'Atenção');
      }
      return;
    }

    this.creating.set(true);

    const payload = {
      ...this.setupForm.getRawValue(),
      role: 'Admin' as Role,
      perfis: ['Admin' as Role],
      ativo: true,
      unidadeId: null,
    };

    this.auth.createUser(payload).subscribe({
      next: () => {
        this.canCreateFirstAdmin.set(false);
        this.auth.login(payload.email, payload.senha).subscribe({
          next: () => this.router.navigateByUrl(this.auth.landingRoute()),
          error: () => {
            this.toastr.warning('Administrador criado, mas não foi possível entrar automaticamente.', 'Primeiro acesso');
            this.creating.set(false);
          },
        });
      },
      error: () => {
        this.toastr.error('Primeiro acesso indisponível. Entre com um usuário existente.', 'Erro');
        this.creating.set(false);
      },
    });
  }

  private getErrorMessage(error?: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error;
      }

      if (error.error?.title) {
        return error.error.title;
      }
    }

    return 'Email ou senha invalidos.';
  }
}
