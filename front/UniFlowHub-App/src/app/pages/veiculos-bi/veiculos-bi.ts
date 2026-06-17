import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AutoRefreshControlComponent } from '../../core/auto-refresh-control.component';
import { AuthService } from '../../core/auth.service';
import { Empresa, Unidade } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';
import { VeiculoAcessorioRanking, VeiculosBiDashboard, VeiculosBiRetornoFiDashboard, VeiculosBiRetornoFiGrupo, VeiculosBiService } from '../../core/veiculos-bi.service';

interface FilialVenda {
  empresaNumero: number;
  empresaNome: string;
  revendaNumero: number;
  filial: string;
  metaNovos: number;
  metaVendaDireta: number;
  anunciadosNovos: number;
  faturadosNovos: number;
  anunciadosDireta: number;
  faturadosDireta: number;
  seminovos: number;
  propostas: number;
  baixados: number;
  faturamento: number;
  margem: number;
}

interface VendaDiaria {
  data: string;
  novos: number;
  vendaDireta: number;
  seminovos: number;
}

interface ModeloRanking {
  modelo: string;
  familia: string;
  unidades: number;
  faturamento: number;
  margemPercentual: number;
}

interface VendedorMeta {
  vendedor: string;
  filial: string;
  meta: number;
  realizado: number;
  faturamento: number;
}

interface ChartSlice {
  label: string;
  value: number;
  color: string;
}

interface PreparacaoBloco {
  titulo: string;
  descricao: string;
  indicador: string;
}

type VeiculosBiTab = 'veiculos' | 'acessorios' | 'retorno-fi';

