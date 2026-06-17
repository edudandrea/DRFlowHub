import { Injectable, signal } from '@angular/core';

export type ProfileFlowMode = 'profile' | 'password';

@Injectable({ providedIn: 'root' })
export class ProfileFlowService {
  readonly mode = signal<ProfileFlowMode>('profile');
  readonly open = signal(false);

  editProfile(): void {
    this.mode.set('profile');
    this.open.set(true);
  }

  changePassword(): void {
    this.mode.set('password');
    this.open.set(true);
  }

  close(): void {
    this.open.set(false);
  }
}
