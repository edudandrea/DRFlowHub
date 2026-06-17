import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, HostListener, OnInit, PLATFORM_ID, computed, inject, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { CartaoPontoService } from '../../core/cartao-ponto.service';
import { CartaoPontoArquivo, CartaoPontoFuncionario, CartaoPontoRegistro } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';

interface FolhaPontoMes {
  mes: string;
  label: string;
  unidadeNome: string;
  cnpjUnidade: string;
  totalRegistros: number;
  totalDias: number;
  confirmadoPeloUsuario: boolean;
  precisaAjuste: boolean;
}

@Component({
  selector: 'app-cartao-ponto',
  imports: [DatePipe, FormsModule],
  templateUrl: './cartao-ponto.html',
  styleUrl: './cartao-ponto.scss',
})
export class CartaoPontoPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly service = inject(CartaoPontoService);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly canManage = computed(() => this.auth.hasAnyRole(['Admin', 'RH']));
  readonly arquivos = signal<CartaoPontoArquivo[]>([]);
  readonly funcionarios = signal<CartaoPontoFuncionario[]>([]);
  readonly selectedFuncionario = signal<CartaoPontoFuncionario | null>(null);
  readonly registros = signal<CartaoPontoRegistro[]>([]);
  readonly selectedArquivoId = signal<number | null>(null);
  readonly selectedMonth = signal<string | null>(null);
  readonly loading = signal(false);
  readonly importing = signal(false);
  readonly modalOpen = signal(false);
  readonly profileMenuOpen = signal(false);
  readonly search = signal('');
  readonly selectedFileName = signal('');
  readonly filteredFuncionarios = computed(() => {
    const term = this.normalize(this.search());
    return this.funcionarios().filter((item) => !term || this.normalize(`${item.nome} ${item.cpf}`).includes(term));
  });
  readonly ajustesPendentes = computed(() => this.funcionarios().filter((item) => item.precisaAjuste).length);
  readonly folhasMensais = computed<FolhaPontoMes[]>(() => {
    const groups = new Map<string, CartaoPontoRegistro[]>();
    for (const registro of this.registros()) {
      const month = registro.data.slice(0, 7);
      groups.set(month, [...(groups.get(month) ?? []), registro]);
    }

    return [...groups.entries()]
      .sort(([a], [b]) => b.localeCompare(a))
      .map(([mes, items]) => ({
        mes,
        label: this.monthLabel(mes),
        unidadeNome: this.selectedFuncionario()?.unidadeNome || '',
        cnpjUnidade: this.selectedFuncionario()?.cnpjUnidade || '',
        totalRegistros: items.length,
        totalDias: new Set(items.map((item) => item.data.slice(0, 10))).size,
        confirmadoPeloUsuario: items.every((item) => item.confirmadoPeloUsuario),
        precisaAjuste: items.some((item) => item.precisaAjuste),
      }));
  });
  readonly groupedRegistros = computed(() => {
    const groups = new Map<string, CartaoPontoRegistro[]>();
    const month = this.selectedMonth();
    const registros = month
      ? this.registros().filter((registro) => registro.data.startsWith(month))
      : this.registros();

    for (const registro of registros) {
      const key = registro.data.slice(0, 10);
      groups.set(key, [...(groups.get(key) ?? []), registro]);
    }

    return [...groups.entries()].map(([data, items]) => ({
      data,
      registros: items.sort((a, b) => a.sequencia - b.sequencia),
    }));
  });

  private selectedFile: File | null = null;
  @ViewChild('arquivoInput') private arquivoInput?: ElementRef<HTMLInputElement>;

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    this.load();
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.listArquivos().subscribe({
      next: (arquivos) => {
        this.arquivos.set(arquivos);
        if (this.canManage() && !this.selectedArquivoId() && arquivos[0]) {
          this.selectedArquivoId.set(arquivos[0].id);
        }
        this.loadFuncionarios();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar o controle de ponto.', 'RH');
      },
    });
  }

  loadFuncionarios(): void {
    this.service.listFuncionarios(this.selectedArquivoId()).subscribe({
      next: (funcionarios) => {
        this.funcionarios.set(funcionarios);
        this.loading.set(false);
        void this.spinner.hide();
        if (!this.canManage() && funcionarios[0]) {
          this.loadUserFolhas(funcionarios[0]);
        }
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar os funcionários.', 'RH');
      },
    });
  }

  changeArquivo(value: string): void {
    this.selectedArquivoId.set(value ? Number(value) : null);
    this.selectedFuncionario.set(null);
    this.registros.set([]);
    this.selectedMonth.set(null);
    this.loadFuncionarios();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedFile = file;
    this.selectedFileName.set(file?.name ?? '');
  }

  importar(): void {
    if (!this.selectedFile || this.importing()) {
      this.toastr.warning('Selecione o TXT do Control iD.', 'RH');
      return;
    }

    this.importing.set(true);
    this.service.importar(this.selectedFile).subscribe({
      next: (arquivo) => {
        this.importing.set(false);
        this.selectedFile = null;
        this.selectedFileName.set('');
        if (this.arquivoInput?.nativeElement) {
          this.arquivoInput.nativeElement.value = '';
        }
        this.selectedArquivoId.set(arquivo.id);
        this.toastr.success('Arquivo de ponto importado.', 'RH');
        this.load();
      },
      error: () => {
        this.importing.set(false);
        this.toastr.error('Não foi possível importar o TXT.', 'Erro');
      },
    });
  }

  openFuncionario(funcionario: CartaoPontoFuncionario): void {
    this.selectedFuncionario.set(funcionario);
    this.selectedMonth.set(null);
    this.service.listRegistros(funcionario.cpf, this.selectedArquivoId()).subscribe({
      next: (registros) => {
        this.registros.set(registros);
        this.modalOpen.set(true);
      },
      error: () => this.toastr.error('Não foi possível carregar o cartão ponto.', 'RH'),
    });
  }

  openFolhaMes(folha: FolhaPontoMes): void {
    const funcionario = this.funcionarios()[0];
    if (!funcionario) {
      return;
    }

    this.selectedFuncionario.set(funcionario);
    this.selectedMonth.set(folha.mes);
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.selectedFuncionario.set(null);
    this.selectedMonth.set(null);
    if (this.canManage()) {
      this.registros.set([]);
    }
  }

  updateRegistro(registro: CartaoPontoRegistro, horario: string): void {
    if (!this.canManage()) {
      return;
    }

    this.service.updateRegistro(registro.id, horario).subscribe({
      next: (updated) => {
        this.registros.set(this.registros().map((item) => item.id === updated.id ? updated : item));
        this.toastr.success('Horario atualizado.', 'RH');
      },
      error: () => this.toastr.error('Informe o horario no formato HH:mm.', 'RH'),
    });
  }

  confirmarUsuario(): void {
    const funcionario = this.selectedFuncionario();
    if (!funcionario) {
      return;
    }

    this.service.responder(funcionario.cpf, false, this.canManage() ? this.selectedArquivoId() : null, this.selectedMonth()).subscribe({
      next: () => {
        this.toastr.success('Cartao ponto confirmado.', 'RH');
        this.closeModal();
        this.loadFuncionarios();
      },
      error: () => this.toastr.error('Não foi possível confirmar o ponto.', 'Erro'),
    });
  }

  solicitarAjuste(): void {
    const funcionario = this.selectedFuncionario();
    if (!funcionario) {
      return;
    }

    this.service.responder(funcionario.cpf, true, this.canManage() ? this.selectedArquivoId() : null, this.selectedMonth()).subscribe({
      next: () => void this.router.navigate(['/solicitacoes']),
      error: () => this.toastr.error('Não foi possível registrar a necessidade de ajuste.', 'Erro'),
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

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private loadUserFolhas(funcionario: CartaoPontoFuncionario): void {
    this.selectedFuncionario.set(funcionario);
    this.service.listRegistros(funcionario.cpf).subscribe({
      next: (registros) => this.registros.set(registros),
      error: () => this.toastr.error('Não foi possível carregar suas folhas ponto.', 'RH'),
    });
  }

  private monthLabel(mes: string): string {
    const date = new Date(`${mes}-01T00:00:00`);
    if (Number.isNaN(date.getTime())) {
      return mes;
    }

    return date.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
  }
}
