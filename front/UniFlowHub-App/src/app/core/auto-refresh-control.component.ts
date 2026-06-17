import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface AutoRefreshOption {
  label: string;
  value: number;
}

@Component({
  selector: 'app-auto-refresh-control',
  imports: [DatePipe, FormsModule],
  template: `
    <div class="auto-refresh">
      <div class="refresh-status">
        <span class="status-dot"></span>
        <span>{{ updatedAt ? ('Atualizado ' + (updatedAt | date: 'dd/MM/yyyy HH:mm')) : 'Aguardando atualização' }}</span>
      </div>

      <label>
        Atualizacao
        <select [ngModel]="intervalMs" (ngModelChange)="setIntervalMs($event)">
          @for (option of options; track option.value) {
            <option [ngValue]="option.value">{{ option.label }}</option>
          }
        </select>
      </label>
    </div>
  `,
  styles: [`
    :host{display:inline-flex;min-width:0}.auto-refresh{display:flex;align-items:center;gap:12px;flex-wrap:wrap;min-width:0;color:var(--color-muted)}.refresh-status{display:inline-flex;align-items:center;gap:8px;min-height:38px;font-size:14px;font-weight:800}.status-dot{width:10px;height:10px;border-radius:999px;background:var(--color-brand-green-strong);box-shadow:0 0 0 4px color-mix(in srgb,var(--color-brand-green-strong) 18%,transparent)}label{display:inline-flex;align-items:center;gap:8px;color:var(--color-muted);font-size:12px;font-weight:800}select{width:auto;min-width:136px;height:38px;padding:0 10px;border:1px solid var(--color-border);border-radius:6px;background:var(--color-field);color:var(--color-text);font:inherit;font-size:13px;font-weight:800}:host-context([data-theme='dark']) .refresh-status,:host-context([data-theme='geely']) .refresh-status{color:#dbeafe}:host-context([data-theme='dark']) label,:host-context([data-theme='geely']) label{color:#cbd7ea}@media(max-width:640px){:host,.auto-refresh,label,select{width:100%}.refresh-status{width:100%}label{display:grid;gap:6px}}
  `],
})
export class AutoRefreshControlComponent implements OnInit, OnDestroy {
  @Input() storageKey = 'uniflowhub.autoRefresh.default';
  @Input() updatedAt: Date | string | null = null;
  @Input() loading = false;
  @Output() refresh = new EventEmitter<void>();

  readonly options: AutoRefreshOption[] = [
    { label: 'Desligada', value: 0 },
    { label: '30 segundos', value: 30_000 },
    { label: '1 minuto', value: 60_000 },
    { label: '5 minutos', value: 300_000 },
    { label: '10 minutos', value: 600_000 },
    { label: '30 minutos', value: 1_800_000 },
  ];

  intervalMs = 0;
  private timerId: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.intervalMs = this.loadInterval();
    this.restartTimer();
  }

  ngOnDestroy(): void {
    this.clearTimer();
  }

  setIntervalMs(value: number | string): void {
    const parsed = Number(value);
    this.intervalMs = Number.isFinite(parsed) ? parsed : 0;
    this.saveInterval();
    this.restartTimer();
  }

  private restartTimer(): void {
    this.clearTimer();

    if (this.intervalMs <= 0) {
      return;
    }

    this.timerId = setInterval(() => {
      if (!this.loading) {
        this.refresh.emit();
      }
    }, this.intervalMs);
  }

  private clearTimer(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }

  private loadInterval(): number {
    if (typeof localStorage === 'undefined') {
      return 0;
    }

    const stored = Number(localStorage.getItem(this.storageKey));
    return this.options.some((option) => option.value === stored) ? stored : 0;
  }

  private saveInterval(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(this.storageKey, String(this.intervalMs));
    }
  }
}
