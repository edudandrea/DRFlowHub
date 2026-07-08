import { DatePipe, isPlatformBrowser } from '@angular/common';
import { Component, DestroyRef, HostListener, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../core/auth.service';
import { GestaoPessoasService } from '../../core/gestao-pessoas.service';
import {
  Empresa,
  GestaoPessoasCargo,
  GestaoPessoasCargoItemPayload,
  GestaoPessoasColaborador,
  GestaoPessoasEtapa,
  GestaoPessoasItem,
  GestaoPessoasItemTipo,
  GestaoPessoasProcesso,
  GestaoPessoasTipoProcesso,
  Unidade,
  User,
} from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';
import { UnidadesService } from '../../core/unidades.service';

type Tab = 'processos' | 'etapas' | 'cargos' | 'itens' | 'colaboradores';

@Component({
  selector: 'app-gestao-pessoas',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './gestao-pessoas.html',
  styleUrl: './gestao-pessoas.scss',
})
export class GestaoPessoasPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
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
  readonly cargosCadastro = signal<GestaoPessoasCargo[]>([]);
  readonly itens = signal<GestaoPessoasItem[]>([]);
  readonly colaboradores = signal<GestaoPessoasColaborador[]>([]);
  readonly empresas = signal<Empresa[]>([]);
  readonly unidades = signal<Unidade[]>([]);
  readonly users = signal<User[]>([]);
  readonly selected = signal<GestaoPessoasProcesso | null>(null);
  readonly selectedEtapa = signal<GestaoPessoasEtapa | null>(null);
  readonly selectedCargo = signal<GestaoPessoasCargo | null>(null);
  readonly selectedItem = signal<GestaoPessoasItem | null>(null);
  readonly selectedColaborador = signal<GestaoPessoasColaborador | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly modalProcessoOpen = signal(false);
  readonly modalEtapaOpen = signal(false);
  readonly modalCargoOpen = signal(false);
  readonly modalItemOpen = signal(false);
  readonly modalColaboradorOpen = signal(false);
  readonly modalRetiradaOpen = signal(false);
  readonly cancelModalOpen = signal(false);
  readonly profileMenuOpen = signal(false);
  readonly activeTab = signal<Tab>('processos');
  readonly tipoFiltro = signal<GestaoPessoasTipoProcesso | 'Todos'>('Todos');
  readonly search = signal('');

  readonly canManage = computed(() => this.auth.hasAnyAccess(['rh-admin', 'rh', 'gestao-pessoas', 'gestao-pessoas-admin', 'cartao-ponto']) || this.auth.hasAnyRole(['RH']));
  readonly canManageCargos = computed(() => this.canManage());
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
  readonly cargos = computed(() => this.cargosCadastro().filter((item) => item.ativo).map((item) => item.nome));
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
  readonly itensAtivos = computed(() => this.itens().filter((item) => item.ativo));

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

  readonly cargoForm = this.fb.nonNullable.group({
    nome: ['', Validators.required],
    departamento: [''],
    descricao: [''],
    ativo: [true],
  });

  readonly itemForm = this.fb.nonNullable.group({
    tipo: ['EPI' as GestaoPessoasItemTipo, Validators.required],
    nome: ['', Validators.required],
    codigo: [''],
    tamanho: [''],
    descricao: [''],
    ativo: [true],
  });

  readonly cargoItemForm = this.fb.nonNullable.group({
    itemId: [0],
    quantidade: [1],
    obrigatorio: [true],
  });
  readonly cargoItensDraft = signal<GestaoPessoasCargoItemPayload[]>([]);
  readonly cargoAcessosDraft = signal<string[]>([]);

  readonly colaboradorForm = this.fb.nonNullable.group({
    nome: ['', Validators.required],
    cpf: [''],
    email: [''],
    telefone: [''],
    departamento: [''],
    cargoId: [0],
    unidadeId: [0],
    dataNascimento: [''],
    dataAdmissao: [''],
    status: ['Ativo'],
    observacoes: [''],
  });

  readonly retiradaForm = this.fb.nonNullable.group({
    colaboradorId: [0, [Validators.required, Validators.min(1)]],
    itemId: [0, [Validators.required, Validators.min(1)]],
    quantidade: [1, [Validators.required, Validators.min(1)]],
    dataRetirada: [this.todayInput()],
    dataDevolucao: [''],
    status: ['Retirado'],
    observacoes: [''],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }
    this.load();
    this.loadCatalogos();
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => {
        const routeTab = this.route.snapshot.data['tab'] as Tab | undefined;
        const tab = routeTab ?? (params.get('tab') as Tab | null) ?? 'processos';
        if (tab && this.canUseTab(tab)) {
          this.activeTab.set(tab);
          return;
        }
        this.activeTab.set('processos');
      });
  }

  loadCatalogos(): void {
    this.unidadesService.listEmpresas().subscribe({
      next: (empresas) => this.empresas.set(empresas.slice().sort((a, b) => a.numero - b.numero || a.nome.localeCompare(b.nome))),
      error: () => this.toastr.error('Nao foi possivel carregar as empresas.', 'Gestao de Pessoas'),
    });

    this.unidadesService.list().subscribe({
      next: (unidades) => this.unidades.set(unidades.slice().sort((a, b) => a.empresaNumero - b.empresaNumero || a.numeroRevenda - b.numeroRevenda)),
      error: () => this.unidades.set([]),
    });

    this.auth.listUsers().subscribe({
      next: (users) => this.users.set(users),
      error: () => this.users.set([]),
    });

    this.loadCargos();
    this.loadItens();
    this.loadColaboradores();
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
    if (!this.canUseTab(tab)) {
      return;
    }
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

  loadCargos(): void {
    this.service.listCargos().subscribe({
      next: (cargos) => this.cargosCadastro.set(cargos),
      error: () => this.toastr.error('Nao foi possivel carregar os cargos.', 'Gestao de Pessoas'),
    });
  }

  loadItens(): void {
    this.service.listItens().subscribe({
      next: (itens) => this.itens.set(itens),
      error: () => this.toastr.error('Nao foi possivel carregar EPIs e uniformes.', 'Gestao de Pessoas'),
    });
  }

  loadColaboradores(): void {
    this.service.listColaboradores().subscribe({
      next: (colaboradores) => {
        this.colaboradores.set(colaboradores);
        this.selectedColaborador.set(colaboradores[0] ?? null);
      },
      error: () => this.toastr.error('Nao foi possivel carregar os colaboradores.', 'Gestao de Pessoas'),
    });
  }

  openNewCargo(): void {
    this.selectedCargo.set(null);
    this.cargoItensDraft.set([]);
    this.cargoAcessosDraft.set([]);
    this.cargoItemForm.reset({ itemId: 0, quantidade: 1, obrigatorio: true });
    this.cargoForm.reset({ nome: '', departamento: '', descricao: '', ativo: true });
    this.modalCargoOpen.set(true);
  }

  editCargo(cargo: GestaoPessoasCargo): void {
    this.selectedCargo.set(cargo);
    this.cargoItensDraft.set(cargo.itens.map((item) => ({ itemId: item.itemId, quantidade: item.quantidade, obrigatorio: item.obrigatorio })));
    this.cargoAcessosDraft.set([...(cargo.acessos ?? [])]);
    this.cargoForm.reset({
      nome: cargo.nome,
      departamento: cargo.departamento,
      descricao: cargo.descricao,
      ativo: cargo.ativo,
    });
    this.modalCargoOpen.set(true);
  }

  addCargoItem(): void {
    const raw = this.cargoItemForm.getRawValue();
    if (!raw.itemId) {
      return;
    }
    const next = this.cargoItensDraft().filter((item) => item.itemId !== raw.itemId);
    next.push({ itemId: raw.itemId, quantidade: Math.max(1, Number(raw.quantidade) || 1), obrigatorio: raw.obrigatorio });
    this.cargoItensDraft.set(next);
    this.cargoItemForm.reset({ itemId: 0, quantidade: 1, obrigatorio: true });
  }

  removeCargoItem(itemId: number): void {
    this.cargoItensDraft.set(this.cargoItensDraft().filter((item) => item.itemId !== itemId));
  }

  toggleCargoAcesso(chave: string): void {
    const current = this.cargoAcessosDraft();
    this.cargoAcessosDraft.set(current.includes(chave) ? current.filter((item) => item !== chave) : [...current, chave]);
  }

  hasCargoAcesso(chave: string): boolean {
    return this.cargoAcessosDraft().includes(chave);
  }

  submitCargo(): void {
    if (this.cargoForm.invalid || this.saving()) {
      this.cargoForm.markAllAsTouched();
      this.toastr.warning('Informe o nome do cargo.', 'Atencao');
      return;
    }
    this.saving.set(true);
    const selected = this.selectedCargo();
    this.service.saveCargo({ ...this.cargoForm.getRawValue(), itens: this.cargoItensDraft(), acessos: this.cargoAcessosDraft() }, selected?.id).subscribe({
      next: (saved) => {
        this.cargosCadastro.set(selected ? this.cargosCadastro().map((item) => item.id === saved.id ? saved : item) : [...this.cargosCadastro(), saved]);
        this.saving.set(false);
        this.modalCargoOpen.set(false);
        this.toastr.success('Cargo salvo.', 'Gestao de Pessoas');
      },
      error: (error) => this.failSave(error?.error || 'Nao foi possivel salvar o cargo.'),
    });
  }

  openNewItem(tipo: GestaoPessoasItemTipo = 'EPI'): void {
    this.selectedItem.set(null);
    this.itemForm.reset({ tipo, nome: '', codigo: '', tamanho: '', descricao: '', ativo: true });
    this.modalItemOpen.set(true);
  }

  editItem(item: GestaoPessoasItem): void {
    this.selectedItem.set(item);
    this.itemForm.reset({
      tipo: item.tipo,
      nome: item.nome,
      codigo: item.codigo,
      tamanho: item.tamanho,
      descricao: item.descricao,
      ativo: item.ativo,
    });
    this.modalItemOpen.set(true);
  }

  submitItem(): void {
    if (this.itemForm.invalid || this.saving()) {
      this.itemForm.markAllAsTouched();
      this.toastr.warning('Informe tipo e nome.', 'Atencao');
      return;
    }
    this.saving.set(true);
    const selected = this.selectedItem();
    this.service.saveItem(this.itemForm.getRawValue(), selected?.id).subscribe({
      next: (saved) => {
        this.itens.set(selected ? this.itens().map((item) => item.id === saved.id ? saved : item) : [...this.itens(), saved]);
        this.saving.set(false);
        this.modalItemOpen.set(false);
        this.toastr.success('Item salvo.', 'Gestao de Pessoas');
      },
      error: (error) => this.failSave(error?.error || 'Nao foi possivel salvar o item.'),
    });
  }

  openNewColaborador(): void {
    this.selectedColaborador.set(null);
    this.colaboradorForm.reset({
      nome: '',
      cpf: '',
      email: '',
      telefone: '',
      departamento: '',
      cargoId: 0,
      unidadeId: 0,
      dataNascimento: '',
      dataAdmissao: '',
      status: 'Ativo',
      observacoes: '',
    });
    this.modalColaboradorOpen.set(true);
  }

  editColaborador(colaborador: GestaoPessoasColaborador): void {
    this.selectedColaborador.set(colaborador);
    this.colaboradorForm.reset({
      nome: colaborador.nome,
      cpf: colaborador.cpf,
      email: colaborador.email,
      telefone: colaborador.telefone,
      departamento: colaborador.departamento,
      cargoId: colaborador.cargoId ?? 0,
      unidadeId: colaborador.unidadeId ?? 0,
      dataNascimento: this.toDateInput(colaborador.dataNascimento),
      dataAdmissao: this.toDateInput(colaborador.dataAdmissao),
      status: colaborador.status || 'Ativo',
      observacoes: colaborador.observacoes,
    });
    this.modalColaboradorOpen.set(true);
  }

  submitColaborador(): void {
    if (this.colaboradorForm.invalid || this.saving()) {
      this.colaboradorForm.markAllAsTouched();
      this.toastr.warning('Informe o nome do colaborador.', 'Atencao');
      return;
    }
    this.saving.set(true);
    const selected = this.selectedColaborador();
    const raw = this.colaboradorForm.getRawValue();
    if (!selected && (!raw.cpf.trim() || !raw.email.trim())) {
      this.saving.set(false);
      this.toastr.warning('Informe CPF e email para criar o usuario do colaborador.', 'Atencao');
      return;
    }
    const payload = {
      ...raw,
      cargoId: raw.cargoId > 0 ? raw.cargoId : null,
      unidadeId: raw.unidadeId > 0 ? raw.unidadeId : null,
      dataNascimento: raw.dataNascimento || null,
      dataAdmissao: raw.dataAdmissao || null,
    };
    this.service.saveColaborador(payload, selected?.id).subscribe({
      next: (saved) => {
        this.colaboradores.set(selected ? this.colaboradores().map((item) => item.id === saved.id ? saved : item) : [saved, ...this.colaboradores()]);
        this.selectedColaborador.set(saved);
        this.saving.set(false);
        this.modalColaboradorOpen.set(false);
        this.toastr.success('Colaborador salvo.', 'Gestao de Pessoas');
      },
      error: (error) => this.failSave(error?.error || 'Nao foi possivel salvar o colaborador.'),
    });
  }

  openRetirada(colaborador: GestaoPessoasColaborador): void {
    this.selectedColaborador.set(colaborador);
    this.retiradaForm.reset({ colaboradorId: colaborador.id, itemId: 0, quantidade: 1, dataRetirada: this.todayInput(), dataDevolucao: '', status: 'Retirado', observacoes: '' });
    this.modalRetiradaOpen.set(true);
  }

  openRetiradaItem(item: GestaoPessoasItem): void {
    this.selectedColaborador.set(null);
    this.retiradaForm.reset({ colaboradorId: 0, itemId: item.id, quantidade: 1, dataRetirada: this.todayInput(), dataDevolucao: '', status: 'Retirado', observacoes: '' });
    this.modalRetiradaOpen.set(true);
  }

  submitRetirada(): void {
    if (this.retiradaForm.invalid || this.saving()) {
      this.retiradaForm.markAllAsTouched();
      return;
    }
    const raw = this.retiradaForm.getRawValue();
    const colaborador = this.colaboradores().find((item) => item.id === raw.colaboradorId);
    if (!colaborador) {
      this.toastr.warning('Selecione o colaborador que esta retirando o item.', 'Atencao');
      return;
    }
    this.saving.set(true);
    this.service.addRetirada(colaborador.id, {
      itemId: raw.itemId,
      quantidade: raw.quantidade,
      dataRetirada: raw.dataRetirada,
      dataDevolucao: raw.dataDevolucao || null,
      status: raw.status,
      observacoes: raw.observacoes,
    }).subscribe({
      next: (retirada) => {
        const updated = { ...colaborador, retiradas: [retirada, ...colaborador.retiradas] };
        this.colaboradores.set(this.colaboradores().map((item) => item.id === colaborador.id ? updated : item));
        this.selectedColaborador.set(updated);
        this.saving.set(false);
        this.modalRetiradaOpen.set(false);
        this.toastr.success('Retirada registrada.', 'Gestao de Pessoas');
      },
      error: (error) => this.failSave(error?.error || 'Nao foi possivel registrar a retirada.'),
    });
  }

  itemLabel(itemId: number): string {
    const item = this.itens().find((entry) => entry.id === itemId);
    return item ? `${item.tipo} - ${item.nome}${item.tamanho ? ' / ' + item.tamanho : ''}` : 'Item';
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

  private toDateInput(value: string | null | undefined): string {
    return value ? value.slice(0, 10) : '';
  }

  private todayInput(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private canUseTab(tab: Tab): boolean {
    if (tab === 'cargos') {
      return this.canManageCargos();
    }
    if (['etapas', 'itens', 'colaboradores'].includes(tab)) {
      return this.canManage();
    }
    return true;
  }
}
