import { Component, inject, OnInit, signal } from '@angular/core';
import { Api, SessionInfo } from './api';
import { Start } from './start';
import { Studio } from './studio';
import { UpdateBanner } from './update-banner';

@Component({
  selector: 'app-root',
  imports: [Start, Studio, UpdateBanner],
  template: `
    <app-update-banner />
    @if (session(); as s) {
      @if (s.open) {
        <app-studio [path]="s.path" (locked)="onLocked()" />
      } @else {
        <app-start [recent]="s.recent" (opened)="session.set($event)" (recentChanged)="reload()" />
      }
    } @else if (error()) {
      <main class="center"><p class="error">{{ error() }}</p></main>
    }
  `,
})
export class App implements OnInit {
  private readonly api = inject(Api);
  protected readonly session = signal<SessionInfo | null>(null);
  protected readonly error = signal('');

  ngOnInit() {
    this.reload();
  }

  protected async reload() {
    try {
      this.session.set(await this.api.session());
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : String(e));
    }
  }

  protected onLocked() {
    this.reload();
  }
}
