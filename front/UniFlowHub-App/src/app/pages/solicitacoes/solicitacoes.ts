import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { DatePipe, isPlatformBrowser } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { PLATFORM_ID } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../core/auth.service';
import { SolicitacaoPayload, SolicitacaoRH, SolicitacaoRHComunicação, Unidade } from '../../core/models';
import { SolicitacoesService } from '../../core/solicitacoes.service';
import { ThemeService } from '../../core/theme.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { UnidadesService } from '../../core/unidades.service';
import { ToastrService } from 'ngx-toastr';
import { Router } from '@angular/router';
import { NgxSpinnerService } from 'ngx-spinner';

const ALLOWED_ATTACHMENT_TYPES = [
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/webp',
];
const ALLOWED_ATTACHMENT_EXTENSIONS = ['.pdf', '.doc', '.docx', '.jpg', '.jpeg', '.png', '.gif', '.webp'];
type SolicitacaoSortField = 'id' | 'titulo' | 'tipoSolicitacao' | 'departamento' | 'status' | 'dataSolicitacao';
type RhColumnKey = 'pendentes' | 'atendimento' | 'concluidos';

interface RhColumn {
  key: RhColumnKey;
  title: string;
  subtitle: string;
  items: SolicitacaoRH[];
}

@Component({
  selector: 'app-solicitacoes',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './solicitacoes.html',
  styleUrl: './solicitacoes.scss',
})
export class SolicitacoesPage implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly service = inject(SolicitacoesService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly theme = inject(ThemeService);

  readonly solicitacoes = signal<SolicitacaoRH[]>([]);
  readonly unidades = signal<Unidade[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly updating = signal(false);
  readonly rating = signal(false);
  readonly createModalOpen = signal(false);
  readonly editModalOpen = signal(false);
  readonly selected = signal<SolicitacaoRH | null>(null);
  readonly detailTab = signal<'detalhes' | 'comunicacao'>('detalhes');
  readonly comunicacoes = signal<SolicitacaoRHComunicação[]>([]);
  readonly loadingComunicacoes = signal(false);
  readonly sendingMessage = signal(false);
  readonly selectedFileName = signal('');
  readonly profileMenuOpen = signal(false);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortField = signal<SolicitacaoSortField>('id');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly draggingSolicitacaoId = signal<number | null>(null);
  readonly dragTargetColumn = signal<RhColumnKey | null>(null);
  readonly user = computed(() => this.auth.user());
  readonly canManageRh = computed(() => this.auth.hasAccess('rh-admin'));
  readonly canApproveRh = computed(() => this.auth.hasAccess('rh-aprovacao'));
  readonly minhasSolicitacoes = computed(() => {
    const currentUserId = this.user()?.id;
    const items = this.canManageRh() || this.canApproveRh()
      ? this.solicitacoes()
      : this.solicitacoes().filter((item) => !currentUserId || item.userid === currentUserId);
    return this.sortItems(items);
  });
  readonly solicitacoesVisiveis = computed(() => this.minhasSolicitacoes().filter((item) => this.isInVisibleDateWindow(item)));
  readonly rhColumns = computed<RhColumn[]>(() => {
    const items = this.pagedSolicitacoes();
    const columns: Omit<RhColumn, 'subtitle'>[] = [
      {
        key: 'pendentes',
        title: 'Pendente',
        items: items.filter((item) => this.columnForSolicitacao(item) === 'pendentes'),
      },
      {
        key: 'atendimento',
        title: 'Em atendimento',
        items: items.filter((item) => this.columnForSolicitacao(item) === 'atendimento'),
      },
      {
        key: 'concluidos',
        title: 'Concluidos',
        items: items.filter((item) => this.columnForSolicitacao(item) === 'concluidos'),
      },
    ];

    return columns.map((column) => ({
      ...column,
      subtitle: `${column.items.length} solicitacao(oes)`,
    }));
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.solicitacoesVisiveis().length / this.pageSize())));
  readonly currentPage = computed(() => this.safePage());
  readonly pagedSolicitacoes = computed(() => this.solicitacoesVisiveis().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));
  readonly avaliacoesPendentes = computed(() => this.solicitacoesVisiveis().filter((item) => this.canEvaluate(item)).length);
  readonly abertas = computed(() => this.solicitacoesVisiveis().filter((item) => item.status === 'Aberta').length);
  readonly encerradas = computed(() => this.solicitacoesVisiveis().filter((item) => !!item.dataEncerramento).length);
  private selectedFile: File | null = null;
  private communicationRefreshId: ReturnType<typeof setInterval> | null = null;

  @ViewChild('anexoInput') private anexoInput?: ElementRef<HTMLInputElement>;

  readonly form = this.fb.nonNullable.group({
    unidade: ['', Validators.required],
    titulo: ['', Validators.required],
    tipoSolicitacao: ['Admissao', Validators.required],
    solicitante: ['', Validators.required],
    departamento: ['', Validators.required],
    descricao: ['', Validators.required],
    anexossUrl: [''],
    prioridade: ['Media', Validators.required],
    responsavel: [''],
    status: ['Aberta', Validators.required],
    observacoes: [''],
  });

  readonly editForm = this.fb.nonNullable.group({
    unidade: ['', Validators.required],
    titulo: ['', Validators.required],
    tipoSolicitacao: ['', Validators.required],
    solicitante: ['', Validators.required],
    departamento: ['', Validators.required],
    descricao: ['', Validators.required],
    anexossUrl: [''],
    prioridade: ['', Validators.required],
    responsavel: [''],
    status: ['Aberta', Validators.required],
    observacoes: [''],
  });

  readonly satisfactionForm = this.fb.nonNullable.group({
    solicitacaoId: [0],
    nota: [0, [Validators.required, Validators.min(1), Validators.max(5)]],
    comentario: [''],
  });

  readonly messageForm = this.fb.nonNullable.group({
    mensagem: ['', Validators.required],
  });
  readonly approvalForm = this.fb.nonNullable.group({
    observacoesAprovacao: [''],
  });

  setSort(field: SolicitacaoSortField): void {
    if (this.sortField() === field) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortField.set(field);
      this.sortDirection.set(field === 'id' || field === 'dataSolicitacao' ? 'desc' : 'asc');
    }
    this.page.set(1);
  }

  previousPage(): void {
    this.page.set(Math.max(1, this.safePage() - 1));
  }

  nextPage(): void {
    this.page.set(Math.min(this.totalPages(), this.safePage() + 1));
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    if (file && !this.isAllowedAttachment(file)) {
      input.value = '';
      this.selectedFile = null;
      this.selectedFileName.set('');
      this.toastr.warning('Envie apenas arquivos DOC, PDF ou imagens.', 'Formato invalido');
      return;
    }

    this.selectedFile = file;
    this.selectedFileName.set(file?.name ?? '');
  }

  private isAllowedAttachment(file: File): boolean {
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    return ALLOWED_ATTACHMENT_TYPES.includes(file.type) || ALLOWED_ATTACHMENT_EXTENSIONS.includes(extension);
  }

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    const user = this.auth.user();
    if (user) {
      this.form.patchValue({
        unidade: this.getUserUnidadeName(),
        solicitante: user.nome,
        departamento: user.departamento,
      });
    }

    this.loadUnidades();
    this.load();
  }

  ngOnDestroy(): void {
    this.stopCommunicationRefresh();
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
      error: () => this.toastr.error('Não foi possível carregar as unidades.', 'Erro'),
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.list().subscribe({
      next: (items) => {
        this.solicitacoes.set(items);
        this.page.set(1);
        this.loading.set(false);
        void this.spinner.hide();
      },
      error: () => {
        this.loading.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível carregar suas solicitações.', 'Erro');
      },
    });
  }

  openCreateModal(): void {
    this.createModalOpen.set(true);
  }

  closeCreateModal(): void {
    if (!this.saving()) {
      this.createModalOpen.set(false);
    }
  }

  openEditModal(item: SolicitacaoRH): void {
    this.selected.set(item);
    this.detailTab.set('detalhes');
    this.editForm.reset({
      unidade: item.unidade,
      titulo: item.titulo,
      tipoSolicitacao: item.tipoSolicitacao,
      solicitante: item.solicitante,
      departamento: item.departamento,
      descricao: item.descricao,
      anexossUrl: item.anexossUrl,
      prioridade: item.prioridade,
      responsavel: item.responsavel,
      status: item.status || 'Aberta',
      observacoes: item.observacoes,
    });
    if (this.canEdit(item)) {
      this.editForm.enable({ emitEvent: false });
    } else {
      this.editForm.disable({ emitEvent: false });
    }
    this.approvalForm.reset({ observacoesAprovacao: item.observacoesAprovacao || '' });
    this.editModalOpen.set(true);
  }

  closeEditModal(): void {
    if (!this.updating()) {
      this.editModalOpen.set(false);
      this.selected.set(null);
    }
  }

  updateSolicitacao(): void {
    const selected = this.selected();
    if (!selected || this.editForm.invalid || this.updating()) {
      this.editForm.markAllAsTouched();
      if (this.editForm.invalid) {
        this.toastr.warning('Confira os campos obrigatórios antes de salvar.', 'Atenção');
      }
      return;
    }

    this.updating.set(true);
    this.service.update(selected.id, this.editForm.getRawValue()).subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((item) => item.id === updated.id ? updated : item));
        this.updating.set(false);
        this.closeEditModal();
        this.toastr.success('Solicitacao atualizada com sucesso.', 'RH');
      },
      error: (error) => {
        this.updating.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível atualizar a solicitação.', error), 'Erro');
      },
    });
  }

  onSolicitacaoDragStart(event: DragEvent, item: SolicitacaoRH): void {
    if (!this.canMoveSolicitacao(item) || this.updating()) {
      event.preventDefault();
      return;
    }

    this.draggingSolicitacaoId.set(item.id);
    event.dataTransfer?.setData('text/plain', String(item.id));
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
    }
  }

  onSolicitacaoDragEnd(): void {
    this.draggingSolicitacaoId.set(null);
    this.dragTargetColumn.set(null);
  }

  onSolicitacaoDragOver(event: DragEvent, columnKey: RhColumnKey): void {
    if (!this.canManageRh() || this.updating() || this.draggingSolicitacaoId() === null) {
      return;
    }

    event.preventDefault();
    this.dragTargetColumn.set(columnKey);
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  }

  onSolicitacaoDragLeave(columnKey: RhColumnKey): void {
    if (this.dragTargetColumn() === columnKey) {
      this.dragTargetColumn.set(null);
    }
  }

  onSolicitacaoDrop(event: DragEvent, columnKey: RhColumnKey): void {
    event.preventDefault();
    if (!this.canManageRh() || this.updating()) {
      this.onSolicitacaoDragEnd();
      return;
    }

    const draggedId = Number(event.dataTransfer?.getData('text/plain') || this.draggingSolicitacaoId());
    const item = this.solicitacoes().find((solicitacao) => solicitacao.id === draggedId);
    this.onSolicitacaoDragEnd();

    if (!item || this.columnForSolicitacao(item) === columnKey) {
      return;
    }

    this.moveSolicitacao(item, columnKey);
  }

  sendMessage(): void {
    const selected = this.selected();
    if (!selected || this.isFinalized(selected) || this.messageForm.invalid || this.sendingMessage()) {
      this.messageForm.markAllAsTouched();
      if (this.isFinalized(selected)) {
        this.toastr.info('Solicitações encerradas ou canceladas não permitem novas mensagens.', 'RH');
      } else if (this.messageForm.invalid) {
        this.toastr.warning('Escreva uma mensagem antes de enviar.', 'Atenção');
      }
      return;
    }

    this.sendingMessage.set(true);
    this.service.sendComunicação(selected.id, this.messageForm.controls.mensagem.value).subscribe({
      next: (message) => {
        this.comunicacoes.set([...this.comunicacoes(), message]);
        this.messageForm.reset({ mensagem: '' });
        this.sendingMessage.set(false);
        this.toastr.success('Mensagem enviada.', 'Comunicação');
      },
      error: (error) => {
        this.sendingMessage.set(false);
        this.toastr.error(this.getErrorMessage('Não foi possível enviar a mensagem.', error), 'Erro');
      },
    });
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
        this.toastr.error(this.getErrorMessage('Não foi possível carregar a comunicação.', error), 'Erro');
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
    if (!selected || !this.editModalOpen()) {
      return;
    }

    this.service.listComunicacoes(selected.id).subscribe({
      next: (items) => {
        if (this.selected()?.id === selected.id && this.editModalOpen()) {
          this.comunicacoes.set(items);
        }
      },
      error: () => undefined,
    });
  }

  submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      if (this.form.invalid) {
        this.toastr.warning('Confira os campos obrigatórios antes de enviar.', 'Atenção');
      }
      return;
    }

    this.saving.set(true);

    const payload: SolicitacaoPayload = {
      ...this.form.getRawValue(),
      userid: this.user()?.id ?? 0,
    };

    this.service.create(payload, this.selectedFile).subscribe({
      next: (created) => {
        this.solicitacoes.set([created, ...this.solicitacoes()]);
        this.form.patchValue({
          titulo: '',
          descricao: '',
          anexossUrl: '',
          responsavel: '',
          prioridade: 'Media',
          status: 'Aberta',
          observacoes: '',
        });
        this.selectedFile = null;
        this.selectedFileName.set('');
        if (this.anexoInput?.nativeElement) {
          this.anexoInput.nativeElement.value = '';
        }
        this.closeCreateModal();
        this.toastr.success('Solicitacao enviada para aprovacao do gestor.', 'Enviado');
        this.saving.set(false);
      },
      error: (error) => {
        this.toastr.error(this.getErrorMessage('Não foi possível criar a solicitação.', error), 'Erro');
        this.saving.set(false);
      },
    });
  }

  setRating(item: SolicitacaoRH, nota: number): void {
    this.satisfactionForm.patchValue({ solicitacaoId: item.id, nota });
  }

  submitSatisfaction(item: SolicitacaoRH): void {
    if (!this.canEvaluate(item) || this.rating()) {
      return;
    }

    if (this.satisfactionForm.controls.solicitacaoId.value !== item.id) {
      this.satisfactionForm.patchValue({ solicitacaoId: item.id });
    }

    if (this.satisfactionForm.invalid) {
      this.satisfactionForm.markAllAsTouched();
      this.toastr.warning('Escolha uma nota de 1 a 5 estrelas.', 'Atenção');
      return;
    }

    this.rating.set(true);
    const value = this.satisfactionForm.getRawValue();
    this.service.rateSatisfaction(item.id, value.nota, value.comentario).subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((solicitacao) => solicitacao.id === updated.id ? updated : solicitacao));
        this.satisfactionForm.reset({ solicitacaoId: 0, nota: 0, comentario: '' });
        this.rating.set(false);
        this.toastr.success('Obrigado pela avaliação do atendimento.', 'Satisfação');
      },
      error: () => {
        this.rating.set(false);
        this.toastr.error('Não foi possível registrar a avaliação.', 'Erro');
      },
    });
  }

  canEvaluate(item: SolicitacaoRH): boolean {
    const current = this.user();
    return !!current && item.userid === current.id && item.avaliacaoPendente && !!item.dataEncerramento;
  }

  canEdit(item: SolicitacaoRH): boolean {
    const current = this.user();
    return !!current
      && !this.isApprovalPending(item)
      && !this.isFinalized(item)
      && (this.canManageRh() || item.userid === current.id);
  }

  canMoveSolicitacao(item: SolicitacaoRH): boolean {
    return this.canManageRh() && !this.isApprovalPending(item) && !this.isFinalized(item) && !this.updating();
  }

  canApprove(item: SolicitacaoRH | null): boolean {
    return !!item && this.canApproveRh() && this.isApprovalPending(item) && !this.isFinalized(item);
  }

  approveSolicitacao(aprovada: boolean): void {
    const selected = this.selected();
    if (!selected || !this.canApprove(selected) || this.updating()) {
      return;
    }

    this.updating.set(true);
    this.service.approve(selected.id, aprovada, this.approvalForm.controls.observacoesAprovacao.value).subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((item) => item.id === updated.id ? updated : item));
        this.selected.set(updated);
        this.updating.set(false);
        this.closeEditModal();
        this.toastr.success(aprovada ? 'Solicitacao aprovada e enviada para o RH.' : 'Solicitacao reprovada.', 'Aprovacao');
      },
      error: (error) => {
        this.updating.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel registrar a aprovacao.', error), 'Erro');
      },
    });
  }

  isFinalized(item: SolicitacaoRH | null): boolean {
    return !!item && (
      !!item.dataEncerramento
      || item.status === 'Concluida'
      || item.status === 'Cancelada'
      || item.status === 'Reprovada pelo gestor'
    );
  }

  isApprovalPending(item: SolicitacaoRH | null): boolean {
    return !!item
      && !this.isFinalized(item)
      && (item.aprovacaoPendente || (!item.aprovada && this.normalize(item.status).includes('aguardando aprovacao')));
  }

  private moveSolicitacao(item: SolicitacaoRH, columnKey: RhColumnKey): void {
    if (columnKey === 'concluidos') {
      this.closeSolicitacaoFromBoard(item);
      return;
    }

    if (this.isFinalized(item)) {
      this.reopenSolicitacaoFromBoard(item, this.statusForColumn(columnKey));
      return;
    }

    this.updateSolicitacaoStatusFromBoard(item, this.statusForColumn(columnKey));
  }

  private updateSolicitacaoStatusFromBoard(item: SolicitacaoRH, status: string): void {
    this.updating.set(true);
    this.service.update(item.id, this.solicitacaoUpdatePayload(item, status)).subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((solicitacao) => solicitacao.id === updated.id ? updated : solicitacao));
        if (this.selected()?.id === updated.id) {
          this.selected.set(updated);
          this.editForm.patchValue({ status: updated.status });
        }
        this.updating.set(false);
        this.toastr.success(`Solicitacao #${updated.id} movida para ${updated.status}.`, 'RH');
      },
      error: (error) => {
        this.updating.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel alterar o status da solicitacao.', error), 'Erro');
      },
    });
  }

  private closeSolicitacaoFromBoard(item: SolicitacaoRH): void {
    this.updating.set(true);
    this.service.close(item.id, 'Concluida pelo quadro Kanban do RH.').subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((solicitacao) => solicitacao.id === updated.id ? updated : solicitacao));
        if (this.selected()?.id === updated.id) {
          this.selected.set(updated);
          this.editForm.patchValue({ status: updated.status });
        }
        this.updating.set(false);
        this.toastr.success(`Solicitacao #${updated.id} concluida.`, 'RH');
      },
      error: (error) => {
        this.updating.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel concluir a solicitacao.', error), 'Erro');
      },
    });
  }

  private reopenSolicitacaoFromBoard(item: SolicitacaoRH, status: string): void {
    this.updating.set(true);
    this.service.reopen(item.id).subscribe({
      next: (reopened) => {
        if (this.normalize(reopened.status) === this.normalize(status)) {
          this.solicitacoes.set(this.solicitacoes().map((solicitacao) => solicitacao.id === reopened.id ? reopened : solicitacao));
          this.updating.set(false);
          this.toastr.success(`Solicitacao #${reopened.id} reaberta.`, 'RH');
          return;
        }

        this.service.update(reopened.id, this.solicitacaoUpdatePayload(reopened, status)).subscribe({
          next: (updated) => {
            this.solicitacoes.set(this.solicitacoes().map((solicitacao) => solicitacao.id === updated.id ? updated : solicitacao));
            this.updating.set(false);
            this.toastr.success(`Solicitacao #${updated.id} movida para ${updated.status}.`, 'RH');
          },
          error: (error) => {
            this.updating.set(false);
            this.toastr.error(this.getErrorMessage('Nao foi possivel alterar o status da solicitacao.', error), 'Erro');
          },
        });
      },
      error: (error) => {
        this.updating.set(false);
        this.toastr.error(this.getErrorMessage('Nao foi possivel reabrir a solicitacao.', error), 'Erro');
      },
    });
  }

  private solicitacaoUpdatePayload(item: SolicitacaoRH, status: string): Omit<SolicitacaoPayload, 'userid'> {
    return {
      unidade: item.unidade,
      titulo: item.titulo,
      tipoSolicitacao: item.tipoSolicitacao,
      solicitante: item.solicitante,
      departamento: item.departamento,
      descricao: item.descricao,
      anexossUrl: item.anexossUrl,
      prioridade: item.prioridade,
      responsavel: item.responsavel,
      status,
      observacoes: item.observacoes,
    };
  }

  private statusForColumn(columnKey: RhColumnKey): string {
    const statuses: Record<RhColumnKey, string> = {
      pendentes: 'Pendente',
      atendimento: 'Em atendimento',
      concluidos: 'Concluida',
    };

    return statuses[columnKey];
  }

  private columnForSolicitacao(item: SolicitacaoRH): RhColumnKey {
    if (this.isFinalized(item)) {
      return 'concluidos';
    }

    const status = this.normalize(item.status);
    if (status.includes('atendimento') || status.includes('andamento')) {
      return 'atendimento';
    }

    return 'pendentes';
  }

  private isInVisibleDateWindow(item: SolicitacaoRH): boolean {
    const createdAt = new Date(item.dataSolicitacao);
    if (Number.isNaN(createdAt.getTime())) {
      return false;
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const yesterday = new Date(today);
    yesterday.setDate(today.getDate() - 1);

    const tomorrow = new Date(today);
    tomorrow.setDate(today.getDate() + 1);

    return createdAt >= yesterday && createdAt < tomorrow;
  }

  isUserUnidadeLocked(): boolean {
    return this.auth.hasAnyRole(['Gestor', 'Usuario']) && !!this.getUserUnidadeName();
  }

  private getUserUnidadeName(): string {
    const user = this.user();
    if (!user) {
      return '';
    }

    return user.unidadeNome || this.unidades().find((unidade) => unidade.id === user.unidadeId)?.nome || '';
  }

  private safePage(): number {
    return Math.min(Math.max(this.page(), 1), this.totalPages());
  }

  private sortItems(items: SolicitacaoRH[]): SolicitacaoRH[] {
    const field = this.sortField();
    const direction = this.sortDirection() === 'asc' ? 1 : -1;
    return items.slice().sort((a, b) => {
      const aValue = a[field];
      const bValue = b[field];
      const result = typeof aValue === 'number' && typeof bValue === 'number'
        ? aValue - bValue
        : String(aValue ?? '').localeCompare(String(bValue ?? ''));
      return result * direction;
    });
  }

  private normalize(value: string | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

  private getErrorMessage(fallback: string, error?: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error;
      }

      if (error.error?.title) {
        return error.error.title;
      }

      if (error.error?.errors) {
        const messages = Object.values(error.error.errors).flat();
        if (messages.length > 0) {
          return messages.join(' ');
        }
      }
    }

    return fallback;
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
}
