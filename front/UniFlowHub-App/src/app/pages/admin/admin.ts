import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { SolicitacaoRH, SolicitacaoRHComunicação, Unidade, User } from '../../core/models';
import { SolicitacoesService } from '../../core/solicitacoes.service';
import { ThemeService } from '../../core/theme.service';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { UnidadesService } from '../../core/unidades.service';

interface BirthdayItem {
  id: number;
  nome: string;
  departamento: string;
  cargo: string;
  day: number;
  isToday: boolean;
}
type AdminSortField = 'id' | 'titulo' | 'solicitante' | 'unidade' | 'departamento' | 'prioridade' | 'status' | 'dataSolicitacao';

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
  readonly users = signal<User[]>([]);
  private attachmentObjectUrl = '';
  private communicationRefreshId: ReturnType<typeof setInterval> | null = null;
  readonly search = signal('');
  readonly dateFrom = signal('');
  readonly dateTo = signal('');
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly sortField = signal<AdminSortField>('id');
  readonly sortDirection = signal<'asc' | 'desc'>('desc');
  readonly user = computed(() => this.auth.user());
  readonly abertas = computed(() => this.solicitacoes().filter((item) => item.status === 'Aberta').length);
  readonly avaliacoesRespondidas = computed(() => this.solicitacoes().filter((item) => !!item.satisfacaoNota).length);
  readonly altaPrioridade = computed(() =>
    this.solicitacoes().filter((item) => item.prioridade === 'Alta' || item.prioridade === 'Critica').length,
  );
  readonly aniversariantesMes = computed<BirthdayItem[]>(() => {
    const today = new Date();
    const currentMonth = today.getMonth();

    return this.users()
      .map((user) => {
        const date = this.dateParts(user.dataNascimento);
        if (!date || date.month !== currentMonth) {
          return null;
        }

        return {
          id: user.id,
          nome: user.nome,
          departamento: user.departamento,
          cargo: user.cargo,
          day: date.day,
          isToday: date.day === today.getDate(),
        };
      })
      .filter((item): item is BirthdayItem => !!item)
      .sort((a, b) => a.day - b.day || a.nome.localeCompare(b.nome));
  });
  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const from = this.parseDateFilter(this.dateFrom(), false);
    const to = this.parseDateFilter(this.dateTo(), true);

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
  readonly paged = computed(() => this.filtered().slice((this.safePage() - 1) * this.pageSize(), this.safePage() * this.pageSize()));

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
    this.loadUsers();
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

  loadUsers(): void {
    this.auth.listUsers().subscribe({
      next: (users) => this.users.set(users),
      error: () => this.users.set([]),
    });
  }

  load(): void {
    this.loading.set(true);
    void this.spinner.show();
    this.service.list().subscribe({
      next: (items) => {
        this.solicitacoes.set(items);
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
    this.loadComunicacoes(item.id);
    this.startCommunicationRefresh();
  }

  closeModal(): void {
    if (this.saving() || this.closing() || this.reopening()) {
      return;
    }

    this.modalOpen.set(false);
    this.stopCommunicationRefresh();
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
    return !!item && (!!item.dataEncerramento || item.status === 'Concluida' || item.status === 'Cancelada');
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

  private dateParts(value: string): { month: number; day: number } | null {
    if (!value) {
      return null;
    }

    const match = value.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (match) {
      return { month: Number(match[2]) - 1, day: Number(match[3]) };
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return null;
    }

    return { month: date.getMonth(), day: date.getDate() };
  }
}
