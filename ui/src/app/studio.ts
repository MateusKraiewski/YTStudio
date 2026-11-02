import { Component, inject, input, output, signal } from '@angular/core';
import { Api, fileName } from './api';
import { Channels } from './channels';
import { Scripts } from './scripts';

type Tab = 'scripts' | 'channels';

/** Tela principal com o arquivo aberto: abas e botão de bloquear. */
@Component({
  selector: 'app-studio',
  imports: [Channels, Scripts],
  template: `
    <header class="topbar">
      <strong>YTStudio</strong>
      <nav class="tabs">
        <button [class.on]="tab() === 'scripts'" (click)="tab.set('scripts')">Roteiros</button>
        <button [class.on]="tab() === 'channels'" (click)="tab.set('channels')">Canais</button>
      </nav>
      <span class="spacer"></span>
      <span class="muted small" [title]="path() ?? ''">{{ name(path()) }}</span>
      <button (click)="lock()" title="Fecha o arquivo e apaga a chave da memória">🔒 Bloquear</button>
    </header>
    @switch (tab()) {
      @case ('scripts') {
        <app-scripts (locked)="locked.emit()" />
      }
      @case ('channels') {
        <app-channels (locked)="locked.emit()" />
      }
    }
  `,
})
export class Studio {
  private readonly api = inject(Api);
  readonly path = input<string | null>(null);
  readonly locked = output<void>();
  protected readonly tab = signal<Tab>('scripts');
  protected readonly name = fileName;

  protected async lock() {
    if (document.querySelector('.dirty') && !confirm('Há alterações não salvas. Bloquear mesmo assim?')) return;
    await this.api.lock();
    this.locked.emit();
  }
}
