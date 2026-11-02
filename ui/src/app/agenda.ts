import { Component, computed, inject, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  Api, ApiError, Channel, fromLocalInput, Publication, PublicationInput, PUB_STATUSES, ScriptSummary,
  time, toLocalInput,
} from './api';

const WEEKDAYS = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
const REMINDERS: { label: string; value: number | null }[] = [
  { label: 'Sem lembrete', value: null },
  { label: '15 minutos antes', value: 15 },
  { label: '1 hora antes', value: 60 },
  { label: '3 horas antes', value: 180 },
  { label: '1 dia antes', value: 1440 },
];

interface Draft {
  title: string;
  channelId: string | null;
  scriptId: string | null;
  when: string; // datetime-local
  status: Publication['status'];
  videoUrl: string;
  notes: string;
  reminderMinutes: number | null;
}

interface Day {
  date: Date;
  inMonth: boolean;
  today: boolean;
  items: Publication[];
}

function dayKey(d: Date) {
  return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}`;
}

/** Agenda de publicação: calendário do mês ou lista dos próximos vídeos. */
@Component({
  selector: 'app-agenda',
  imports: [FormsModule],
  template: `
    <main class="split agenda">
      <section class="calendar">
        <div class="cal-head">
          <div class="text-tabs">
            <button [class.on]="mode() === 'month'" (click)="setMode('month')">Mês</button>
            <button [class.on]="mode() === 'list'" (click)="setMode('list')">Próximos</button>
          </div>
          @if (mode() === 'month') {
            <button class="icon" title="Mês anterior" (click)="shift(-1)">‹</button>
            <h2 class="month-title">{{ monthTitle() }}</h2>
            <button class="icon" title="Próximo mês" (click)="shift(1)">›</button>
            <button (click)="goToday()">Hoje</button>
          } @else {
            <h2 class="month-title">Próximos 60 dias</h2>
          }
          <span class="spacer"></span>
          <select [ngModel]="filterChannel()" (ngModelChange)="filterChannel.set($event); load()" aria-label="Canal">
            <option value="">Todos os canais</option>
            @for (c of channels(); track c.id) {
              <option [value]="c.id">{{ c.name }}</option>
            }
          </select>
          <button class="primary" (click)="startNew(null)">+ Agendar</button>
        </div>

        @if (mode() === 'month') {
          <div class="grid">
            @for (w of weekdays; track w) {
              <div class="wd">{{ w }}</div>
            }
            @for (d of days(); track d.date.getTime()) {
              <div class="cell" [class.out]="!d.inMonth" [class.today]="d.today" (click)="startNew(d.date)">
                <span class="num">{{ d.date.getDate() }}</span>
                @for (p of d.items; track p.id) {
                  <button class="ev" [class.done]="p.status === 'Publicado'" [class.cancel]="p.status === 'Cancelado'"
                    [class.active]="p.id === editingId()" [style.border-left-color]="color(p.channelId)"
                    (click)="$event.stopPropagation(); edit(p)" [title]="p.title">
                    <span class="t">{{ time(p.scheduledAt) }}</span> {{ p.title }}
                  </button>
                }
              </div>
            }
          </div>
        } @else {
          <div class="upcoming">
            @for (g of upcoming(); track g.label) {
              <h3>{{ g.label }}</h3>
              @for (p of g.items; track p.id) {
                <button class="channel" [class.active]="p.id === editingId()" (click)="edit(p)">
                  <span class="dot" [style.background]="color(p.channelId)"></span>
                  <span>
                    <strong>{{ time(p.scheduledAt) }} · {{ p.title }}</strong>
                    <span class="muted small">{{ channelName(p.channelId) }} · {{ p.status }}</span>
                  </span>
                </button>
              }
            } @empty {
              <p class="muted">Nada agendado nos próximos 60 dias.</p>
            }
          </div>
        }
        @if (error() && !draft()) {
          <p class="error">{{ error() }}</p>
        }
      </section>

      @if (draft(); as d) {
        <aside class="side-editor">
          <form (ngSubmit)="save()">
            <h2>{{ editingId() ? 'Editar agendamento' : 'Agendar vídeo' }}</h2>
            <label>Roteiro
              <select name="script" [(ngModel)]="d.scriptId" (ngModelChange)="pickScript($event)">
                <option [ngValue]="null">Sem roteiro</option>
                @for (s of scripts(); track s.id) {
                  <option [ngValue]="s.id">{{ s.title }}</option>
                }
              </select>
            </label>
            <label>Título <input name="title" [(ngModel)]="d.title" maxlength="300" placeholder="Usa o do roteiro se ficar vazio" /></label>
            <label>Canal
              <select name="channel" [(ngModel)]="d.channelId">
                <option [ngValue]="null">Sem canal</option>
                @for (c of channels(); track c.id) {
                  <option [ngValue]="c.id">{{ c.name }}</option>
                }
              </select>
            </label>
            <label>Data e hora <input name="when" type="datetime-local" [(ngModel)]="d.when" required /></label>
            <div class="row">
              <label>Status
                <select name="status" [(ngModel)]="d.status">
                  @for (st of statuses; track st) {
                    <option [value]="st">{{ st }}</option>
                  }
                </select>
              </label>
              <label>Lembrete
                <select name="reminder" [(ngModel)]="d.reminderMinutes">
                  @for (r of reminders; track r.label) {
                    <option [ngValue]="r.value">{{ r.label }}</option>
                  }
                </select>
              </label>
            </div>
            <label>Link do vídeo <input name="url" [(ngModel)]="d.videoUrl" placeholder="https://youtu.be/…" /></label>
            <label>Anotações <textarea name="notes" rows="4" [(ngModel)]="d.notes"></textarea></label>
            @if (error()) {
              <p class="error">{{ error() }}</p>
            }
            <div class="actions">
              @if (editingId()) {
                <button type="button" class="danger" (click)="remove()" [disabled]="busy()">Excluir</button>
              }
              <span class="spacer"></span>
              <button type="button" (click)="close()">Fechar</button>
              <button class="primary" type="submit" [disabled]="busy()">{{ busy() ? 'Salvando…' : 'Salvar' }}</button>
            </div>
          </form>
        </aside>
      }
    </main>
  `,
})
export class Agenda implements OnInit {
  private readonly api = inject(Api);
  readonly locked = output<void>();

  protected readonly weekdays = WEEKDAYS;
  protected readonly reminders = REMINDERS;
  protected readonly statuses = PUB_STATUSES;
  protected readonly time = time;

  protected readonly mode = signal<'month' | 'list'>('month');
  protected readonly cursor = signal(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  protected readonly items = signal<Publication[]>([]);
  protected readonly channels = signal<Channel[]>([]);
  protected readonly scripts = signal<ScriptSummary[]>([]);
  protected readonly filterChannel = signal('');
  protected readonly draft = signal<Draft | null>(null);
  protected readonly editingId = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected readonly monthTitle = computed(() => {
    const t = this.cursor().toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
    return t.charAt(0).toUpperCase() + t.slice(1);
  });

  /** 6 semanas a partir do domingo antes do dia 1. */
  private readonly gridStart = computed(() => {
    const first = this.cursor();
    return new Date(first.getFullYear(), first.getMonth(), 1 - first.getDay());
  });

  protected readonly days = computed<Day[]>(() => {
    const start = this.gridStart();
    const month = this.cursor().getMonth();
    const todayKey = dayKey(new Date());
    const byDay = new Map<string, Publication[]>();
    for (const p of this.items()) {
      const k = dayKey(new Date(p.scheduledAt));
      byDay.set(k, [...(byDay.get(k) ?? []), p]);
    }
    return Array.from({ length: 42 }, (_, i) => {
      const date = new Date(start.getFullYear(), start.getMonth(), start.getDate() + i);
      const k = dayKey(date);
      return { date, inMonth: date.getMonth() === month, today: k === todayKey, items: byDay.get(k) ?? [] };
    });
  });

  protected readonly upcoming = computed(() => {
    const groups: { label: string; items: Publication[] }[] = [];
    for (const p of this.items()) {
      const raw = new Date(p.scheduledAt).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' });
      const label = raw.charAt(0).toUpperCase() + raw.slice(1);
      const last = groups.at(-1);
      if (last?.label === label) last.items.push(p);
      else groups.push({ label, items: [p] });
    }
    return groups;
  });

  async ngOnInit() {
    await this.guard(async () => {
      const [channels, scripts] = await Promise.all([this.api.channels(), this.api.scripts()]);
      this.channels.set(channels);
      this.scripts.set(scripts);
    });
    await this.load();
  }

  protected async load() {
    let from: Date;
    let to: Date;
    if (this.mode() === 'month') {
      from = this.gridStart();
      to = new Date(from.getFullYear(), from.getMonth(), from.getDate() + 42);
    } else {
      const now = new Date();
      from = new Date(now.getFullYear(), now.getMonth(), now.getDate());
      to = new Date(from.getFullYear(), from.getMonth(), from.getDate() + 60);
    }
    await this.guard(async () =>
      this.items.set(await this.api.publications({ from: from.toISOString(), to: to.toISOString(), channelId: this.filterChannel() })),
    );
  }

  protected setMode(m: 'month' | 'list') {
    this.mode.set(m);
    this.load();
  }

  protected shift(months: number) {
    const c = this.cursor();
    this.cursor.set(new Date(c.getFullYear(), c.getMonth() + months, 1));
    this.load();
  }

  protected goToday() {
    const now = new Date();
    this.cursor.set(new Date(now.getFullYear(), now.getMonth(), 1));
    this.load();
  }

  protected color(id: string | null) {
    return this.channels().find((c) => c.id === id)?.color ?? 'var(--border)';
  }

  protected channelName(id: string | null) {
    return this.channels().find((c) => c.id === id)?.name ?? 'Sem canal';
  }

  protected startNew(day: Date | null) {
    const base = day ?? new Date(Date.now() + 24 * 3600 * 1000);
    const at = new Date(base.getFullYear(), base.getMonth(), base.getDate(), 18, 0);
    this.editingId.set(null);
    this.error.set('');
    this.draft.set({
      title: '', channelId: this.filterChannel() || null, scriptId: null, when: toLocalInput(at.toISOString()),
      status: 'Planejado', videoUrl: '', notes: '', reminderMinutes: 60,
    });
  }

  protected edit(p: Publication) {
    this.editingId.set(p.id);
    this.error.set('');
    this.draft.set({
      title: p.title, channelId: p.channelId, scriptId: p.scriptId, when: toLocalInput(p.scheduledAt),
      status: p.status, videoUrl: p.videoUrl ?? '', notes: p.notes ?? '', reminderMinutes: p.reminderMinutes,
    });
  }

  /** Ao escolher um roteiro, sugere o título e o canal dele. */
  protected pickScript(id: string | null) {
    const d = this.draft();
    const s = this.scripts().find((x) => x.id === id);
    if (!d || !s) return;
    if (!d.title) d.title = s.title;
    if (!d.channelId) d.channelId = s.channelId;
  }

  protected close() {
    this.draft.set(null);
    this.editingId.set(null);
    this.error.set('');
  }

  protected async save() {
    const d = this.draft();
    if (!d) return;
    if (!d.when) {
      this.error.set('Escolha a data e a hora.');
      return;
    }
    const input: PublicationInput = {
      title: d.title, channelId: d.channelId, scriptId: d.scriptId, scheduledAt: fromLocalInput(d.when),
      status: d.status, videoUrl: d.videoUrl || null, notes: d.notes || null, reminderMinutes: d.reminderMinutes,
    };
    await this.guard(async () => {
      const id = this.editingId();
      const saved = id ? await this.api.updatePublication(id, input) : await this.api.createPublication(input);
      await this.load();
      this.edit(saved);
    });
  }

  protected async remove() {
    const id = this.editingId();
    const d = this.draft();
    if (!id || !d || !confirm(`Tirar "${d.title}" da agenda?`)) return;
    await this.guard(async () => {
      await this.api.deletePublication(id);
      this.close();
      await this.load();
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
