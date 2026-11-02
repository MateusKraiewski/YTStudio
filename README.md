# YTStudio

App de desktop para Windows, macOS e Linux que reúne a gestão dos seus canais do YouTube: canais, roteiros,
agenda de publicação e senhas. Tudo fica **num único arquivo cifrado** (`.ytv`) no seu computador, igual ao KeePass.
Não tem servidor nem mensalidade, e o backup é só copiar o arquivo.

> Pronto: arquivo cifrado, canais, roteiros (editor Markdown, status, tags, busca e histórico de versões) e atualização automática.
> Agenda e cofre de senhas chegam nas próximas fases.

## Como funciona o arquivo `.ytv`

- É um banco SQLite **inteiro cifrado** com AES-256 (esquema SQLCipher 4, via [SQLite3 Multiple Ciphers](https://github.com/utelle/SQLite3MultipleCiphers)).
- A chave sai da sua **senha mestra** com **Argon2id** (64 MB de memória, 3 iterações, 4 threads, salt aleatório de 16 bytes).
  O salt fica nos 16 primeiros bytes do próprio arquivo, então não há arquivo extra.
- A senha mestra não é salva em lugar nenhum. **Se você esquecer, não há como recuperar o arquivo.**
- Ao bloquear ou fechar o app, a chave é apagada da memória.
- O app nunca sobrescreve um arquivo existente ao criar um novo.
- O caminho dos últimos arquivos abertos (sem senha nem conteúdo) fica em `%APPDATA%\YTStudio`
  (Windows), `~/Library/Application Support/YTStudio` (Mac) ou `~/.config/YTStudio` (Linux).

## Instalar

Os instaladores saem do GitHub Actions (aba **Actions** → execução mais recente → *Artifacts*):

| Sistema | Arquivo |
| --- | --- |
| Windows 10/11 | `YTStudio-x.y.z-windows-x64-setup.exe` (ou o `.zip` portátil) |
| Mac Apple Silicon / Intel | `YTStudio-x.y.z-macos-arm64.dmg` / `-macos-x64.dmg` |
| Linux (Ubuntu/Debian) | `ytstudio_x.y.z_amd64.deb` (ou o `.tar.gz`) |

Avisos na primeira abertura, porque o app ainda não tem assinatura paga:
- **Windows**: o SmartScreen pode avisar; clique em *Mais informações* → *Executar assim mesmo*.
- **Mac**: clique com o botão direito no app → *Abrir*.
- **Linux**: precisa de `libwebkit2gtk-4.1-0` (o `.deb` já instala).

## Atualizações

O app confere sozinho (ao abrir e a cada 6 horas) se existe um Release mais novo neste repositório. Se existir,
aparece uma faixa no topo com **Atualizar agora**: o app baixa o instalador, confere tamanho e SHA-256 e se atualiza,
fechando e abrindo de novo. No Windows roda o instalador em modo silencioso; no Mac troca o `YTStudio.app`; no Linux
instala o `.deb` (pede a senha do sistema).

Para publicar uma versão nova:
1. Suba o número em `Directory.Build.props` (ex.: `0.3.0`) junto com as mudanças.
2. No GitHub: **Releases → Draft a new release → Choose a tag → digite `v0.3.0` → Create new tag**, e publique.
   (Ou pelo terminal: `git tag v0.3.0 && git push origin v0.3.0`.)
3. O GitHub Actions gera os instaladores e anexa ao Release. Os apps instalados avisam em seguida.

## Desenvolver

Precisa de [.NET 10 SDK](https://dotnet.microsoft.com/download) e Node 22.

```bash
# interface com recarga automática (porta 4200, chama a API na 5199)
cd ui && npm install && npm start

# em outro terminal: API + janela apontando para o ng serve
dotnet run --project src/YTStudio.Desktop -- --dev

# testes
dotnet test
```

Para gerar a interface embutida no app: `cd ui && npm run build` (vai para `src/YTStudio.Desktop/wwwroot`).
`--no-window` sobe só a API, útil para testar sem interface gráfica.

## Estrutura

| Pasta | O que tem |
| --- | --- |
| `src/YTStudio.Core` | Modelos, banco (EF Core) e o arquivo cifrado (`Vault/`) |
| `src/YTStudio.Desktop` | API local (ASP.NET Core em 127.0.0.1) + janela Photino |
| `ui/` | Interface em Angular |
| `tests/YTStudio.Tests` | Testes do arquivo cifrado, da API de roteiros e das atualizações |
| `packaging/` | Scripts dos instaladores (Inno Setup, .dmg, .deb) |

A API só aceita chamadas com o token aleatório que a janela recebe ao abrir, e confere o cabeçalho `Host`,
para que outros programas ou sites abertos no navegador não consigam ler seus dados pelo localhost.

Mesma stack do [AIClipper](https://github.com/MateusKraiewski/AIClipper) (.NET + Angular), para os dois poderem
compartilhar código e conversar depois.

Para mudar o banco: `dotnet ef migrations add NomeDaMudanca -p src/YTStudio.Core -s src/YTStudio.Core -o Data/Migrations`
(depois de `dotnet tool restore`). Arquivos antigos são atualizados sozinhos ao abrir.
