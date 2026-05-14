import { Component, ElementRef, HostListener, OnDestroy, OnInit, TemplateRef, ViewChild, computed, inject, signal } from '@angular/core';
import { DatePipe, isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { NgxSpinnerService } from 'ngx-spinner';
import { HttpErrorResponse } from '@angular/common/http';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { ChamadosTIService } from '../../core/chamados-ti.service';
import { ChamadoTI, ChamadoTIComunicacao, ChamadoTIPayload, Unidade, User } from '../../core/models';
import { ThemeService } from '../../core/theme.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { UnidadesService } from '../../core/unidades.service';

const ALLOWED_ATTACHMENT_TYPES = [
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/webp',
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
];
const ALLOWED_ATTACHMENT_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.pdf', '.doc', '.docx'];
const RUSTDESK_SERVER = '192.168.1.224';
const RUSTDESK_KEY = '0EEoPPZgEaSLlDamIVqua2oAgP0DTHsiPczrZz4WfiY=';
type TiTab = 'pendentes' | 'meus' | 'todos' | 'concluidos';
type TiSortField = 'id' | 'titulo' | 'solicitante' | 'unidade' | 'departamento' | 'prioridade' | 'status' | 'dataAbertura';

interface TicketMovementAlert {
  id: number;
  ticketId: number;
  title: string;
  date: string;
}

@Component({
  selector: 'app-ti',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './ti.html',
  styleUrl: './ti.scss',
})
export class TiPage implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(ChamadosTIService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);
  private readonly toastr = inject(ToastrService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly theme = inject(ThemeService);
  readonly chamados = signal<ChamadoTI[]>([]);
  readonly unidades = signal<Unidade[]>([]);
  readonly responsaveis = signal<User[]>([]);
  readonly selected = signal<ChamadoTI | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly updating = signal(false);
  readonly selectedFileName = signal('');
  readonly selectedFilePreviewUrl = signal<SafeResourceUrl | null>(null);
  readonly selectedFilePreviewType = signal<'image' | 'pdf' | 'download' | ''>('');
  readonly attachmentPreviewUrl = signal<SafeResourceUrl | null>(null);
  readonly attachmentPreviewType = signal<'image' | 'pdf' | 'download' | ''>('');
  readonly profileMenuOpen = signal(false);
  readonly activeTab = signal<TiTab>('pendentes');
  readonly detailTab = signal<'detalhes' | 'comunicacao'>('detalhes');
  readonly filterTerm = signal('');
  readonly dateFrom = signal('');
  readonly dateTo = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortField = signal<TiSortField>('id');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly comunicacoes = signal<ChamadoTIComunicacao[]>([]);
  readonly movementAlerts = signal<TicketMovementAlert[]>([]);
  readonly loadingComunicacoes = signal(false);
  readonly sendingMessage = signal(false);
  readonly closing = signal(false);
  readonly reopening = signal(false);
  readonly rating = signal(false);
  readonly createdTicketNumber = signal<number | null>(null);
  readonly user = computed(() => this.auth.user());
  readonly canManage = computed(() => this.auth.hasAccess('ti-admin'));
  readonly abertos = computed(() => this.chamados().filter((item) => item.status === 'Aberto').length);
  readonly pendentes = computed(() => this.chamados().filter((item) => !this.isConcluido(item) && !item.responsavel?.trim()).length);
  readonly meusChamados = computed(() => this.chamados().filter((item) => !this.isConcluido(item) && this.isMine(item)).length);
  readonly concluidos = computed(() => this.chamados().filter((item) => this.isConcluido(item)).length);
  readonly avaliacoesPendentes = computed(() => this.chamados().filter((item) => this.canEvaluate(item)).length);
  readonly chamadosRespondidos = computed(() => this.movementAlerts().length);
  readonly filteredChamados = computed(() => {
    let items = this.chamados();

    if (this.canManage()) {
      if (this.activeTab() === 'concluidos') {
        items = this.sortConcluidos(items.filter((item) => this.isConcluido(item)));
      } else {
        items = items.filter((item) => !this.isConcluido(item));
      }

      if (this.activeTab() === 'pendentes') {
        items = items.filter((item) => !item.responsavel?.trim());
      }

      if (this.activeTab() === 'meus') {
        items = items.filter((item) => this.isMine(item));
      }
    }

    const term = this.normalize(this.filterTerm());
    const from = this.parseDateFilter(this.dateFrom(), false);
    const to = this.parseDateFilter(this.dateTo(), true);
    const isConcluidosTab = this.canManage() && this.activeTab() === 'concluidos';

    const filtered = items.filter((item) => {
      const itemDate = this.getFilterDate(item, isConcluidosTab);
      const matchesTerm = !term || this.matchesFilter(item, term);
      const matchesFrom = !from || itemDate >= from;
      const matchesTo = !to || itemDate <= to;

      return matchesTerm && matchesFrom && matchesTo;
    });

    return this.sortItems(filtered);
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filteredChamados().length / this.pageSize())));
  readonly pagedChamados = computed(() => this.filteredChamados().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));
  private selectedAttachment: File | null = null;
  private selectedAttachmentObjectUrl = '';
  private attachmentObjectUrl = '';
  private ticketModalRef?: BsModalRef;
  private createTicketModalRef?: BsModalRef;
  private communicationRefreshId: ReturnType<typeof setInterval> | null = null;
  private ticketMovementRefreshId: ReturnType<typeof setInterval> | null = null;
  private knownTicketMovements = new Map<number, string>();
  private movementAlertSequence = 0;

  @ViewChild('anexoInput') private anexoInput?: ElementRef<HTMLInputElement>;

  readonly form = this.fb.nonNullable.group({
    titulo: ['', Validators.required],
    categoria: ['Suporte', Validators.required],
    descricao: ['', Validators.required],
    solicitante: ['', Validators.required],
    unidade: ['', Validators.required],
    departamento: ['', Validators.required],
    prioridade: ['Media', Validators.required],
    status: ['Aberto', Validators.required],
    responsavel: [''],
    acessoRemotoUrl: [''],
    rustDeskId: [''],
    rustDeskSenha: [''],
    rustDeskServidor: [RUSTDESK_SERVER],
    rustDeskKey: [RUSTDESK_KEY],
    equipamentoNome: [''],
    equipamentoIp: [''],
    equipamentoSistemaOperacional: [''],
    observacoes: [''],
  });

  readonly adminForm = this.fb.nonNullable.group({
    titulo: ['', Validators.required],
    categoria: ['', Validators.required],
    descricao: ['', Validators.required],
    solicitante: ['', Validators.required],
    unidade: ['', Validators.required],
    departamento: ['', Validators.required],
    prioridade: ['', Validators.required],
    status: ['', Validators.required],
    responsavel: [''],
    acessoRemotoUrl: [''],
    rustDeskId: [''],
    rustDeskSenha: [''],
    rustDeskServidor: [''],
    rustDeskKey: [''],
    equipamentoNome: [''],
    equipamentoIp: [''],
    equipamentoSistemaOperacional: [''],
    observacoes: [''],
  });

  readonly messageForm = this.fb.nonNullable.group({
    mensagem: ['', Validators.required],
  });

  readonly closeForm = this.fb.nonNullable.group({
    observacoesEncerramento: ['', Validators.required],
  });

  readonly satisfactionForm = this.fb.nonNullable.group({
    nota: [0, [Validators.required, Validators.min(1), Validators.max(5)]],
    comentario: [''],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    const user = this.user();
    if (user) {
      this.form.patchValue({
        solicitante: user.nome,
        unidade: this.getUserUnidadeName(),
        departamento: user.departamento,
        equipamentoNome: this.getBrowserComputerName(),
        equipamentoSistemaOperacional: this.getOperatingSystem(),
      });
    }

    this.loadUnidades();
    this.load();
    if (this.canManage()) {
      this.loadResponsaveis();
      this.startTicketMovementRefresh();
    }
  }

  ngOnDestroy(): void {
    this.stopCommunicationRefresh();
    this.stopTicketMovementRefresh();
  }

  loadUnidades(): void {
    this.unidadesService.list().subscribe({
      next: (unidades) => {
        this.unidades.set(unidades);
        const userUnidade = this.getUserUnidadeName();
        if (userUnidade) {
          this.form.patchValue({ unidade: userUnidade });
        }
      },
      error: () => this.toastr.error('Nao foi possivel carregar as unidades.', 'Erro'),
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.list().subscribe({
      next: (items) => {
        this.chamados.set(items);
        this.loading.set(false);
        void this.spinner.hide();
        const firstVisible = this.filteredChamados()[0] ?? items[0];
        if (!this.selected() && firstVisible) {
          this.select(firstVisible);
        }
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Nao foi possivel carregar os chamados de TI.', 'Erro');
      },
    });
  }

  loadResponsaveis(): void {
    this.auth.listAdministradores().subscribe({
      next: (users) => this.responsaveis.set(users),
      error: () => this.toastr.error('Nao foi possivel carregar os responsaveis.', 'Erro'),
    });
  }

  setTab(tab: TiTab): void {
    this.activeTab.set(tab);
    this.page.set(1);
    this.clearAttachmentPreview();
    const first = this.filteredChamados()[0] ?? null;
    this.selected.set(null);
    this.comunicacoes.set([]);
    if (first) {
      this.select(first);
    }
  }

  setSort(field: TiSortField): void {
    if (this.sortField() === field) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortField.set(field);
      this.sortDirection.set(field === 'id' || field === 'dataAbertura' ? 'desc' : 'asc');
    }
    this.page.set(1);
  }

  previousPage(): void {
    this.page.set(Math.max(1, this.safePage() - 1));
  }

  nextPage(): void {
    this.page.set(Math.min(this.totalPages(), this.safePage() + 1));
  }

  setFilter(value: string): void {
    this.filterTerm.set(value);
    const selected = this.selected();
    if (selected && !this.filteredChamados().some((item) => item.id === selected.id)) {
      const first = this.filteredChamados()[0] ?? null;
      this.selected.set(null);
      this.comunicacoes.set([]);
      if (first) {
        this.select(first);
      }
    }
  }

  clearDateFilters(): void {
    this.dateFrom.set('');
    this.dateTo.set('');
  }

  completedSearchHint(): string {
    if (!this.canManage() || this.activeTab() !== 'concluidos') {
      return '';
    }

    const hasSearch = !!this.normalize(this.filterTerm()) || !!this.dateFrom() || !!this.dateTo();
    if (hasSearch) {
      return `${this.filteredChamados().length} chamado(s) concluido(s) encontrado(s) pela consulta.`;
    }

    const hidden = Math.max(this.concluidos() - 10, 0);
    return hidden > 0
      ? `Mostrando os 10 concluidos mais recentes. Consulte por numero, titulo ou data para exibir os demais.`
      : 'Mostrando todos os chamados concluidos.';
  }

  focusPendingEvaluation(): void {
    this.filterTerm.set('');
    const pending = this.chamados().find((item) => this.canEvaluate(item));
    if (pending) {
      this.select(pending);
      this.toastr.info(`Abra o chamado #${pending.id} para avaliar o atendimento.`, 'Satisfacao');
    }
  }

  openMovementAlert(alert: TicketMovementAlert): void {
    const item = this.chamados().find((chamado) => chamado.id === alert.ticketId);
    if (!item) {
      this.toastr.info(`Chamado #${alert.ticketId} nao esta mais na lista atual.`, 'TI');
      return;
    }

    this.activeTab.set('meus');
    this.select(item);
  }

  onAttachmentSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.clearSelectedFilePreview();

    if (file && !this.isAllowedAttachment(file)) {
      input.value = '';
      this.selectedAttachment = null;
      this.selectedFileName.set('');
      this.toastr.warning('Envie apenas PDF, DOC, DOCX ou imagens JPG, PNG, GIF e WEBP.', 'Formato invalido');
      return;
    }

    this.selectedAttachment = file;
    this.selectedFileName.set(file?.name ?? '');

    if (file) {
      this.previewSelectedAttachment(file);
    }
  }

  openCreateTicketModal(template: TemplateRef<void>): void {
    this.createTicketModalRef = this.modalService.show(template, {
      animated: true,
      backdrop: true,
      class: 'modal-lg modal-dialog-centered drflow-modal-shell',
      ignoreBackdropClick: this.saving(),
      keyboard: !this.saving(),
    });
  }

  closeCreateTicketModal(): void {
    if (this.saving()) {
      return;
    }

    this.createTicketModalRef?.hide();
    this.createTicketModalRef = undefined;
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      if (this.form.invalid) {
        this.toastr.warning('Confira os campos obrigatorios antes de abrir o chamado.', 'Atencao');
      }
      return;
    }

    this.saving.set(true);
    const payload: ChamadoTIPayload = {
      ...this.form.getRawValue(),
      userid: this.user()?.id ?? 0,
    };

    this.service.create(payload, this.selectedAttachment).subscribe({
      next: (created) => {
        this.chamados.set([created, ...this.chamados()]);
        this.form.patchValue({
          titulo: '',
          descricao: '',
          unidade: this.getUserUnidadeName(),
          prioridade: 'Media',
          status: 'Aberto',
          responsavel: '',
          acessoRemotoUrl: '',
          rustDeskId: '',
          rustDeskSenha: '',
          rustDeskServidor: RUSTDESK_SERVER,
          rustDeskKey: RUSTDESK_KEY,
          equipamentoNome: this.getBrowserComputerName(),
          equipamentoIp: '',
          equipamentoSistemaOperacional: this.getOperatingSystem(),
          observacoes: '',
        });
        this.selectedAttachment = null;
        this.selectedFileName.set('');
        this.clearSelectedFilePreview();
        if (this.anexoInput?.nativeElement) {
          this.anexoInput.nativeElement.value = '';
        }
        this.saving.set(false);
        this.closeCreateTicketModal();
        this.selected.set(created);
        this.createdTicketNumber.set(created.id);
        this.toastr.success(`Chamado #${created.id} aberto com sucesso.`, 'TI');
      },
      error: (error) => {
        this.saving.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel abrir o chamado.', error), 'Erro');
      },
    });
  }

  select(item: ChamadoTI): void {
    this.selected.set(item);
    this.detailTab.set('detalhes');
    this.clearAttachmentPreview();
    this.loadComunicacoes(item.id);
    this.adminForm.patchValue({
      titulo: item.titulo,
      categoria: item.categoria,
      descricao: item.descricao,
      solicitante: item.solicitante,
      unidade: item.unidade,
      departamento: item.departamento,
      prioridade: item.prioridade,
      status: item.status,
      responsavel: item.responsavel,
      acessoRemotoUrl: item.acessoRemotoUrl,
      rustDeskId: item.rustDeskId,
      rustDeskSenha: item.rustDeskSenha,
      rustDeskServidor: item.rustDeskServidor || RUSTDESK_SERVER,
      rustDeskKey: item.rustDeskKey || RUSTDESK_KEY,
      equipamentoNome: item.equipamentoNome,
      equipamentoIp: item.equipamentoIp,
      equipamentoSistemaOperacional: item.equipamentoSistemaOperacional,
      observacoes: item.observacoes,
    });
    this.closeForm.reset({ observacoesEncerramento: item.observacoesEncerramento || '' });
    this.satisfactionForm.reset({ nota: item.satisfacaoNota ?? 0, comentario: '' });
  }

  openTicketModal(item: ChamadoTI, template: TemplateRef<void>): void {
    this.select(item);
    this.startCommunicationRefresh();
    this.ticketModalRef = this.modalService.show(template, {
      animated: true,
      backdrop: true,
      class: 'modal-xl modal-dialog-centered drflow-modal-shell ti-ticket-modal-shell',
      ignoreBackdropClick: this.updating() || this.sendingMessage() || this.closing() || this.reopening() || this.rating(),
      keyboard: !(this.updating() || this.sendingMessage() || this.closing() || this.reopening() || this.rating()),
    });
  }

  closeTicketModal(): void {
    if (this.updating() || this.sendingMessage() || this.closing() || this.reopening() || this.rating()) {
      return;
    }

    this.clearAttachmentPreview();
    this.stopCommunicationRefresh();
    this.ticketModalRef?.hide();
    this.ticketModalRef = undefined;
  }

  update(): void {
    const selected = this.selected();
    if (!selected || this.adminForm.invalid || this.updating()) {
      this.adminForm.markAllAsTouched();
      if (this.adminForm.invalid) {
        this.toastr.warning('Confira os campos obrigatorios antes de salvar.', 'Atencao');
      }
      return;
    }

    this.updating.set(true);
    this.service.update(selected.id, this.adminForm.getRawValue()).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.rememberTicketMovement(updated);
        this.selected.set(updated);
        this.updating.set(false);
        this.toastr.success('Chamado atualizado com sucesso.', 'TI');
      },
      error: (error) => {
        this.updating.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel atualizar o chamado.', error), 'Erro');
      },
    });
  }

  reopenTicket(): void {
    const selected = this.selected();
    if (!selected || this.reopening()) {
      return;
    }

    this.reopening.set(true);
    this.service.reopen(selected.id).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.rememberTicketMovement(updated);
        this.selected.set(updated);
        this.adminForm.patchValue({ status: updated.status });
        this.closeForm.reset({ observacoesEncerramento: '' });
        this.reopening.set(false);
        this.toastr.success(`Chamado #${updated.id} reaberto.`, 'TI');
      },
      error: (error) => {
        this.reopening.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel reabrir o chamado.', error), 'Erro');
      },
    });
  }

  sendMessage(): void {
    const selected = this.selected();
    if (!selected || this.messageForm.invalid || this.sendingMessage()) {
      this.messageForm.markAllAsTouched();
      if (this.messageForm.invalid) {
        this.toastr.warning('Escreva uma mensagem antes de enviar.', 'Atencao');
      }
      return;
    }

    this.sendingMessage.set(true);
    this.service.sendComunicacao(selected.id, this.messageForm.controls.mensagem.value).subscribe({
      next: (message) => {
        this.comunicacoes.set([...this.comunicacoes(), message]);
        this.rememberTicketMovement({
          ...selected,
          ultimaMovimentacao: message.dataCriacao,
        });
        this.messageForm.reset({ mensagem: '' });
        this.sendingMessage.set(false);
        this.toastr.success('Mensagem enviada.', 'Comunicacao');
      },
      error: (error) => {
        this.sendingMessage.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel enviar a mensagem.', error), 'Erro');
      },
    });
  }

  closeTicket(): void {
    const selected = this.selected();
    if (!selected || this.closing() || this.closeForm.invalid) {
      this.closeForm.markAllAsTouched();
      if (this.closeForm.invalid) {
        this.toastr.warning('Informe as observacoes de encerramento antes de concluir.', 'Atencao');
      }
      return;
    }

    this.closing.set(true);
    this.service.close(selected.id, this.closeForm.controls.observacoesEncerramento.value).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.rememberTicketMovement(updated);
        this.selected.set(updated);
        this.adminForm.patchValue({ status: updated.status });
        this.closeForm.patchValue({ observacoesEncerramento: updated.observacoesEncerramento });
        this.closing.set(false);
        this.toastr.success('Chamado encerrado com data e hora atuais.', 'TI');
      },
      error: (error) => {
        this.closing.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel encerrar o chamado.', error), 'Erro');
      },
    });
  }

  setRating(nota: number): void {
    this.satisfactionForm.patchValue({ nota });
  }

  submitSatisfaction(): void {
    const selected = this.selected();
    if (!selected || !this.canEvaluate(selected) || this.satisfactionForm.invalid || this.rating()) {
      this.satisfactionForm.markAllAsTouched();
      if (this.satisfactionForm.invalid) {
        this.toastr.warning('Escolha uma nota de 1 a 5 estrelas.', 'Atencao');
      }
      return;
    }

    this.rating.set(true);
    const formValue = this.satisfactionForm.getRawValue();
    this.service.rateSatisfaction(selected.id, formValue.nota, formValue.comentario).subscribe({
      next: (updated) => {
        this.chamados.set(this.chamados().map((item) => item.id === updated.id ? updated : item));
        this.rememberTicketMovement(updated);
        this.selected.set(updated);
        this.satisfactionForm.reset({ nota: 0, comentario: '' });
        this.rating.set(false);
        this.toastr.success('Obrigado pela avaliacao do atendimento.', 'Satisfacao');
      },
      error: (error) => {
        this.rating.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel registrar a avaliacao.', error), 'Erro');
      },
    });
  }

  previewAttachment(item = this.selected()): void {
    if (!item?.anexoImagemUrl) {
      this.toastr.info('Este chamado nao possui anexo.', 'Anexo');
      return;
    }

    this.service.downloadAttachment(item.id).subscribe({
      next: (blob) => {
        this.clearAttachmentPreview();
        this.attachmentObjectUrl = URL.createObjectURL(blob);
        this.attachmentPreviewType.set(this.getAttachmentPreviewType(item.anexoImagemUrl, blob.type));
        this.attachmentPreviewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.attachmentObjectUrl));
      },
      error: () => this.toastr.error('Nao foi possivel carregar o anexo.', 'Erro'),
    });
  }

  downloadAttachment(item = this.selected()): void {
    if (!item?.anexoImagemUrl) {
      this.toastr.info('Este chamado nao possui anexo.', 'Anexo');
      return;
    }

    this.service.downloadAttachment(item.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = this.attachmentFileName(item.anexoImagemUrl);
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toastr.error('Nao foi possivel baixar o anexo.', 'Erro'),
    });
  }

  openRemoteAccess(url = this.adminForm.controls.acessoRemotoUrl.value): void {
    const target = url.trim();
    if (!target) {
      this.toastr.info('Informe um link de acesso remoto no chamado.', 'Acesso remoto');
      return;
    }

    window.open(target, '_blank', 'noopener,noreferrer');
  }

  openRustDeskAccess(): void {
    const link = this.buildRustDeskLink();
    if (!link) {
      this.toastr.info('Este chamado ainda nao possui ID RustDesk do equipamento.', 'RustDesk');
      return;
    }

    window.location.href = link;
  }

  copyRustDeskPassword(): void {
    const password = this.adminForm.controls.rustDeskSenha.value.trim();
    if (!password) {
      this.toastr.info('Nenhuma senha RustDesk informada.', 'RustDesk');
      return;
    }

    if (!navigator.clipboard) {
      this.toastr.info('Copie a senha diretamente do campo RustDesk.', 'RustDesk');
      return;
    }

    void navigator.clipboard.writeText(password).then(
      () => this.toastr.success('Senha RustDesk copiada.', 'RustDesk'),
      () => this.toastr.error('Nao foi possivel copiar a senha.', 'RustDesk'),
    );
  }

  rustDeskLinkPreview(): string {
    return this.buildRustDeskLink() || '';
  }

  private buildRustDeskLink(): string {
    const rustDeskId = this.adminForm.controls.rustDeskId.value.trim();
    if (!rustDeskId) {
      return '';
    }

    const params = new URLSearchParams();
    const server = this.adminForm.controls.rustDeskServidor.value.trim() || RUSTDESK_SERVER;
    const key = this.adminForm.controls.rustDeskKey.value.trim() || RUSTDESK_KEY;
    if (server) {
      params.set('server', server);
    }
    if (key) {
      params.set('key', key);
    }

    const query = params.toString();
    return `rustdesk://${encodeURIComponent(rustDeskId)}${query ? `?${query}` : ''}`;
  }

  rustDeskServerLabel(): string {
    return this.adminForm.controls.rustDeskServidor.value.trim() || RUSTDESK_SERVER;
  }

  equipmentSummary(item = this.selected()): string {
    if (!item) {
      return 'Equipamento nao identificado';
    }

    const name = item.equipamentoNome || 'Nome nao informado';
    const ip = item.equipamentoIp || 'IP nao identificado';
    const os = item.equipamentoSistemaOperacional || 'Sistema operacional nao identificado';
    return `${name} - ${ip} - ${os}`;
  }

  attachmentFileName(path: string): string {
    return path.split('/').pop() || 'anexo';
  }

  private isMine(item: ChamadoTI): boolean {
    const current = this.user();
    if (!current) {
      return false;
    }

    return item.responsavel === current.nome || item.responsavel === current.email;
  }

  private isConcluido(item: ChamadoTI): boolean {
    return !!item.dataEncerramento || this.normalize(item.status) === 'concluido';
  }

  canEvaluate(item: ChamadoTI | null): boolean {
    const current = this.user();
    return !!item?.dataEncerramento && !!current && item.userid === current.id && !this.canManage() && item.avaliacaoPendente;
  }

  canReopen(item: ChamadoTI | null): boolean {
    const current = this.user();
    return !!item && !!current && !!item.dataEncerramento && (this.canManage() || item.userid === current.id);
  }

  private loadComunicacoes(id: number): void {
    this.loadingComunicacoes.set(true);
    this.service.listComunicacoes(id).subscribe({
      next: (items) => {
        this.comunicacoes.set(items);
        this.loadingComunicacoes.set(false);
      },
      error: (error) => {
        this.comunicacoes.set([]);
        this.loadingComunicacoes.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel carregar a comunicacao.', error), 'Erro');
      },
    });
  }

  private startCommunicationRefresh(): void {
    this.stopCommunicationRefresh();
    if (!this.isBrowser) {
      return;
    }

    this.communicationRefreshId = setInterval(() => this.refreshComunicacoesSilently(), 3000);
  }

  private stopCommunicationRefresh(): void {
    if (!this.communicationRefreshId) {
      return;
    }

    clearInterval(this.communicationRefreshId);
    this.communicationRefreshId = null;
  }

  private refreshComunicacoesSilently(): void {
    const selected = this.selected();
    if (!selected || !this.ticketModalRef) {
      return;
    }

    this.service.listComunicacoes(selected.id).subscribe({
      next: (items) => {
        if (this.selected()?.id === selected.id && this.ticketModalRef) {
          this.comunicacoes.set(items);
        }
      },
      error: () => undefined,
    });
  }

  private startTicketMovementRefresh(): void {
    this.stopTicketMovementRefresh();
    if (!this.isBrowser) {
      return;
    }

    this.ticketMovementRefreshId = setInterval(() => this.refreshTicketMovements(), 10000);
  }

  private stopTicketMovementRefresh(): void {
    if (!this.ticketMovementRefreshId) {
      return;
    }

    clearInterval(this.ticketMovementRefreshId);
    this.ticketMovementRefreshId = null;
  }

  private refreshTicketMovements(): void {
    const currentUser = this.user();
    if (!currentUser || !this.canManage()) {
      return;
    }

    this.service.list().subscribe({
      next: (items) => {
        const currentIds = new Set(items.map((item) => item.id));
        for (const id of [...this.knownTicketMovements.keys()]) {
          if (!currentIds.has(id)) {
            this.knownTicketMovements.delete(id);
          }
        }

        const movedTickets = items.filter((item) => this.detectAssignedTicketMovement(item));
        this.chamados.set(items);

        for (const item of movedTickets) {
          this.addMovementAlert(item);
        }
      },
      error: () => undefined,
    });
  }

  private detectAssignedTicketMovement(item: ChamadoTI): boolean {
    const key = item.ultimaMovimentacao || item.dataAbertura;
    const previous = this.knownTicketMovements.get(item.id);
    this.knownTicketMovements.set(item.id, key);

    if (!previous || previous === key || !this.isMine(item) || this.isConcluido(item)) {
      return false;
    }

    return true;
  }

  private addMovementAlert(item: ChamadoTI): void {
    this.toastr.info(`Chamado #${item.id} respondido.`, 'TI');

    const alert: TicketMovementAlert = {
      id: ++this.movementAlertSequence,
      ticketId: item.id,
      title: item.titulo,
      date: item.ultimaMovimentacao || new Date().toISOString(),
    };

    this.movementAlerts.set([alert, ...this.movementAlerts().filter((existing) => existing.ticketId !== item.id)].slice(0, 4));
  }

  private rememberTicketMovement(item: Pick<ChamadoTI, 'id' | 'ultimaMovimentacao' | 'dataAbertura'>): void {
    this.knownTicketMovements.set(item.id, item.ultimaMovimentacao || item.dataAbertura);
  }

  private matchesFilter(item: ChamadoTI, term: string): boolean {
    const createdAt = new Date(item.dataAbertura);
    const closedAt = item.dataEncerramento ? new Date(item.dataEncerramento) : null;
    const formattedDate = Number.isNaN(createdAt.getTime()) ? '' : createdAt.toLocaleDateString('pt-BR');
    const formattedClosedDate = closedAt && !Number.isNaN(closedAt.getTime()) ? closedAt.toLocaleDateString('pt-BR') : '';
    const fields = [
      String(item.id),
      item.titulo,
      item.descricao,
      item.responsavel,
      item.dataAbertura,
      item.dataEncerramento,
      formattedDate,
      formattedClosedDate,
    ];

    return fields.some((value) => this.normalize(value).includes(term));
  }

  private getFilterDate(item: ChamadoTI, useClosingDate: boolean): Date {
    const date = new Date(useClosingDate ? item.dataEncerramento || item.dataAbertura : item.dataAbertura);
    return Number.isNaN(date.getTime()) ? new Date(0) : date;
  }

  private sortConcluidos(items: ChamadoTI[]): ChamadoTI[] {
    return [...items].sort((a, b) => {
      const dateA = this.getFilterDate(a, true).getTime();
      const dateB = this.getFilterDate(b, true).getTime();
      return dateB - dateA || b.id - a.id;
    });
  }

  private normalize(value: string | number | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private safePage(): number {
    return Math.min(Math.max(this.page(), 1), this.totalPages());
  }

  private sortItems(items: ChamadoTI[]): ChamadoTI[] {
    const field = this.sortField();
    const direction = this.sortDirection() === 'asc' ? 1 : -1;
    return items.slice().sort((a, b) => {
      const aValue = a[field];
      const bValue = b[field];
      const result = typeof aValue === 'number' && typeof bValue === 'number'
        ? aValue - bValue
        : this.normalize(aValue as string | number | null | undefined).localeCompare(this.normalize(bValue as string | number | null | undefined));
      return result * direction;
    });
  }

  private getUserUnidadeName(): string {
    const user = this.user();
    if (!user) {
      return '';
    }

    return user.unidadeNome || this.unidades().find((unidade) => unidade.id === user.unidadeId)?.nome || '';
  }

  private getBrowserComputerName(): string {
    return 'Nao informado pelo navegador';
  }

  private getOperatingSystem(): string {
    if (!this.isBrowser) {
      return '';
    }

    const userAgent = navigator.userAgent;
    const navigatorWithData = navigator as Navigator & { userAgentData?: { platform?: string } };
    const platform = navigatorWithData.userAgentData?.platform || navigator.platform || '';

    if (/windows/i.test(userAgent) || /win/i.test(platform)) {
      return 'Windows';
    }

    if (/android/i.test(userAgent)) {
      return 'Android';
    }

    if (/iphone|ipad|ipod/i.test(userAgent)) {
      return 'iOS';
    }

    if (/mac/i.test(platform)) {
      return 'macOS';
    }

    if (/linux/i.test(platform)) {
      return 'Linux';
    }

    return platform || 'Nao identificado';
  }

  private parseDateFilter(value: string, endOfDay: boolean): Date | null {
    if (!value) {
      return null;
    }

    const date = new Date(`${value}T${endOfDay ? '23:59:59.999' : '00:00:00.000'}`);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  logout(): void {
    this.auth.logout();
  }

  goHome(): void {
    void this.router.navigate(['/hub']);
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

  private isAllowedAttachment(file: File): boolean {
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    return ALLOWED_ATTACHMENT_TYPES.includes(file.type) || ALLOWED_ATTACHMENT_EXTENSIONS.includes(extension);
  }

  private previewSelectedAttachment(file: File): void {
    this.selectedAttachmentObjectUrl = URL.createObjectURL(file);
    this.selectedFilePreviewType.set(this.getAttachmentPreviewType(file.name, file.type));
    this.selectedFilePreviewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.selectedAttachmentObjectUrl));
  }

  private getAttachmentPreviewType(path: string, mimeType = ''): 'image' | 'pdf' | 'download' {
    const extension = path.slice(path.lastIndexOf('.')).toLowerCase();
    if (mimeType.startsWith('image/') || ['.jpg', '.jpeg', '.png', '.gif', '.webp'].includes(extension)) {
      return 'image';
    }

    if (mimeType === 'application/pdf' || extension === '.pdf') {
      return 'pdf';
    }

    return 'download';
  }

  private clearAttachmentPreview(): void {
    if (this.attachmentObjectUrl) {
      URL.revokeObjectURL(this.attachmentObjectUrl);
      this.attachmentObjectUrl = '';
    }

    this.attachmentPreviewUrl.set(null);
    this.attachmentPreviewType.set('');
  }

  private clearSelectedFilePreview(): void {
    if (this.selectedAttachmentObjectUrl) {
      URL.revokeObjectURL(this.selectedAttachmentObjectUrl);
      this.selectedAttachmentObjectUrl = '';
    }

    this.selectedFilePreviewUrl.set(null);
    this.selectedFilePreviewType.set('');
  }

  private getErrorMessage(fallback: string, error?: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error;
      }

      if (error.error?.title) {
        return error.error.title;
      }
    }

    return fallback;
  }
}
