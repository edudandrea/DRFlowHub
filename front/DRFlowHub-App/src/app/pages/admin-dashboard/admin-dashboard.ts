import { Component, OnInit, inject, signal } from '@angular/core';
import { AuthService } from '../../core/auth.service';
import { User } from '../../core/models';

@Component({
  selector: 'app-admin-dashboard',
  imports: [],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.scss',
})
export class AdminDashboardPage implements OnInit {
  private readonly auth = inject(AuthService);
  readonly users = signal<User[]>([]);

  readonly activeUsers = () => this.users().filter((user) => user.ativo).length;
  readonly profiles = () => new Set(this.users().map((user) => user.role)).size;

  ngOnInit(): void {
    this.auth.listUsers().subscribe({ next: (users) => this.users.set(users) });
  }
}
