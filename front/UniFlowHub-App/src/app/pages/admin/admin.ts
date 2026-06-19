import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { SolicitacaoRH, SolicitacaoRHComunicação, Unidade } from '../../core/models';
import { SolicitacoesService } from '../../core/solicitacoes.service';
import { ThemeService } from '../../core/theme.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { UnidadesService } from '../../core/unidades.service';

type AdminSortField = 'id' | 'titulo' | 'solicitante' | 'unidade' | 'departamento' | 'prioridade' | 'status' | 'dataSolicitacao';
type RhColumnKey = 'pendentes' | 'atendimento' | 'concluidos';

interface RhColumn {
  key: RhColumnKey;
  title: string;
  subtitle: string;
  items: SolicitacaoRH[];
}

@Component({
  selector: 'app-admin',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './admin.html',
  styleUrl: './admin.scss',
})
export class AdminPage implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly service = inject(SolicitacoesService);
  private readonly unidadesService = inject(UnidadesService);
  private readonly spinner = inject(NgxSpinnerService);
  private readonly toastr = inject(ToastrService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly router = inject(Router);
  private readonly profileFlow = inject(ProfileFlowService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly theme = inject(ThemeService);

  readonly solicitacoes = signal<SolicitacaoRH[]>([]);
  readonly selected = signal<SolicitacaoRH | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly closing = signal(false);
  readonly reopening = signal(false);
  readonly modalOpen = signal(false);
  readonly detailTab = signal<'detalhes' | 'comunicacao'>('detalhes');
  readonly comunicacoes = signal<SolicitacaoRHComunicação[]>([]);
  readonly loadingComunicacoes = signal(false);
  readonly sendingMessage = signal(false);
  readonly attachmentPreviewUrl = signal<SafeResourceUrl | null>(null);
  readonly attachmentPreviewType = signal<'image' | 'pdf' | 'download' | null>(null);
  readonly profileMenuOpen = signal(false);
  readonly unidades = signal<Unidade[]>([]);
  private attachmentObjectUrl = '';
  private communicationRefreshId: ReturnType<typeof setInterval> | null = null;
  readonly search = signal('');
  readonly dateFrom = signal('');
  readonly dateTo = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortField = signal<AdminSortField>('id');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly draggingSolicitacaoId = signal<number | null>(null);
  readonly dragTargetColumn = signal<RhColumnKey | null>(null);
  readonly user = computed(() => this.auth.user());
  readonly canOpenSolicitacaoFromPanel = computed(() => !this.auth.hasAccess('rh-admin'));
  readonly abertas = computed(() => this.filtered().filter((item) => item.status === 'Aberta').length);
  readonly avaliacoesRespondidas = computed(() => this.filtered().filter((item) => !!item.satisfacaoNota).length);
  readonly altaPrioridade = computed(() =>
    this.filtered().filter((item) => item.prioridade === 'Alta' || item.prioridade === 'Critica').length,
  );
  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const hasDateFilter = !!this.dateFrom() || !!this.dateTo();
    const from = hasDateFilter ? this.parseDateFilter(this.dateFrom(), false) : this.startOfYesterday();
    const to = hasDateFilter ? this.parseDateFilter(this.dateTo(), true) : this.endOfToday();

    const filtered = this.solicitacoes().filter((item) => {
      const itemDate = new Date(item.dataSolicitacao);
      const matchesTerm = !term || [item.titulo, item.solicitante, item.departamento, item.status, item.prioridade]
        .join(' ')
        .toLowerCase()
        .includes(term);
      const matchesFrom = !from || itemDate >= from;
      const matchesTo = !to || itemDate <= to;

      return matchesTerm && matchesFrom && matchesTo;
    });

    return this.sortItems(filtered);
  });
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filtered().length / this.pageSize())));
  readonly currentPage = computed(() => this.safePage());
  readonly paged = computed(() => this.filtered().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));
  readonly rhColumns = computed<RhColumn[]>(() => {
    const items = this.paged();
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

  readonly form = this.fb.nonNullable.group({
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

  readonly closeForm = this.fb.nonNullable.group({
    observacoesEncerramento: ['', Validators.required],
  });

  readonly messageForm = this.fb.nonNullable.group({
    mensagem: ['', Validators.required],
  });

  ngOnInit(): void {
    if (!this.isBrowser) {
      return;
    }

    this.load();
    this.loadUnidades();
  }

  ngOnDestroy(): void {
    this.stopCommunicationRefresh();
  }

  loadUnidades(): void {
    this.unidadesService.list().subscribe({
      next: (unidades) => this.unidades.set(unidades),
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
        this.toastr.error('Não foi possível carregar as solicitações.', 'Erro');
      },
    });
  }

  select(item: SolicitacaoRH): void {
    this.selected.set(item);
    this.detailTab.set('detalhes');
    this.modalOpen.set(true);
    this.clearAttachmentPreview();
    this.form.patchValue({
      unidade: item.unidade,
      titulo: item.titulo,
      tipoSolicitacao: item.tipoSolicitacao,
      solicitante: item.solicitante,
      departamento: item.departamento,
      descricao: item.descricao,
      anexossUrl: item.anexossUrl,
      prioridade: item.prioridade,
      responsavel: item.responsavel,
      status: item.status,
      observacoes: item.observacoes,
    });
    this.closeForm.reset({ observacoesEncerramento: item.observacoesEncerramento || '' });
    this.messageForm.reset({ mensagem: '' });
  }

  closeModal(): void {
    if (this.saving() || this.closing() || this.reopening()) {
      return;
    }

    this.modalOpen.set(false);
    this.comunicacoes.set([]);
    this.clearAttachmentPreview();
  }

  previewAttachment(): void {
    const selected = this.selected();
    if (!selected?.anexossUrl) {
      this.toastr.info('Esta solicitação não possui anexo.', 'Anexo');
      return;
    }

    this.service.downloadAttachment(selected.id).subscribe({
      next: (blob) => {
        this.clearAttachmentPreview();
        this.attachmentObjectUrl = URL.createObjectURL(blob);
        this.attachmentPreviewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.attachmentObjectUrl));

        if (blob.type.startsWith('image/')) {
          this.attachmentPreviewType.set('image');
        } else if (blob.type === 'application/pdf') {
          this.attachmentPreviewType.set('pdf');
        } else {
          this.attachmentPreviewType.set('download');
        }
      },
      error: () => this.toastr.error('Não foi possível carregar o anexo.', 'Erro'),
    });
  }

  downloadAttachment(): void {
    const selected = this.selected();
    if (!selected?.anexossUrl) {
      this.toastr.info('Esta solicitação não possui anexo.', 'Anexo');
      return;
    }

    this.service.downloadAttachment(selected.id).subscribe({
      next: (blob) => {
        const objectUrl = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = objectUrl;
        link.download = this.attachmentFileName(selected.anexossUrl);
        link.click();
        URL.revokeObjectURL(objectUrl);
      },
      error: () => this.toastr.error('Não foi possível baixar o anexo.', 'Erro'),
    });
  }

  attachmentFileName(path: string): string {
    return path.split('/').pop() || 'anexo';
  }

  private clearAttachmentPreview(): void {
    if (this.attachmentObjectUrl) {
      URL.revokeObjectURL(this.attachmentObjectUrl);
      this.attachmentObjectUrl = '';
    }

    this.attachmentPreviewUrl.set(null);
    this.attachmentPreviewType.set(null);
  }

  update(): void {
    const selected = this.selected();
    if (!selected || this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      if (this.form.invalid) {
        this.toastr.warning('Confira os campos obrigatórios antes de salvar.', 'Atenção');
      }
      return;
    }

    this.saving.set(true);
    void this.spinner.show();
    this.service.update(selected.id, this.form.getRawValue()).subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((item) => item.id === updated.id ? updated : item));
        this.selected.set(updated);
        this.saving.set(false);
        void this.spinner.hide();
        this.modalOpen.set(false);
        this.stopCommunicationRefresh();
        this.toastr.success('Atendimento atualizado com sucesso.', 'Salvo');
      },
      error: () => {
        this.saving.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível salvar a atualização.', 'Erro');
      },
    });
  }

  closeSolicitacao(): void {
    const selected = this.selected();
    if (!selected || this.closing() || this.closeForm.invalid) {
      this.closeForm.markAllAsTouched();
      if (this.closeForm.invalid) {
        this.toastr.warning('Informe as observacoes de encerramento antes de concluir.', 'Atenção');
      }
      return;
    }

    this.closing.set(true);
    void this.spinner.show();
    this.service.close(selected.id, this.closeForm.controls.observacoesEncerramento.value).subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((item) => item.id === updated.id ? updated : item));
        this.selected.set(updated);
        this.form.patchValue({ status: updated.status });
        this.closeForm.patchValue({ observacoesEncerramento: updated.observacoesEncerramento });
        this.closing.set(false);
        void this.spinner.hide();
        this.toastr.success('Solicitacao encerrada com sucesso.', 'RH');
      },
      error: () => {
        this.closing.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível encerrar a solicitação.', 'Erro');
      },
    });
  }

  reopenSolicitacao(): void {
    const selected = this.selected();
    if (!selected || this.reopening()) {
      return;
    }

    this.reopening.set(true);
    void this.spinner.show();
    this.service.reopen(selected.id).subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((item) => item.id === updated.id ? updated : item));
        this.selected.set(updated);
        this.form.patchValue({ status: updated.status });
        this.closeForm.reset({ observacoesEncerramento: '' });
        this.reopening.set(false);
        void this.spinner.hide();
        this.toastr.success('Solicitacao reaberta com sucesso.', 'RH');
      },
      error: () => {
        this.reopening.set(false);
        void this.spinner.hide();
        this.toastr.error('Não foi possível reabrir a solicitação.', 'Erro');
      },
    });
  }

  onSolicitacaoDragStart(event: DragEvent, item: SolicitacaoRH): void {
    if (!this.canMoveSolicitacao(item)) {
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
    if (this.isBusy() || this.draggingSolicitacaoId() === null) {
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
    const draggedId = Number(event.dataTransfer?.getData('text/plain') || this.draggingSolicitacaoId());
    this.onSolicitacaoDragEnd();

    if (this.isBusy()) {
      return;
    }

    const item = this.solicitacoes().find((solicitacao) => solicitacao.id === draggedId);
    if (!item || !this.canMoveSolicitacao(item) || this.columnForSolicitacao(item) === columnKey) {
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
      error: () => {
        this.sendingMessage.set(false);
        this.toastr.error('Não foi possível enviar a mensagem.', 'Erro');
      },
    });
  }

  logout(): void {
    this.auth.logout();
  }

  goHome(): void {
    void this.router.navigate(['/hub']);
  }

  openNewSolicitacao(): void {
    void this.router.navigate(['/solicitacoes']);
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

  clearDateFilters(): void {
    this.dateFrom.set('');
    this.dateTo.set('');
    this.page.set(1);
  }

  setSearchTerm(value: string): void {
    this.search.set(value);
    this.page.set(1);
  }

  setDateFrom(value: string): void {
    this.dateFrom.set(value);
    this.page.set(1);
  }

  setDateTo(value: string): void {
    this.dateTo.set(value);
    this.page.set(1);
  }

  setSort(field: AdminSortField): void {
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

  isFinalized(item: SolicitacaoRH | null): boolean {
    return !!item && (
      !!item.dataEncerramento
      || item.status === 'Concluida'
      || item.status === 'Cancelada'
    );
  }

  canMoveSolicitacao(item: SolicitacaoRH): boolean {
    return !this.isBusy() && !this.isFinalized(item);
  }

  private loadComunicacoes(id: number): void {
    this.loadingComunicacoes.set(true);
    this.service.listComunicacoes(id).subscribe({
      next: (items) => {
        this.comunicacoes.set(items);
        this.loadingComunicacoes.set(false);
      },
      error: () => {
        this.comunicacoes.set([]);
        this.loadingComunicacoes.set(false);
        this.toastr.error('Não foi possível carregar a comunicação.', 'Erro');
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
    if (!selected || !this.modalOpen()) {
      return;
    }

    this.service.listComunicacoes(selected.id).subscribe({
      next: (items) => {
        if (this.selected()?.id === selected.id && this.modalOpen()) {
          this.comunicacoes.set(items);
        }
      },
      error: () => undefined,
    });
  }

  private parseDateFilter(value: string, endOfDay: boolean): Date | null {
    if (!value) {
      return null;
    }

    const date = new Date(`${value}T${endOfDay ? '23:59:59.999' : '00:00:00.000'}`);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  private startOfYesterday(): Date {
    const date = new Date();
    date.setHours(0, 0, 0, 0);
    date.setDate(date.getDate() - 1);
    return date;
  }

  private endOfToday(): Date {
    const date = new Date();
    date.setHours(23, 59, 59, 999);
    return date;
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

  private moveSolicitacao(item: SolicitacaoRH, columnKey: RhColumnKey): void {
    if (columnKey === 'concluidos') {
      this.closeSolicitacaoFromBoard(item);
      return;
    }

    const status = this.statusForColumn(columnKey);
    if (this.isFinalized(item)) {
      this.reopenSolicitacaoFromBoard(item, status);
      return;
    }

    this.updateSolicitacaoStatusFromBoard(item, status);
  }

  private updateSolicitacaoStatusFromBoard(item: SolicitacaoRH, status: string): void {
    const responsavel = this.normalize(status).includes('atendimento')
      ? this.user()?.nome || item.responsavel
      : item.responsavel;

    this.saving.set(true);
    void this.spinner.show();
    this.service.update(item.id, this.solicitacaoUpdatePayload(item, status, responsavel)).subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((solicitacao) => solicitacao.id === updated.id ? updated : solicitacao));
        if (this.selected()?.id === updated.id) {
          this.selected.set(updated);
          this.form.patchValue({ status: updated.status, responsavel: updated.responsavel });
        }
        this.saving.set(false);
        void this.spinner.hide();
        this.toastr.success(`Solicitacao #${updated.id} movida para ${updated.status}.`, 'RH');
      },
      error: () => {
        this.saving.set(false);
        void this.spinner.hide();
        this.toastr.error('Nao foi possivel mover a solicitacao.', 'Erro');
      },
    });
  }

  private closeSolicitacaoFromBoard(item: SolicitacaoRH): void {
    this.closing.set(true);
    void this.spinner.show();
    this.service.close(item.id, 'Concluida pelo Kanban do RH.').subscribe({
      next: (updated) => {
        this.solicitacoes.set(this.solicitacoes().map((solicitacao) => solicitacao.id === updated.id ? updated : solicitacao));
        if (this.selected()?.id === updated.id) {
          this.selected.set(updated);
          this.form.patchValue({ status: updated.status });
          this.closeForm.patchValue({ observacoesEncerramento: updated.observacoesEncerramento });
        }
        this.closing.set(false);
        void this.spinner.hide();
        this.toastr.success(`Solicitacao #${updated.id} concluida.`, 'RH');
      },
      error: () => {
        this.closing.set(false);
        void this.spinner.hide();
        this.toastr.error('Nao foi possivel concluir a solicitacao.', 'Erro');
      },
    });
  }

  private reopenSolicitacaoFromBoard(item: SolicitacaoRH, status: string): void {
    this.reopening.set(true);
    void this.spinner.show();
    this.service.reopen(item.id).subscribe({
      next: (reopened) => {
        this.reopening.set(false);
        void this.spinner.hide();
        this.updateSolicitacaoStatusFromBoard(reopened, status);
      },
      error: () => {
        this.reopening.set(false);
        void this.spinner.hide();
        this.toastr.error('Nao foi possivel reabrir a solicitacao.', 'Erro');
      },
    });
  }

  private solicitacaoUpdatePayload(item: SolicitacaoRH, status: string, responsavel = item.responsavel): Parameters<SolicitacoesService['update']>[1] {
    return {
      unidade: item.unidade,
      titulo: item.titulo,
      tipoSolicitacao: item.tipoSolicitacao,
      solicitante: item.solicitante,
      departamento: item.departamento,
      descricao: item.descricao,
      anexossUrl: item.anexossUrl,
      prioridade: item.prioridade,
      responsavel,
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
    if (status.includes('atendimento') || status.includes('andamento') || status.includes('analise')) {
      return 'atendimento';
    }

    return 'pendentes';
  }

  private isBusy(): boolean {
    return this.saving() || this.closing() || this.reopening();
  }

  private normalize(value: string | null | undefined): string {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .trim();
  }

}
