import { Component, inject, input, OnDestroy, OnInit, output, signal } from '@angular/core';
import { Agenda } from './agenda';
import { Api, ApiError, fileName, Publication, time } from './api';
import { Channels } from './channels';
import { Scripts } from './scripts';

type Tab = 'scripts' | 'agenda' | 'channels';

const REMINDER_POLL_MS = 30_000;

/** Tela principal com o arquivo aberto: abas e botão de bloquear. */
@Component({
  selector: 'app-studio',
  imports: [Agenda, Channels, Scripts],
  template: `
    <header class="topbar">
      <strong>YTStudio</strong>
      <nav class="tabs">
        <button [class.on]="tab() === 'scripts'" (click)="tab.set('scripts')">Roteiros</button>
        <button [class.on]="tab() === 'agenda'" (click)="tab.set('agenda')">Agenda</button>
        <button [class.on]="tab() === 'channels'" (click)="tab.set('channels')">Canais</button>
      </nav>
      <span class="spacer"></span>
      <span class="muted small" [title]="path() ?? ''">{{ name(path()) }}</span>
      <button (click)="lock()" title="Fecha o arquivo e apaga a chave da memória">🔒 Bloquear</button>
    </header>
    @for (r of reminders(); track r.id) {
      <div class="banner reminder" role="alert">
        <span>⏰ <strong>{{ r.title }}</strong> está agendado para {{ when(r) }}.</span>
        <span class="spacer"></span>
        <button (click)="openAgenda(r)">Ver na agenda</button>
        <button class="icon" title="Fechar" (click)="dismiss(r.id)">✕</button>
      </div>
    }
    @switch (tab()) {
      @case ('scripts') {
        <app-scripts (locked)="locked.emit()" />
      }
      @case ('agenda') {
        <app-agenda (locked)="locked.emit()" />
      }
      @case ('channels') {
        <app-channels (locked)="locked.emit()" />
      }
    }
  `,
})
export class Studio implements OnInit, OnDestroy {
  private readonly api = inject(Api);
  readonly path = input<string | null>(null);
  readonly locked = output<void>();
  protected readonly tab = signal<Tab>('scripts');
  protected readonly name = fileName;
  protected readonly reminders = signal<Publication[]>([]);
  private timer?: ReturnType<typeof setInterval>;

  ngOnInit() {
    this.poll();
    this.timer = setInterval(() => this.poll(), REMINDER_POLL_MS);
  }

  ngOnDestroy() {
    clearInterval(this.timer);
  }

  private async poll() {
    try {
      const due = await this.api.takeReminders();
      if (due.length) this.reminders.update((list) => [...list, ...due]);
    } catch (e) {
      if (e instanceof ApiError && e.status === 423) this.locked.emit();
    }
  }

  protected when(p: Publication) {
    const d = new Date(p.scheduledAt);
    const sameDay = d.toDateString() === new Date().toDateString();
    return sameDay ? `hoje às ${time(p.scheduledAt)}` : `${d.toLocaleDateString('pt-BR')} às ${time(p.scheduledAt)}`;
  }

  protected dismiss(id: string) {
    this.reminders.update((list) => list.filter((r) => r.id !== id));
  }

  protected openAgenda(p: Publication) {
    this.dismiss(p.id);
    this.tab.set('agenda');
  }

  protected async lock() {
    if (document.querySelector('.dirty') && !confirm('Há alterações não salvas. Bloquear mesmo assim?')) return;
    await this.api.lock();
    this.locked.emit();
  }
}
