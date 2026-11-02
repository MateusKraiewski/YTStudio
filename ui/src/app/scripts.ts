import { Component, computed, HostListener, inject, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { marked } from 'marked';
import {
  Api, ApiError, Channel, minutes, ScriptDetail, ScriptInput, ScriptStatus, ScriptSummary,
  shortDate, STATUSES, VersionSummary,
} from './api';

interface Draft {
  title: string;
  channelId: string | null;
  status: ScriptStatus;
  tags: string;
  content: string;
  description: string;
  thumbnailIdea: string;
}

function toDraft(s: ScriptDetail | null, channelId: string | null): Draft {
  return {
    title: s?.title ?? '',
    channelId: s ? s.channelId : channelId,
    status: s?.status ?? 'Ideia',
    tags: s?.tags.join(', ') ?? '',
    content: s?.content ?? '',
    description: s?.description ?? '',
    thumbnailIdea: s?.thumbnailIdea ?? '',
  };
}

function toInput(d: Draft): ScriptInput {
  return {
    title: d.title,
    channelId: d.channelId || null,
    status: d.status,
    tags: d.tags.split(','),
    content: d.content,
    description: d.description || null,
    thumbnailIdea: d.thumbnailIdea || null,
  };
}

function countWords(text: string): number {
  return text.split(/\s+/).filter((w) => /[\p{L}\p{N}]/u.test(w)).length;
}

/** Roteiros: lista com filtros à esquerda, editor Markdown à direita. */
@Component({
  selector: 'app-scripts',
  imports: [FormsModule],
  template: `
    <main class="split">
      <aside class="list">
        <div class="list-head">
          <h2>Roteiros <span class="muted">({{ scripts().length }})</span></h2>
          <button class="primary" (click)="startNew()">+ Novo</button>
        </div>
        <input class="search" type="search" placeholder="Buscar no título, tags ou texto…"
          [ngModel]="query()" (ngModelChange)="query.set($event); reload()" />
        <div class="filters">
          <select [ngModel]="filterChannel()" (ngModelChange)="filterChannel.set($event); reload()" aria-label="Canal">
            <option value="">Todos os canais</option>
            @for (c of channels(); track c.id) {
              <option [value]="c.id">{{ c.name }}</option>
            }
          </select>
          <select [ngModel]="filterStatus()" (ngModelChange)="filterStatus.set($event); reload()" aria-label="Status">
            <option value="">Todos os status</option>
            @for (st of statuses; track st) {
              <option [value]="st">{{ st }}</option>
            }
          </select>
        </div>
        @for (s of scripts(); track s.id) {
          <button class="channel script-item" [class.active]="s.id === current()?.id" (click)="open(s.id)">
            <span class="dot" [style.background]="channelColor(s.channelId)"></span>
            <span>
              <strong>{{ s.title }}</strong>
              <span class="muted small">
                <span class="chip" [attr.data-status]="s.status">{{ s.status }}</span>
                {{ channelName(s.channelId) }} · {{ minutes(s.readingSeconds) }}
              </span>
            </span>
          </button>
        } @empty {
          <p class="muted">{{ hasFilter() ? 'Nenhum roteiro encontrado.' : 'Nenhum roteiro ainda. Clique em "+ Novo".' }}</p>
        }
      </aside>

      <section class="editor wide" [class.dirty]="dirty()">
        @if (draft(); as d) {
          <form (ngSubmit)="save()" class="script-form">
            <input class="title-input" name="title" [(ngModel)]="d.title" (ngModelChange)="touch()" placeholder="Título do vídeo" maxlength="300" />
            <div class="row3">
              <label>Canal
                <select name="channel" [(ngModel)]="d.channelId" (ngModelChange)="touch()">
                  <option [ngValue]="null">Sem canal</option>
                  @for (c of channels(); track c.id) {
                    <option [ngValue]="c.id">{{ c.name }}</option>
                  }
                </select>
              </label>
              <label>Status
                <select name="status" [(ngModel)]="d.status" (ngModelChange)="touch()">
                  @for (st of statuses; track st) {
                    <option [value]="st">{{ st }}</option>
                  }
                </select>
              </label>
              <label>Tags (separe por vírgula)
                <input name="tags" [(ngModel)]="d.tags" (ngModelChange)="touch()" placeholder="carros, review, honda" />
              </label>
            </div>

            <div class="text-tabs">
              <button type="button" [class.on]="view() === 'edit'" (click)="view.set('edit')">Texto</button>
              <button type="button" [class.on]="view() === 'preview'" (click)="view.set('preview')">Prévia</button>
              <span class="spacer"></span>
              <span class="muted small">{{ words() }} palavras · {{ minutes(readingSeconds()) }} de leitura</span>
            </div>
            @if (view() === 'edit') {
              <textarea name="content" class="content" [(ngModel)]="d.content" (ngModelChange)="touch()"
                placeholder="Escreva o roteiro aqui. Dá para usar Markdown: # Título, **negrito**, - lista…"></textarea>
            } @else {
              <article class="preview content" [innerHTML]="html()"></article>
            }

            <details class="extra">
              <summary>Descrição e thumbnail</summary>
              <label>Descrição do vídeo no YouTube
                <textarea name="description" rows="5" [(ngModel)]="d.description" (ngModelChange)="touch()"></textarea>
              </label>
              <label>Ideia de thumbnail
                <input name="thumb" [(ngModel)]="d.thumbnailIdea" (ngModelChange)="touch()" maxlength="1000" />
              </label>
            </details>

            @if (error()) {
              <p class="error">{{ error() }}</p>
            }
            <div class="actions">
              @if (current()) {
                <button type="button" class="danger" (click)="remove()" [disabled]="busy()">Excluir</button>
                <button type="button" (click)="toggleHistory()">{{ history() ? 'Fechar histórico' : 'Histórico' }}</button>
              }
              <span class="spacer"></span>
              @if (dirty()) {
                <span class="muted small">Alterações não salvas</span>
              } @else if (current(); as c) {
                <span class="muted small">Salvo em {{ shortDate(c.updatedAt) }}</span>
              }
              <button class="primary" type="submit" [disabled]="busy()" title="Ctrl+S">{{ busy() ? 'Salvando…' : 'Salvar' }}</button>
            </div>
          </form>

          @if (history(); as versions) {
            <aside class="history">
              <h3>Versões salvas</h3>
              @for (v of versions; track v.id; let first = $first) {
                <button class="channel" [class.active]="v.id === previewVersion()?.id" (click)="showVersion(v)">
                  <span>
                    <strong>{{ shortDate(v.savedAt) }}</strong>
                    <span class="muted small">{{ v.words }} palavras{{ first ? ' · atual' : '' }}</span>
                  </span>
                </button>
              } @empty {
                <p class="muted small">Nenhuma versão ainda. Cada vez que você salva o texto, uma cópia fica aqui.</p>
              }
              @if (previewVersion(); as pv) {
                <pre class="version-text">{{ pv.content }}</pre>
                <button (click)="restore(pv.content)">Usar este texto no editor</button>
              }
            </aside>
          }
        } @else {
          <div class="center muted">Escolha um roteiro na lista ou crie um novo.</div>
        }
      </section>
    </main>
  `,
})
export class Scripts implements OnInit {
  private readonly api = inject(Api);
  readonly locked = output<void>();

  protected readonly statuses = STATUSES;
  protected readonly minutes = minutes;
  protected readonly shortDate = shortDate;

  protected readonly scripts = signal<ScriptSummary[]>([]);
  protected readonly channels = signal<Channel[]>([]);
  protected readonly query = signal('');
  protected readonly filterChannel = signal('');
  protected readonly filterStatus = signal('');
  protected readonly current = signal<ScriptDetail | null>(null);
  protected readonly draft = signal<Draft | null>(null);
  protected readonly dirty = signal(false);
  protected readonly view = signal<'edit' | 'preview'>('edit');
  protected readonly history = signal<VersionSummary[] | null>(null);
  protected readonly previewVersion = signal<{ id: string; content: string } | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  /** Muda a cada edição, para os computed abaixo recalcularem (o rascunho é mutado pelo ngModel). */
  private readonly rev = signal(0);

  protected readonly hasFilter = computed(() => !!(this.query() || this.filterChannel() || this.filterStatus()));
  protected readonly words = computed(() => (this.rev(), countWords(this.draft()?.content ?? '')));
  protected readonly readingSeconds = computed(() => Math.round((this.words() / 150) * 60));
  protected readonly html = computed(() => (this.rev(), marked.parse(this.draft()?.content ?? '', { async: false })));

  private searchTimer?: ReturnType<typeof setTimeout>;

  async ngOnInit() {
    await this.guard(async () => {
      this.channels.set(await this.api.channels());
      this.scripts.set(await this.api.scripts());
    });
  }

  @HostListener('window:keydown', ['$event'])
  protected onKey(e: KeyboardEvent) {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's' && this.draft()) {
      e.preventDefault();
      this.save();
    }
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onUnload(e: BeforeUnloadEvent) {
    if (this.dirty()) e.preventDefault();
  }

  protected channelName(id: string | null) {
    return this.channels().find((c) => c.id === id)?.name ?? 'Sem canal';
  }

  protected channelColor(id: string | null) {
    return this.channels().find((c) => c.id === id)?.color ?? 'var(--border)';
  }

  protected reload() {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.guard(async () => this.scripts.set(await this.list())), 200);
  }

  private list() {
    return this.api.scripts({ q: this.query(), channelId: this.filterChannel(), status: this.filterStatus() });
  }

  protected touch() {
    this.dirty.set(true);
    this.rev.update((r) => r + 1);
  }

  private confirmDiscard() {
    return !this.dirty() || confirm('Há alterações não salvas neste roteiro. Descartar?');
  }

  protected startNew() {
    if (!this.confirmDiscard()) return;
    this.setCurrent(null);
    this.draft.set(toDraft(null, this.filterChannel() || null));
    this.view.set('edit');
  }

  protected async open(id: string) {
    if (id === this.current()?.id || !this.confirmDiscard()) return;
    await this.guard(async () => {
      const s = await this.api.script(id);
      this.setCurrent(s);
      this.draft.set(toDraft(s, null));
    });
  }

  private setCurrent(s: ScriptDetail | null) {
    this.current.set(s);
    this.dirty.set(false);
    this.history.set(null);
    this.previewVersion.set(null);
    this.error.set('');
    this.rev.update((r) => r + 1);
  }

  protected async save() {
    const d = this.draft();
    if (!d || this.busy()) return;
    await this.guard(async () => {
      const id = this.current()?.id;
      const saved = id ? await this.api.updateScript(id, toInput(d)) : await this.api.createScript(toInput(d));
      const showHistory = !!this.history();
      this.setCurrent(saved);
      this.draft.set(toDraft(saved, null));
      this.scripts.set(await this.list());
      if (showHistory) this.history.set(await this.api.versions(saved.id));
    });
  }

  protected async remove() {
    const c = this.current();
    if (!c || !confirm(`Excluir o roteiro "${c.title}" e todas as versões? Isso não pode ser desfeito.`)) return;
    await this.guard(async () => {
      await this.api.deleteScript(c.id);
      this.setCurrent(null);
      this.draft.set(null);
      this.scripts.set(await this.list());
    });
  }

  protected async toggleHistory() {
    const c = this.current();
    if (this.history() || !c) {
      this.history.set(null);
      this.previewVersion.set(null);
      return;
    }
    await this.guard(async () => this.history.set(await this.api.versions(c.id)));
  }

  protected async showVersion(v: VersionSummary) {
    const c = this.current();
    if (!c) return;
    await this.guard(async () => this.previewVersion.set(await this.api.version(c.id, v.id)));
  }

  protected restore(content: string) {
    const d = this.draft();
    if (!d) return;
    d.content = content;
    this.view.set('edit');
    this.touch();
  }

  private async guard(work: () => Promise<void>) {
    this.busy.set(true);
    this.error.set('');
    try {
      await work();
    } catch (e) {
      if (e instanceof ApiError && e.status === 423) {
        this.locked.emit();
        return;
      }
      this.error.set(e instanceof ApiError ? e.message : 'Algo deu errado. Tente de novo.');
    } finally {
      this.busy.set(false);
    }
  }
}
