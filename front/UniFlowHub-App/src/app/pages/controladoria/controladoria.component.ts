import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { AutoRefreshControlComponent } from '../../core/auto-refresh-control.component';
import { ControladoriaService, GuiaIcmsFilter } from '../../core/controladoria.service';
import { Empresa, GuiaIcms, Unidade } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { addDays, toDateInputValue } from '../../core/date-utils';
import { UnidadesService } from '../../core/unidades.service';

type GuiaStatusFilter = 'todas' | 'pendentes' | 'pagas';
type GuiaSortField = 'numeroNota' | 'empresa' | 'revenda' | 'transacao' | 'valor' | 'uf' | 'status';

const ESTADOS_BRASIL = [
  'AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG',
  'PA', 'PB', 'PR', 'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SC', 'SP', 'SE', 'TO',
];

@Component({
  selector: 'app-controladoria',
  imports: [CurrencyPipe, FormsModule, AutoRefreshControlComponent],
  templateUrl: './controladoria.component.html',
  styleUrl: './controladoria.component.css',
})
export class ControladoriaComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly service = inject(ControladoriaService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly guias = signal<GuiaIcms[]>([]);
  readonly empresasCadastro = signal<Empresa[]>([]);
  readonly revendasCadastro = signal<Unidade[]>([]);
  readonly selected = signal<GuiaIcms | null>(null);
  readonly loading = signal(false);
  readonly atualizadoEm = signal<Date | null>(null);
  readonly updatingId = signal('');
  readonly profileMenuOpen = signal(false);
  readonly activeFilter = signal<GuiaStatusFilter>('pendentes');
  readonly search = signal('');
  readonly empresaFilter = signal('');
  readonly revendaFilter = signal('');
  readonly dataInicioFilter = signal(toDateInputValue(addDays(new Date(), -30)));
  readonly dataFimFilter = signal(toDateInputValue(new Date()));
  readonly transacaoFilter = signal('');
  readonly ufFilter = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortField = signal<GuiaSortField>('numeroNota');
  readonly sortDirection = signal<'asc' | 'desc'>('asc');
  readonly batchUpdating = signal(false);
  readonly showFilteredTotal = signal(false);
  readonly estados = ESTADOS_BRASIL;

  readonly pagas = computed(() => this.guias().filter((item) => this.isPago(item)).length);
  readonly pendentes = computed(() => this.guias().filter((item) => !this.isPago(item)).length);
  readonly totalValorPendente = computed(() => this.guias().filter((item) => !this.isPago(item)).reduce((sum, item) => sum + item.valor, 0));
  readonly totalDifalPendente = computed(() => this.guias().filter((item) => !this.isPago(item)).reduce((sum, item) => sum + item.difal, 0));
  readonly totalFcpPendente = computed(() => this.guias().filter((item) => !this.isPago(item)).reduce((sum, item) => sum + item.fcp, 0));
  readonly vencidas = computed(() => this.guias().filter((item) => !this.isPago(item) && this.isOverdue(item)).length);
  readonly empresasOrdenadas = computed(() => this.empresasCadastro().slice().sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome)));
  readonly revendasDaEmpresa = computed(() => {
    const empresa = Number(this.empresaFilter());
    return this.revendasCadastro()
      .filter((unidade) => !empresa || unidade.empresaNumero === empresa)
      .sort((a, b) => a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda));
  });

  readonly filteredGuias = computed(() => {
    const term = this.normalize(this.search());
    let items = this.guias();

    if (this.activeFilter() === 'pendentes') {
      items = items.filter((item) => !this.isPago(item));
    } else if (this.activeFilter() === 'pagas') {
      items = items.filter((item) => this.isPago(item));
    }

    const filtered = items.filter((item) => !term || [
      item.id,
      item.documento,
      item.empresa,
      item.revenda,
      item.numeroNota,
      item.transacao,
      item.cnpj,
      item.competencia,
      item.uf,
      item.status,
      item.observacoes,
    ].some((value) => this.normalize(value).includes(term)));

    return this.sortGuias(filtered);
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filteredGuias().length / this.pageSize())));
  readonly pagedGuias = computed(() => this.filteredGuias().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));
  readonly filteredPendingGuias = computed(() => this.filteredGuias().filter((item) => !this.isPago(item)));
  readonly filteredPendingTotal = computed(() => this.filteredPendingGuias().reduce((sum, item) => sum + item.valor, 0));

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    this.loadCadastros();
    this.load();
  }

  loadCadastros(): void {
    this.unidadesService.listEmpresas().subscribe({
      next: (empresas) => this.empresasCadastro.set(empresas),
      error: () => this.toastr.error('Não foi possível carregar as empresas cadastradas.', 'Controladoria'),
    });

    this.unidadesService.list().subscribe({
      next: (revendas) => this.revendasCadastro.set(revendas),
      error: () => this.toastr.error('Não foi possível carregar as revendas cadastradas.', 'Controladoria'),
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.listGuiasIcms(this.getOracleFilter()).subscribe({
      next: (items) => {
        this.guias.set(items);
        this.page.set(1);
        this.selected.set(this.pagedGuias()[0] ?? items[0] ?? null);
        this.atualizadoEm.set(new Date());
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar as guias de ICMS. Confira seu perfil, a conexão Oracle e a query do serviço.', 'Controladoria');
      },
    });
  }

  applyOracleFilters(): void {
    this.page.set(1);
    this.load();
  }

  setEmpresaFilter(value: string | number): void {
    this.empresaFilter.set(String(value ?? ''));
    this.revendaFilter.set('');
  }

  clearOracleFilters(): void {
    this.empresaFilter.set('');
    this.revendaFilter.set('');
    this.transacaoFilter.set('');
    this.ufFilter.set('');
    this.dataInicioFilter.set(toDateInputValue(addDays(new Date(), -30)));
    this.dataFimFilter.set(toDateInputValue(new Date()));
    this.showFilteredTotal.set(false);
    this.page.set(1);
    this.load();
  }

  setFilter(filter: GuiaStatusFilter): void {
    this.activeFilter.set(filter);
    this.page.set(1);
    this.selected.set(this.pagedGuias()[0] ?? null);
  }

  setSort(field: GuiaSortField): void {
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

  select(item: GuiaIcms): void {
    this.selected.set(item);
  }

  markStatus(item: GuiaIcms, status: 'Pago' | 'Pendente'): void {
    if (this.updatingId()) {
      return;
    }

    this.updatingId.set(item.id);
    this.service.updateGuiaIcmsPagamento(item.id, status).subscribe({
      next: (updatedStatus) => {
        const updated = {
          ...item,
          status: updatedStatus.status,
          dataPagamento: updatedStatus.dataPagamento,
        };
        this.guias.set(this.guias().map((guia) => guia.id === item.id ? updated : guia));
        if (this.selected()?.id === item.id) {
          this.selected.set(updated);
        }
        this.updatingId.set('');
        this.toastr.success(status === 'Pago' ? 'Guia marcada como paga no PostgreSQL.' : 'Marcacao removida do PostgreSQL.', 'ICMS');
      },
      error: (error) => {
        this.updatingId.set('');
        this.toastr.error(this.getErrorMessage('Não foi possível atualizar a marcacao no PostgreSQL.', error), 'Controladoria');
      },
    });
  }

  showTotalGuiasFiltradas(): void {
    this.showFilteredTotal.set(true);
  }

  payFilteredGuias(): void {
    if (this.batchUpdating() || this.filteredPendingGuias().length === 0) {
      return;
    }

    const guiaIds = this.filteredPendingGuias().map((item) => item.id);
    this.batchUpdating.set(true);
    this.service.updateGuiasIcmsPagamentoLote(guiaIds).subscribe({
      next: (result) => {
        const ids = new Set(guiaIds);
        this.guias.set(this.guias().map((guia) => ids.has(guia.id)
          ? { ...guia, status: result.status, dataPagamento: result.dataPagamento }
          : guia));
        if (this.selected() && ids.has(this.selected()!.id)) {
          const updated = this.guias().find((guia) => guia.id === this.selected()!.id) ?? null;
          this.selected.set(updated);
        }
        this.batchUpdating.set(false);
        this.showFilteredTotal.set(true);
        this.toastr.success(`${result.atualizadas} guia(s) baixada(s) em lote.`, 'ICMS');
      },
      error: (error) => {
        this.batchUpdating.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível efetuar a baixa em lote.', error), 'Controladoria');
      },
    });
  }

  statusLabel(item: GuiaIcms): string {
    return this.isPago(item) ? 'Pago' : 'Pendente';
  }

  isPago(item: GuiaIcms): boolean {
    return this.normalize(item.status) === 'pago';
  }

  isOverdue(item: GuiaIcms): boolean {
    if (!item.dataVencimento) {
      return false;
    }

    const dueDate = new Date(item.dataVencimento);
    if (Number.isNaN(dueDate.getTime())) {
      return false;
    }

    dueDate.setHours(23, 59, 59, 999);
    return dueDate.getTime() < Date.now();
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

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private getOracleFilter(): GuiaIcmsFilter {
    return {
      empresa: this.empresaFilter(),
      revenda: this.revendaFilter(),
      transacao: this.transacaoFilter(),
      uf: this.ufFilter(),
      dataInicio: this.dataInicioFilter(),
      dataFim: this.dataFimFilter(),
    };
  }

  private safePage(): number {
    return Math.min(Math.max(this.page(), 1), this.totalPages());
  }

  private sortGuias(items: GuiaIcms[]): GuiaIcms[] {
    const direction = this.sortDirection() === 'asc' ? 1 : -1;
    const field = this.sortField();
    return items.slice().sort((a, b) => {
      const aValue = field === 'valor' ? a.valor : this.normalize(a[field]);
      const bValue = field === 'valor' ? b.valor : this.normalize(b[field]);
      return (aValue < bValue ? -1 : aValue > bValue ? 1 : 0) * direction;
    });
  }

  private getErrorMessage(fallback: string, error?: unknown): string {
    if (error && typeof error === 'object' && 'error' in error) {
      const response = error as { error?: unknown };
      if (typeof response.error === 'string' && response.error.trim()) {
        return response.error;
      }
    }

    return fallback;
  }
}
