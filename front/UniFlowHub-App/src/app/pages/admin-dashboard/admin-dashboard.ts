import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth.service';
import { User } from '../../core/models';
import { ProfileFlowService } from '../../core/profile-flow.service';
import { ThemeService } from '../../core/theme.service';

@Component({
  selector: 'app-admin-dashboard',
  imports: [],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.scss',
})
export class AdminDashboardPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly profileFlow = inject(ProfileFlowService);
  readonly theme = inject(ThemeService);
  readonly user = computed(() => this.auth.user());
  readonly profileMenuOpen = signal(false);
  readonly users = signal<User[]>([]);
  readonly canViewRhAdmin = computed(() => this.auth.hasAccess('rh-admin'));
  readonly aniversariantesMes = computed(() => {
    const currentMonth = new Date().getMonth();
    return this.users()
      .filter((user) => user.ativo && this.birthDate(user)?.getMonth() === currentMonth)
      .sort((a, b) => (this.birthDate(a)?.getDate() ?? 0) - (this.birthDate(b)?.getDate() ?? 0));
  });

  readonly activeUsers = () => this.users().filter((user) => user.ativo).length;
  readonly profiles = () => new Set(this.users().map((user) => user.role)).size;

  ngOnInit(): void {
    this.auth.listUsers().subscribe({ next: (users) => this.users.set(users) });
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

  birthDay(user: User): string {
    const date = this.birthDate(user);
    return date ? String(date.getDate()).padStart(2, '0') : '--';
  }

  birthMonthName(): string {
    return new Intl.DateTimeFormat('pt-BR', { month: 'long' }).format(new Date());
  }

  printBirthdaysPdf(): void {
    const popup = window.open('', '_blank', 'width=960,height=720');
    if (!popup) {
      return;
    }

    const rows = this.aniversariantesMes()
      .map((user) => `
        <tr>
          <td>${this.escapeHtml(this.birthDay(user))}</td>
          <td>${this.escapeHtml(user.nome)}</td>
          <td>${this.escapeHtml(user.departamento || '-')}</td>
          <td>${this.escapeHtml(user.cargo || '-')}</td>
          <td>${this.escapeHtml(user.unidadeNome || '-')}</td>
        </tr>
      `)
      .join('');

    popup.document.write(`
      <html>
        <head>
          <title>Aniversariantes do mes</title>
          <style>
            body { font-family: Arial, sans-serif; margin: 32px; color: #111827; }
            h1 { margin: 0 0 6px; font-size: 24px; }
            p { margin: 0 0 20px; color: #4b5563; }
            table { width: 100%; border-collapse: collapse; }
            th, td { padding: 10px 12px; border: 1px solid #d1d5db; text-align: left; font-size: 13px; }
            th { background: #eef2ff; color: #1f2937; }
          </style>
        </head>
        <body>
          <h1>Aniversariantes de ${this.escapeHtml(this.birthMonthName())}</h1>
          <p>Emitido em ${new Date().toLocaleDateString('pt-BR')}</p>
          <table>
            <thead>
              <tr>
                <th>Dia</th>
                <th>Nome</th>
                <th>Departamento</th>
                <th>Cargo</th>
                <th>Unidade</th>
              </tr>
            </thead>
            <tbody>${rows || '<tr><td colspan="5">Nenhum aniversariante no mes.</td></tr>'}</tbody>
          </table>
          <script>window.onload = () => { window.print(); };</script>
        </body>
      </html>
    `);
    popup.document.close();
  }

  @HostListener('document:click', ['$event'])
  closeProfileMenuOnDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.profile-area')) {
      this.profileMenuOpen.set(false);
    }
  }

  private birthDate(user: User): Date | null {
    if (!user.dataNascimento) {
      return null;
    }

    const [datePart] = user.dataNascimento.split('T');
    const parts = datePart.split('-').map((part) => Number(part));
    if (parts.length >= 3 && parts.every((part) => Number.isFinite(part))) {
      return new Date(parts[0], parts[1] - 1, parts[2]);
    }

    const parsed = new Date(user.dataNascimento);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }

  private escapeHtml(value: string): string {
    return value
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }
}
