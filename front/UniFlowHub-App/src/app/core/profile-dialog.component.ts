import { Component, computed, effect, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from './auth.service';
import { toDateInputValue } from './date-utils';
import { ProfileFlowService } from './profile-flow.service';

@Component({
  selector: 'app-profile-dialog',
  imports: [ReactiveFormsModule],
  template: `
    @if (flow.open()) {
      <div class="profile-modal-backdrop" role="presentation" (click)="$event.target === $event.currentTarget && close()">
        <section class="profile-modal" role="dialog" aria-modal="true" aria-labelledby="profile-modal-title" (click)="$event.stopPropagation()">
          <div class="profile-modal-header">
            <div>
              <p class="system-name">Minha conta</p>
              <h2 id="profile-modal-title">{{ isPassword() ? 'Trocar senha' : 'Editar perfil' }}</h2>
              <span>{{ user()?.email }}</span>
            </div>
            <button class="icon-button" type="button" aria-label="Fechar" (click)="close()" [disabled]="saving()">x</button>
          </div>

          @if (!isPassword()) {
            <form class="user-form" [formGroup]="profileForm" (ngSubmit)="submitProfile()">
              <label>
                Nome
                <input formControlName="nome" />
              </label>
              <label>
                CPF
                <input formControlName="cpf" autocomplete="off" />
              </label>
              <div class="grid two">
                <label>
                  Departamento
                  <input formControlName="departamento" />
                </label>
                <label>
                  Cargo
                  <input formControlName="cargo" />
                </label>
              </div>
              <label>
                Data de nascimento
                <input type="date" formControlName="dataNascimento" />
              </label>
              <div class="modal-actions">
                <button class="secondary" type="button" (click)="close()" [disabled]="saving()">Cancelar</button>
                <button class="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Salvando...' : 'Salvar perfil' }}</button>
              </div>
            </form>
          } @else {
            <form class="user-form" [formGroup]="passwordForm" (ngSubmit)="submitPassword()">
              <label>
                Senha atual
                <input type="password" formControlName="senhaAtual" />
              </label>
              <div class="grid two">
                <label>
                  Nova senha
                  <input type="password" formControlName="novaSenha" />
                </label>
                <label>
                  Confirmar senha
                  <input type="password" formControlName="confirmacao" />
                </label>
              </div>
              <div class="modal-actions">
                <button class="secondary" type="button" (click)="close()" [disabled]="saving()">Cancelar</button>
                <button class="primary" type="submit" [disabled]="saving()">{{ saving() ? 'Salvando...' : 'Trocar senha' }}</button>
              </div>
            </form>
          }
        </section>
      </div>
    }
  `,
})
export class ProfileDialogComponent {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly toastr = inject(ToastrService);
  readonly flow = inject(ProfileFlowService);

  readonly user = computed(() => this.auth.user());
  readonly isPassword = computed(() => this.flow.mode() === 'password');
  readonly saving = signal(false);

  readonly profileForm = this.fb.nonNullable.group({
    nome: ['', Validators.required],
    cpf: ['', Validators.required],
    departamento: ['', Validators.required],
    cargo: ['', Validators.required],
    dataNascimento: ['', Validators.required],
  });

  readonly passwordForm = this.fb.nonNullable.group({
    senhaAtual: ['', Validators.required],
    novaSenha: ['', [Validators.required, Validators.minLength(6)]],
    confirmacao: ['', Validators.required],
  });

  constructor() {
    effect(() => {
      if (!this.flow.open() || this.flow.mode() !== 'profile') {
        return;
      }

      const user = this.user();
      this.profileForm.reset({
        nome: user?.nome ?? '',
        cpf: user?.cpf ?? '',
        departamento: user?.departamento ?? '',
        cargo: user?.cargo ?? '',
        dataNascimento: toDateInputValue(user?.dataNascimento),
      });
    });

    effect(() => {
      if (this.flow.open() && this.flow.mode() === 'password') {
        this.passwordForm.reset({ senhaAtual: '', novaSenha: '', confirmacao: '' });
      }
    });
  }

  close(): void {
    if (!this.saving()) {
      this.flow.close();
    }
  }

  submitProfile(): void {
    if (this.profileForm.invalid || this.saving()) {
      this.profileForm.markAllAsTouched();
      this.toastr.warning('Confira os campos obrigatórios.', 'Perfil');
      return;
    }

    this.saving.set(true);
    const payload = this.profileForm.getRawValue();

    this.auth.updateProfile(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.flow.close();
        this.toastr.success('Perfil atualizado com sucesso.', 'Perfil');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Não foi possível atualizar o perfil.', 'Erro');
      },
    });
  }

  submitPassword(): void {
    if (this.passwordForm.invalid || this.saving()) {
      this.passwordForm.markAllAsTouched();
      this.toastr.warning('Preencha as senhas corretamente.', 'Seguranca');
      return;
    }

    const value = this.passwordForm.getRawValue();
    if (value.novaSenha !== value.confirmacao) {
      this.toastr.warning('A confirmacao precisa ser igual a nova senha.', 'Seguranca');
      return;
    }

    this.saving.set(true);
    this.auth.changePassword(value.senhaAtual, value.novaSenha).subscribe({
      next: () => {
        this.saving.set(false);
        this.flow.close();
        this.toastr.success('Senha alterada com sucesso.', 'Seguranca');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Não foi possível alterar a senha.', 'Erro');
      },
    });
  }

}
