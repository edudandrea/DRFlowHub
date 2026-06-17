import { isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { AutoRefreshControlComponent } from '../../core/auto-refresh-control.component';
import { Empresa, Unidade, VeiculoEstoque } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';
import { VeiculosService } from '../../core/veiculos.service';

type VeiculoSortField = 'codigoVeiculo' | 'modelo' | 'cor' | 'chassi' | 'revenda' | 'reservado';

@Component({
  selector: 'app-veiculos',
  imports: [FormsModule, AutoRefreshControlComponent],
  templateUrl: './veiculos.component.html',
  styleUrls: ['./veiculos.component.css'],
})
export class VeiculosComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly service = inject(VeiculosService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly router = inject(Router);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly items = signal<VeiculoEstoque[]>([]);
  readonly selected = signal<VeiculoEstoque | null>(null);
  readonly empresas = signal<Empresa[]>([]);
  readonly revendas = signal<Unidade[]>([]);
  readonly empresaNumero = signal<number | null>(null);
  readonly revendaNumero = signal<number | null>(null);
  readonly loading = signal(false);
  readonly atualizadoEm = signal<Date | null>(null);
  readonly updatingChassi = signal('');
  readonly profileMenuOpen = signal(false);
  readonly busca = signal('');
  readonly reservadoFilter = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortField = signal<VeiculoSortField>('codigoVeiculo');
  readonly sortDirection = signal<'asc' | 'desc'>('asc');
  readonly canReserve = computed(() => this.auth.hasAnyRole(['Admin', 'Qualidade Nissan', 'TI']));
  readonly totalReservados = computed(() => this.items().filter((item) => item.reservado).length);
  readonly totalDisponiveis = computed(() => this.items().filter((item) => !item.reservado).length);
  readonly revendasDaEmpresa = computed(() => {
    const empresa = this.empresaNumero();
    return this.revendas()
      .filter((revenda) => !empresa || revenda.empresaNumero === empresa)
      .sort((a, b) => a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda));
  });
  readonly filtered = computed(() => this.sortItems(this.items()));
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize())));
  readonly paged = computed(() => this.filtered().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));

  ngOnInit(): void {
    if (this.isBrowser) {
      this.loadEmpresas();
      this.loadRevendas();
    }
  }

  loadEmpresas(): void {
    this.unidadesService.listEmpresas().subscribe({
      next: (empresas) => {
        const allowed = this.user()?.role === 'Qualidade Nissan'
          ? empresas.filter((empresa) => empresa.numero === 2)
          : empresas;
        const sorted = allowed.slice().sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome));
        this.empresas.set(sorted);
        this.empresaNumero.set(sorted[0]?.numero ?? null);
        if (this.empresaNumero()) {
          this.load();
        }
      },
      error: () => this.toastr.error('Não foi possível carregar as empresas cadastradas.', 'Estoque'),
    });
  }

  loadRevendas(): void {
    this.unidadesService.list().subscribe({
      next: (revendas) => this.revendas.set(revendas),
      error: () => this.toastr.error('Não foi possível carregar as revendas cadastradas.', 'Estoque'),
    });
  }

  load(): void {
    if (!this.empresaNumero()) {
      this.items.set([]);
      this.selected.set(null);
      this.toastr.warning('Selecione uma empresa para consultar o estoque.', 'Estoque');
      return;
    }

    this.loading.set(true);
    void this.spinner.show();
    this.service.listEstoque({ empresa: this.empresaNumero(), revenda: this.revendaNumero(), busca: this.busca(), reservado: this.reservadoFilter() }).subscribe({
      next: (items) => {
        this.items.set(items);
        this.page.set(1);
        this.selected.set(items[0] ?? null);
        this.atualizadoEm.set(new Date());
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar o estoque de veículos.', 'Estoque');
      },
    });
  }

  clearFilters(): void {
    this.busca.set('');
    this.revendaNumero.set(null);
    this.reservadoFilter.set('');
    this.load();
  }

  setEmpresa(value: string | number | null): void {
    const numero = Number(value);
    this.empresaNumero.set(Number.isFinite(numero) && numero > 0 ? numero : null);
    this.revendaNumero.set(null);
    this.page.set(1);
    this.load();
  }

  setRevenda(value: string | number | null): void {
    const numero = Number(value);
    this.revendaNumero.set(Number.isFinite(numero) && numero > 0 ? numero : null);
    this.page.set(1);
  }

  select(item: VeiculoEstoque): void {
    this.selected.set(item);
  }

  updateReserva(item: VeiculoEstoque, reservado: boolean): void {
    if (!this.canReserve() || this.updatingChassi()) {
      return;
    }

    this.updatingChassi.set(item.chassi);
    this.service.updateReserva(item.chassi, item.empresa || this.empresaNumero() || 0, reservado).subscribe({
      next: (updated) => {
        const merged = {
          ...item,
          ...updated,
          codigoVeiculo: item.codigoVeiculo,
          modelo: item.modelo,
          descricaoModelo: item.descricaoModelo,
          cor: item.cor,
          descricaoCor: item.descricaoCor,
          revenda: item.revenda,
        };
        this.items.set(this.items().map((current) => current.chassi === item.chassi ? merged : current));
        this.selected.set(merged);
        this.updatingChassi.set('');
        this.toastr.success(reservado ? 'Veículo marcado como reservado.' : 'Veículo liberado no estoque.', 'Estoque');
      },
      error: () => {
        this.updatingChassi.set('');
        this.toastr.error('Não foi possível atualizar a reserva.', 'Estoque');
      },
    });
  }

  setSort(field: VeiculoSortField): void {
    if (this.sortField() === field) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortField.set(field);
      this.sortDirection.set('asc');
    }
    this.page.set(1);
  }

  previousPage(): void {
    this.page.set(Math.max(1, this.safePage() - 1));
  }

  nextPage(): void {
    this.page.set(Math.min(this.totalPages(), this.safePage() + 1));
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

  private safePage(): number {
    return Math.min(Math.max(this.page(), 1), this.totalPages());
  }

  private sortItems(items: VeiculoEstoque[]): VeiculoEstoque[] {
    const field = this.sortField();
    const direction = this.sortDirection() === 'asc' ? 1 : -1;
    return items.slice().sort((a, b) => {
      const aValue = field === 'reservado' ? Number(a.reservado) : field === 'revenda' ? a.revenda : this.normalize(a[field]);
      const bValue = field === 'reservado' ? Number(b.reservado) : field === 'revenda' ? b.revenda : this.normalize(b[field]);
      return (aValue < bValue ? -1 : aValue > bValue ? 1 : 0) * direction;
    });
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }
}
