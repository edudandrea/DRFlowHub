import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { GestaoPessoasService } from '../../core/gestao-pessoas.service';
import { Empresa, GestaoPessoasEtapa, GestaoPessoasProcesso, GestaoPessoasTipoProcesso, User } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';

type Tab = 'processos' | 'etapas';

@Component({
  selector: 'app-gestao-pessoas',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './gestao-pessoas.html',
  styleUrl: './gestao-pessoas.scss',
})
export class GestaoPessoasPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly service = inject(GestaoPessoasService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly processos = signal<GestaoPessoasProcesso[]>([]);
  readonly etapas = signal<GestaoPessoasEtapa[]>([]);
  readonly empresas = signal<Empresa[]>([]);
  readonly users = signal<User[]>([]);
  readonly selected = signal<GestaoPessoasProcesso | null>(null);
  readonly selectedEtapa = signal<GestaoPessoasEtapa | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly modalProcessoOpen = signal(false);
  readonly modalEtapaOpen = signal(false);
  readonly cancelModalOpen = signal(false);
  readonly profileMenuOpen = signal(false);
  readonly activeTab = signal<Tab>('processos');
  readonly tipoFiltro = signal<GestaoPessoasTipoProcesso | 'Todos'>('Todos');
  readonly search = signal('');

  readonly canManage = computed(() => this.auth.hasAccess('rh-admin') || this.auth.hasAnyRole(['RH']));
  readonly canMove = computed(() => this.canManage());
  readonly etapasDoProcesso = computed(() => this.selected() ? this.etapasDoItem(this.selected()!) : []);
  readonly etapaAtualIndex = computed(() => this.etapaAtualIndexFor(this.selected()));
  readonly departamentos = computed(() => this.uniqueSorted([
    ...this.users().map((item) => item.departamento),
    'Administrativo',
    'RH',
    'TI',
    'Financeiro',
    'Controladoria',
    'Compras',
    'Qualidade Nissan',
    'Pecas',
    'Operacional',
    'Comercial',
  ]));
  readonly cargos = computed(() => this.uniqueSorted(this.users().map((item) => item.cargo)));
  readonly filteredProcessos = computed(() => {
    const tipo = this.tipoFiltro();
    const term = this.normalize(this.search());
    return this.processos().filter((item) => {
      const matchesTipo = tipo === 'Todos' || item.tipoProcesso === tipo;
      const matchesTerm = !term || [
        item.titulo,
        item.solicitante,
        item.colaboradorNome,
        item.departamento,
        item.status,
        item.etapaAtualNome,
      ].some((value) => this.normalize(value).includes(term));
      return matchesTipo && matchesTerm;
    });
  });
  readonly admissaoCount = computed(() => this.processos().filter((item) => item.tipoProcesso === 'Admissao').length);
  readonly demissaoCount = computed(() => this.processos().filter((item) => item.tipoProcesso === 'Demissao').length);
  readonly pendentesGestor = computed(() => this.processos().filter((item) => item.status === 'Em andamento').length);

  readonly processoForm = this.fb.nonNullable.group({
    tipoProcesso: ['Admissao' as GestaoPessoasTipoProcesso, Validators.required],
    titulo: ['', Validators.required],
    solicitante: ['', Validators.required],
    unidade: ['', Validators.required],
    departamento: ['', Validators.required],
    colaboradorNome: ['', Validators.required],
    cargo: [''],
    descricao: ['', Validators.required],
    prioridade: ['Media'],
    observacoes: [''],
    userid: [0],
  });

  readonly etapaForm = this.fb.nonNullable.group({
    nome: ['', Validators.required],
    tipoProcesso: ['Admissao' as GestaoPessoasTipoProcesso, Validators.required],
    ordem: [1, [Validators.required, Validators.min(1)]],
    ativa: [true],
  });

  readonly movimentoForm = this.fb.nonNullable.group({ observacoes: [''] });
  readonly cancelForm = this.fb.nonNullable.group({ motivoCancelamento: ['', Validators.required] });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }
    this.load();
    this.loadCatalogos();
  }

  loadCatalogos(): void {
    this.unidadesService.listEmpresas().subscribe({
      next: (empresas) => this.empresas.set(empresas.slice().sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome))),
      error: () => this.toastr.error('Nao foi possivel carregar as empresas.', 'Gestao de Pessoas'),
    });

    this.auth.listUsers().subscribe({
      next: (users) => this.users.set(users),
      error: () => this.users.set([]),
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.listProcessos().subscribe({
      next: (processos) => {
        this.processos.set(processos);
        this.selected.set(processos[0] ?? null);
        this.loadEtapas(false);
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Nao foi possivel carregar os processos.', 'Gestao de Pessoas');
      },
    });
  }

  loadEtapas(showSpinner = true): void {
    if (showSpinner) {
      this.loading.set(true);
      void this.spinner.show();
    }
    this.service.listEtapas().subscribe({
      next: (etapas) => {
        this.etapas.set(etapas);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Nao foi possivel carregar as etapas.', 'Gestao de Pessoas');
      },
    });
  }

  setTab(tab: Tab): void {
    this.activeTab.set(tab);
  }

  select(item: GestaoPessoasProcesso): void {
    this.selected.set(item);
    this.movimentoForm.reset({ observacoes: '' });
  }

  openNewProcesso(tipoProcesso: GestaoPessoasTipoProcesso = 'Admissao'): void {
    const user = this.user();
    this.processoForm.reset({
      tipoProcesso,
      titulo: tipoProcesso === 'Admissao' ? 'Admissao de colaborador' : 'Demissao de colaborador',
      solicitante: user?.nome ?? '',
      unidade: this.defaultEmpresaValue(),
      departamento: user?.departamento ?? '',
      colaboradorNome: '',
      cargo: '',
      descricao: '',
      prioridade: 'Media',
      observacoes: '',
      userid: user?.id ?? 0,
    });
    this.modalProcessoOpen.set(true);
  }

  submitProcesso(): void {
    if (this.processoForm.invalid || this.saving()) {
      this.processoForm.markAllAsTouched();
      this.toastr.warning('Preencha os campos obrigatorios.', 'Atencao');
      return;
    }

    this.saving.set(true);
    this.service.createProcesso(this.processoForm.getRawValue()).subscribe({
      next: (saved) => {
        this.processos.set([saved, ...this.processos()]);
        this.selected.set(saved);
        this.saving.set(false);
        this.modalProcessoOpen.set(false);
        this.toastr.success('Processo criado.', 'Gestao de Pessoas');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(error?.error || 'Nao foi possivel salvar o processo.', 'Erro');
      },
    });
  }

  openNewEtapa(tipoProcesso: GestaoPessoasTipoProcesso = 'Admissao'): void {
    const nextOrder = Math.max(0, ...this.etapas().filter((etapa) => etapa.tipoProcesso === tipoProcesso).map((etapa) => etapa.ordem)) + 1;
    this.selectedEtapa.set(null);
    this.etapaForm.reset({ nome: '', tipoProcesso, ordem: nextOrder, ativa: true });
    this.modalEtapaOpen.set(true);
  }

  etapasOrdenadas(): GestaoPessoasEtapa[] {
    return this.etapas()
      .slice()
      .sort((a, b) => a.tipoProcesso.localeCompare(b.tipoProcesso) || a.ordem - b.ordem || a.nome.localeCompare(b.nome));
  }

  editEtapa(etapa: GestaoPessoasEtapa): void {
    this.selectedEtapa.set(etapa);
    this.etapaForm.reset({
      nome: etapa.nome,
      tipoProcesso: etapa.tipoProcesso,
      ordem: etapa.ordem,
      ativa: etapa.ativa,
    });
    this.modalEtapaOpen.set(true);
  }

  submitEtapa(): void {
    if (this.etapaForm.invalid || this.saving()) {
      this.etapaForm.markAllAsTouched();
      this.toastr.warning('Informe nome, tipo e ordem.', 'Atencao');
      return;
    }

    this.saving.set(true);
    const selected = this.selectedEtapa();
    this.service.saveEtapa(this.etapaForm.getRawValue(), selected?.id).subscribe({
      next: (saved) => {
        this.etapas.set(selected ? this.etapas().map((item) => item.id === saved.id ? saved : item) : [...this.etapas(), saved]);
        this.saving.set(false);
        this.modalEtapaOpen.set(false);
        this.selectedEtapa.set(null);
        this.toastr.success('Etapa salva.', 'Gestao de Pessoas');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(error?.error || 'Nao foi possivel salvar a etapa.', 'Erro');
      },
    });
  }

  advance(): void {
    const selected = this.selected();
    if (!selected || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.service.advance(selected.id, this.movimentoForm.controls.observacoes.value).subscribe({
      next: (saved) => this.replaceProcesso(saved, saved.status === 'Concluido' ? 'Processo concluido.' : 'Etapa avancada.'),
      error: (error) => this.failSave(error?.error || 'Nao foi possivel avancar a etapa.'),
    });
  }

  advanceItem(item: GestaoPessoasProcesso): void {
    if (this.saving()) {
      return;
    }
    this.select(item);
    if (!this.canMove() || !item.etapaAtualId || this.isFinalized(item)) {
      this.toastr.info('Este processo ainda nao pode ser avancado.', 'Gestao de Pessoas');
      return;
    }
    this.saving.set(true);
    this.service.advance(item.id, this.movimentoForm.controls.observacoes.value).subscribe({
      next: (saved) => this.replaceProcesso(saved, saved.status === 'Concluido' ? 'Processo concluido.' : 'Etapa avancada.'),
      error: (error) => this.failSave(error?.error || 'Nao foi possivel avancar a etapa.'),
    });
  }

  back(): void {
    const selected = this.selected();
    if (!selected || this.saving()) {
      return;
    }
    this.saving.set(true);
    this.service.back(selected.id, this.movimentoForm.controls.observacoes.value).subscribe({
      next: (saved) => this.replaceProcesso(saved, 'Processo voltou para a etapa anterior.'),
      error: (error) => this.failSave(error?.error || 'Nao foi possivel voltar a etapa.'),
    });
  }

  backItem(item: GestaoPessoasProcesso): void {
    if (this.saving()) {
      return;
    }
    this.select(item);
    if (!this.canMove() || this.etapaAtualIndexFor(item) <= 0 || this.isFinalized(item)) {
      this.toastr.info('Nao ha etapa anterior para este processo.', 'Gestao de Pessoas');
      return;
    }
    this.saving.set(true);
    this.service.back(item.id, this.movimentoForm.controls.observacoes.value).subscribe({
      next: (saved) => this.replaceProcesso(saved, 'Processo voltou para a etapa anterior.'),
      error: (error) => this.failSave(error?.error || 'Nao foi possivel voltar a etapa.'),
    });
  }

  openCancel(item?: GestaoPessoasProcesso): void {
    if (item) {
      this.select(item);
    }
    this.cancelForm.reset({ motivoCancelamento: '' });
    this.cancelModalOpen.set(true);
  }

  cancelProcesso(): void {
    const selected = this.selected();
    if (!selected || this.cancelForm.invalid || this.saving()) {
      this.cancelForm.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.service.cancel(selected.id, this.cancelForm.controls.motivoCancelamento.value).subscribe({
      next: (saved) => {
        this.cancelModalOpen.set(false);
        this.replaceProcesso(saved, 'Processo cancelado.');
      },
      error: (error) => this.failSave(error?.error || 'Nao foi possivel cancelar o processo.'),
    });
  }

  etapasDoItem(item: GestaoPessoasProcesso): GestaoPessoasEtapa[] {
    return this.etapas()
      .filter((etapa) => etapa.ativa && etapa.tipoProcesso === item.tipoProcesso)
      .sort((a, b) => a.ordem - b.ordem || a.nome.localeCompare(b.nome));
  }

  etapaAtualIndexFor(item: GestaoPessoasProcesso | null): number {
    return item ? this.etapasDoItem(item).findIndex((etapa) => etapa.id === item.etapaAtualId) : -1;
  }

  etapaClassFor(item: GestaoPessoasProcesso, etapa: GestaoPessoasEtapa, index: number): string {
    if (!item.etapaAtualId) {
      return 'pending';
    }
    if (item.status === 'Concluido') {
      return 'done';
    }
    const current = this.etapaAtualIndexFor(item);
    if (etapa.id === item.etapaAtualId) {
      return 'current';
    }
    return index < current ? 'done' : 'pending';
  }

  etapaClass(etapa: GestaoPessoasEtapa, index: number): string {
    const selected = this.selected();
    return selected ? this.etapaClassFor(selected, etapa, index) : 'pending';
  }

  stepColorFor(item: GestaoPessoasProcesso, index: number): string {
    if (item.status === 'Concluido') {
      return '#16a34a';
    }
    const current = this.etapaAtualIndexFor(item);
    if (current >= 0 && index < current) {
      return '#16a34a';
    }
    if (index === current) {
      return '#0ea5e9';
    }
    return '#dc2626';
  }

  stepColor(index: number): string {
    const selected = this.selected();
    return selected ? this.stepColorFor(selected, index) : '#dc2626';
  }

  onFlowStepClick(item: GestaoPessoasProcesso, index: number, event: MouseEvent): void {
    event.stopPropagation();
    this.select(item);
    const current = this.etapaAtualIndexFor(item);
    if (!this.canMove() || this.isFinalized(item) || current < 0) {
      return;
    }
    if (index === current || index === current + 1) {
      this.advanceItem(item);
    }
  }

  isFinalized(item = this.selected()): boolean {
    return !!item && ['Cancelado', 'Concluido'].includes(item.status);
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

  private replaceProcesso(saved: GestaoPessoasProcesso, message: string): void {
    this.processos.set(this.processos().map((item) => item.id === saved.id ? saved : item));
    this.selected.set(saved);
    this.movimentoForm.reset({ observacoes: '' });
    this.saving.set(false);
    this.toastr.success(message, 'Gestao de Pessoas');
  }

  private failSave(message: string): void {
    this.saving.set(false);
    this.toastr.error(message, 'Erro');
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private uniqueSorted(values: Array<string | null | undefined>): string[] {
    return Array.from(new Set(values.map((value) => String(value ?? '').trim()).filter(Boolean)))
      .sort((a, b) => a.localeCompare(b));
  }

  private defaultEmpresaValue(): string {
    const unidadeNome = this.user()?.unidadeNome ?? '';
    return this.empresas().some((empresa) => empresa.nome === unidadeNome) ? unidadeNome : '';
  }
}
