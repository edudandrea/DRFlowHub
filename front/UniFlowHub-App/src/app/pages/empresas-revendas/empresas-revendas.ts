import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { BrandingService } from '../../core/branding.service';
import { Unidade } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';

interface EmpresaRevendasNode {
  empresaNumero: number;
  empresaNome: string;
  empresaAtiva: boolean;
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
  readonly revendas = signal<Unidade[]>([]);
  readonly selectedRevenda = signal<Unidade | null>(null);
  readonly expandedEmpresaNumero = signal<number | null>(null);
  readonly profileMenuOpen = signal(false);
  readonly montadoraModalOpen = signal(false);
  readonly saving = signal(false);
  readonly savingKey = signal<string | null>(null);

  readonly empresaTree = computed<EmpresaRevendasNode[]>(() => {
    const grouped = new Map<number, EmpresaRevendasNode>();
    for (const revenda of this.revendas()) {
      const node = grouped.get(revenda.empresaNumero) ?? {
        empresaNumero: revenda.empresaNumero,
        empresaNome: revenda.empresa,
        empresaAtiva: revenda.empresaAtiva !== false,
        revendas: [],
      };
      node.empresaAtiva = revenda.empresaAtiva !== false;
      node.revendas.push(revenda);
      grouped.set(revenda.empresaNumero, node);
    }

    return Array.from(grouped.values())
      .map((node) => ({
        ...node,
        revendas: node.revendas.sort((a, b) => a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda)),
      }))
      .sort((a, b) => a.empresaNumero - b.empresaNumero || a.empresaNome.localeCompare(b.empresaNome));
  });

  readonly montadoraForm = this.fb.nonNullable.group({
    montadora: ['', Validators.required],
    logoMontadoraUrl: [''],
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.listEmpresasRevendas(true).subscribe({
      next: (items) => this.revendas.set(items.map((item) => this.normalizeRevenda(item)).sort((a, b) => a.empresaNumero - b.empresaNumero || a.numeroRevenda - b.numeroRevenda)),
      error: () => this.toastr.error('Nao foi possivel consultar empresas e revendas no Oracle.', 'Erro'),
    });
  }

  selectEmpresa(empresaNumero: number): void {
    this.expandedEmpresaNumero.set(this.expandedEmpresaNumero() === empresaNumero ? null : empresaNumero);
  }

  editMontadora(item: Unidade): void {
    this.selectedRevenda.set(item);
    this.montadoraForm.reset({
      montadora: item.montadora ?? '',
      logoMontadoraUrl: item.logoMontadoraUrl ?? '',
    });
    this.montadoraModalOpen.set(true);
  }

  closeMontadoraModal(): void {
    if (!this.saving()) {
      this.montadoraModalOpen.set(false);
    }
  }

  saveMontadora(): void {
    const selected = this.selectedRevenda();
    if (!selected) {
      return;
    }

    if (this.montadoraForm.invalid) {
      this.montadoraForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.service.updateMontadora(selected.empresaNumero, selected.numeroRevenda, this.montadoraForm.getRawValue()).subscribe({
      next: (saved) => {
        const normalized = this.normalizeRevenda(saved);
        this.revendas.set(this.revendas().map((item) => this.sameRevenda(item, normalized) ? { ...item, ...normalized } : item));
        this.saving.set(false);
        this.montadoraModalOpen.set(false);
        this.selectedRevenda.set(null);
        this.toastr.success('Montadora atualizada.', 'Cadastros');
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Nao foi possivel salvar a montadora.', 'Erro');
      },
    });
  }

  onLogoSelected(event: Event, revenda = this.selectedRevenda()): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !revenda) {
      return;
    }

    if (!file.type.startsWith('image/')) {
      this.toastr.warning('Selecione um arquivo de imagem.', 'Logo da montadora');
      input.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.updateMontadoraLogo(revenda, String(reader.result ?? ''));
      input.value = '';
    };
    reader.readAsDataURL(file);
  }

  clearLogo(revenda = this.selectedRevenda()): void {
    if (revenda) {
      this.updateMontadoraLogo(revenda, '');
    }
  }

  toggleEmpresaStatus(node: EmpresaRevendasNode, event: Event): void {
    event.stopPropagation();
    const ativa = !node.empresaAtiva;
    this.savingKey.set(`empresa:${node.empresaNumero}`);
    this.service.updateEmpresaStatus(node.empresaNumero, ativa).subscribe({
      next: () => {
        this.revendas.set(this.revendas().map((item) => {
          if (item.empresaNumero !== node.empresaNumero) {
            return item;
          }

          const revendaAtiva = item.revendaAtiva !== false;
          return { ...item, empresaAtiva: ativa, ativa: ativa && revendaAtiva };
        }));
        this.savingKey.set(null);
        this.toastr.success(ativa ? 'Empresa reativada.' : 'Empresa desativada com suas revendas.', 'Cadastros');
      },
      error: () => {
        this.savingKey.set(null);
        this.toastr.error('Nao foi possivel atualizar o status da empresa.', 'Erro');
      },
    });
  }

  toggleRevendaStatus(revenda: Unidade, event?: Event): void {
    event?.stopPropagation();
    const ativa = revenda.revendaAtiva === false;
    this.savingKey.set(this.revendaKey(revenda));
    this.service.updateRevendaStatus(revenda.empresaNumero, revenda.numeroRevenda, ativa).subscribe({
      next: (saved) => {
        const normalized = this.normalizeRevenda(saved);
        this.revendas.set(this.revendas().map((item) => this.sameRevenda(item, normalized) ? { ...item, ...normalized } : item));
        this.selectedRevenda.update((item) => item && this.sameRevenda(item, normalized) ? { ...item, ...normalized } : item);
        this.savingKey.set(null);
        this.toastr.success(ativa ? 'Revenda reativada.' : 'Revenda desativada.', 'Cadastros');
      },
      error: () => {
        this.savingKey.set(null);
        this.toastr.error('Nao foi possivel atualizar o status da revenda.', 'Erro');
      },
    });
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

  private updateMontadoraLogo(revenda: Unidade, logoMontadoraUrl: string): void {
    const key = this.revendaKey(revenda);
    this.savingKey.set(key);
    this.service.updateMontadora(revenda.empresaNumero, revenda.numeroRevenda, {
      montadora: revenda.montadora ?? this.montadoraForm.controls.montadora.value,
      logoMontadoraUrl,
    }).subscribe({
      next: (saved) => {
        const normalized = this.normalizeRevenda(saved);
        this.revendas.set(this.revendas().map((item) => this.sameRevenda(item, normalized) ? { ...item, ...normalized } : item));
        this.montadoraForm.controls.logoMontadoraUrl.setValue(normalized.logoMontadoraUrl ?? '');
        this.savingKey.set(null);
        this.toastr.success(logoMontadoraUrl ? 'Logo atualizada.' : 'Logo removida.', 'Cadastros');
      },
      error: () => {
        this.savingKey.set(null);
        this.toastr.error('Nao foi possivel atualizar a logo.', 'Erro');
      },
    });
  }

  private sameRevenda(a: Unidade, b: Unidade): boolean {
    return a.empresaNumero === b.empresaNumero && a.numeroRevenda === b.numeroRevenda;
  }

  private normalizeRevenda(item: Unidade & { empresaNome?: string; nomeRevenda?: string }): Unidade {
    return {
      ...item,
      empresa: item.empresa || item.empresaNome || '',
      revenda: item.revenda || item.nomeRevenda || '',
    };
  }

  private revendaKey(revenda: Unidade): string {
    return `${revenda.empresaNumero}:${revenda.numeroRevenda}`;
  }
}
