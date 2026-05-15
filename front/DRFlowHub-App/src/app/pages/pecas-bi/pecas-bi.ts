import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { Empresa, Unidade } from '../../core/models';
import { PecasBiData, PecaVendaMensal, PecaVendedor, PecasBiService } from '../../core/pecas-bi.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';

@Component({
  selector: 'app-pecas-bi',
  imports: [DatePipe, FormsModule],
  templateUrl: './pecas-bi.html',
  styleUrl: './pecas-bi.scss',
})
export class PecasBiPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly service = inject(PecasBiService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly profileMenuOpen = signal(false);
  readonly loading = signal(false);
  readonly data = signal<PecasBiData | null>(null);
  readonly empresas = signal<Empresa[]>([]);
  readonly revendas = signal<Unidade[]>([]);
  readonly empresaNumero = signal<number | null>(null);
  readonly revendaNumero = signal<number | null>(null);
  readonly dataInicio = signal(this.toDateInput(this.monthsAgo(1)));
  readonly dataFim = signal(this.toDateInput(new Date()));
  readonly canal = signal('Todos');
  readonly metaModalSeller = signal<PecaVendedor | null>(null);
  readonly metaDraft = signal<number>(0);
  readonly metaDataInicioDraft = signal('');
  readonly metaDataFimDraft = signal('');
  readonly savingMeta = signal(false);
  readonly hoveredChannel = signal<string | null>(null);

  readonly canais = computed(() => ['Todos', 'P21', 'P23', 'P41']);
  readonly vendas = computed(() => this.data()?.vendasMensais ?? []);
  readonly categorias = computed(() => this.data()?.categorias ?? []);
  readonly pecas = computed(() => this.data()?.pecas ?? []);
  readonly vendedores = computed(() => this.data()?.vendedores ?? []);
  readonly canaisData = computed(() => this.data()?.canais ?? []);
  readonly clientes = computed(() => this.data()?.clientes ?? []);
  readonly seguradoras = computed(() => this.data()?.seguradoras ?? []);
  readonly canViewSellerRanking = computed(() => this.data()?.podeVerRankingVendedores ?? false);
  readonly isGerenteEmpresaPecas = computed(() => this.user()?.role === 'Gerente de Pecas');
  readonly isGerenteGeralPecas = computed(() => this.user()?.role === 'Gerente Geral de Pecas');
  readonly isVendedorPecas = computed(() => this.user()?.role === 'Vendedor de Pecas');
  readonly userEmpresaNumero = computed(() => this.revendas().find((revenda) => revenda.id === this.user()?.unidadeId)?.empresaNumero ?? null);
  readonly canUseEmpresaRevendaFilters = computed(() => !this.isVendedorPecas());
  readonly empresasDisponiveis = computed(() => {
    const empresaNumero = this.userEmpresaNumero();
    if (this.isGerenteEmpresaPecas()) {
      return this.empresas().filter((empresa) => empresa.numero === empresaNumero);
    }

    return this.empresas();
  });
  readonly revendasDaEmpresa = computed(() => {
    const empresa = this.isGerenteEmpresaPecas() ? this.userEmpresaNumero() : this.empresaNumero();
    return this.revendas()
      .filter((revenda) => !empresa || revenda.empresaNumero === empresa)
      .sort((a, b) => a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda));
  });
  readonly faturamentoTotal = computed(() => this.vendas().reduce((total, item) => total + item.faturamento, 0));
  readonly margemTotal = computed(() => this.vendas().reduce((total, item) => total + item.margem, 0));
  readonly quantidadeTotal = computed(() => this.vendas().reduce((total, item) => total + item.quantidade, 0));
  readonly ticketMedio = computed(() => this.quantidadeTotal() ? this.faturamentoTotal() / this.quantidadeTotal() : 0);
  readonly margemPercentual = computed(() => this.faturamentoTotal() ? (this.margemTotal() / this.faturamentoTotal()) * 100 : 0);
  readonly crescimento = computed(() => {
    const vendas = this.vendas();
    const atual = vendas.at(-1)?.faturamento ?? 0;
    const anterior = vendas.at(-2)?.faturamento ?? 0;
    return anterior ? ((atual - anterior) / anterior) * 100 : 0;
  });
  readonly maxMensal = computed(() => Math.max(...this.vendas().map((item) => item.faturamento), 1));
  readonly maxMargemMensal = computed(() => Math.max(...this.vendas().map((item) => Math.max(item.margem, 0)), 1));
  readonly maxCategoria = computed(() => Math.max(...this.categorias().map((item) => item.faturamento), 1));
  readonly maxCanal = computed(() => Math.max(...this.canaisData().map((item) => item.faturamento), 1));
  readonly canaisTotal = computed(() => this.canaisData().reduce((total, item) => total + item.faturamento, 0));
  readonly curvaA = computed(() => this.pecas().filter((_, index) => index < 2));
  readonly curvaB = computed(() => this.pecas().filter((_, index) => index >= 2 && index < 4));
  readonly curvaC = computed(() => this.pecas().filter((_, index) => index >= 4));
  readonly forecast = computed(() => {
    const vendas = this.vendas();
    const media = vendas.slice(-3).reduce((total, item) => total + item.faturamento, 0) / Math.max(vendas.slice(-3).length, 1);
    return media * 1.07;
  });
  readonly minhaMeta = computed(() => this.data()?.minhaMeta ?? null);
  readonly metaPercentual = computed(() => {
    const meta = this.minhaMeta();
    return meta?.valorMeta ? (meta.valorVendido / meta.valorMeta) * 100 : 0;
  });
  readonly metaProgressWidth = computed(() => this.progressWidth(this.metaPercentual()));
  readonly metaStatus = computed(() => {
    const percent = this.metaPercentual();
    if (percent >= 100) {
      return 'success';
    }
    return percent >= 80 ? 'warning' : 'danger';
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    this.loadEmpresas();
    this.loadRevendas();
    this.load();
  }

  loadEmpresas(): void {
    this.unidadesService.listEmpresas().subscribe({
      next: (empresas) => this.empresas.set(empresas.slice().sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome))),
      error: () => this.toastr.error('Nao foi possivel carregar as empresas cadastradas.', 'B.I Pecas'),
    });
  }

  loadRevendas(): void {
    this.unidadesService.list().subscribe({
      next: (revendas) => {
        this.revendas.set(revendas);
        this.applyUserScopeDefaults();
      },
      error: () => this.toastr.error('Nao foi possivel carregar as revendas cadastradas.', 'B.I Pecas'),
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.load({
      dataInicio: this.dataInicio(),
      dataFim: this.dataFim(),
      empresa: this.empresaNumero(),
      revenda: this.revendaNumero(),
      canal: this.canal(),
    }).subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Nao foi possivel carregar o B.I de venda de pecas.', 'Erro');
      },
    });
  }

  barHeight(item: PecaVendaMensal): number {
    return this.barHeightValue(item.faturamento);
  }

  barHeightValue(value: number): number {
    return Math.max(8, (value / this.maxMensal()) * 100);
  }

  percent(value: number, max: number): number {
    return Math.max(2, (value / max) * 100);
  }

  percentOfTotal(value: number, total: number): number {
    return total ? Math.max(0, (value / total) * 100) : 0;
  }

  donutGradient(): string {
    const colors = ['var(--color-brand-blue)', 'var(--color-brand-green-strong)', '#f59e0b', '#8b5cf6', '#64748b'];
    const total = this.canaisTotal();
    if (!total) {
      return 'conic-gradient(#e2e8f0 0 100%)';
    }

    let start = 0;
    const segments = this.canaisData().map((item, index) => {
      const end = start + (item.faturamento / total) * 100;
      const segment = `${colors[index % colors.length]} ${start.toFixed(2)}% ${end.toFixed(2)}%`;
      start = end;
      return segment;
    });

    return `conic-gradient(${segments.join(', ')})`;
  }

  channelColor(index: number): string {
    return ['#2454d6', '#1fae6a', '#f59e0b', '#8b5cf6', '#64748b'][index % 5];
  }

  channelOffsetPercent(index: number): number {
    const total = this.canaisTotal();
    if (!total || index <= 0) {
      return 0;
    }

    return this.canaisData()
      .slice(0, index)
      .reduce((sum, item) => sum + (item.faturamento / total) * 100, 0);
  }

  hoveredChannelValue(): number {
    const channel = this.hoveredChannel();
    return this.canaisData().find((item) => item.nome === channel)?.faturamento ?? this.canaisTotal();
  }

  hoveredChannelLabel(): string {
    const channel = this.hoveredChannel();
    return channel ? this.channelLabel(channel) : 'Total';
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('pt-BR').format(Math.round(value));
  }

  formatPercent(value: number): string {
    return `${value.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
  }

  clientListTotal(items: { faturamento: number }[]): number {
    return items.reduce((total, item) => total + item.faturamento, 0);
  }

  channelLabel(value: string): string {
    const labels: Record<string, string> = {
      P21: 'Venda de peças',
      P23: 'Venda de seguradora',
      P41: 'Mercado Livre',
      
    };

    return labels[value] ?? value;
  }

  sellerGoalPercent(seller: PecaVendedor): number {
    return seller.metaVendas ? (seller.faturamento / seller.metaVendas) * 100 : 0;
  }

  sellerGoalProgressWidth(seller: PecaVendedor): number {
    return this.progressWidth(this.sellerGoalPercent(seller));
  }

  sellerGoalClass(seller: PecaVendedor): string {
    const percent = this.sellerGoalPercent(seller);
    if (percent >= 100) {
      return 'success';
    }
    return percent >= 80 ? 'warning' : 'danger';
  }

  private progressWidth(value: number): number {
    return Math.max(0, Math.min(value, 100));
  }

  openMetaModal(seller: PecaVendedor): void {
    if (!seller.cpfVendedor) {
      this.toastr.warning('Vendedor sem CPF no retorno do Oracle.', 'Meta de vendas');
      return;
    }

    this.metaModalSeller.set(seller);
    this.metaDraft.set(seller.metaVendas ?? 0);
    this.metaDataInicioDraft.set(this.toDateInputOrDefault(seller.metaDataInicio, this.firstDayOfCurrentMonth()));
    this.metaDataFimDraft.set(this.toDateInputOrDefault(seller.metaDataFim, new Date()));
  }

  closeMetaModal(): void {
    if (this.savingMeta()) {
      return;
    }

    this.metaModalSeller.set(null);
    this.metaDraft.set(0);
    this.metaDataInicioDraft.set('');
    this.metaDataFimDraft.set('');
  }

  saveMeta(): void {
    const seller = this.metaModalSeller();
    if (!seller) {
      return;
    }

    const valorMeta = Number(this.metaDraft());
    if (!Number.isFinite(valorMeta) || valorMeta < 0) {
      this.toastr.error('Informe uma meta valida.', 'Meta de vendas');
      return;
    }

    if (!this.metaDataInicioDraft() || !this.metaDataFimDraft()) {
      this.toastr.error('Informe o periodo da meta.', 'Meta de vendas');
      return;
    }

    if (this.metaDataInicioDraft() > this.metaDataFimDraft()) {
      this.toastr.error('A data inicial da meta nao pode ser maior que a data final.', 'Meta de vendas');
      return;
    }

    this.savingMeta.set(true);
    this.service.saveMeta({
      cpfVendedor: seller.cpfVendedor,
      nomeVendedor: seller.nome,
      valorMeta,
      dataInicio: this.metaDataInicioDraft(),
      dataFim: this.metaDataFimDraft(),
    }).subscribe({
      next: () => {
        this.savingMeta.set(false);
        this.metaModalSeller.set(null);
        this.toastr.success('Meta de vendas atualizada.', 'B.I Pecas');
        this.load();
      },
      error: () => {
        this.savingMeta.set(false);
        this.toastr.error('Nao foi possivel salvar a meta do vendedor.', 'Erro');
      },
    });
  }

  setEmpresa(value: string | number | null): void {
    if (!this.canUseEmpresaRevendaFilters() || this.isGerenteEmpresaPecas()) {
      return;
    }

    const numero = Number(value);
    this.empresaNumero.set(Number.isFinite(numero) && numero > 0 ? numero : null);
    this.revendaNumero.set(null);
  }

  setRevenda(value: string | number | null): void {
    if (!this.canUseEmpresaRevendaFilters()) {
      return;
    }

    const numero = Number(value);
    this.revendaNumero.set(Number.isFinite(numero) && numero > 0 ? numero : null);
  }

  private applyUserScopeDefaults(): void {
    if (this.isVendedorPecas()) {
      this.empresaNumero.set(null);
      this.revendaNumero.set(null);
      return;
    }

    if (this.isGerenteEmpresaPecas()) {
      this.empresaNumero.set(this.userEmpresaNumero());
      this.revendaNumero.set(null);
    }
  }

  private monthsAgo(months: number): Date {
    const value = new Date();
    value.setMonth(value.getMonth() - months, 1);
    return value;
  }

  private toDateInput(value: Date): string {
    return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
  }

  private toDateInputOrDefault(value: string | null | undefined, fallback: Date): string {
    if (!value) {
      return this.toDateInput(fallback);
    }

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? this.toDateInput(fallback) : this.toDateInput(date);
  }

  private firstDayOfCurrentMonth(): Date {
    const date = new Date();
    return new Date(date.getFullYear(), date.getMonth(), 1);
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
}
