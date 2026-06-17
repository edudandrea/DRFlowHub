import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AutoRefreshControlComponent } from '../../core/auto-refresh-control.component';
import { AuthService } from '../../core/auth.service';
import { Empresa, Unidade } from '../../core/models';
import { PecasBiData, PecaCanalDetalhe, PecaVendaMensal, PecaVendedor, PecasBiService } from '../../core/pecas-bi.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';

const ALLOWED_CHANNELS = ['P21', 'P23', 'P41', 'G21', 'P71'] as const;
const EXCLUDED_CHART_CHANNELS = new Set(['F21', 'P51', 'O21']);
const PECAS_BI_EMPRESA_BY_ACCESS: Record<string, number> = {
  'pecas-bi-renault': 1,
  'pecas-bi-nissan': 2,
  'pecas-bi-gm': 5,
  'pecas-bi-fiat': 6,
  'pecas-bi-peugeot-citroen': 7,
  'pecas-bi-bajaj': 8,
  'pecas-bi-geely': 9,
  'pecas-bi-mg': 10,
};

@Component({
  selector: 'app-pecas-bi',
  imports: [DatePipe, FormsModule, AutoRefreshControlComponent],
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
  readonly empresasPermitidasServidor = signal<number[] | null>(null);
  readonly revendasPermitidasServidor = signal<number[] | null>(null);
  readonly escopoServidorCarregado = signal(false);
  readonly empresaNumero = signal<number | null>(null);
  readonly revendasSelecionadas = signal<number[]>([]);
  readonly dataInicio = signal(this.toDateInput(this.firstDayOfCurrentMonth()));
  readonly dataFim = signal(this.toDateInput(new Date()));
  readonly rankingDataInicio = signal(this.toDateInput(this.firstDayOfCurrentMonth()));
  readonly rankingDataFim = signal(this.toDateInput(new Date()));
  readonly canaisSelecionados = signal<string[]>([]);
  readonly metaModalSeller = signal<PecaVendedor | null>(null);
  readonly metaDraft = signal<number>(0);
  readonly metaDataInicioDraft = signal('');
  readonly metaDataFimDraft = signal('');
  readonly savingMeta = signal(false);
  readonly hoveredChannel = signal<string | null>(null);
  readonly channelReportOpen = signal(false);
  readonly channelReportLoading = signal(false);
  readonly channelReportName = signal('');
  readonly channelReportItems = signal<PecaCanalDetalhe[]>([]);
  readonly revendaPickerOpen = signal(false);
  readonly canalPickerOpen = signal(false);
  readonly pecaSortField = signal<'nome' | 'quantidade' | 'faturamento'>('faturamento');
  readonly expandedSellerKey = signal<string | null>(null);
  readonly metaMesInicio = signal(this.monthInputValue(new Date(new Date().getFullYear(), 0, 1)));
  readonly metaMesFim = signal(this.monthInputValue(new Date(new Date().getFullYear(), 11, 1)));

  readonly canais = computed(() => this.canShowGmTransactions()
    ? [...ALLOWED_CHANNELS]
    : ALLOWED_CHANNELS.filter((canal) => canal !== 'G21' && canal !== 'P71'));
  readonly vendas = computed(() => this.data()?.vendasMensais ?? []);
  readonly categorias = computed(() => this.data()?.categorias ?? []);
  readonly pecas = computed(() => this.data()?.pecas ?? []);
  readonly vendedores = computed(() => this.data()?.vendedores ?? []);
  readonly canaisData = computed(() => (this.data()?.canais ?? []).filter((item) => {
    const canal = item.nome?.trim().toUpperCase() ?? '';
    return !EXCLUDED_CHART_CHANNELS.has(canal)
      && canal !== 'P47'
      && (this.canShowGmTransactions() || (canal !== 'G21' && canal !== 'P71'));
  }));
  readonly clientes = computed(() => this.data()?.clientes ?? []);
  readonly seguradoras = computed(() => this.data()?.seguradoras ?? []);
  readonly canViewSellerRanking = computed(() => this.data()?.podeVerRankingVendedores ?? false);
  readonly canViewClientRanking = computed(() => this.canViewSellerRanking() || this.isVendedorPecas());
  readonly isGerenteEmpresaPecas = computed(() => this.user()?.role === 'Gerente de Pecas');
  readonly isGerenteGeralPecas = computed(() => this.user()?.role === 'Gerente Geral de Pecas');
  readonly isVendedorPecas = computed(() => this.user()?.role === 'Vendedor de Pecas');
  readonly userEmpresaNumero = computed(() => this.revendas().find((revenda) => revenda.id === this.user()?.unidadeId)?.empresaNumero ?? null);
  readonly hasPecasBiAcessoGeral = computed(() => {
    const user = this.user();
    const acessos = user?.acessos ?? [];
    const hasEmpresaSpecificAccess = acessos.some((acesso) => Number.isFinite(PECAS_BI_EMPRESA_BY_ACCESS[acesso]));
    return user?.role === 'Admin'
      || user?.role === 'TI'
      || this.isGerenteGeralPecas()
      || (user?.acessos ?? []).includes('pecas-admin')
      || ((user?.acessos ?? []).includes('vendas-pecas') && !hasEmpresaSpecificAccess);
  });
  readonly empresasPermitidasPorPerfil = computed(() => {
    const empresasServidor = this.empresasPermitidasServidor();
    if (this.escopoServidorCarregado() && empresasServidor === null) {
      return null;
    }

    if (empresasServidor) {
      return empresasServidor;
    }

    if (this.hasPecasBiAcessoGeral()) {
      return null;
    }

    const empresas = (this.user()?.acessos ?? [])
      .map((acesso) => PECAS_BI_EMPRESA_BY_ACCESS[acesso])
      .filter((empresa): empresa is number => Number.isFinite(empresa));

    return [...new Set(empresas)];
  });
  readonly canUseEmpresaRevendaFilters = computed(() => true);
  readonly hasAcessoGeralServidor = computed(() => this.escopoServidorCarregado() && this.empresasPermitidasServidor() === null);
  readonly isEmpresaFilterLocked = computed(() => !this.hasAcessoGeralServidor() && (this.isGerenteEmpresaPecas() || (this.empresasPermitidasPorPerfil()?.length === 1)));
  readonly gerenteEmpresaNumeroEfetiva = computed(() => this.hasAcessoGeralServidor() ? null : this.empresasPermitidasPorPerfil()?.[0] ?? this.userEmpresaNumero());
  readonly empresasDisponiveis = computed(() => {
    if (this.isGerenteEmpresaPecas() && !this.hasAcessoGeralServidor()) {
      const empresaNumero = this.gerenteEmpresaNumeroEfetiva();
      return this.empresas().filter((empresa) => empresa.numero === empresaNumero);
    }

    const empresasPermitidas = this.empresasPermitidasPorPerfil();
    if (empresasPermitidas) {
      return this.empresas().filter((empresa) => empresasPermitidas.includes(empresa.numero));
    }

    return this.empresas();
  });
  readonly revendasDaEmpresa = computed(() => {
    const empresa = this.isGerenteEmpresaPecas() ? this.gerenteEmpresaNumeroEfetiva() : this.empresaNumero();
    const empresasPermitidas = this.empresasPermitidasPorPerfil();
    const revendasPermitidas = this.revendasPermitidasServidor();
    return this.revendas()
      .filter((revenda) => !empresa || revenda.empresaNumero === empresa)
      .filter((revenda) => !empresasPermitidas || empresasPermitidas.includes(revenda.empresaNumero))
      .filter((revenda) => !revendasPermitidas || revendasPermitidas.includes(revenda.numeroRevenda))
      .sort((a, b) => a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda));
  });
  readonly revendasSelecionadasLabel = computed(() => {
    const selected = this.revendasSelecionadas();
    if (!selected.length) {
      return 'Todas as revendas';
    }

    return selected
      .slice()
      .sort((a, b) => a - b)
      .map((numero) => `${numero}`)
      .join(', ');
  });
  readonly canaisSelecionadosLabel = computed(() => {
    const selected = this.canaisSelecionados();
    if (!selected.length) {
      return 'Todas as transacoes';
    }

    return selected.map((canal) => this.channelLabel(canal)).join(', ');
  });
  readonly faturamentoTotal = computed(() => this.vendas().reduce((total, item) => total + item.faturamento, 0));
  readonly margemTotal = computed(() => this.vendas().reduce((total, item) => total + item.margem, 0));
  readonly rentabilidadeTotal = computed(() => this.vendas().reduce((total, item) => total + item.rentabilidade, 0));
  readonly quantidadeTotal = computed(() => this.vendas().reduce((total, item) => total + item.quantidade, 0));
  readonly ticketMedio = computed(() => this.quantidadeTotal() ? this.faturamentoTotal() / this.quantidadeTotal() : 0);
  readonly margemPercentual = computed(() => this.faturamentoTotal() ? (this.margemTotal() / this.faturamentoTotal()) * 100 : 0);
  readonly rentabilidadePercentual = computed(() => {
    const faturamento = this.faturamentoTotal();
    if (!faturamento) {
      return 0;
    }

    return (this.rentabilidadeTotal() / faturamento) * 100;
  });
  readonly margemLiquida = computed(() => this.faturamentoTotal() * (this.rentabilidadePercentual() / 100));
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
    return percent < 70 ? 'danger' : 'warning';
  });
  readonly metaMessage = computed(() => {
    const status = this.metaStatus();
    if (status === 'success') {
      return 'Meta atingida! Excelente venda, parabens pelo resultado. Continue nesse ritmo!';
    }

    if (status === 'warning') {
      return 'Você está perto da meta. Falta pouco para fechar o período com chave de ouro.';
    }

    return 'A meta ainda precisa de atencao. Foque nas oportunidades abertas e acompanhe sua evolucao.';
  });
  readonly pecasOrdenadas = computed(() => {
    const field = this.pecaSortField();
    return this.pecas().slice().sort((a, b) => {
      if (field === 'nome') {
        return a.nome.localeCompare(b.nome, 'pt-BR', { sensitivity: 'base' });
      }

      return (b[field] ?? 0) - (a[field] ?? 0);
    });
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
      next: (empresas) => {
        this.empresas.set(empresas.slice().sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome)));
        this.applyUserScopeDefaults();
      },
      error: () => this.toastr.error('Não foi possível carregar as empresas cadastradas.', 'B.I Peças'),
    });
  }

  loadRevendas(): void {
    this.unidadesService.list().subscribe({
      next: (revendas) => {
        this.revendas.set(revendas);
        this.applyUserScopeDefaults();
      },
      error: () => this.toastr.error('Não foi possível carregar as revendas cadastradas.', 'B.I Peças'),
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.load({
      dataInicio: this.dataInicio(),
      dataFim: this.dataFim(),
      rankingDataInicio: this.rankingDataInicio(),
      rankingDataFim: this.rankingDataFim(),
      empresa: this.empresaNumero(),
      revenda: this.revendasSelecionadas(),
      canal: this.canaisSelecionados(),
    }).subscribe({
      next: (data) => {
        this.empresasPermitidasServidor.set(data.empresasPermitidas ?? null);
        this.revendasPermitidasServidor.set(data.revendasPermitidas ?? null);
        this.escopoServidorCarregado.set(true);
        this.applyUserScopeDefaults();
        this.data.set(data);
        this.expandedSellerKey.set(null);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar o B.I de venda de peças.', 'Erro');
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

  channelColor(index: number): string {
    return ['#06a6c8', '#2f7ed8', '#f5a623', '#78b800', '#6f7f8c', '#d84f8f', '#7b61ff'][index % 7];
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

  channelTransform(index: number): string {
    const item = this.canaisData()[index];
    if (!item || this.hoveredChannel() !== item.nome) {
      return '';
    }

    const angle = this.channelMidAngle(index);
    const distance = 5;
    return `translate(${Math.cos(angle) * distance} ${Math.sin(angle) * distance})`;
  }

  channelSlicePath(index: number): string {
    const total = this.canaisTotal();
    const item = this.canaisData()[index];
    if (!total || !item) {
      return '';
    }

    const startPercent = this.channelOffsetPercent(index);
    const endPercent = startPercent + this.percentOfTotal(item.faturamento, total);
    return this.describePieSlice(60, 60, 47, startPercent, endPercent);
  }

  channelLabelX(index: number): number {
    return 60 + Math.cos(this.channelMidAngle(index)) * 29;
  }

  channelLabelY(index: number): number {
    return 60 + Math.sin(this.channelMidAngle(index)) * 29;
  }

  shouldShowChannelClients(item: { nome: string }): boolean {
    return this.hoveredChannel() === item.nome;
  }

  shouldShowChannelPercent(item: { faturamento: number }): boolean {
    return this.percentOfTotal(item.faturamento, this.canaisTotal()) >= 7;
  }

  hoveredChannelValue(): number {
    const channel = this.hoveredChannel();
    return this.canaisData().find((item) => item.nome === channel)?.faturamento ?? this.canaisTotal();
  }

  hoveredChannelLabel(): string {
    const channel = this.hoveredChannel();
    return channel ? this.channelLabel(channel) : 'Total';
  }

  hoveredChannelClients(): number {
    const channel = this.hoveredChannel();
    return this.canaisData().find((item) => item.nome === channel)?.clientesAtendidos ?? 0;
  }

  openChannelReport(channel: string): void {
    if (!channel) {
      return;
    }

    this.channelReportName.set(channel);
    this.channelReportOpen.set(true);
    this.channelReportLoading.set(true);
    this.channelReportItems.set([]);

    this.service.loadCanalDetalhes(channel, {
      dataInicio: this.dataInicio(),
      dataFim: this.dataFim(),
      empresa: this.empresaNumero(),
      revenda: this.revendasSelecionadas(),
      canal: channel,
    }).subscribe({
      next: (items) => {
        this.channelReportItems.set(items);
        this.channelReportLoading.set(false);
      },
      error: () => {
        this.channelReportLoading.set(false);
        this.toastr.error('Não foi possível gerar o relatório do canal.', 'B.I Peças');
      },
    });
  }

  closeChannelReport(): void {
    this.channelReportOpen.set(false);
    this.channelReportName.set('');
    this.channelReportItems.set([]);
  }

  downloadChannelReportPdf(): void {
    if (!this.isBrowser || !this.channelReportItems().length) {
      return;
    }

    const title = `Relatório do canal - ${this.channelLabel(this.channelReportName())}`;
    const rows = this.channelReportItems().map((item) => `
      <tr>
        <td>${this.escapeHtml(item.cliente)}</td>
        <td>${this.escapeHtml(item.numeroNotaFiscal)}</td>
        <td>${this.formatDate(item.data)}</td>
        <td>${this.formatMoney(item.faturamento)}</td>
      </tr>
    `).join('');
    const report = window.open('', '_blank', 'width=1100,height=800');
    if (!report) {
      this.toastr.warning('Permita pop-ups para gerar o PDF.', 'Relatório');
      return;
    }

    report.document.write(`
      <html>
        <head>
          <title>${this.escapeHtml(title)}</title>
          <style>
            body { font-family: Arial, sans-serif; margin: 28px; color: #111827; }
            h1 { margin: 0 0 18px; font-size: 22px; }
            table { width: 100%; border-collapse: collapse; }
            th, td { padding: 10px 12px; border-bottom: 1px solid #d1d5db; text-align: left; font-size: 12px; }
            th { background: #f3f4f6; text-transform: uppercase; }
            td:last-child, th:last-child { text-align: right; }
          </style>
        </head>
        <body>
          <h1>${this.escapeHtml(title)}</h1>
          <table>
            <thead><tr><th>Cliente</th><th>Nota fiscal</th><th>Data</th><th>Faturamento</th></tr></thead>
            <tbody>${rows}</tbody>
          </table>
        </body>
      </html>
    `);
    report.document.close();
    report.focus();
    report.print();
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
      G21: 'Venda em Garantia',
      P71: 'Venda site Chevrolet',
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
    return percent < 70 ? 'danger' : 'warning';
  }

  metaColor(): string {
    return this.statusColor(this.metaStatus());
  }

  metaBackground(): string {
    return this.metaStatus() === 'success' ? 'var(--color-brand-green-soft)' : 'var(--color-surface)';
  }

  sellerGoalColor(seller: PecaVendedor): string {
    return this.statusColor(this.sellerGoalClass(seller));
  }

  sellerKey(seller: PecaVendedor): string {
    return seller.cpfVendedor || seller.nome;
  }

  toggleSellerDetails(seller: PecaVendedor): void {
    const key = this.sellerKey(seller);
    this.expandedSellerKey.set(this.expandedSellerKey() === key ? null : key);
  }

  isSellerExpanded(seller: PecaVendedor): boolean {
    return this.expandedSellerKey() === this.sellerKey(seller);
  }

  sellerMetaDiaria(seller: PecaVendedor): number {
    return this.sellerGoalSummary(seller).metaDiaOriginal;
  }

  sellerDailyRows(seller: PecaVendedor): { data: Date; faturamento: number; pedidos: number; metaDiaria: number }[] {
    const start = this.safeDate(this.rankingDataInicio());
    const end = this.safeDate(this.rankingDataFim());
    if (!start || !end || start > end) {
      return [];
    }

    const vendasPorDia = new Map((seller.vendasDiarias ?? []).map((item) => [this.toDateInput(new Date(item.data)), item]));
    const metaDiaria = this.sellerGoalSummary(seller).novoValorDiario;
    const rows: { data: Date; faturamento: number; pedidos: number; metaDiaria: number }[] = [];
    const cursor = new Date(start);

    while (cursor <= end) {
      const key = this.toDateInput(cursor);
      const venda = vendasPorDia.get(key);
      rows.push({
        data: new Date(cursor),
        faturamento: venda?.faturamento ?? 0,
        pedidos: venda?.pedidos ?? 0,
        metaDiaria,
      });
      cursor.setDate(cursor.getDate() + 1);
    }

    return rows;
  }

  sellerDailyPercent(value: number, seller: PecaVendedor): number {
    const target = this.sellerGoalSummary(seller).novoValorDiario;
    return target > 0 ? this.progressWidth((value / target) * 100) : 0;
  }

  sellerGoalSummary(seller: PecaVendedor): {
    meta: number;
    faturamentoAtual: number;
    saldoRestante: number;
    diasUteis: number;
    diasTrabalhados: number;
    diasFaltantes: number;
    metaDiaOriginal: number;
    novoValorDiario: number;
  } {
    const meta = seller.metaVendas || 0;
    const faturamentoAtual = seller.faturamento || 0;
    const saldoRestante = Math.max(meta - faturamentoAtual, 0);
    const start = this.safeDate(seller.metaDataInicio) ?? this.safeDate(this.rankingDataInicio());
    const end = this.safeDate(seller.metaDataFim) ?? this.safeDate(this.rankingDataFim());

    if (!start || !end || start > end) {
      return {
        meta,
        faturamentoAtual,
        saldoRestante,
        diasUteis: 0,
        diasTrabalhados: 0,
        diasFaltantes: 0,
        metaDiaOriginal: 0,
        novoValorDiario: 0,
      };
    }

    const today = this.startOfDay(new Date());
    const workedUntil = today < start ? new Date(start.getTime() - 86400000) : today > end ? end : today;
    const diasUteis = this.businessDaysBetween(start, end);
    const diasTrabalhados = workedUntil < start ? 0 : this.businessDaysBetween(start, workedUntil);
    const diasFaltantes = Math.max(diasUteis - diasTrabalhados, 0);

    return {
      meta,
      faturamentoAtual,
      saldoRestante,
      diasUteis,
      diasTrabalhados,
      diasFaltantes,
      metaDiaOriginal: meta > 0 && diasUteis > 0 ? meta / diasUteis : 0,
      novoValorDiario: saldoRestante > 0 && diasFaltantes > 0 ? saldoRestante / diasFaltantes : 0,
    };
  }

  sellerGoalChartHeight(value: number, seller: PecaVendedor): number {
    const summary = this.sellerGoalSummary(seller);
    const max = Math.max(summary.meta, summary.faturamentoAtual, summary.saldoRestante, 1);
    return Math.max(4, (value / max) * 100);
  }

  sellerMonthlyGoals(seller: PecaVendedor): { mes: Date; valorMeta: number }[] {
    const start = this.safeMonth(this.metaMesInicio());
    const end = this.safeMonth(this.metaMesFim());
    if (!start || !end || start > end) {
      return [];
    }

    return (seller.metasMensais ?? [])
      .map((item) => ({
        mes: this.safeMonth(item.mes) ?? this.safeDate(item.mes) ?? new Date(0),
        valorMeta: item.valorMeta,
      }))
      .filter((item) => item.mes.getTime() > 0 && item.mes >= start && item.mes <= end)
      .sort((a, b) => a.mes.getTime() - b.mes.getTime());
  }

  sellerMonthlyGoalHeight(value: number, seller: PecaVendedor): number {
    const max = Math.max(...this.sellerMonthlyGoals(seller).map((item) => item.valorMeta), 1);
    return Math.max(4, (value / max) * 100);
  }

  pecaSortLabel(): string {
    const labels: Record<'nome' | 'quantidade' | 'faturamento', string> = {
      nome: 'Nome do item',
      quantidade: 'Quantidade',
      faturamento: 'Valor do item',
    };

    return labels[this.pecaSortField()];
  }

  private statusColor(status: string): string {
    if (status === 'success') {
      return 'var(--color-brand-green-strong)';
    }

    return status === 'warning' ? '#f59e0b' : '#dc2626';
  }

  private progressWidth(value: number): number {
    return Math.max(0, Math.min(value, 100));
  }

  private safeDate(value: string | null | undefined): Date | null {
    if (!value) {
      return null;
    }

    const [year, month, day] = value.slice(0, 10).split('-').map((part) => Number(part));
    if (!year || !month || !day) {
      return null;
    }

    return new Date(year, month - 1, day);
  }

  private safeMonth(value: string | null | undefined): Date | null {
    if (!value) {
      return null;
    }

    const [year, month] = value.slice(0, 7).split('-').map((part) => Number(part));
    if (!year || !month) {
      return null;
    }

    return new Date(year, month - 1, 1);
  }

  private monthInputValue(value: Date): string {
    return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}`;
  }

  private startOfDay(value: Date): Date {
    return new Date(value.getFullYear(), value.getMonth(), value.getDate());
  }

  private businessDaysBetween(start: Date, end: Date): number {
    if (start > end) {
      return 0;
    }

    let total = 0;
    const cursor = this.startOfDay(start);
    const last = this.startOfDay(end);
    while (cursor <= last) {
      const day = cursor.getDay();
      if (day !== 0 && day !== 6) {
        total += 1;
      }
      cursor.setDate(cursor.getDate() + 1);
    }

    return total;
  }

  private channelMidAngle(index: number): number {
    const start = this.channelOffsetPercent(index);
    const item = this.canaisData()[index];
    const size = item ? this.percentOfTotal(item.faturamento, this.canaisTotal()) : 0;
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

  private formatDate(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? '' : date.toLocaleDateString('pt-BR');
  }

  private escapeHtml(value: string): string {
    return String(value ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
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
      this.toastr.error('Informe o período da meta.', 'Meta de vendas');
      return;
    }

    if (this.metaDataInicioDraft() > this.metaDataFimDraft()) {
      this.toastr.error('A data inicial da meta não pode ser maior que a data final.', 'Meta de vendas');
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
        this.toastr.success('Meta de vendas atualizada.', 'B.I Peças');
        this.load();
      },
      error: () => {
        this.savingMeta.set(false);
        this.toastr.error('Não foi possível salvar a meta do vendedor.', 'Erro');
      },
    });
  }

  setEmpresa(value: string | number | null): void {
    if (!this.canUseEmpresaRevendaFilters() || this.isEmpresaFilterLocked()) {
      const empresaObrigatoria = this.isGerenteEmpresaPecas()
        ? this.gerenteEmpresaNumeroEfetiva()
        : this.empresasPermitidasPorPerfil()?.[0] ?? null;
      this.empresaNumero.set(empresaObrigatoria);
      this.revendasSelecionadas.set([]);
      return;
    }

    const numero = Number(value);
    const empresasPermitidas = this.empresasPermitidasPorPerfil();
    const isAllowed = !empresasPermitidas || empresasPermitidas.includes(numero);
    this.empresaNumero.set(Number.isFinite(numero) && numero > 0 && isAllowed ? numero : null);
    this.revendasSelecionadas.set([]);
    this.clearGmTransactionsWhenUnavailable();
  }

  toggleCanal(value: string): void {
    if (!ALLOWED_CHANNELS.includes(value as typeof ALLOWED_CHANNELS[number])) {
      return;
    }

    const selected = new Set(this.canaisSelecionados());
    if (selected.has(value)) {
      selected.delete(value);
    } else {
      selected.add(value);
    }

    this.canaisSelecionados.set([...selected]);
  }

  isCanalSelected(value: string): boolean {
    return this.canaisSelecionados().includes(value);
  }

  clearCanais(): void {
    this.canaisSelecionados.set([]);
  }

  private canShowGmTransactions(): boolean {
    return this.empresaNumero() === 5;
  }

  private clearGmTransactionsWhenUnavailable(): void {
    if (this.canShowGmTransactions()) {
      return;
    }

    this.canaisSelecionados.set(this.canaisSelecionados().filter((canal) => canal !== 'G21' && canal !== 'P71'));
  }

  setRevendas(values: unknown): void {
    if (!this.canUseEmpresaRevendaFilters()) {
      return;
    }

    const selected = Array.isArray(values) ? values : [values];
    const revendas = selected
      .map((value) => Number(value))
      .filter((value) => Number.isFinite(value) && value > 0);

    this.revendasSelecionadas.set([...new Set(revendas)]);
  }

  toggleRevenda(numero: number): void {
    if (!this.canUseEmpresaRevendaFilters()) {
      return;
    }

    const selected = new Set(this.revendasSelecionadas());
    if (selected.has(numero)) {
      selected.delete(numero);
    } else {
      selected.add(numero);
    }

    this.revendasSelecionadas.set([...selected].sort((a, b) => a - b));
  }

  isRevendaSelected(numero: number): boolean {
    return this.revendasSelecionadas().includes(numero);
  }

  clearRevendas(): void {
    this.revendasSelecionadas.set([]);
  }

  private applyUserScopeDefaults(): void {
    if (this.isVendedorPecas()) {
      this.empresaNumero.set(this.userEmpresaNumero());
      this.revendasSelecionadas.set([]);
      return;
    }

    if (this.isGerenteEmpresaPecas()) {
      this.empresaNumero.set(this.gerenteEmpresaNumeroEfetiva());
      this.revendasSelecionadas.set([]);
      return;
    }

    const empresasPermitidas = this.empresasPermitidasPorPerfil();
    if (empresasPermitidas?.length === 1) {
      this.empresaNumero.set(empresasPermitidas[0]);
      this.revendasSelecionadas.set([]);
      return;
    }

    if (!empresasPermitidas && !this.empresaNumero() && this.empresasDisponiveis().length === 1) {
      this.empresaNumero.set(this.empresasDisponiveis()[0].numero);
      this.revendasSelecionadas.set([]);
      return;
    }

    if (empresasPermitidas?.length && this.empresaNumero() && !empresasPermitidas.includes(this.empresaNumero()!)) {
      this.empresaNumero.set(null);
      this.revendasSelecionadas.set([]);
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
    if (!target?.closest('.revenda-picker')) {
      this.revendaPickerOpen.set(false);
    }
    if (!target?.closest('.canal-picker')) {
      this.canalPickerOpen.set(false);
    }
  }
}
