# ETBBS DSL/LBR Language Server (experimental)

This is a minimal LSP server that provides diagnostics and keyword completion for `.lbr` files.

Project: `ETBBS.Lsp`

## Build

```bash
dotnet build ETBBS.Lsp
```

## Run (stdio)

The server speaks JSON-RPC over stdio as per LSP. It supports:

- `initialize`, `shutdown`, `exit`
- `textDocument/didOpen`, `textDocument/didChange` → diagnostics
- `textDocument/completion` → context-aware completions (targeting, range, actions)
- `textDocument/hover` → keyword help and brief docs
- `textDocument/formatting` → basic brace indentation
- `textDocument/codeAction` → quick fixes for common warnings (e.g., min_range > range, chance 0%)
- `workspace/symbol` → role IDs and skill names

Use any LSP client to spawn `ETBBS.Lsp` as a stdio server.

## Localization / 本地化

The server can localize messages (English/中文):

- Preferred: pass `locale` in `initialize` params (e.g., `"zh-CN"`).
- Or pass `initializationOptions.lang` (e.g., `"zh-CN"`).
- Or set environment variable `ETBBS_LSP_LANG=zh-CN` before launching.

Examples:

```jsonc
// initialize request params (excerpt)
{
  "method": "initialize",
  "params": {
    "locale": "zh-CN",
    "initializationOptions": { "lang": "zh-CN" }
  }
}
```

PowerShell / Bash:

```powershell
$env:ETBBS_LSP_LANG = 'zh-CN'
dotnet run --project ETBBS.Lsp
```

When Chinese is selected, diagnostics like parser errors and static-analysis warnings will be shown in Chinese (e.g., `DSL 解析错误，第 12 行，第 8 列：...`、`概率为 0%：then 分支不可达`). Quick-fix titles and hover docs are also localized.

## VSCode client (sample)

Create a basic extension or use an LSP client like `vscode-languageclient` to attach to the binary. Example user setting via an LSP wrapper (pseudo):

```jsonc
{
  "etbbs.lsp.command": "dotnet",
  "etbbs.lsp.args": ["run", "--project", "ETBBS.Lsp"]
}
```

## VSCode Extension/Launch/Settings Examples

Below is a minimal VSCode extension client that connects to the ETBBS LSP via stdio and enables Chinese localization.

1) package.json (extension manifest)

```json
{
  "name": "etbbs-lsp-client",
  "displayName": "ETBBS LBR/DSL Client",
  "publisher": "you",
  "version": "0.0.1",
  "engines": { "vscode": "^1.85.0" },
  "activationEvents": ["onLanguage:lbr"],
  "main": "./out/extension.js",
  "contributes": {
    "languages": [
      { "id": "lbr", "aliases": ["LBR"], "extensions": [".lbr"] }
    ]
  },
  "dependencies": { "vscode-languageclient": "^9.0.0" },
  "devDependencies": { "@types/vscode": "^1.85.0", "esbuild": "^0.20.0" }
}
```

2) src/extension.ts (client entry)

```ts
import * as vscode from 'vscode';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

export function activate(context: vscode.ExtensionContext) {
  const serverOptions: ServerOptions = {
    command: 'dotnet',
    args: ['run', '--project', '${workspaceFolder}/ETBBS.Lsp'],
    options: { env: { ...process.env, ETBBS_LSP_LANG: 'zh-CN' } }
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'lbr' }],
    initializationOptions: { lang: 'zh-CN' },
    locale: 'zh-CN'
  } as any;

  client = new LanguageClient('etbbs-lsp', 'ETBBS LSP', serverOptions, clientOptions);
  context.subscriptions.push(client.start());
}

export function deactivate() { return client?.stop(); }
```

3) .vscode/launch.json (Run Extension)

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Run Extension",
      "type": "extensionHost",
      "request": "launch",
      "runtimeExecutable": "${execPath}",
      "args": ["--extensionDevelopmentPath=${workspaceFolder}"],
      "outFiles": ["${workspaceFolder}/out/**/*.js"],
      "env": { "ETBBS_LSP_LANG": "zh-CN" }
    }
  ]
}
```

4) .vscode/settings.json

```json
{
  "files.associations": { "*.lbr": "lbr" },
  "editor.formatOnSave": true,
  "etbbs.lsp.lang": "zh-CN"
}
```

构建与调试：

- 使用 esbuild/tsc 将 `src/extension.ts` 打包到 `out/extension.js` 后，使用 “Run Extension” 启动。
- 打开任意 `.lbr` 文件即可触发 LSP 连接，能看到中文诊断/补全/快速修复等。

Diagnostics are derived from the core parsers:

- LBR syntax errors include line/column and a caret, surfaced as LSP diagnostics.
- Basic semantic warnings: empty `id`/`name`.
- DSL diagnostics inside skill bodies with accurate ranges (parse errors and static-analysis warnings).

Completion returns a small set of common LBR/DSL keywords and context-sensitive suggestions.

> Note: This server is intentionally minimal but now includes mapping for DSL bodies. Extend with semantic tokens or richer formatting as needed.

## Diagnostics Examples / 诊断示例

- LBR parse error → `LBR 解析错误，第 5 行，第 10 列：...`
- DSL parse error in a skill body → `技能‘烧掉他们’：DSL 解析错误，第 15 行，第 6 列：...`
- Static analysis → `技能‘号召’：概率为 0%：then 分支不可达`
