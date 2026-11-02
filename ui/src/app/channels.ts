import { Component, computed, inject, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, ApiError, Channel, ChannelInput } from './api';

const COLORS = ['#E53935', '#FB8C00', '#FDD835', '#43A047', '#00897B', '#1E88E5', '#5E35B1', '#D81B60', '#6D4C41', '#546E7A'];

function emptyInput(): ChannelInput {
  return { name: '', url: '', niche: '', language: 'Português', color: COLORS[0], notes: '' };
}

/** Cadastro de canais (fase 1). Roteiros, agenda e cofre entram nas próximas fases. */
@Component({
  selector: 'app-channels',
  imports: [FormsModule],
  template: `
    <main class="split">
      <aside class="list">
        <div class="list-head">
          <h2>Canais <span class="muted">({{ channels().length }})</span></h2>
          <button class="primary" (click)="startNew()">+ Novo</button>
        </div>
        <input class="search" type="search" placeholder="Buscar canal…" [ngModel]="query()" (ngModelChange)="query.set($event)" />
        @for (c of filtered(); track c.id) {
          <button class="channel" [class.active]="c.id === editingId()" (click)="edit(c)">
            <span class="dot" [style.background]="c.color"></span>
            <span>
              <strong>{{ c.name }}</strong>
              <span class="muted small">{{ c.niche || 'Sem nicho' }}</span>
            </span>
          </button>
        } @empty {
          <p class="muted">{{ channels().length ? 'Nenhum canal encontrado.' : 'Nenhum canal ainda. Clique em "+ Novo".' }}</p>
        }
      </aside>

      <section class="editor">
        @if (form(); as f) {
          <form (ngSubmit)="save()">
            <h2>{{ editingId() ? 'Editar canal' : 'Novo canal' }}</h2>
            <label>Nome <input name="name" [(ngModel)]="f.name" required maxlength="200" /></label>
            <label>Link do canal <input name="url" [(ngModel)]="f.url" placeholder="https://www.youtube.com/@seucanal" /></label>
            <div class="row">
              <label>Nicho <input name="niche" [(ngModel)]="f.niche" placeholder="Carros, Política…" maxlength="100" /></label>
              <label>Idioma <input name="language" [(ngModel)]="f.language" maxlength="50" /></label>
            </div>
            <fieldset>
              <legend>Cor</legend>
              <div class="colors">
                @for (color of colors; track color) {
                  <button type="button" class="swatch" [class.selected]="f.color === color" [style.background]="color"
                    [attr.aria-label]="'Cor ' + color" (click)="f.color = color"></button>
                }
              </div>
            </fieldset>
            <label>Anotações <textarea name="notes" rows="6" [(ngModel)]="f.notes"></textarea></label>

            @if (error()) {
              <p class="error">{{ error() }}</p>
            }
            <div class="actions">
              @if (editingId()) {
                <button type="button" class="danger" (click)="remove()" [disabled]="busy()">Excluir</button>
              }
              <span class="spacer"></span>
              <button type="button" (click)="cancel()">Cancelar</button>
              <button class="primary" type="submit" [disabled]="busy()">{{ busy() ? 'Salvando…' : 'Salvar' }}</button>
            </div>
          </form>
        } @else {
          <div class="center muted">Escolha um canal na lista ou crie um novo.</div>
        }
      </section>
    </main>
  `,
})
export class Channels implements OnInit {
  private readonly api = inject(Api);

  readonly locked = output<void>();

  protected readonly colors = COLORS;
  protected readonly channels = signal<Channel[]>([]);
  protected readonly query = signal('');
  protected readonly form = signal<ChannelInput | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected readonly filtered = computed(() => {
    const q = this.query().trim().toLocaleLowerCase();
    return q
      ? this.channels().filter((c) => `${c.name} ${c.niche ?? ''}`.toLocaleLowerCase().includes(q))
      : this.channels();
  });

  async ngOnInit() {
    await this.guard(async () => this.channels.set(await this.api.channels()));
  }

  protected startNew() {
    this.editingId.set(null);
    this.form.set(emptyInput());
    this.error.set('');
  }

  protected edit(c: Channel) {
    this.editingId.set(c.id);
    this.form.set({ name: c.name, url: c.url ?? '', niche: c.niche ?? '', language: c.language ?? '', color: c.color, notes: c.notes ?? '' });
    this.error.set('');
  }

  protected cancel() {
    this.form.set(null);
    this.editingId.set(null);
    this.error.set('');
  }

  protected async save() {
    const f = this.form();
    if (!f) return;
    await this.guard(async () => {
      const id = this.editingId();
      const saved = id ? await this.api.updateChannel(id, f) : await this.api.createChannel(f);
      this.channels.set(await this.api.channels());
      this.edit(saved);
    });
  }

  protected async remove() {
    const id = this.editingId();
    const f = this.form();
    if (!id || !f || !confirm(`Excluir o canal "${f.name}"? Isso não pode ser desfeito.`)) return;
    await this.guard(async () => {
      await this.api.deleteChannel(id);
      this.channels.update((list) => list.filter((c) => c.id !== id));
      this.cancel();
    });
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
