import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { AuthService } from '../../core/auth.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';

interface VendaVeiculoMensal {
  mes: string;
  unidades: number;
  faturamento: number;
  margem: number;
}

interface VendaVeiculoRanking {
  modelo: string;
  familia: string;
  unidades: number;
  faturamento: number;
  margemPercentual: number;
  ticketMedio: number;
}

interface VendaVeiculoCanal {
  nome: string;
  unidades: number;
  faturamento: number;
}

@Component({
  selector: 'app-veiculos-bi',
  imports: [DatePipe],
  templateUrl: './veiculos-bi.html',
  styleUrl: './veiculos-bi.scss',
})
export class VeiculosBiPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly profileMenuOpen = signal(false);
  readonly loading = signal(false);
  readonly atualizadoEm = signal(new Date().toISOString());
  readonly periodo = signal('Semestre atual');
  readonly empresa = signal('Todas');
  readonly canal = signal('Todos');

  readonly empresas = computed(() => this.user()?.role === 'Qualidade Nissan' ? ['Empresa 2'] : ['Todas', 'Empresa 2', 'Empresa 3', 'Empresa 4']);
  readonly canaisFiltro = ['Todos', 'Showroom', 'Venda direta', 'Consorcio', 'Frotista'];
  readonly vendas = signal<VendaVeiculoMensal[]>([]);
  readonly modelos = signal<VendaVeiculoRanking[]>([]);
  readonly canais = signal<VendaVeiculoCanal[]>([]);

  readonly faturamentoTotal = computed(() => this.vendas().reduce((total, item) => total + item.faturamento, 0));
  readonly margemTotal = computed(() => this.vendas().reduce((total, item) => total + item.margem, 0));
  readonly unidadesTotal = computed(() => this.vendas().reduce((total, item) => total + item.unidades, 0));
  readonly ticketMedio = computed(() => this.unidadesTotal() ? this.faturamentoTotal() / this.unidadesTotal() : 0);
  readonly margemPercentual = computed(() => this.faturamentoTotal() ? (this.margemTotal() / this.faturamentoTotal()) * 100 : 0);
  readonly crescimento = computed(() => {
    const vendas = this.vendas();
    const atual = vendas.at(-1)?.unidades ?? 0;
    const anterior = vendas.at(-2)?.unidades ?? 0;
    return anterior ? ((atual - anterior) / anterior) * 100 : 0;
  });
  readonly maxMensal = computed(() => Math.max(...this.vendas().map((item) => item.faturamento), 1));
  readonly maxModelo = computed(() => Math.max(...this.modelos().map((item) => item.faturamento), 1));
  readonly maxCanal = computed(() => Math.max(...this.canais().map((item) => item.faturamento), 1));
  readonly forecast = computed(() => {
    const ultimos = this.vendas().slice(-3);
    const media = ultimos.reduce((total, item) => total + item.unidades, 0) / Math.max(ultimos.length, 1);
    return Math.round(media * 1.06);
  });

  ngOnInit(): void {
    if (this.isBrowser) {
      if (this.user()?.role === 'Qualidade Nissan') {
        this.empresa.set('Empresa 2');
      }
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    window.setTimeout(() => {
      this.vendas.set([
        { mes: 'Jan', unidades: 42, faturamento: 5480000, margem: 438000 },
        { mes: 'Fev', unidades: 47, faturamento: 6210000, margem: 501000 },
        { mes: 'Mar', unidades: 51, faturamento: 6880000, margem: 562000 },
        { mes: 'Abr', unidades: 55, faturamento: 7420000, margem: 618000 },
        { mes: 'Mai', unidades: 59, faturamento: 7960000, margem: 671000 },
        { mes: 'Jun', unidades: 56, faturamento: 7630000, margem: 644000 },
      ]);
      this.modelos.set([
        { modelo: 'Kicks Advance', familia: 'SUV', unidades: 72, faturamento: 9360000, margemPercentual: 8.8, ticketMedio: 130000 },
        { modelo: 'Frontier Platinum', familia: 'Pickup', unidades: 38, faturamento: 10374000, margemPercentual: 9.4, ticketMedio: 273000 },
        { modelo: 'Versa Exclusive', familia: 'Sedan', unidades: 54, faturamento: 5778000, margemPercentual: 7.6, ticketMedio: 107000 },
        { modelo: 'Sentra Advance', familia: 'Sedan', unidades: 28, faturamento: 4620000, margemPercentual: 8.2, ticketMedio: 165000 },
      ]);
      this.canais.set([
        { nome: 'Showroom', unidades: 126, faturamento: 16240000 },
        { nome: 'Venda direta', unidades: 74, faturamento: 10620000 },
        { nome: 'Frotista', unidades: 42, faturamento: 6870000 },
        { nome: 'Consorcio', unidades: 18, faturamento: 2850000 },
      ]);
      this.atualizadoEm.set(new Date().toISOString());
      this.loading.set(false);
      void this.spinner.hide();
    }, 180);
  }

  barHeight(value: number): number {
    return Math.max(8, (value / this.maxMensal()) * 100);
  }

  percent(value: number, max: number): number {
    return Math.max(2, (value / max) * 100);
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
  closeProfileMenuOnDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.profile-area')) {
      this.profileMenuOpen.set(false);
    }
  }
}
