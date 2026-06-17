import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AutoRefreshControlComponent } from '../../core/auto-refresh-control.component';
import { AuthService } from '../../core/auth.service';
import { Empresa, RepasseDashboard, RepasseResumoEmpresa, RepasseVeiculo, Unidade } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { RepassesService } from '../../core/repasses.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';

interface SituacaoResumo {
  nome: string;
  total: number;
  percentual: number;
  custo: number;
}

interface PieSlice {
  item: RepasseVeiculo;
  index: number;
  color: string;
  path: string;
  percent: number;
  labelX: number;
  labelY: number;
  midAngle: number;
}

const REPASSE_EMPRESAS = new Map<number, string>([
  [1, 'Renault'],
  [2, 'Nissan'],
  [5, 'GM'],
  [6, 'DFSUL'],
  [7, 'DRSUL Peugeot/Citroen'],
  [8, 'Bajaj'],
  [9, 'Geely'],
  [10, 'MG'],
]);

@Component({
  selector: 'app-repasses',
  imports: [CommonModule, FormsModule, AutoRefreshControlComponent],
  templateUrl: './repasses.component.html',
  styleUrls: ['./repasses.component.css'],
})
export class RepassesComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly repassesService = inject(RepassesService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly router = inject(Router);
  private readonly profileFlow = inject(ProfileFlowService);

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly empresas = signal<Empresa[]>([]);
  readonly revendas = signal<Unidade[]>([]);
  readonly dashboard = signal<RepasseDashboard>({ veiculos: [], topDiasEstoque: [], resumos: [] });
  readonly loading = signal(false);
  readonly atualizadoEm = signal<Date | null>(null);
  readonly profileMenuOpen = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly buscaDetalhe = signal('');
  readonly diasInicio = signal<number | null>(null);
  readonly diasFim = signal<number | null>(null);
  readonly situacaoFiltro = signal('');
  readonly hoveredTopIndex = signal<number | null>(null);
  readonly resumoSelecionado = signal<RepasseResumoEmpresa | null>(null);

  readonly empresaSelecionada = signal<number | null>(null);
  readonly revendaSelecionada = signal<number | null>(null);
  readonly dataInicio = signal(this.toDateInputValue(new Date()));
  readonly dataFim = signal(this.toDateInputValue(new Date()));

  readonly revendasFiltradas = computed(() => {
    const empresaNumero = this.empresaSelecionada();
    const empresa = this.empresas().find((item) => item.numero === empresaNumero);

    return this.revendas()
      .filter((item) => !empresa || item.empresaId === empresa.id || item.empresaNumero === empresaNumero)
      .sort((a, b) => a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda));
  });

  readonly empresasRepasse = computed(() => {
    const cadastradas = new Map(this.empresas().map((item) => [item.numero, item]));

    return Array.from(REPASSE_EMPRESAS.entries()).map(([numero, nome]) => ({
      ...(cadastradas.get(numero) ?? { id: numero, numero, dataCadastro: '' }),
      numero,
      nome,
    }) as Empresa);
  });

  readonly veiculos = computed(() => this.dashboard().veiculos);
  readonly resumos = computed(() => this.dashboard().resumos ?? []);
  readonly maiorCustoEmpresa = computed(() => Math.max(...this.resumos().map((item) => item.custoPara), 1));
  readonly resumoTotal = computed<RepasseResumoEmpresa>(() => {
    const items = this.resumos();
    const volumePara = items.reduce((total, item) => total + item.volumePara, 0);
    const custoPara = items.reduce((total, item) => total + item.custoPara, 0);
    const mediaPonderada = volumePara > 0
      ? items.reduce((total, item) => total + item.mediaGiroEstoque * item.volumePara, 0) / volumePara
      : 0;

    return {
      empresa: 0,
      nomeEmpresa: 'Totais',
      volumeDe: items.reduce((total, item) => total + item.volumeDe, 0),
      volumePara,
      custoDe: items.reduce((total, item) => total + item.custoDe, 0),
      custoPara,
      ticketMedio: volumePara > 0 ? custoPara / volumePara : 0,
      mediaGiroEstoque: Math.round(mediaPonderada),
      distorcao: items.reduce((total, item) => total + item.distorcao, 0),
      limiteAutorizado: items.reduce((total, item) => total + item.limiteAutorizado, 0),
    };
  });
  readonly situacoesFiltro = computed(() => [...new Set(this.veiculos().map((item) => item.situacao).filter(Boolean))]
    .sort((a, b) => a.localeCompare(b, 'pt-BR', { sensitivity: 'base' })));
  readonly veiculosFiltrados = computed(() => {
    const term = this.normalize(this.buscaDetalhe());
    const diasInicio = this.diasInicio();
    const diasFim = this.diasFim();
    const situacao = this.situacaoFiltro();

    return this.veiculos().filter((item) => {
      const matchesTerm = !term || [item.placa, item.modelo].some((value) => this.normalize(value).includes(term));
      const matchesDiasInicio = diasInicio === null || item.diasEstoque >= diasInicio;
      const matchesDiasFim = diasFim === null || item.diasEstoque <= diasFim;
      const matchesSituacao = !situacao || item.situacao === situacao;

      return matchesTerm && matchesDiasInicio && matchesDiasFim && matchesSituacao;
    });
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.veiculosFiltrados().length / this.pageSize())));
  readonly pagedVeiculos = computed(() => this.veiculosFiltrados().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));
  readonly custoTotalFiltrado = computed(() => this.veiculosFiltrados().reduce((total, item) => total + item.custoContabil, 0));
  readonly totalVeiculosFiltrado = computed(() => this.veiculosFiltrados().length);
  readonly topDiasEstoque = computed(() => this.dashboard().topDiasEstoque);
  readonly activeTopVehicle = computed(() => {
    const items = this.topDiasEstoque();
    return items[this.hoveredTopIndex() ?? 0] ?? items[0] ?? null;
  });
  readonly hoveredTopVehicle = computed(() => {
    const index = this.hoveredTopIndex();
    return index === null ? null : this.topDiasEstoque()[index] ?? null;
  });
  readonly pieSlices = computed<PieSlice[]>(() => {
    const items = this.topDiasEstoque();
    const total = items.reduce((sum, item) => sum + Math.max(item.diasEstoque, 0), 0);
    let cursor = 0;

    if (!items.length || total <= 0) {
      return [];
    }

    return items.map((item, index) => {
      const value = Math.max(item.diasEstoque, 0);
      const startAngle = cursor;
      const endAngle = cursor + (value / total) * 360;
      const midAngle = startAngle + (endAngle - startAngle) / 2;
      const label = this.polarToCartesian(50, 50, 27, midAngle);
      cursor = endAngle;

      return {
        item,
        index,
        color: this.legendColor(index),
        path: this.describeArc(50, 50, 45, startAngle, endAngle),
        percent: (value / total) * 100,
        labelX: label.x,
        labelY: label.y,
        midAngle,
      };
    });
  });
  readonly totalVeiculos = computed(() => this.veiculos().length);
  readonly custoTotal = computed(() => this.veiculos().reduce((total, item) => total + item.custoContabil, 0));
  readonly mediaDias = computed(() => {
    const items = this.veiculos();
    return items.length ? items.reduce((total, item) => total + item.diasEstoque, 0) / items.length : 0;
  });
  readonly estoqueCritico = computed(() => this.veiculos().filter((item) => item.diasEstoque >= 90).length);
  readonly maiorTempo = computed(() => this.topDiasEstoque()[0]?.diasEstoque ?? 0);
  readonly situacoes = computed<SituacaoResumo[]>(() => {
    const total = Math.max(this.totalVeiculos(), 1);
    const groups = new Map<string, { total: number; custo: number }>();

    for (const item of this.veiculos()) {
      const key = item.situacao || 'Sem situacao';
      const current = groups.get(key) ?? { total: 0, custo: 0 };
      current.total += 1;
      current.custo += item.custoContabil;
      groups.set(key, current);
    }

    return Array.from(groups.entries())
      .map(([nome, value]) => ({
        nome,
        total: value.total,
        percentual: (value.total / total) * 100,
        custo: value.custo,
      }))
      .sort((a, b) => b.total - a.total);
  });

  ngOnInit(): void {
    this.loadCadastros();
    this.loadDashboard();
  }

  loadCadastros(): void {
    this.unidadesService.listEmpresas().subscribe({
      next: (items) => this.empresas.set(items.filter((item) => REPASSE_EMPRESAS.has(item.numero)).sort((a, b) => a.numero - b.numero)),
    });

    this.unidadesService.list().subscribe({
      next: (items) => this.revendas.set(items.filter((item) => REPASSE_EMPRESAS.has(item.empresaNumero)).sort((a, b) => a.empresaNumero - b.empresaNumero || a.numeroRevenda - b.numeroRevenda)),
    });
  }

  nomeEmpresa(numero: number, fallback = ''): string {
    return REPASSE_EMPRESAS.get(numero) ?? (fallback || String(numero));
  }

  loadDashboard(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.repassesService.dashboard({
      empresa: this.empresaSelecionada(),
      revenda: this.revendaSelecionada(),
      dataInicio: this.dataInicio(),
      dataFim: this.dataFim(),
    }).subscribe({
      next: (result) => {
        this.dashboard.set(result);
        this.page.set(1);
        this.atualizadoEm.set(new Date());
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar os repasses.', 'Repasse');
      },
    });
  }

  onEmpresaChange(): void {
    this.revendaSelecionada.set(null);
    this.page.set(1);
    this.loadDashboard();
  }

  onRevendaChange(): void {
    this.page.set(1);
    this.loadDashboard();
  }

  clearFilters(): void {
    this.empresaSelecionada.set(null);
    this.revendaSelecionada.set(null);
    this.dataInicio.set(this.toDateInputValue(new Date()));
    this.dataFim.set(this.toDateInputValue(new Date()));
    this.page.set(1);
    this.hoveredTopIndex.set(null);
    this.loadDashboard();
  }

  onDateChange(): void {
    this.page.set(1);
  }

  openResumoModal(resumo: RepasseResumoEmpresa): void {
    this.resumoSelecionado.set(resumo);
  }

  closeResumoModal(): void {
    this.resumoSelecionado.set(null);
  }

  clearDetailFilters(): void {
    this.buscaDetalhe.set('');
    this.diasInicio.set(null);
    this.diasFim.set(null);
    this.situacaoFiltro.set('');
    this.page.set(1);
  }

  setDiasInicio(value: string | number | null): void {
    this.diasInicio.set(this.normalizeNumberFilter(value));
    this.page.set(1);
  }

  setDiasFim(value: string | number | null): void {
    this.diasFim.set(this.normalizeNumberFilter(value));
    this.page.set(1);
  }

  setBuscaDetalhe(value: string): void {
    this.buscaDetalhe.set(value);
    this.page.set(1);
  }

  setSituacaoFiltro(value: string): void {
    this.situacaoFiltro.set(value);
    this.page.set(1);
  }

  printFilteredPdf(): void {
    const items = this.veiculosFiltrados();
    const printWindow = window.open('', '_blank', 'width=1120,height=820');

    if (!printWindow) {
      this.toastr.warning('Permita pop-ups para imprimir o PDF dos filtros.', 'Repasse');
      return;
    }

    printWindow.document.open();
    printWindow.document.write(this.buildPrintHtml(items));
    printWindow.document.close();
    printWindow.focus();

    window.setTimeout(() => {
      printWindow.print();
      printWindow.close();
    }, 300);
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

  legendColor(index: number): string {
    return ['#2563eb', '#16a34a', '#f59e0b', '#14b8a6', '#7c3aed'][index] ?? '#64748b';
  }

  setHoveredTop(index: number | null): void {
    this.hoveredTopIndex.set(index);
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 }).format(value);
  }

  formatPercent(value: number): string {
    return `${value.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`;
  }

  formatSignedMoney(value: number): string {
    const formatted = this.formatMoney(Math.abs(value));
    return value < 0 ? `-${formatted}` : formatted;
  }

  sliceLabelTransform(slice: PieSlice): string {
    return `rotate(${slice.midAngle} ${slice.labelX} ${slice.labelY})`;
  }

  trackByVehicle(_: number, item: RepasseVeiculo): string {
    return `${item.empresa}-${item.revenda}-${item.placa}-${item.modelo}`;
  }

  trackByResumo(_: number, item: RepasseResumoEmpresa): number {
    return item.empresa;
  }

  empresaCustoPercentual(item: RepasseResumoEmpresa): number {
    return Math.min(100, Math.max(3, (item.custoPara / this.maiorCustoEmpresa()) * 100));
  }

  empresaLimitePercentual(item: RepasseResumoEmpresa): number {
    if (item.limiteAutorizado <= 0) {
      return 0;
    }

    return Math.min(100, Math.max(3, (item.custoPara / item.limiteAutorizado) * 100));
  }

  private safePage(): number {
    return Math.min(Math.max(this.page(), 1), this.totalPages());
  }

  private normalizeNumberFilter(value: string | number | null): number | null {
    if (value === null || value === '') {
      return null;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private toDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private buildPrintHtml(items: RepasseVeiculo[]): string {
    const filters = [
      ['Empresa', this.printEmpresaLabel()],
      ['Revenda', this.printRevendaLabel()],
      ['Periodo', `${this.formatDateLabel(this.dataInicio())} para ${this.formatDateLabel(this.dataFim())}`],
      ['Placa ou modelo', this.buscaDetalhe() || 'Todos'],
      ['Dias inicial', this.diasInicio() === null ? 'Todos' : String(this.diasInicio())],
      ['Dias final', this.diasFim() === null ? 'Todos' : String(this.diasFim())],
      ['Situacao', this.situacaoFiltro() || 'Todas'],
    ];

    const filterHtml = filters.map(([label, value]) => `
      <div>
        <span>${this.escapeHtml(label)}</span>
        <strong>${this.escapeHtml(value)}</strong>
      </div>
    `).join('');

    const rows = items.map((item) => `
      <tr>
        <td>
          <strong>${this.escapeHtml(this.nomeEmpresa(item.empresa, item.nomeEmpresa))}</strong>
          <small>${this.escapeHtml(`${item.empresa}.${item.revenda} - ${item.nomeRevenda}`)}</small>
        </td>
        <td>${this.escapeHtml(item.modelo)}</td>
        <td>${this.escapeHtml(item.placa || 'Sem placa')}</td>
        <td>${this.escapeHtml(this.formatMoney(item.custoContabil))}</td>
        <td>${this.escapeHtml(item.situacao)}</td>
        <td>${this.escapeHtml(String(item.diasEstoque))}</td>
      </tr>
    `).join('');

    return `<!doctype html>
      <html lang="pt-BR">
      <head>
        <meta charset="utf-8">
        <title>Veiculos em estoque para repasse</title>
        <style>
          *{box-sizing:border-box}
          body{margin:0;padding:28px;color:#0f172a;font-family:Arial,Helvetica,sans-serif}
          header{display:flex;justify-content:space-between;gap:18px;margin-bottom:22px;border-bottom:2px solid #0f766e;padding-bottom:16px}
          h1{margin:4px 0 0;font-size:24px}
          p{margin:0;color:#475569}
          .eyebrow{color:#075985;font-size:11px;font-weight:800;letter-spacing:.08em;text-transform:uppercase}
          .generated{text-align:right;font-size:12px}
          .filters,.totals{display:grid;gap:10px;margin-bottom:18px}
          .filters{grid-template-columns:repeat(4,minmax(0,1fr))}
          .filters div,.totals div{border:1px solid #cbd5e1;border-radius:6px;padding:10px}
          .filters span,.totals span{display:block;color:#64748b;font-size:11px;font-weight:800;text-transform:uppercase}
          .filters strong,.totals strong{display:block;margin-top:4px;font-size:13px}
          .totals{grid-template-columns:repeat(2,minmax(0,1fr))}
          .totals strong{color:#075985;font-size:20px}
          table{width:100%;border-collapse:collapse}
          th,td{border-bottom:1px solid #cbd5e1;padding:9px 8px;text-align:left;font-size:12px;vertical-align:top}
          th{color:#475569;text-transform:uppercase}
          td small{display:block;margin-top:3px;color:#64748b}
          @page{margin:14mm}
          @media print{body{padding:0}.filters{grid-template-columns:repeat(4,1fr)}}
        </style>
      </head>
      <body>
        <header>
          <div>
            <span class="eyebrow">Detalhamento</span>
            <h1>Veiculos em estoque para repasse</h1>
            <p>Relatorio gerado com os filtros aplicados na tela.</p>
          </div>
          <div class="generated">
            <strong>${this.escapeHtml(new Date().toLocaleString('pt-BR'))}</strong>
            <p>${this.escapeHtml(items.length)} de ${this.escapeHtml(String(this.totalVeiculos()))} registros</p>
          </div>
        </header>

        <section class="filters">${filterHtml}</section>

        <section class="totals">
          <div>
            <span>Total contabil filtrado</span>
            <strong>${this.escapeHtml(this.formatMoney(this.custoTotalFiltrado()))}</strong>
          </div>
          <div>
            <span>Total de veiculos filtrado</span>
            <strong>${this.escapeHtml(this.formatNumber(this.totalVeiculosFiltrado()))}</strong>
          </div>
        </section>

        <table>
          <thead>
            <tr>
              <th>Empresa / Revenda</th>
              <th>Modelo</th>
              <th>Placa</th>
              <th>Custo contabil</th>
              <th>Situacao</th>
              <th>Dias estoque</th>
            </tr>
          </thead>
          <tbody>${rows || '<tr><td colspan="6">Nenhum veiculo encontrado.</td></tr>'}</tbody>
        </table>
      </body>
      </html>`;
  }

  private printEmpresaLabel(): string {
    const empresa = this.empresaSelecionada();
    return empresa === null ? 'Todas as empresas' : `${empresa} - ${this.nomeEmpresa(empresa)}`;
  }

  private printRevendaLabel(): string {
    const revenda = this.revendaSelecionada();
    const item = this.revendasFiltradas().find((value) => value.numeroRevenda === revenda);
    return item ? `${item.empresaNumero}.${item.numeroRevenda} - ${item.revenda}` : 'Todas as revendas';
  }

  private formatDateLabel(value: string): string {
    const [year, month, day] = value.split('-');
    return year && month && day ? `${day}/${month}/${year}` : value;
  }

  private escapeHtml(value: string | number): string {
    return String(value)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  private describeArc(cx: number, cy: number, radius: number, startAngle: number, endAngle: number): string {
    if (endAngle - startAngle >= 359.99) {
      const start = this.polarToCartesian(cx, cy, radius, 0);
      const middle = this.polarToCartesian(cx, cy, radius, 180);

      return [
        `M ${cx} ${cy}`,
        `L ${start.x} ${start.y}`,
        `A ${radius} ${radius} 0 1 0 ${middle.x} ${middle.y}`,
        `A ${radius} ${radius} 0 1 0 ${start.x} ${start.y}`,
        'Z',
      ].join(' ');
    }

    const start = this.polarToCartesian(cx, cy, radius, endAngle);
    const end = this.polarToCartesian(cx, cy, radius, startAngle);
    const largeArcFlag = endAngle - startAngle <= 180 ? '0' : '1';

    return [
      `M ${cx} ${cy}`,
      `L ${start.x} ${start.y}`,
      `A ${radius} ${radius} 0 ${largeArcFlag} 0 ${end.x} ${end.y}`,
      'Z',
    ].join(' ');
  }

  private polarToCartesian(cx: number, cy: number, radius: number, angle: number): { x: number; y: number } {
    const angleInRadians = (angle - 90) * Math.PI / 180;

    return {
      x: cx + (radius * Math.cos(angleInRadians)),
      y: cy + (radius * Math.sin(angleInRadians)),
    };
  }
}