@Component({
  selector: 'app-veiculos-bi',
  imports: [DatePipe, AutoRefreshControlComponent],
  templateUrl: './veiculos-bi.html',
  styleUrl: './veiculos-bi.scss',
})
export class VeiculosBiPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly toastr = inject(ToastrService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly veiculosBiService = inject(VeiculosBiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly Math = Math;
  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly profileMenuOpen = signal(false);
  readonly revendaPickerOpen = signal(false);
  readonly hoveredSlice = signal('');
  readonly loading = signal(false);
  readonly activeTab = signal<VeiculosBiTab>('veiculos');
  readonly atualizadoEm = signal(new Date().toISOString());
  readonly dataInicio = signal(this.defaultStartDate());
  readonly dataFim = signal(this.defaultEndDate());
  readonly empresaNumero = signal<number | null>(null);
  readonly revendasSelecionadas = signal<number[]>([]);
  readonly vendasPeriodoPage = signal(1);
  readonly vendasPeriodoPageSize = 10;

  readonly empresas = signal<Empresa[]>([]);
  readonly revendas = signal<Unidade[]>([]);
  readonly vendasFiliais = signal<FilialVenda[]>([]);
  readonly vendasDiarias = signal<VendaDiaria[]>([]);
  readonly modelos = signal<ModeloRanking[]>([]);
  readonly vendedores = signal<VendedorMeta[]>([]);
  readonly acessorios = signal<VeiculoAcessorioRanking[]>([]);
  readonly retornoFi = signal<VeiculosBiRetornoFiDashboard | null>(null);

  readonly acessoriosPreparacao = computed<PreparacaoBloco[]>(() => {
    const acessorios = this.acessorios();
    const faturamento = acessorios.reduce((total, item) => total + item.faturamento, 0);
    const quantidade = acessorios.reduce((total, item) => total + item.quantidade, 0);
    const rentabilidade = acessorios.reduce((total, item) => total + item.rentabilidade, 0);
    const top = acessorios[0];
    return [
      { titulo: 'Acessorios vendidos', descricao: `${this.formatMoney(faturamento)} em ${this.formatNumber(quantidade)} itens`, indicador: acessorios.length ? 'Departamento 7' : 'Sem dados' },
      { titulo: 'Ticket medio de acessorios', descricao: quantidade ? `${this.formatMoney(faturamento / quantidade)} por item vendido` : 'Aguardando vendas no periodo.', indicador: 'Oracle' },
      { titulo: top?.nome ?? 'Top acessorio', descricao: top ? `${this.formatNumber(top.quantidade)} un. - margem ${this.formatPercent(top.margemPercentual)}` : 'Nenhum acessorio encontrado.', indicador: rentabilidade ? this.formatMoney(rentabilidade) : 'Sem margem' },
    ];
  });

  readonly financeiroPreparacao: PreparacaoBloco[] = [
    { titulo: 'Retorno financeiro', descricao: 'Receita financeira, bonus, despesas e margem liquida.', indicador: 'Query pendente' },
    { titulo: 'Custo e valor presente', descricao: 'Comparativo entre venda, custo contabil e valor presente.', indicador: 'Filtro pronto' },
    { titulo: 'Pendencias por titulo', descricao: 'Acompanhamento por vencimento e status financeiro.', indicador: 'A integrar' },
  ];

  readonly empresasDisponiveis = computed(() => this.empresas().slice().sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome)));
  readonly revendasDaEmpresa = computed(() => this.revendas()
    .filter((revenda) => !this.empresaNumero() || revenda.empresaNumero === this.empresaNumero())
    .sort((a, b) => a.empresaNumero - b.empresaNumero || a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda)));
  readonly revendasSelecionadasLabel = computed(() => {
    const selected = this.revendasSelecionadas();
    if (!selected.length) {
      return 'Todas as revendas';
    }

    return selected.slice().sort((a, b) => a - b).map((numero) => `${numero}`).join(', ');
  });

  readonly vendasFiltradas = computed(() => {
    const empresa = this.empresaNumero();
    const revendas = this.revendasSelecionadas();
    return this.vendasFiliais()
      .filter((item) => !empresa || item.empresaNumero === empresa)
      .filter((item) => !revendas.length || revendas.includes(item.revendaNumero));
  });

  readonly metaNovosTotal = computed(() => this.sumFiliais('metaNovos'));
  readonly metaDiretaTotal = computed(() => this.sumFiliais('metaVendaDireta'));
  readonly metaTotal = computed(() => this.metaNovosTotal() + this.metaDiretaTotal());
  readonly novosTotal = computed(() => this.sumFiliais('faturadosNovos'));
  readonly diretaTotal = computed(() => this.sumFiliais('faturadosDireta'));
  readonly seminovosTotal = computed(() => this.sumFiliais('seminovos'));
  readonly unidadesTotal = computed(() => this.novosTotal() + this.diretaTotal() + this.seminovosTotal());
  readonly anunciadosTotal = computed(() => this.sumFiliais('anunciadosNovos') + this.sumFiliais('anunciadosDireta'));
  readonly propostasTotal = computed(() => this.sumFiliais('propostas'));
  readonly baixadosTotal = computed(() => this.sumFiliais('baixados'));
  readonly faturamentoTotal = computed(() => this.sumFiliais('faturamento'));
  readonly margemTotal = computed(() => this.sumFiliais('margem'));
  readonly ticketMedio = computed(() => this.unidadesTotal() ? this.faturamentoTotal() / this.unidadesTotal() : 0);
  readonly atingimento = computed(() => this.metaTotal() ? (this.novosTotal() + this.diretaTotal()) / this.metaTotal() * 100 : 0);
  readonly conversao = computed(() => this.propostasTotal() ? this.unidadesTotal() / this.propostasTotal() * 100 : 0);
  readonly margemPercentual = computed(() => this.faturamentoTotal() ? this.margemTotal() / this.faturamentoTotal() * 100 : 0);

  readonly mixRealizado = computed<ChartSlice[]>(() => [
    { label: 'Novos loja', value: this.novosTotal(), color: '#2563eb' },
    { label: 'Venda direta', value: this.diretaTotal(), color: '#16a34a' },
    { label: 'Seminovos', value: this.seminovosTotal(), color: '#f59e0b' },
  ]);
  readonly mixMeta = computed<ChartSlice[]>(() => [
    { label: 'Meta novos', value: this.metaNovosTotal(), color: '#2563eb' },
    { label: 'Meta direta', value: this.metaDiretaTotal(), color: '#16a34a' },
  ]);
  readonly mixTower = computed(() => [
    { label: 'Novos loja', meta: this.metaNovosTotal(), realizado: this.novosTotal(), color: '#2563eb' },
    { label: 'Venda direta', meta: this.metaDiretaTotal(), realizado: this.diretaTotal(), color: '#16a34a' },
    { label: 'Seminovos', meta: 0, realizado: this.seminovosTotal(), color: '#f59e0b' },
  ]);
  readonly funilSlices = computed<ChartSlice[]>(() => [
    { label: 'Propostas', value: this.propostasTotal(), color: '#2563eb' },
    { label: 'Baixados', value: this.baixadosTotal(), color: '#14b8a6' },
    { label: 'Realizado', value: this.unidadesTotal(), color: '#16a34a' },
  ]);
  readonly entregasPies = computed(() => this.vendasDiariasFiltradas().slice(-6).map((item) => ({
    ...item,
    total: item.novos + item.vendaDireta + item.seminovos,
    slices: [
      { label: 'Novos', value: item.novos, color: '#2563eb' },
      { label: 'Direta', value: item.vendaDireta, color: '#16a34a' },
      { label: 'Seminovos', value: item.seminovos, color: '#f59e0b' },
    ],
  })));
  readonly topModelos = computed(() => this.modelos().slice().sort((a, b) => b.unidades - a.unidades).slice(0, 10));
  readonly vendedoresFiltrados = computed(() => {
    const revendas = new Set(this.vendasFiltradas().map((item) => item.filial));
    return this.vendedores()
      .filter((item) => !revendas.size || revendas.has(item.filial))
      .sort((a, b) => (b.realizado / Math.max(b.meta, 1)) - (a.realizado / Math.max(a.meta, 1)));
  });
  readonly maxFilial = computed(() => Math.max(...this.vendasFiltradas().flatMap((item) => [
    item.metaNovos + item.metaVendaDireta,
    item.faturadosNovos + item.faturadosDireta + item.seminovos,
  ]), 1));
  readonly maxModelo = computed(() => Math.max(...this.topModelos().map((item) => item.unidades), 1));
  readonly maxMixTower = computed(() => Math.max(...this.mixTower().flatMap((item) => [item.meta, item.realizado]), 1));
  readonly vendasPeriodoTotalPages = computed(() => Math.max(1, Math.ceil(this.vendasFiltradas().length / this.vendasPeriodoPageSize)));
  readonly vendasPeriodoPaginadas = computed(() => {
    const page = Math.min(this.vendasPeriodoPage(), this.vendasPeriodoTotalPages());
    const start = (page - 1) * this.vendasPeriodoPageSize;
    return this.vendasFiltradas().slice(start, start + this.vendasPeriodoPageSize);
  });

  readonly vendasDiariasFiltradas = computed(() => {
    const start = this.dataInicio();
    const end = this.dataFim();
    return this.vendasDiarias().filter((item) => item.data >= start && item.data <= end);
  });
  readonly acessoriosQuantidadeTotal = computed(() => this.acessorios().reduce((total, item) => total + item.quantidade, 0));
  readonly acessoriosFaturamentoTotal = computed(() => this.acessorios().reduce((total, item) => total + item.faturamento, 0));
  readonly acessoriosRentabilidadeTotal = computed(() => this.acessorios().reduce((total, item) => total + item.rentabilidade, 0));
  readonly acessoriosTicketMedio = computed(() => this.acessoriosQuantidadeTotal() ? this.acessoriosFaturamentoTotal() / this.acessoriosQuantidadeTotal() : 0);
  readonly acessoriosMargemPercentual = computed(() => this.acessoriosFaturamentoTotal() ? this.acessoriosRentabilidadeTotal() / this.acessoriosFaturamentoTotal() * 100 : 0);
  readonly maxAcessorioFaturamento = computed(() => Math.max(...this.acessorios().map((item) => item.faturamento), 1));
  readonly retornoContratos = computed(() => this.retornoFi()?.contratos ?? 0);
  readonly retornoTotal = computed(() => this.retornoFi()?.retornoTotal ?? 0);
  readonly retornoValorFinanciado = computed(() => this.retornoFi()?.valorFinanciado ?? 0);
  readonly retornoValorVenda = computed(() => this.retornoFi()?.valorVenda ?? 0);
  readonly retornoComissaoTotal = computed(() => this.retornoFi()?.comissaoTotal ?? 0);
  readonly retornoTicketMedio = computed(() => this.retornoContratos() ? this.retornoTotal() / this.retornoContratos() : 0);
  readonly retornoSobreFinanciado = computed(() => this.retornoValorFinanciado() ? this.retornoTotal() / this.retornoValorFinanciado() * 100 : 0);
  readonly retornoSobreVenda = computed(() => this.retornoValorVenda() ? this.retornoValorFinanciado() / this.retornoValorVenda() * 100 : 0);
  readonly maxRetornoFinanceira = computed(() => Math.max(...(this.retornoFi()?.financeiras ?? []).map((item) => item.retorno), 1));
  readonly maxRetornoVendedor = computed(() => Math.max(...(this.retornoFi()?.vendedores ?? []).map((item) => item.retorno), 1));
  readonly retornoFinanceiroSlices = computed<ChartSlice[]>(() => [
    { label: 'Retorno', value: this.retornoTotal(), color: '#16a34a' },
    { label: 'Financiado', value: Math.max(this.retornoValorFinanciado() - this.retornoTotal(), 0), color: '#2563eb' },
  ]);
  readonly retornoParcelasSlices = computed<ChartSlice[]>(() => {
    const colors = ['#2563eb', '#16a34a', '#f59e0b', '#14b8a6', '#7c3aed', '#ef4444', '#64748b'];
    return (this.retornoFi()?.parcelas ?? []).map((item, index) => ({
      label: item.nome,
      value: item.quantidade,
      color: colors[index % colors.length],
    }));
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
      next: (empresas) => this.empresas.set(empresas),
      error: (error) => {
        this.empresas.set([]);
        this.toastr.error(this.getErrorMessage('Nao foi possivel carregar as empresas.', error), 'B.I Veiculos');
      },
    });
  }

  loadRevendas(): void {
    this.unidadesService.list().subscribe({
      next: (revendas) => this.revendas.set(revendas),
      error: (error) => {
        this.revendas.set([]);
        this.toastr.error(this.getErrorMessage('Nao foi possivel carregar as revendas.', error), 'B.I Veiculos');
      },
    });
  }

  load(): void {
    if (this.dataInicio() > this.dataFim()) {
      this.toastr.warning('A data inicial nao pode ser maior que a data final.', 'Periodo invalido');
      return;
    }

    this.loading.set(true);
    void this.spinner.show();
    this.veiculosBiService.loadDashboard(this.dashboardFilter()).subscribe({
      next: (data) => {
        this.applyDashboard(data);
        this.loadAcessorios();
        this.loadRetornoFi();
        this.toastr.success('B.I de veiculos atualizado.', 'Atualizacao concluida');
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: (error) => {
        this.vendasFiliais.set([]);
        this.vendasDiarias.set([]);
        this.modelos.set([]);
        this.vendedores.set([]);
        this.acessorios.set([]);
        this.retornoFi.set(null);
        this.atualizadoEm.set(new Date().toISOString());
        this.toastr.error(this.getErrorMessage('Nao foi possivel carregar o B.I de veiculos.', error), 'Erro');
        this.loading.set(false);
        void this.spinner.hide();
      },
    });
  }

  private dashboardFilter(): { dataInicio: string; dataFim: string; empresa: number | null; revenda: number[] } {
    return {
      dataInicio: this.dataInicio(),
      dataFim: this.dataFim(),
      empresa: this.empresaNumero(),
      revenda: this.revendasSelecionadas(),
    };
  }

  private applyDashboard(data: VeiculosBiDashboard): void {
    this.vendasFiliais.set(data.filiais ?? []);
    this.vendasDiarias.set(data.vendasDiarias ?? []);
    this.modelos.set(data.modelos ?? []);
    this.vendedores.set(data.vendedores ?? []);
    this.atualizadoEm.set(data.atualizadoEm || new Date().toISOString());
  }

  private defaultStartDate(): string {
    const date = new Date();
    date.setDate(1);
    return this.toDateInputValue(date);
  }

  private defaultEndDate(): string {
    return this.toDateInputValue(new Date());
  }

  private toDateInputValue(date: Date): string {
    return [
      date.getFullYear(),
      String(date.getMonth() + 1).padStart(2, '0'),
      String(date.getDate()).padStart(2, '0'),
    ].join('-');
  }

  setEmpresa(value: string): void {
    const numero = Number(value);
    this.empresaNumero.set(Number.isFinite(numero) && numero > 0 ? numero : null);
    this.revendasSelecionadas.set([]);
    this.vendasPeriodoPage.set(1);
  }

  toggleRevenda(numero: number): void {
    const selected = new Set(this.revendasSelecionadas());
    selected.has(numero) ? selected.delete(numero) : selected.add(numero);
    this.revendasSelecionadas.set([...selected].sort((a, b) => a - b));
    this.vendasPeriodoPage.set(1);
  }

  clearRevendas(): void {
    this.revendasSelecionadas.set([]);
    this.vendasPeriodoPage.set(1);
    this.revendaPickerOpen.set(false);
  }

  previousVendasPeriodoPage(): void {
    this.vendasPeriodoPage.set(Math.max(1, this.vendasPeriodoPage() - 1));
  }

  nextVendasPeriodoPage(): void {
    this.vendasPeriodoPage.set(Math.min(this.vendasPeriodoTotalPages(), this.vendasPeriodoPage() + 1));
  }

  setTab(tab: VeiculosBiTab): void {
    this.activeTab.set(tab);
  }

  isRevendaSelected(numero: number): boolean {
    return this.revendasSelecionadas().includes(numero);
  }

  percent(value: number, max: number): number {
    return Math.min(100, Math.max(2, value / Math.max(max, 1) * 100));
  }

  towerHeight(value: number, max: number): number {
    return value ? Math.min(100, Math.max(4, value / Math.max(max, 1) * 100)) : 0;
  }

  metaPercentual(meta: VendedorMeta): number {
    return meta.meta ? meta.realizado / meta.meta * 100 : 0;
  }

  retornoPercentual(item: VeiculosBiRetornoFiGrupo): number {
    return item.valorFinanciado ? item.retorno / item.valorFinanciado * 100 : 0;
  }

  filialAtingimento(item: FilialVenda): number {
    const meta = item.metaNovos + item.metaVendaDireta;
    return meta ? (item.faturadosNovos + item.faturadosDireta + item.seminovos) / meta * 100 : 0;
  }

  pieBackground(slices: ChartSlice[]): string {
    const total = slices.reduce((sum, item) => sum + item.value, 0);
    if (!total) {
      return 'conic-gradient(#e5e7eb 0 100%)';
    }

    let start = 0;
    const stops = slices.map((item) => {
      const end = start + item.value / total * 100;
      const stop = `${item.color} ${start}% ${end}%`;
      start = end;
      return stop;
    });
    return `conic-gradient(${stops.join(', ')})`;
  }

  slicePath(slices: ChartSlice[], index: number): string {
    const total = slices.reduce((sum, item) => sum + item.value, 0);
    const item = slices[index];
    if (!total || !item) {
      return '';
    }

    const startPercent = slices.slice(0, index).reduce((sum, slice) => sum + slice.value / total * 100, 0);
    const endPercent = startPercent + item.value / total * 100;
    return this.describePieSlice(60, 60, 47, startPercent, endPercent);
  }

  slicePercent(slices: ChartSlice[], value: number): number {
    const total = slices.reduce((sum, item) => sum + item.value, 0);
    return total ? value / total * 100 : 0;
  }

  sliceLabelX(slices: ChartSlice[], index: number): number {
    return 60 + Math.cos(this.sliceMidAngle(slices, index)) * 29;
  }

  sliceLabelY(slices: ChartSlice[], index: number): number {
    return 60 + Math.sin(this.sliceMidAngle(slices, index)) * 29;
  }

  sliceLabelTransform(slices: ChartSlice[], index: number): string {
    const x = this.sliceLabelX(slices, index);
    const y = this.sliceLabelY(slices, index);
    return `rotate(0 ${x} ${y})`;
  }

  sliceTransform(slices: ChartSlice[], index: number, key: string): string {
    if (this.hoveredSlice() !== key) {
      return '';
    }

    const angle = this.sliceMidAngle(slices, index);
    const distance = 6;
    return `translate(${Math.cos(angle) * distance} ${Math.sin(angle) * distance})`;
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumFractionDigits: 0 }).format(value);
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('pt-BR').format(Math.round(value));
  }

  formatPercent(value: number): string {
    return `${value.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
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
  closeMenusOnDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.profile-area')) {
      this.profileMenuOpen.set(false);
    }
    if (!target?.closest('.revenda-picker')) {
      this.revendaPickerOpen.set(false);
    }
  }

  private rebuildDashboard(): void {
    const filiais = this.sourceRevendas().map((revenda, index) => this.mockFilial(revenda, index));
    this.vendasFiliais.set(filiais);
    this.vendasDiarias.set(this.mockVendasDiarias(filiais));
    this.modelos.set(this.mockModelos());
    this.vendedores.set(this.mockVendedores(filiais));
  }

  private loadAcessorios(): void {
    this.veiculosBiService.loadAcessorios({
      dataInicio: this.dataInicio(),
      dataFim: this.dataFim(),
      empresa: this.empresaNumero(),
      revenda: this.revendasSelecionadas(),
    }).subscribe({
      next: (items) => this.acessorios.set(items),
      error: (error) => {
        this.acessorios.set([]);
        this.toastr.error(this.getErrorMessage('Nao foi possivel carregar os acessorios.', error), 'B.I Veiculos');
      },
    });
  }

  private loadRetornoFi(): void {
    this.veiculosBiService.loadRetornoFi(this.dashboardFilter()).subscribe({
      next: (data) => this.retornoFi.set(data),
      error: (error) => {
        this.retornoFi.set(null);
        this.toastr.error(this.getErrorMessage('Nao foi possivel carregar os retornos F&I.', error), 'B.I Veiculos');
      },
    });
  }

  private getErrorMessage(fallback: string, error: unknown): string {
    if (typeof error === 'object' && error && 'error' in error) {
      const body = (error as { error?: unknown }).error;
      if (typeof body === 'string' && body.trim()) {
        return body;
      }

      if (typeof body === 'object' && body && 'message' in body) {
        const message = (body as { message?: unknown }).message;
        if (typeof message === 'string' && message.trim()) {
          return message;
        }
      }
    }

    return fallback;
  }

  private sourceRevendas(): Unidade[] {
    const cadastradas = this.revendas();
    if (cadastradas.length) {
      return cadastradas;
    }

    return [
      { id: 1, nome: 'Matriz', empresaId: 1, empresaNumero: 6, numeroRevenda: 1, empresa: 'Empresa 6', revenda: 'Cachoeira', cnpj: '', endereco: '', dataCadastro: '' },
      { id: 2, nome: 'Filial', empresaId: 1, empresaNumero: 6, numeroRevenda: 2, empresa: 'Empresa 6', revenda: 'Gramado', cnpj: '', endereco: '', dataCadastro: '' },
      { id: 3, nome: 'Filial', empresaId: 1, empresaNumero: 6, numeroRevenda: 3, empresa: 'Empresa 6', revenda: 'Iguatemi', cnpj: '', endereco: '', dataCadastro: '' },
      { id: 4, nome: 'Filial', empresaId: 1, empresaNumero: 6, numeroRevenda: 4, empresa: 'Empresa 6', revenda: 'Osorio', cnpj: '', endereco: '', dataCadastro: '' },
    ];
  }

  private mockFilial(revenda: Unidade, index: number): FilialVenda {
    const base = 12 + (index % 7) * 5;
    const direct = 5 + (index % 5) * 4;
    const delivered = Math.max(4, Math.round(base * (0.68 + (index % 4) * 0.08)));
    const directDone = Math.max(2, Math.round(direct * (0.62 + (index % 3) * 0.1)));
    const seminovos = 2 + (index % 6) * 3;
    const ticket = 118000 + (index % 6) * 14500;

    return {
      empresaNumero: revenda.empresaNumero,
      empresaNome: revenda.empresa || `Empresa ${revenda.empresaNumero}`,
      revendaNumero: revenda.numeroRevenda,
      filial: revenda.revenda || revenda.nome || `Revenda ${revenda.numeroRevenda}`,
      metaNovos: base,
      metaVendaDireta: direct,
      anunciadosNovos: delivered + 3,
      faturadosNovos: delivered,
      anunciadosDireta: directDone + 2,
      faturadosDireta: directDone,
      seminovos,
      propostas: delivered + directDone + seminovos + 12 + index,
      baixados: delivered + directDone + seminovos + 7,
      faturamento: (delivered + directDone + seminovos) * ticket,
      margem: (delivered + directDone + seminovos) * ticket * (0.072 + (index % 4) * 0.006),
    };
  }

  private mockVendasDiarias(filiais: FilialVenda[]): VendaDiaria[] {
    const totalNovos = filiais.reduce((total, item) => total + item.faturadosNovos, 0);
    const totalDireta = filiais.reduce((total, item) => total + item.faturadosDireta, 0);
    const totalSeminovos = filiais.reduce((total, item) => total + item.seminovos, 0);
    return [1, 5, 9, 13, 17, 21, 25, 29].map((day, index) => ({
      data: `2026-05-${String(day).padStart(2, '0')}`,
      novos: Math.max(1, Math.round(totalNovos * (0.06 + index * 0.011))),
      vendaDireta: Math.max(1, Math.round(totalDireta * (0.05 + index * 0.01))),
      seminovos: Math.max(1, Math.round(totalSeminovos * (0.045 + index * 0.008))),
    }));
  }

  private mockModelos(): ModeloRanking[] {
    const names = ['Kicks Advance', 'Frontier Platinum', 'Versa Exclusive', 'Sentra Advance', 'Pulse Audace', 'Fastback Impetus', '208 Allure', 'Partner Rapid', 'Oroch Outsider', 'March SV', 'Argo Trekking', 'Toro Volcano'];
    return names.map((modelo, index) => {
      const unidades = 44 - index * 3 + (index % 2);
      return {
        modelo,
        familia: index % 3 === 0 ? 'SUV' : index % 3 === 1 ? 'Pickup' : 'Hatch/Sedan',
        unidades,
        faturamento: unidades * (112000 + index * 7800),
        margemPercentual: 7.1 + (index % 5) * 0.6,
      };
    });
  }

  private mockVendedores(filiais: FilialVenda[]): VendedorMeta[] {
    const nomes = ['Ana Costa', 'Bruno Lima', 'Carla Souza', 'Diego Rocha', 'Fernanda Alves', 'Gustavo Melo', 'Helena Prado', 'Igor Martins'];
    return nomes.map((vendedor, index) => {
      const filial = filiais[index % Math.max(filiais.length, 1)];
      const meta = 18 + (index % 4) * 4;
      const realizado = Math.max(5, Math.round(meta * (0.72 + (index % 5) * 0.07)));
      return {
        vendedor,
        filial: filial?.filial ?? 'Sem filial',
        meta,
        realizado,
        faturamento: realizado * (125000 + index * 9000),
      };
    });
  }

  private sumFiliais(field: keyof Pick<FilialVenda, 'metaNovos' | 'metaVendaDireta' | 'faturadosNovos' | 'faturadosDireta' | 'seminovos' | 'anunciadosNovos' | 'anunciadosDireta' | 'propostas' | 'baixados' | 'faturamento' | 'margem'>): number {
    return this.vendasFiltradas().reduce((total, item) => total + Number(item[field] ?? 0), 0);
  }

  private sliceMidAngle(slices: ChartSlice[], index: number): number {
    const total = slices.reduce((sum, item) => sum + item.value, 0);
    if (!total) {
      return -Math.PI / 2;
    }

    const start = slices.slice(0, index).reduce((sum, item) => sum + item.value / total * 100, 0);
    const size = (slices[index]?.value ?? 0) / total * 100;
    return ((start + size / 2) / 100) * Math.PI * 2 - Math.PI / 2;
  }

  private describePieSlice(cx: number, cy: number, radius: number, startPercent: number, endPercent: number): string {
    if (endPercent - startPercent >= 99.999) {
      return `M ${cx} ${cy - radius} A ${radius} ${radius} 0 1 1 ${cx} ${cy + radius} A ${radius} ${radius} 0 1 1 ${cx} ${cy - radius} Z`;
    }

    const start = this.pointOnCircle(cx, cy, radius, startPercent);
    const end = this.pointOnCircle(cx, cy, radius, endPercent);
    const largeArcFlag = endPercent - startPercent > 50 ? 1 : 0;
    return `M ${cx} ${cy} L ${start.x} ${start.y} A ${radius} ${radius} 0 ${largeArcFlag} 1 ${end.x} ${end.y} Z`;
  }

  private pointOnCircle(cx: number, cy: number, radius: number, percent: number): { x: number; y: number } {
    const angle = (percent / 100) * Math.PI * 2 - Math.PI / 2;
    return {
      x: cx + Math.cos(angle) * radius,
      y: cy + Math.sin(angle) * radius,
    };
  }
}
