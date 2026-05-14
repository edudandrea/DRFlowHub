import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { LoginResponse, Role, User, UserCreatePayload, UserProfileUpdatePayload, UserUpdatePayload } from './models';

const API_URL = '/api';
const SESSION_KEY = 'drflowhub.session';

interface SetupStatus {
  canCreateFirstAdmin: boolean;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly session = signal<LoginResponse | null>(this.loadSession());
  readonly user = computed(() => this.session()?.user ?? null);
  readonly token = computed(() => this.session()?.token ?? null);
  readonly isLoggedIn = computed(() => !!this.token());
  readonly isAdminArea = computed(() => this.hasAnyRole(['Admin', 'RH']));

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
  ) {}

  login(email: string, senha: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${API_URL}/auth/login`, { email, senha }).pipe(
      tap((response) => this.setSession(response)),
    );
  }

  createUser(payload: UserCreatePayload): Observable<User> {
    return this.http.post<User>(`${API_URL}/auth/register`, payload);
  }

  updateUser(id: number, payload: UserUpdatePayload): Observable<User> {
    return this.http.post<User>(`${API_URL}/users/${id}/update`, payload);
  }

  updateProfile(payload: UserProfileUpdatePayload): Observable<User> {
    return this.http.put<User>(`${API_URL}/users/me`, payload).pipe(
      tap((user) => this.updateSessionUser(user)),
    );
  }

  changePassword(senhaAtual: string, novaSenha: string): Observable<unknown> {
    return this.http.post(`${API_URL}/users/me/password`, { senhaAtual, novaSenha });
  }

  setupStatus(): Observable<SetupStatus> {
    return this.http.get<SetupStatus>(`${API_URL}/auth/setup-status`);
  }

  listUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${API_URL}/users`);
  }

  listAdministradores(): Observable<User[]> {
    return this.http.get<User[]>(`${API_URL}/users/administradores`);
  }

  logout(): void {
    this.session.set(null);
    if (this.hasStorage()) {
      sessionStorage.removeItem(SESSION_KEY);
      localStorage.removeItem(SESSION_KEY);
    }
    this.router.navigateByUrl('/login');
  }

  hasAnyRole(roles: Role[]): boolean {
    const role = this.user()?.role;
    return !!role && roles.includes(role);
  }

  hasAccess(access: string): boolean {
    const user = this.user();
    return !!user && (user.role === 'Admin' || (user.acessos ?? []).includes(access));
  }

  hasAnyAccess(accesses: string[]): boolean {
    return accesses.some((access) => this.hasAccess(access));
  }

  landingRoute(): string {
    if (this.hasAccess('dashboard-admin')) {
      return '/admin';
    }

    return '/hub';
  }

  private setSession(response: LoginResponse): void {
    this.session.set(response);
    if (this.hasStorage()) {
      localStorage.removeItem(SESSION_KEY);
      sessionStorage.setItem(SESSION_KEY, JSON.stringify(response));
    }
  }

  private updateSessionUser(user: User): void {
    const current = this.session();
    if (!current) {
      return;
    }

    const next = { ...current, user };
    this.session.set(next);
    if (this.hasStorage()) {
      sessionStorage.setItem(SESSION_KEY, JSON.stringify(next));
    }
  }

  private loadSession(): LoginResponse | null {
    if (!this.hasStorage()) {
      return null;
    }

    localStorage.removeItem(SESSION_KEY);
    const raw = sessionStorage.getItem(SESSION_KEY);
    if (!raw) {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as LoginResponse;
      if (new Date(parsed.expiresAt).getTime() <= Date.now()) {
        sessionStorage.removeItem(SESSION_KEY);
        return null;
      }

      return parsed;
    } catch {
      sessionStorage.removeItem(SESSION_KEY);
      return null;
    }
  }

  private hasStorage(): boolean {
    return typeof localStorage !== 'undefined';
  }
}
