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

  @HostListener('document:click', ['$event'])
  closeProfileMenuOnDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (!target?.closest('.profile-area')) {
      this.profileMenuOpen.set(false);
    }
  }
}
