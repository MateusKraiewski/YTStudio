import { Component, inject, OnInit, signal } from '@angular/core';
import { Api, ApiError, UpdateInfo } from './api';

const RECHECK_MS = 6 * 60 * 60 * 1000;

/** Faixa no topo avisando que há versão nova, com o botão que atualiza sem sair do app. */
@Component({
  selector: 'app-update-banner',
  template: `
    @if (info(); as i) {
      @if (i.available && !dismissed()) {
        <div class="banner" role="status">
          @if (state() === 'installing') {
            <span>Baixando e instalando a versão {{ i.latest }}… o YTStudio vai fechar e abrir de novo sozinho.</span>
          } @else {
            <span>Nova versão <strong>{{ i.latest }}</strong> disponível (você está na {{ i.current }}).</span>
            @if (i.pageUrl) {
              <button class="link-inline" (click)="notes(i.pageUrl)">O que mudou</button>
            }
            <span class="spacer"></span>
            @if (error()) {
              <span class="error small">{{ error() }}</span>
            }
            <button class="primary" (click)="install()" [disabled]="!i.canInstall">Atualizar agora</button>
            <button class="icon" title="Lembrar depois" (click)="dismissed.set(true)">✕</button>
          }
        </div>
      }
    }
  `,
})
export class UpdateBanner implements OnInit {
  private readonly api = inject(Api);
  protected readonly info = signal<UpdateInfo | null>(null);
  protected readonly dismissed = signal(false);
  protected readonly state = signal<'idle' | 'installing'>('idle');
  protected readonly error = signal('');

  ngOnInit() {
    this.check();
    setInterval(() => this.check(), RECHECK_MS);
  }

  private async check() {
    try {
      this.info.set(await this.api.checkUpdate());
    } catch {
      /* sem internet: tenta de novo mais tarde */
    }
  }

  protected async install() {
    if (document.querySelector('.dirty') && !confirm('Há alterações não salvas que serão perdidas. Atualizar mesmo assim?')) return;
    this.error.set('');
    this.state.set('installing');
    try {
      await this.api.installUpdate();
    } catch (e) {
      this.state.set('idle');
      this.error.set(e instanceof ApiError ? e.message : 'Não foi possível atualizar agora.');
    }
  }

  protected notes(url: string) {
    this.api.openUrl(url).catch(() => {});
  }
}
