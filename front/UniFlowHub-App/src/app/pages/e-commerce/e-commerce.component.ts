import { Component, ElementRef, HostListener, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { finalize } from 'rxjs';
import { ECommerceDashboard, ECommerceMonthlySale, ECommerceService, ECommerceUnit } from '../../core/e-commerce.service';
import { AuthService } from '../../core/auth.service';
import { AutoRefreshControlComponent } from '../../core/auto-refresh-control.component';
import { Empresa, Unidade } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';

type UnitSortField = 'realizado' | 'margem' | 'ticket' | 'notas';

interface ChartSlice {
  label: string;
  value: number;
  color: string;
}

interface PieSlice extends ChartSlice {
  share: number;
  path: string;
}

interface HoveredSlice {
  context: 'company' | 'result';
  label: string;
  value: number;
  share: number;
}

interface MonthlySeries {
  ano: number;
  color: string;
  total: number;
  points: MonthlyPoint[];
}

interface MonthlyPoint {
  mes: number;
  label: string;
  value: number;
  x: number;
  y: number;
}

@Component({
  selector: 'app-e-commerce',
  standalone: true,
  imports: [AutoRefreshControlComponent, DatePipe, FormsModule],
  templateUrl: './e-commerce.component.html',
  styleUrls: ['./e-commerce.component.css'],
})
export class ECommerceComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly eCommerceService = inject(ECommerceService);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly toastr = inject(ToastrService);

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly profileMenuOpen = signal(false);
  readonly revendaPickerOpen = signal(false);
  readonly hoveredSlice = signal<HoveredSlice | null>(null);
  readonly hoveredCompanySlice = computed(() => this.hoveredSlice()?.context === 'company' ? this.hoveredSlice() : null);
  readonly hoveredResultSlice = computed(() => this.hoveredSlice()?.context === 'result' ? this.hoveredSlice() : null);
  readonly loading = signal(false);
  readonly importingSpreadsheet = signal(false);
  readonly selectedSpreadsheetName = signal('');
  readonly spreadsheetContribution = signal<number | null>(null);
  readonly sortField = signal<UnitSortField>('realizado');
  readonly empresas = signal<Empresa[]>([]);
  readonly revendas = signal<Unidade[]>([]);
  readonly data = signal<ECommerceDashboard | null>(null);
  readonly empresaNumero = signal<number | null>(null);
  readonly revendasSelecionadas = signal<string[]>([]);
  readonly dataInicio = signal(this.toDateInput(this.firstDayOfCurrentMonth()));
  readonly dataFim = signal(this.toDateInput(new Date()));
  readonly monthLabels = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
  private selectedSpreadsheet: File | null = null;
  @ViewChild('ecommerceSpreadsheetInput') private ecommerceSpreadsheetInput?: ElementRef<HTMLInputElement>;

  readonly unidades = computed(() => this.data()?.unidades ?? []);
  readonly evolucaoAnual = computed(() => this.data()?.evolucaoAnual ?? []);
  readonly evolucaoMensal = computed(() => this.data()?.evolucaoMensal ?? []);
  readonly empresasDisponiveis = computed(() => {
    const empresasComRevenda = new Set(this.revendas().map((revenda) => revenda.empresaNumero));
    return this.empresas()
      .filter((empresa) => empresasComRevenda.has(empresa.numero))
      .sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome));
  });
  readonly revendasDaEmpresa = computed(() => {
    const empresa = this.empresaNumero();
    return this.revendas()
      .filter((revenda) => !empresa || revenda.empresaNumero === empresa)
      .sort((a, b) => a.empresaNumero - b.empresaNumero || a.numeroRevenda - b.numeroRevenda || a.revenda.localeCompare(b.revenda));
  });
  readonly revendasSelecionadasLabel = computed(() => {
    const selected = this.revendasSelecionadas();
    if (!selected.length) {
      return 'Todas as revendas';
    }

    const labels = new Map(this.revendas().map((revenda) => [this.revendaKey(revenda), this.revendaLabel(revenda)]));
    return selected.slice().sort().map((key) => labels.get(key) ?? key).join(', ');
  });

  readonly totals = computed(() => {
    const unidades = this.unidades();
    const realizado = unidades.reduce((total, unit) => total + unit.realizado, 0);
    const invoices = unidades.reduce((total, unit) => total + unit.notasEmitidas, 0);
    const contribution = this.spreadsheetContribution() ?? 0;
    const profitability = unidades.reduce((total, unit) => total + unit.realizado - unit.custo - unit.impostos, 0);
    const cost = unidades.reduce((total, unit) => total + unit.custo, 0);
    const taxes = unidades.reduce((total, unit) => total + unit.impostos + unit.despesas, 0);

    return {
      realized: realizado,
      invoices,
      averageTicket: invoices ? realizado / invoices : 0,
      contribution,
      contributionMargin: realizado ? contribution / realizado : 0,
      profitability,
      profitabilityMargin: realizado ? profitability / realizado : 0,
      cost,
      taxes,
    };
  });
  readonly sortedUnits = computed(() => {
    const field = this.sortField();
    return this.unidades().slice().sort((a, b) => this.sortValue(b, field) - this.sortValue(a, field));
  });
  readonly strongestUnit = computed(() => this.sortedUnits()[0] ?? null);
  readonly marginAlerts = computed(() => this.unidades().filter((unit) => unit.margemContribuicaoPercentual < 0));
  readonly firstMarginAlert = computed(() => this.marginAlerts()[0] ?? null);
  readonly maxRealized = computed(() => Math.max(...this.unidades().map((unit) => unit.realizado), 1));
  readonly maxMonthlyRealized = computed(() => Math.max(...this.evolucaoMensal().map((item) => item.realizado), 1));
  readonly hasData = computed(() => this.unidades().length > 0);
  readonly monthlySeries = computed<MonthlySeries[]>(() => {
    const colors = ['#0f766e', '#0284c7', '#1d4ed8', '#f59e0b', '#7c3aed'];
    const grouped = new Map<number, ECommerceMonthlySale[]>();
    this.evolucaoMensal().forEach((item) => {
      grouped.set(item.ano, [...(grouped.get(item.ano) ?? []), item]);
    });

    return Array.from(grouped.entries())
      .sort(([yearA], [yearB]) => yearA - yearB)
      .map(([ano, items], index) => {
        const monthMap = new Map(items.map((item) => [item.mes, item]));
        const total = items.reduce((sum, item) => sum + item.realizado, 0);
        const points = this.monthLabels.map((label, monthIndex) => {
          const mes = monthIndex + 1;
          const value = monthMap.get(mes)?.realizado ?? 0;
          return {
            mes,
            label,
            value,
            x: 6 + monthIndex * 8,
            y: 92 - (value / this.maxMonthlyRealized()) * 78,
          };
        });
        return { ano, color: colors[index % colors.length], total, points };
      });
  });
  readonly companyPie = computed<ChartSlice[]>(() => {
    const colors = ['#006aa6', '#008c84', '#28c98f', '#f59e0b', '#7c3aed', '#ef4444'];
    const top = this.sortedUnits().slice(0, 5).map((unit, index) => ({
      label: unit.nomeCurto,
      value: unit.realizado,
      color: colors[index % colors.length],
    }));
    const others = this.sortedUnits().slice(5).reduce((total, unit) => total + unit.realizado, 0);
    return others > 0 ? [...top, { label: 'Outras', value: others, color: '#64748b' }] : top;
  });
  readonly resultPie = computed<ChartSlice[]>(() => [
    { label: 'Custo', value: Math.max(this.totals().cost, 0), color: '#006aa6' },
    { label: 'Impostos/desp.', value: Math.max(this.totals().taxes, 0), color: '#f59e0b' },
    { label: 'Margem', value: Math.max(this.totals().contribution, 0), color: '#008c84' },
  ].filter((slice) => slice.value > 0));

  ngOnInit(): void {
    this.loadEmpresas();
    this.load();
  }

  @HostListener('document:click', ['$event'])
  closeRevendaPickerOnOutsideClick(event: MouseEvent): void {
    const picker = this.elementRef.nativeElement.querySelector('.revenda-picker');
    if (picker && event.target instanceof Node && !picker.contains(event.target)) {
      this.revendaPickerOpen.set(false);
    }
  }

  load(): void {
    this.spreadsheetContribution.set(null);
    this.loading.set(true);
    this.eCommerceService
      .load({
        dataInicio: this.dataInicio(),
        dataFim: this.dataFim(),
        empresa: this.empresaNumero(),
        revenda: this.revendasSelecionadas(),
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (data) => this.data.set(data),
        error: () => this.data.set({ atualizadoEm: new Date().toISOString(), unidades: [], evolucaoAnual: [], evolucaoMensal: [] }),
      });
  }

  loadEmpresas(): void {
    this.unidadesService.listEmpresas().subscribe({ next: (empresas) => this.empresas.set(empresas), error: () => this.empresas.set([]) });
    this.unidadesService.listEmpresasRevendas().subscribe({ next: (revendas) => this.revendas.set(revendas), error: () => this.revendas.set([]) });
  }

  onSpreadsheetSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedSpreadsheet = file;
    this.selectedSpreadsheetName.set(file?.name ?? '');
  }

  importSpreadsheet(): void {
    if (!this.selectedSpreadsheet) {
      this.toastr.warning('Selecione uma planilha .xlsx de e-commerce.', 'E-commerce');
      return;
    }

    if (this.importingSpreadsheet()) {
      return;
    }

    this.importingSpreadsheet.set(true);
    this.eCommerceService.importarPlanilha(this.selectedSpreadsheet)
      .pipe(finalize(() => this.importingSpreadsheet.set(false)))
      .subscribe({
        next: (result) => {
          this.spreadsheetContribution.set(result.margemContribuicaoValor);
          this.selectedSpreadsheet = null;
          this.selectedSpreadsheetName.set('');
          if (this.ecommerceSpreadsheetInput?.nativeElement) {
            this.ecommerceSpreadsheetInput.nativeElement.value = '';
          }
          this.toastr.success(`${result.linhasImportadas} linha(s) importada(s).`, 'E-commerce');
        },
        error: () => this.toastr.error('Nao foi possivel importar a planilha.', 'E-commerce'),
      });
  }

  setEmpresa(value: string | number | null): void {
    const numero = value === null || value === '' ? null : Number(value);
    this.empresaNumero.set(Number.isFinite(numero) ? numero : null);
    this.revendasSelecionadas.set([]);
    this.revendaPickerOpen.set(false);
    this.load();
  }

  setDataInicio(value: string): void {
    this.dataInicio.set(value);
    this.load();
  }

  setDataFim(value: string): void {
    this.dataFim.set(value);
    this.load();
  }

  toggleRevenda(revenda: Unidade): void {
    const key = this.revendaKey(revenda);
    const selected = this.revendasSelecionadas();
    this.revendasSelecionadas.set(selected.includes(key) ? selected.filter((item) => item !== key) : [...selected, key]);
    this.load();
  }

  isRevendaSelected(revenda: Unidade): boolean {
    return this.revendasSelecionadas().includes(this.revendaKey(revenda));
  }

  clearRevendas(): void {
    this.revendasSelecionadas.set([]);
    this.revendaPickerOpen.set(false);
    this.load();
  }

  goHome(): void {
    void this.router.navigateByUrl('/hub');
  }

  editProfile(): void {
    this.profileMenuOpen.set(false);
    this.profileFlow.editProfile();
  }

  changePassword(): void {
    this.profileMenuOpen.set(false);
    this.profileFlow.changePassword();
  }

  logout(): void {
    this.profileMenuOpen.set(false);
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }

  realizedWidth(unit: ECommerceUnit): number {
    return this.maxRealized() ? (unit.realizado / this.maxRealized()) * 100 : 0;
  }

  rankingLabel(unit: ECommerceUnit): string {
    return unit.vendedorNome?.trim() || unit.nomeCurto;
  }

  linePoints(series: MonthlySeries): string {
    return series.points.map((point) => `${point.x},${point.y}`).join(' ');
  }

  areaPoints(series: MonthlySeries): string {
    return `6,94 ${this.linePoints(series)} 94,94`;
  }

  pieSlices(slices: ChartSlice[]): PieSlice[] {
    const total = slices.reduce((sum, slice) => sum + slice.value, 0);
    if (!total) {
      return [];
    }

    let cursor = 0;
    return slices.map((slice) => {
      const share = slice.value / total;
      const startAngle = cursor;
      cursor += share * 360;
      return {
        ...slice,
        share,
        path: this.piePath(startAngle, cursor),
      };
    });
  }

  sliceShare(slice: ChartSlice, slices: ChartSlice[]): number {
    const total = slices.reduce((sum, item) => sum + item.value, 0);
    return total ? slice.value / total : 0;
  }

  setHoveredSlice(context: 'company' | 'result', slice: ChartSlice, slices: ChartSlice[]): void {
    this.hoveredSlice.set({
      context,
      label: slice.label,
      value: slice.value,
      share: this.sliceShare(slice, slices),
    });
  }

  clearHoveredSlice(context: 'company' | 'result'): void {
    if (this.hoveredSlice()?.context === context) {
      this.hoveredSlice.set(null);
    }
  }

  progressWidth(value: number): number {
    return Math.max(0, Math.min(value * 100, 100));
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
  }

  formatNumber(value: number): string {
    return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 }).format(value);
  }

  formatPercent(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'percent', minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value);
  }

  marginClass(unit: ECommerceUnit): string {
    return unit.margemContribuicaoPercentual >= 0 ? 'ok' : 'attention';
  }

  revendaLabel(revenda: Unidade): string {
    const nome = revenda.revenda?.trim() || revenda.nome?.trim() || `Revenda ${revenda.numeroRevenda}`;
    return `${revenda.empresaNumero}.${revenda.numeroRevenda} - ${nome}`;
  }

  private sortValue(unit: ECommerceUnit, field: UnitSortField): number {
    if (field === 'margem') {
      return unit.margemContribuicaoPercentual;
    }

    if (field === 'ticket') {
      return unit.ticketMedio;
    }

    if (field === 'notas') {
      return unit.notasEmitidas;
    }

    return unit.realizado;
  }

  private revendaKey(revenda: Unidade): string {
    return `${revenda.empresaNumero}:${revenda.numeroRevenda}`;
  }

  private piePath(startAngle: number, endAngle: number): string {
    const center = 50;
    const radius = 40;
    const normalizedEnd = endAngle - startAngle >= 359.999 ? startAngle + 359.999 : endAngle;
    const start = this.polarToCartesian(center, center, radius, startAngle - 90);
    const end = this.polarToCartesian(center, center, radius, normalizedEnd - 90);
    const largeArc = normalizedEnd - startAngle > 180 ? 1 : 0;
    return `M ${center} ${center} L ${start.x} ${start.y} A ${radius} ${radius} 0 ${largeArc} 1 ${end.x} ${end.y} Z`;
  }

  private polarToCartesian(centerX: number, centerY: number, radius: number, angleInDegrees: number): { x: number; y: number } {
    const angleInRadians = (angleInDegrees * Math.PI) / 180;
    return {
      x: centerX + radius * Math.cos(angleInRadians),
      y: centerY + radius * Math.sin(angleInRadians),
    };
  }

  private firstDayOfCurrentMonth(): Date {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), 1);
  }

  private toDateInput(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
