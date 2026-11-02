import { Component, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, ApiError, fileName, SessionInfo } from './api';

const MIN_PASSWORD = 12;

type Mode = { kind: 'menu' } | { kind: 'open'; path: string } | { kind: 'create'; path: string };

/** Tela inicial: escolher, abrir ou criar o arquivo .ytv (igual ao KeePass). */
@Component({
  selector: 'app-start',
  imports: [FormsModule],
  template: `
    <main class="center">
      <section class="card start">
        <h1>YTStudio</h1>
        <p class="muted">Seus canais, roteiros, agenda e senhas num único arquivo cifrado.</p>

        @switch (mode().kind) {
          @case ('menu') {
            @if (recent().length) {
              <h2>Arquivos recentes</h2>
              <ul class="recent">
                @for (p of recent(); track p) {
                  <li>
                    <button class="link" (click)="startOpen(p)" [title]="p">
                      <strong>{{ name(p) }}</strong>
                      <span class="muted small">{{ p }}</span>
                    </button>
                    <button class="icon" title="Tirar da lista" (click)="forget(p)">✕</button>
                  </li>
                }
              </ul>
            }
            <div class="actions">
              <button (click)="pickOpen()">Abrir arquivo…</button>
              <button class="primary" (click)="pickCreate()">Criar arquivo novo…</button>
            </div>
          }
          @case ('open') {
            <form (ngSubmit)="open()">
              <p>Abrindo <strong>{{ name(path()) }}</strong></p>
              <label>
                Senha mestra
                <input type="password" name="pw" [(ngModel)]="password" autofocus autocomplete="current-password" />
              </label>
              <div class="actions">
                <button type="button" (click)="back()">Voltar</button>
                <button class="primary" type="submit" [disabled]="busy() || !password">
                  {{ busy() ? 'Abrindo…' : 'Abrir' }}
                </button>
              </div>
            </form>
          }
          @case ('create') {
            <form (ngSubmit)="create()">
              <p>Criando <strong>{{ name(path()) }}</strong></p>
              <label>
                Senha mestra (mínimo {{ min }} caracteres)
                <input type="password" name="pw" [(ngModel)]="password" autofocus autocomplete="new-password" />
              </label>
              <label>
                Repita a senha mestra
                <input type="password" name="pw2" [(ngModel)]="confirm" autocomplete="new-password" />
              </label>
              <p class="warn small">
                Guarde essa senha num lugar seguro. Sem ela não existe como recuperar o arquivo, nem por nós.
              </p>
              <div class="actions">
                <button type="button" (click)="back()">Voltar</button>
                <button class="primary" type="submit" [disabled]="busy()">
                  {{ busy() ? 'Criando…' : 'Criar' }}
                </button>
              </div>
            </form>
          }
        }

        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
      </section>
    </main>
  `,
})
export class Start {
  private readonly api = inject(Api);

  readonly recent = input<string[]>([]);
  readonly opened = output<SessionInfo>();
  readonly recentChanged = output<void>();

  protected readonly min = MIN_PASSWORD;
  protected readonly mode = signal<Mode>({ kind: 'menu' });
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected password = '';
  protected confirm = '';

  protected readonly name = fileName;

  protected path() {
    const m = this.mode();
    return m.kind === 'menu' ? '' : m.path;
  }

  protected back() {
    this.reset({ kind: 'menu' });
  }

  protected startOpen(path: string) {
    this.reset({ kind: 'open', path });
  }

  protected async pickOpen() {
    await this.guard(async () => {
      const { path } = await this.api.pickOpen();
      if (path) this.startOpen(path);
    });
  }

  protected async pickCreate() {
    await this.guard(async () => {
      const { path } = await this.api.pickSave();
      if (path) this.reset({ kind: 'create', path });
    });
  }

  protected async forget(path: string) {
    await this.guard(async () => {
      await this.api.forgetRecent(path);
      this.recentChanged.emit();
    });
  }

  protected async open() {
    await this.guard(async () => {
      const session = await this.api.openVault(this.path(), this.password);
      this.password = '';
      this.opened.emit(session);
    });
  }

  protected async create() {
    if (this.password.length < MIN_PASSWORD) {
      this.error.set(`A senha mestra precisa ter pelo menos ${MIN_PASSWORD} caracteres.`);
      return;
    }
    if (this.password !== this.confirm) {
      this.error.set('As duas senhas não são iguais.');
      return;
    }
    await this.guard(async () => {
      const session = await this.api.createVault(this.path(), this.password);
      this.password = this.confirm = '';
      this.opened.emit(session);
    });
  }

  private reset(mode: Mode) {
    this.mode.set(mode);
    this.password = this.confirm = '';
    this.error.set('');
  }

  private async guard(work: () => Promise<void>) {
    this.busy.set(true);
    this.error.set('');
    try {
      await work();
    } catch (e) {
      this.error.set(e instanceof ApiError ? e.message : 'Algo deu errado. Tente de novo.');
    } finally {
      this.busy.set(false);
    }
  }
}
