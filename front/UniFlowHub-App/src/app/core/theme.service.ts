import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'pink' | 'green' | 'nissan' | 'renault' | 'gm' | 'peugeot-citroen' | 'bajaj' | 'geely';

export interface ThemeOption {
  value: ThemeMode;
  label: string;
}

const THEME_KEY = 'uniflowhub.theme';
const THEME_OPTIONS: ThemeOption[] = [
  { value: 'light', label: 'UniFlowHub' },
  { value: 'dark', label: 'Dark' },
  { value: 'pink', label: 'Pink' },
  { value: 'green', label: 'Green' },
  { value: 'nissan', label: 'Nissan' },
  { value: 'renault', label: 'Renault' },
  { value: 'gm', label: 'GM' },
  { value: 'peugeot-citroen', label: 'Peugeot/Citroen' },
  { value: 'bajaj', label: 'Bajaj' },
  { value: 'geely', label: 'Geely' },
];

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly mode = signal<ThemeMode>(this.loadTheme());

  readonly options = THEME_OPTIONS;
  readonly current = computed(() => this.mode());
  readonly isDark = computed(() => this.mode() === 'dark' || this.mode() === 'geely');
  readonly label = computed(() => this.options.find((item) => item.value === this.mode())?.label ?? 'Light');

  constructor() {
    this.apply(this.mode());
  }

  toggle(): void {
    this.setTheme(this.isDark() ? 'light' : 'dark');
  }

  setTheme(mode: ThemeMode | string): void {
    const nextMode = this.isThemeMode(mode) ? mode : 'light';
    this.mode.set(nextMode);
    this.apply(nextMode);
    if (this.hasStorage()) {
      localStorage.setItem(THEME_KEY, nextMode);
    }
  }

  private apply(mode: ThemeMode): void {
    if (!this.document.documentElement) {
      return;
    }

    this.document.documentElement.setAttribute('data-theme', mode);
  }

  private loadTheme(): ThemeMode {
    const documentTheme = this.document.documentElement?.getAttribute('data-theme');

    if (this.hasStorage()) {
      const stored = localStorage.getItem(THEME_KEY);
      if (this.isThemeMode(stored)) {
        return stored;
      }
    }

    return this.isThemeMode(documentTheme) ? documentTheme : 'light';
  }

  private hasStorage(): boolean {
    return typeof localStorage !== 'undefined';
  }

  private isThemeMode(value: unknown): value is ThemeMode {
    return typeof value === 'string' && THEME_OPTIONS.some((item) => item.value === value);
  }
}
