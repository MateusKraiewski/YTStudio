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
    return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
  }
}

export function fileName(path: string | null): string {
  return path?.split(/[\\/]/).pop() ?? '';
}
