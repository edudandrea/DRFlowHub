import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { BrandingService } from '../../core/branding.service';
import { Empresa, Unidade } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';

interface EmpresaRevendasNode {
  empresa: Empresa;
  revendas: Unidade[];
}

@Component({
  selector: 'app-empresas-revendas',
  imports: [ReactiveFormsModule],
  templateUrl: './empresas-revendas.html',
  styleUrl: './empresas-revendas.scss',
})
export class EmpresasRevendasPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly service = inject(UnidadesService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly profileFlow = inject(ProfileFlowService);
  readonly branding = inject(BrandingService);

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly empresas = signal<Empresa[]>([]);
  readonly revendas = signal<Unidade[]>([]);
  readonly selectedEmpresa = signal<Empresa | null>(null);
  readonly selectedRevenda = signal<Unidade | null>(null);
  readonly expandedEmpresaId = signal<number | null>(null);
  readonly profileMenuOpen = signal(false);
  readonly empresaModalOpen = signal(false);
  readonly revendaModalOpen = signal(false);
  readonly saving = signal(false);
  readonly savingLogoId = signal<number | null>(null);
  readonly empresaTree = computed<EmpresaRevendasNode[]>(() => this.empresas().map((empresa) => ({
    empresa,
    revendas: this.revendas()
      .filter((revenda) => revenda.empresaId === empresa.id)
      .sort((a, b) => a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda)),
  })));

  readonly empresaForm = this.fb.nonNullable.group({
    numero: [0, [Validators.required, Validators.min(1)]],
    nome: ['', Validators.required],
  });

  readonly revendaForm = this.fb.nonNullable.group({
    empresaId: [0, [Validators.required, Validators.min(1)]],
    numeroRevenda: [0, [Validators.required, Validators.min(1)]],
    revenda: ['', Validators.required],
    cnpj: ['', Validators.required],
    endereco: ['', Validators.required],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.listEmpresas().subscribe({ next: (items) => this.empresas.set(items.sort((a, b) => a.numero - b.numero)) });
    this.service.list().subscribe({ next: (items) => this.revendas.set(items.sort((a, b) => a.empresaNumero - b.empresaNumero || a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda))) });
  }

  selectEmpresa(item: Empresa): void {
    this.expandedEmpresaId.set(this.expandedEmpresaId() === item.id ? null : item.id);
  }

  goHome(): void {
    void this.router.navigate(['/hub']);
  }

  logout(): void {
    this.auth.logout();
  }

  editProfile(): void {
    this.profileMenuOpen.set(false);
    this.profileFlow.editProfile();
  }

  changePassword(): void {
    this.profileMenuOpen.set(false);
    this.profileFlow.changePassword();
  }

  @HostListener('document:click', ['$event'])
  closeProfileMenuOnDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.profile-area')) {
      this.profileMenuOpen.set(false);
    }
  }

  editEmpresa(item: Empresa): void {
    this.selectedEmpresa.set(item);
    this.empresaForm.reset({ numero: item.numero, nome: item.nome });
    this.empresaModalOpen.set(true);
  }

  novaEmpresa(): void {
    this.selectedEmpresa.set(null);
    this.empresaForm.reset({ numero: 0, nome: '' });
    this.empresaModalOpen.set(true);
  }

  closeEmpresaModal(): void {
    if (!this.saving()) {
      this.empresaModalOpen.set(false);
    }
  }

  saveEmpresa(): void {
    if (this.empresaForm.invalid) {
      this.empresaForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const selected = this.selectedEmpresa();
    const payload = {
      ...this.empresaForm.getRawValue(),
      logoUrl: selected?.logoUrl ?? '',
    };
    const request = selected ? this.service.updateEmpresa(selected.id, payload) : this.service.createEmpresa(payload);
    request.subscribe({
      next: () => {
        this.branding.refresh();
        this.saving.set(false);
        this.empresaModalOpen.set(false);
        this.selectedEmpresa.set(null);
        this.load();
        this.toastr.success('Empresa salva.', 'Cadastros');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Não foi possível salvar a empresa.', 'Erro');
      },
    });
  }

  onCompanyLogoSelected(event: Event, empresa: Empresa): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    if (!file.type.startsWith('image/')) {
      this.toastr.warning('Selecione um arquivo de imagem.', 'Logo da empresa');
      input.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.updateCompanyLogo(empresa, String(reader.result ?? ''));
      input.value = '';
    };
    reader.readAsDataURL(file);
  }

  clearCompanyLogo(empresa: Empresa): void {
    this.updateCompanyLogo(empresa, '');
  }

  private updateCompanyLogo(empresa: Empresa, logoUrl: string): void {
    this.savingLogoId.set(empresa.id);
    this.service.updateEmpresa(empresa.id, {
      numero: empresa.numero,
      nome: empresa.nome,
      logoUrl,
    }).subscribe({
      next: (saved) => {
        this.empresas.set(this.empresas().map((item) => item.id === saved.id ? saved : item).sort((a, b) => a.numero - b.numero));
        this.branding.refresh();
        this.savingLogoId.set(null);
        this.toastr.success(logoUrl ? 'Logo atualizada.' : 'Logo removida.', 'Empresas');
      },
      error: () => {
        this.savingLogoId.set(null);
        this.toastr.error('NÃ£o foi possÃ­vel atualizar a logo.', 'Erro');
      },
    });
  }

  editRevenda(item: Unidade): void {
    this.selectedRevenda.set(item);
    this.revendaForm.reset({
      empresaId: item.empresaId ?? 0,
      numeroRevenda: item.numeroRevenda,
      revenda: item.revenda,
      cnpj: item.cnpj,
      endereco: item.endereco,
    });
    this.revendaModalOpen.set(true);
  }

  novaRevenda(empresaId = this.expandedEmpresaId() ?? 0): void {
    this.selectedRevenda.set(null);
    this.revendaForm.reset({ empresaId, numeroRevenda: 0, revenda: '', cnpj: '', endereco: '' });
    this.revendaModalOpen.set(true);
  }

  closeRevendaModal(): void {
    if (!this.saving()) {
      this.revendaModalOpen.set(false);
    }
  }

  saveRevenda(): void {
    if (this.revendaForm.invalid) {
      this.revendaForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const selected = this.selectedRevenda();
    const request = selected ? this.service.update(selected.id, this.revendaForm.getRawValue()) : this.service.create(this.revendaForm.getRawValue());
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.revendaModalOpen.set(false);
        this.selectedRevenda.set(null);
        this.load();
        this.toastr.success('Revenda salva.', 'Cadastros');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Não foi possível salvar a revenda.', 'Erro');
      },
    });
  }
}
