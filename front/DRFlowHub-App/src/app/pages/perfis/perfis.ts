import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { AcessoSistema, PerfilSistema, PerfisService } from '../../core/perfis.service';

@Component({
  selector: 'app-perfis',
  imports: [FormsModule],
  templateUrl: './perfis.html',
  styleUrl: './perfis.scss',
})
export class PerfisPage implements OnInit {
  private readonly service = inject(PerfisService);
  private readonly toastr = inject(ToastrService);

  readonly perfis = signal<PerfilSistema[]>([]);
  readonly acessos = signal<AcessoSistema[]>([]);
  readonly selected = signal<PerfilSistema | null>(null);
  readonly nome = signal('');
  readonly acessosSelecionados = signal<string[]>([]);
  readonly modalOpen = signal(false);
  readonly saving = signal(false);
  readonly acessosPorGrupo = computed(() => {
    const groups = new Map<string, AcessoSistema[]>();
    for (const acesso of this.acessos()) {
      groups.set(acesso.grupo, [...(groups.get(acesso.grupo) ?? []), acesso]);
    }
    return Array.from(groups.entries()).map(([grupo, items]) => ({ grupo, items }));
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.listAcessos().subscribe({ next: (items) => this.acessos.set(items) });
    this.service.list().subscribe({ next: (items) => this.perfis.set(items) });
  }

  select(perfil: PerfilSistema): void {
    this.selected.set(perfil);
    this.nome.set(perfil.nome);
    this.acessosSelecionados.set([...(perfil.acessos ?? [])]);
    this.modalOpen.set(true);
  }

  novo(): void {
    this.selected.set(null);
    this.nome.set('');
    this.acessosSelecionados.set([]);
    this.modalOpen.set(true);
  }

  closeModal(): void {
    if (!this.saving()) {
      this.modalOpen.set(false);
    }
  }

  toggleAcesso(chave: string, checked: boolean): void {
    const current = this.acessosSelecionados();
    this.acessosSelecionados.set(checked ? Array.from(new Set([...current, chave])) : current.filter((item) => item !== chave));
  }

  hasAcesso(chave: string): boolean {
    return this.acessosSelecionados().includes(chave);
  }

  save(): void {
    this.saving.set(true);
    this.service.save({ nome: this.nome(), acessos: this.acessosSelecionados() }).subscribe({
      next: () => {
        this.saving.set(false);
        this.toastr.success('Perfil salvo com sucesso.', 'Perfis');
        this.modalOpen.set(false);
        this.selected.set(null);
        this.nome.set('');
        this.acessosSelecionados.set([]);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.toastr.error('Nao foi possivel salvar o perfil.', 'Erro');
      },
    });
  }
}
