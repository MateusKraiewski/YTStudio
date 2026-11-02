import { Injectable } from '@angular/core';

export interface SessionInfo {
  open: boolean;
  path: string | null;
  recent: string[];
}

export interface Channel {
  id: string;
  name: string;
  url: string | null;
  niche: string | null;
  language: string | null;
  color: string;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export type ScriptStatus = 'Ideia' | 'Roteiro' | 'Gravado' | 'Editado' | 'Publicado';
export const STATUSES: ScriptStatus[] = ['Ideia', 'Roteiro', 'Gravado', 'Editado', 'Publicado'];

export interface ScriptSummary {
  id: string;
  title: string;
  channelId: string | null;
  status: ScriptStatus;
  tags: string[];
  words: number;
  readingSeconds: number;
  updatedAt: string;
}

export interface ScriptDetail extends ScriptSummary {
  content: string;
  description: string | null;
  thumbnailIdea: string | null;
  createdAt: string;
}

export interface ScriptInput {
  title: string;
  channelId: string | null;
  status: ScriptStatus;
  tags: string[];
  content: string;
  description: string | null;
  thumbnailIdea: string | null;
}

export interface VersionSummary {
  id: string;
  savedAt: string;
  words: number;
}

export interface UpdateInfo {
  current: string;
  latest: string | null;
  available: boolean;
  notes: string | null;
  pageUrl: string | null;
  canInstall: boolean;
}

export type PublicationStatus = 'Planejado' | 'Pronto' | 'Publicado' | 'Cancelado';
export const PUB_STATUSES: PublicationStatus[] = ['Planejado', 'Pronto', 'Publicado', 'Cancelado'];

export interface Publication {
  id: string;
  channelId: string | null;
  scriptId: string | null;
  title: string;
  scheduledAt: string;
  status: PublicationStatus;
  videoUrl: string | null;
  notes: string | null;
  reminderMinutes: number | null;
}

export type PublicationInput = Omit<Publication, 'id'>;

export type ChannelInput = Pick<Channel, 'name' | 'url' | 'niche' | 'language' | 'color' | 'notes'>;

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
  }
}

const TOKEN_KEY = 'ytstudio.token';

/** Fala com a API local do app. Toda chamada leva o token que a janela recebeu na URL. */
@Injectable({ providedIn: 'root' })
export class Api {
  private readonly token: string;

  constructor() {
    const fromUrl = new URLSearchParams(location.search).get('t');
    if (fromUrl) {
      sessionStorage.setItem(TOKEN_KEY, fromUrl);
      history.replaceState(null, '', location.pathname); // tira o token da barra de endereço
    }
    this.token = fromUrl ?? sessionStorage.getItem(TOKEN_KEY) ?? '';
  }

  session() {
    return this.call<SessionInfo>('GET', '/api/session');
  }

  pickOpen() {
    return this.call<{ path: string | null }>('POST', '/api/dialog/open');
  }

  pickSave() {
    return this.call<{ path: string | null }>('POST', '/api/dialog/save');
  }

  createVault(path: string, password: string) {
    return this.call<SessionInfo>('POST', '/api/vault/create', { path, password });
  }

  openVault(path: string, password: string) {
    return this.call<SessionInfo>('POST', '/api/vault/open', { path, password });
  }

  lock() {
    return this.call<void>('POST', '/api/vault/lock');
  }

  forgetRecent(path: string) {
    return this.call<void>('DELETE', `/api/recent?path=${encodeURIComponent(path)}`);
  }

  channels() {
    return this.call<Channel[]>('GET', '/api/channels');
  }

  createChannel(input: ChannelInput) {
    return this.call<Channel>('POST', '/api/channels', input);
  }

  updateChannel(id: string, input: ChannelInput) {
    return this.call<Channel>('PUT', `/api/channels/${id}`, input);
  }

  deleteChannel(id: string) {
    return this.call<void>('DELETE', `/api/channels/${id}`);
  }

  scripts(filter: { channelId?: string; status?: string; q?: string } = {}) {
    const params = new URLSearchParams(Object.entries(filter).filter(([, v]) => !!v) as [string, string][]);
    return this.call<ScriptSummary[]>('GET', `/api/scripts?${params}`);
  }

  script(id: string) {
    return this.call<ScriptDetail>('GET', `/api/scripts/${id}`);
  }

  createScript(input: ScriptInput) {
    return this.call<ScriptDetail>('POST', '/api/scripts', input);
  }

  updateScript(id: string, input: ScriptInput) {
    return this.call<ScriptDetail>('PUT', `/api/scripts/${id}`, input);
  }

  deleteScript(id: string) {
    return this.call<void>('DELETE', `/api/scripts/${id}`);
  }

  versions(id: string) {
    return this.call<VersionSummary[]>('GET', `/api/scripts/${id}/versions`);
  }

  version(id: string, versionId: string) {
    return this.call<{ id: string; savedAt: string; content: string }>('GET', `/api/scripts/${id}/versions/${versionId}`);
  }

  publications(filter: { from?: string; to?: string; channelId?: string } = {}) {
    const params = new URLSearchParams(Object.entries(filter).filter(([, v]) => !!v) as [string, string][]);
    return this.call<Publication[]>('GET', `/api/publications?${params}`);
  }

  createPublication(input: PublicationInput) {
    return this.call<Publication>('POST', '/api/publications', input);
  }

  updatePublication(id: string, input: PublicationInput) {
    return this.call<Publication>('PUT', `/api/publications/${id}`, input);
  }

  deletePublication(id: string) {
    return this.call<void>('DELETE', `/api/publications/${id}`);
  }

  takeReminders() {
    return this.call<Publication[]>('POST', '/api/publications/reminders/take');
  }

  checkUpdate(force = false) {
    return this.call<UpdateInfo>('GET', `/api/update${force ? '?force=true' : ''}`);
  }

  installUpdate() {
    return this.call<void>('POST', '/api/update/install');
  }

  openUrl(url: string) {
    return this.call<void>('POST', '/api/open-url', { url });
  }

  private async call<T>(method: string, url: string, body?: unknown): Promise<T> {
    const res = await fetch(url, {
      method,
      headers: {
        'X-YTStudio-Token': this.token,
        ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!res.ok) {
      let message = `Erro ${res.status}`;
      try {
        message = (await res.json()).error ?? message;
      } catch {
        /* resposta sem JSON */
      }
      throw new ApiError(message, res.status);
    }
    return res.status === 204 || res.status === 202 ? (undefined as T) : ((await res.json()) as T);
  }
}

export function fileName(path: string | null): string {
  return path?.split(/[\\/]/).pop() ?? '';
}

export function minutes(seconds: number): string {
  if (seconds < 60) return seconds === 0 ? '0 min' : '< 1 min';
  return `~${Math.round(seconds / 60)} min`;
}

export function shortDate(iso: string): string {
  return new Date(iso).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

/** ISO (UTC) → valor de <input type="datetime-local"> no fuso do computador. */
export function toLocalInput(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function fromLocalInput(value: string): string {
  return new Date(value).toISOString();
}

export function time(iso: string): string {
  return new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}
