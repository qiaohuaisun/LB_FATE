// ETBBS LBR Tools - plain JS VS Code extension
// Provides: syntax (via TextMate), snippets, completions, hovers, symbols, basic diagnostics.

const vscode = require('vscode');

/** @param {vscode.ExtensionContext} context */
function activate(context) {
  const LBR = { language: 'lbr', scheme: 'file' };

  // Completion provider
  const completionProvider = vscode.languages.registerCompletionItemProvider(
    LBR,
    {
      provideCompletionItems(doc, pos) {
        const items = [];
        const ctx = getContext(doc, pos);

        if (ctx.atStart) {
          items.push(snippet('role', 'role "${1:Name}" id "${2:id}" {\n  description "${3:Describe the role}";\n  vars {\n    "hp" = ${4:30};\n    "mp" = ${5:5};\n  }\n  tags { "${6:tag}" }\n  skills {\n    skill "${7:Skill}" {\n      range ${8:3}; targeting ${9|self,allies,enemies,any|}; cooldown ${10:1};\n      deal ${11:5} damage to ${12|target,caster,it|};\n      consume mp = ${13:1};\n    }\n  }\n}\n', 'role skeleton'));
        }

        if (ctx.inRole && !ctx.inAnyBlock) {
          ['description', 'vars', 'tags', 'skills'].forEach(k => items.push(keyword(k)));
        }

        if (ctx.inVars) {
          VAR_KEYS.forEach(v => items.push(withDetail(keyword(`"${v.key}"`), v.detail)));
        }

        if (ctx.inTags) {
          TAGS.forEach(t => items.push(keyword(`"${t}"`)));
        }

        if (ctx.inSkills && !ctx.inSkill) {
          items.push(snippet('skill', 'skill "${1:Skill Name}" {\n  range ${2:3}; targeting ${3|self,allies,enemies,any|}; cooldown ${4:1};\n  $0\n}\n', 'skill block'));
        }

        if (ctx.inSkill) {
          // meta
          ['range', 'cooldown', 'min_range', 'sealed_until'].forEach(k => items.push(keyword(k)));
          items.push(snippet('targeting', 'targeting ${1|self,allies,enemies,any,tile|}', 'targeting'));
          items.push(snippet('cost mp', 'cost mp ${1:1}', 'mp cost'));
          // control
          items.push(snippet('if', 'if ${1:caster} has tag "${2:tag}" then {\n  ${3:deal 3 damage to target}\n} else {\n  ${4:heal 2 to caster}\n}', 'if/then/else'));
          items.push(snippet('repeat', 'repeat ${1:2} times {\n  ${2:deal 2 damage to target}\n}', 'repeat block'));
          items.push(snippet('parallel', 'parallel {\n  ${1:deal 3 damage to target};\n  ${2:heal 2 to caster};\n}', 'parallel'));
          items.push(snippet('foreach', 'for each ${1|enemies,allies,units|} ${2:of caster} ${3:in range ${4:2} of ${5|caster,target,point|}} ${6:with tag "${7:tag}"} ${8:limit ${9:3}} do {\n  ${10:deal 3 damage to it}\n}', 'for each'));
          items.push(snippet('nearest', 'nearest ${1|enemies,allies|} of ${2|caster,target,point|}', 'nearest selector'));
          items.push(snippet('farthest', 'farthest ${1|enemies,allies|} of ${2|caster,target,point|}', 'farthest selector'));
          // actions
          items.push(snippet('deal', 'deal ${1:5} damage to ${2|target,caster,it|}', 'deal true damage'));
          items.push(snippet('deal physical', 'deal physical ${1:8} damage to ${2|target,caster,it|} from ${3|caster,target,it|} ${4:ignore defense ${5:50}%}', 'deal physical'));
          items.push(snippet('deal magic', 'deal magic ${1:8} damage to ${2|target,caster,it|} from ${3|caster,target,it|} ${4:ignore resist ${5:50}%}', 'deal magic'));
          items.push(snippet('heal', 'heal ${1:5} to ${2|target,caster,it|}', 'heal'));
          items.push(snippet('move', 'move ${1|target,caster,it|} to (${2:x},${3:y})', 'move unit'));
          items.push(snippet('dash', 'dash towards ${1|target,caster,it|} up to ${2:3}', 'dash towards'));
          items.push(snippet('line physical', 'line physical ${1:6} to ${2|target,caster,it|} length ${3:3} radius ${4:0} ${5:ignore defense ${6:50}%}', 'line physical'));
          items.push(snippet('line magic', 'line magic ${1:6} to ${2|target,caster,it|} length ${3:3} radius ${4:0} ${5:ignore resist ${6:50}%}', 'line magic'));
          items.push(snippet('line', 'line ${1:3} to ${2|target,caster,it|} length ${3:3} radius ${4:0}', 'line true'));
          items.push(snippet('add tag', 'add tag "${1:tag}" to ${2|target,caster,it|}', 'add unit tag'));
          items.push(snippet('remove tag', 'remove tag "${1:tag}" from ${2|target,caster,it|}', 'remove unit tag'));
          items.push(snippet('set unit var', 'set unit(${1|caster,target,it|}) var "${2:key}" = ${3:value}', 'set unit var'));
          items.push(snippet('set tile var', 'set tile(${1:x},${2:y}) var "${3:key}" = ${4:value}', 'set tile var'));
          items.push(snippet('set global var', 'set global var "${1:key}" = ${2:value}', 'set global var'));
          items.push(snippet('consume mp', 'consume mp = ${1:1}', 'consume mp'));
        }

        return new vscode.CompletionList(items, false);
      }
    },
    '"', '\n', ' ', '\\', '{'
  );

  // Hover provider
  const hoverProvider = vscode.languages.registerHoverProvider(LBR, {
    provideHover(doc, pos) {
      const word = doc.getText(doc.getWordRangeAtPosition(pos, /[A-Za-z_][A-Za-z_\-]*/));
      if (!word) return;
      const v = VAR_KEYS.find(v => v.key.toLowerCase() === word.toLowerCase());
      if (v) return new vscode.Hover(markdown(`var "${v.key}": ${v.detail}`));
      const k = DSL_DOCS[word.toLowerCase()];
      if (k) return new vscode.Hover(markdown(k));
      return;
    }
  });

  // Folding provider
  const foldingProvider = vscode.languages.registerFoldingRangeProvider(LBR, {
    provideFoldingRanges(doc) {
      const text = doc.getText();
      const ranges = [];
      const stack = [];
      for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        if (ch === '"') { i++; while (i < text.length) { const c = text[i++]; if (c === '"') break; if (c === '\\') i++; } continue; }
        if (ch === '#') { while (i < text.length && text[i] !== '\n') i++; continue; }
        if (ch === '{') stack.push(i);
        if (ch === '}') {
          const start = stack.pop();
          if (start !== undefined) {
            const s = doc.positionAt(start);
            const e = doc.positionAt(i);
            if (e.line > s.line) ranges.push(new vscode.FoldingRange(s.line, e.line, vscode.FoldingRangeKind.Region));
          }
        }
      }
      return ranges;
    }
  });

  // Symbols
  const symbolProvider = vscode.languages.registerDocumentSymbolProvider(LBR, {
    provideDocumentSymbols(doc) {
      const text = doc.getText();
      const roleMatch = /role\s+"([^"]+)"\s+id\s+"([^"]+)"\s*\{/i.exec(text);
      const symbols = [];
      if (roleMatch) {
        const roleName = roleMatch[1];
        const rolePos = doc.positionAt(roleMatch.index);
        const roleSym = new vscode.DocumentSymbol(
          `role ${roleName}`,
          '',
          vscode.SymbolKind.Class,
          new vscode.Range(rolePos, new vscode.Position(doc.lineCount - 1, 1000)),
          new vscode.Range(rolePos, rolePos)
        );

        const skillRegex = /skill\s+"([^"]+)"\s*\{/gi;
        let m;
        while ((m = skillRegex.exec(text)) !== null) {
          const name = m[1];
          const start = doc.positionAt(m.index);
          const endIndex = findMatchingBrace(text, m.index + m[0].length - 1);
          const end = doc.positionAt(endIndex >= 0 ? endIndex : m.index + m[0].length);
          // Extract skill meta summary
          let detail = 'skill';
          if (endIndex >= 0) {
            const body = text.slice(m.index, endIndex);
            const range = body.match(/\brange\s+(\d+)/i)?.[1];
            const cd = body.match(/\bcooldown\s+(\d+)/i)?.[1];
            const tgt = body.match(/\btargeting\s+(any|enemies|allies|self|tile)/i)?.[1];
            const mp = body.match(/\bcost\s+mp\s+(\d+)/i)?.[1];
            const minr = body.match(/\bmin_range\s+(\d+)/i)?.[1];
            const sealed = body.match(/\bsealed_until\s+(\d+)/i)?.[1];
            const parts = [];
            if (range) parts.push(`range ${range}`);
            if (tgt) parts.push(`targeting ${tgt}`);
            if (cd) parts.push(`cd ${cd}`);
            if (mp) parts.push(`mp ${mp}`);
            if (minr) parts.push(`min ${minr}`);
            if (sealed) parts.push(`seal ${sealed}`);
            if (parts.length) detail = parts.join(', ');
          }
          roleSym.children.push(new vscode.DocumentSymbol(
            name,
            detail,
            vscode.SymbolKind.Function,
            new vscode.Range(start, end),
            new vscode.Range(start, start)
          ));
        }
        symbols.push(roleSym);
      }
      return symbols;
    }
  });

  // Diagnostics
  const diagCollection = vscode.languages.createDiagnosticCollection('lbr');
  const workspaceDiag = vscode.languages.createDiagnosticCollection('lbr-workspace');
  const validateActive = () => {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.document.languageId !== 'lbr') return;
    diagCollection.set(editor.document.uri, validate(editor.document));
  };
  async function validateWorkspace() {
    const conf = vscode.workspace.getConfiguration();
    if (!conf.get('etbbs-lbr.validate.workspaceDuplicates', true)) return;
    const files = await vscode.workspace.findFiles('**/*.lbr');
    const idToUris = new Map();
    for (const f of files) {
      try {
        const data = await vscode.workspace.fs.readFile(f);
        const text = Buffer.from(data).toString('utf8');
        const m = /role\s+"([^"]+)"\s+id\s+"([^"]+)"/i.exec(text);
        if (m) {
          const id = m[2];
          if (!idToUris.has(id)) idToUris.set(id, []);
          idToUris.get(id).push(f);
        }
      } catch {}
    }
    workspaceDiag.clear();
    for (const [id, uris] of idToUris.entries()) {
      if (uris.length > 1) {
        for (const uri of uris) {
          const diags = workspaceDiag.get(uri) ?? [];
          diags.push(new vscode.Diagnostic(new vscode.Range(new vscode.Position(0, 0), new vscode.Position(0, 1)), `Duplicate role id '${id}' in workspace`, vscode.DiagnosticSeverity.Error));
          workspaceDiag.set(uri, diags);
        }
      }
    }
  }
  context.subscriptions.push(
    vscode.workspace.onDidOpenTextDocument(d => d.languageId === 'lbr' && diagCollection.set(d.uri, validate(d))),
    vscode.workspace.onDidChangeTextDocument(e => e.document.languageId === 'lbr' && diagCollection.set(e.document.uri, validate(e.document))),
    vscode.workspace.onDidSaveTextDocument(d => d.languageId === 'lbr' && diagCollection.set(d.uri, validate(d))),
    vscode.workspace.onDidSaveTextDocument(() => validateWorkspace()),
    vscode.workspace.onDidCreateFiles(() => validateWorkspace()),
    vscode.workspace.onDidDeleteFiles(() => validateWorkspace()),
    vscode.workspace.onDidRenameFiles(() => validateWorkspace()),
    vscode.window.onDidChangeActiveTextEditor(() => validateActive()),
    vscode.commands.registerCommand('etbbs-lbr.validate', validateActive),
    vscode.commands.registerCommand('etbbs-lbr.formatDocument', () => {
      const ed = vscode.window.activeTextEditor;
      if (!ed || ed.document.languageId !== 'lbr') return;
      return formatApply(ed.document);
    }),
    vscode.commands.registerCommand('etbbs-lbr.createRole', async () => {
      const name = await vscode.window.showInputBox({ prompt: 'Role Name', validateInput: s => !s ? 'Name required' : undefined });
      if (!name) return;
      const id = await vscode.window.showInputBox({ prompt: 'Role Id', value: toId(name), validateInput: s => !s ? 'Id required' : undefined });
      if (!id) return;
      const uriSel = await vscode.window.showOpenDialog({ canSelectFiles: false, canSelectFolders: true, canSelectMany: false, openLabel: 'Select folder to create .lbr' });
      const folder = uriSel?.[0] ?? vscode.workspace.workspaceFolders?.[0]?.uri;
      if (!folder) return;
      const file = vscode.Uri.joinPath(folder, `${id}.lbr`);
      const tpl = roleTemplate(name, id);
      await vscode.workspace.fs.writeFile(file, Buffer.from(tpl, 'utf8'));
      const doc = await vscode.workspace.openTextDocument(file);
      await vscode.window.showTextDocument(doc);
    }),
    completionProvider,
    hoverProvider,
    foldingProvider,
    symbolProvider,
    diagCollection,
    workspaceDiag,
    vscode.languages.registerDocumentFormattingEditProvider(LBR, { provideDocumentFormattingEdits })
  );

  // Initial
  validateActive();
  validateWorkspace();
}

function deactivate() {}

// ---------- Helpers ----------

/** @param {string} text @returns {vscode.MarkdownString} */
function markdown(text) {
  const md = new vscode.MarkdownString(text);
  md.isTrusted = false;
  return md;
}

function keyword(label) {
  const it = new vscode.CompletionItem(label, vscode.CompletionItemKind.Keyword);
  it.insertText = label;
  return it;
}

function withDetail(item, detail) {
  item.detail = detail;
  return item;
}

function snippet(label, body, detail) {
  const it = new vscode.CompletionItem(label, vscode.CompletionItemKind.Snippet);
  it.insertText = new vscode.SnippetString(body);
  it.detail = detail;
  it.filterText = label;
  it.preselect = true;
  return it;
}

/**
 * Quick context detection: role/vars/tags/skills/skill
 */
function getContext(doc, pos) {
  const text = doc.getText();
  const upTo = doc.offsetAt(pos);
  // naive scan braces with section hints
  let inRole = false;
  let inVars = false;
  let inTags = false;
  let inSkills = false;
  let inSkill = false;
  let stack = [];
  let i = 0;
  while (i < upTo) {
    const ch = text[i];
    // skip strings
    if (ch === '"') { i++; while (i < upTo) { const c = text[i++]; if (c === '"') break; if (c === '\\') i++; } continue; }
    // skip comments to EOL
    if (ch === '#') { while (i < upTo && text[i] !== '\n') i++; continue; }
    // keyword checks
    if (matchAhead(text, i, 'role')) inRole = true;
    if (matchAhead(text, i, 'vars')) { if (!inVars && !inTags && !inSkill) inVars = true; }
    if (matchAhead(text, i, 'tags')) { if (!inTags && !inVars && !inSkill) inTags = true; }
    if (matchAhead(text, i, 'skills')) { if (!inSkills) inSkills = true; }
    if (matchAhead(text, i, 'skill')) { if (inSkills) inSkill = true; }
    if (ch === '{') stack.push('{');
    if (ch === '}') {
      stack.pop();
      // leave innermost contexts on closing brace
      if (inSkill) { inSkill = false; }
      else if (inSkills) { inSkills = false; }
      else if (inTags) { inTags = false; }
      else if (inVars) { inVars = false; }
      else if (inRole) { inRole = false; }
    }
    i++;
  }
  const inAnyBlock = inVars || inTags || inSkills || inSkill;
  return { atStart: upTo < 10, inRole, inVars, inTags, inSkills, inSkill, inAnyBlock };
}

function matchAhead(text, i, kw) {
  // case-insensitive word boundary
  if (i + kw.length > text.length) return false;
  const slice = text.substring(i, i + kw.length);
  if (slice.toLowerCase() !== kw) return false;
  if (i > 0 && /[A-Za-z0-9_]/.test(text[i - 1])) return false;
  if (i + kw.length < text.length && /[A-Za-z0-9_]/.test(text[i + kw.length])) return false;
  return true;
}

function findMatchingBrace(text, startIndex) {
  // expects startIndex to be at the '{' or just after it
  let i = startIndex;
  while (i < text.length && text[i] !== '{') i++;
  if (i >= text.length) return -1;
  let depth = 1; i++;
  while (i < text.length) {
    const ch = text[i++];
    if (ch === '"') { while (i < text.length) { const c = text[i++]; if (c === '"') break; if (c === '\\') i++; } continue; }
    if (ch === '#') { while (i < text.length && text[i] !== '\n') i++; continue; }
    if (ch === '{') depth++;
    if (ch === '}') { depth--; if (depth === 0) return i; }
  }
  return -1;
}

/** @param {vscode.TextDocument} doc */
function validate(doc) {
  const diags = [];
  const text = doc.getText();

  // Role header
  if (!/role\s+"[^"]+"\s+id\s+"[^"]+"\s*\{/i.test(text)) {
    diags.push(diagAt(doc, 0, 0, 0, 1, vscode.DiagnosticSeverity.Warning, "Missing 'role " + "\"Name\" id \"id\" {" + "' header"));
  }

  // Brace balance
  const balance = braceBalance(text);
  if (balance.count !== 0) {
    const last = doc.lineCount - 1; const col = (doc.lineAt(last).text || '').length;
    diags.push(new vscode.Diagnostic(new vscode.Range(new vscode.Position(last, Math.max(0, col - 1)), new vscode.Position(last, col)), balance.count > 0 ? 'Unclosed { braces' : 'Too many } braces', vscode.DiagnosticSeverity.Error));
  }

  // Rough check of skills blocks
  const skillRegex = /skill\s+"([^"]+)"\s*\{/gi;
  let m; let pos = 0;
  while ((m = skillRegex.exec(text)) !== null) {
    const openIndex = m.index + m[0].length - 1;
    const matchEnd = findMatchingBrace(text, openIndex);
    if (matchEnd < 0) {
      const p = doc.positionAt(openIndex);
      diags.push(new vscode.Diagnostic(new vscode.Range(p, p), `Unclosed skill block '${m[1]}'`, vscode.DiagnosticSeverity.Error));
    }
    pos = m.index + 1;
  }

  // Vars entries pattern
  const varsBlocks = blockRanges(text, /\bvars\b/gi, doc);
  for (const r of varsBlocks) {
    const s = doc.offsetAt(r.start);
    const e = doc.offsetAt(r.end);
    const slice = text.slice(s, e);
    const lineStart = r.start.line;
    const lines = slice.split(/\r?\n/);
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i].trim();
      if (!line || line.startsWith('#') || line === '{' || line === '}') continue;
      if (!/^"[A-Za-z0-9_\.]+"\s*=\s*(true|false|\-?\d+(?:\.\d+)?|"[^"]+")/.test(line)) {
        const p = new vscode.Position(lineStart + i, 0);
        diags.push(new vscode.Diagnostic(new vscode.Range(p, p), `Unrecognized var entry: ${line}`, vscode.DiagnosticSeverity.Warning));
      }
    }
  }

  // Hint unknown var keys in vars blocks
  for (const r of varsBlocks) {
    const s = doc.offsetAt(r.start);
    const e = doc.offsetAt(r.end);
    const slice = text.slice(s, e);
    const lineStart = r.start.line;
    const lines = slice.split(/\r?\n/);
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i].trim();
      const m = line.match(/^\"([A-Za-z0-9_\.]+)\"\s*=\s*(true|false|\-?\d+(?:\.\d+)?|\"[^\"]+\")/);
      if (!m) continue;
      const key = m[1];
      if (!VAR_KEYS.some(v => v.key === key)) {
        const p = new vscode.Position(lineStart + i, 0);
        diags.push(new vscode.Diagnostic(new vscode.Range(p, p), `Unknown var key '${key}'`, vscode.DiagnosticSeverity.Hint));
      }
    }
  }
  return diags;
}

function braceBalance(text) {
  let depth = 0;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (ch === '"') { i++; while (i < text.length) { const c = text[i++]; if (c === '"') break; if (c === '\\') i++; } continue; }
    if (ch === '#') { while (i < text.length && text[i] !== '\n') i++; continue; }
    if (ch === '{') depth++;
    if (ch === '}') depth--;
  }
  return { count: depth };
}

function blockRanges(text, kwRegex, doc) {
  const ranges = [];
  let m;
  while ((m = kwRegex.exec(text)) !== null) {
    const afterKw = text.indexOf('{', m.index);
    if (afterKw < 0) continue;
    const end = findMatchingBrace(text, afterKw);
    if (end < 0) continue;
    ranges.push({ start: doc.positionAt(m.index), end: doc.positionAt(end) });
  }
  return ranges;
}

function diagAt(doc, sl, sc, el, ec, severity, message) {
  return new vscode.Diagnostic(new vscode.Range(new vscode.Position(sl, sc), new vscode.Position(el, ec)), message, severity);
}

// ---------- Formatter ----------

/** @param {vscode.TextDocument} doc */
function provideDocumentFormattingEdits(doc) {
  const edits = [];
  const formatted = formatText(doc.getText(), vscode.workspace.getConfiguration().get('etbbs-lbr.format.indentSize', 2));
  const full = new vscode.Range(new vscode.Position(0, 0), doc.lineAt(doc.lineCount - 1).range.end);
  edits.push(vscode.TextEdit.replace(full, formatted));
  return edits;
}

function formatApply(doc) {
  const ws = new vscode.WorkspaceEdit();
  const formatted = formatText(doc.getText(), vscode.workspace.getConfiguration().get('etbbs-lbr.format.indentSize', 2));
  const full = new vscode.Range(new vscode.Position(0, 0), doc.lineAt(doc.lineCount - 1).range.end);
  ws.replace(doc.uri, full, formatted);
  return vscode.workspace.applyEdit(ws);
}

function formatText(text, indentSize) {
  const lines = text.split(/\r?\n/);
  let depth = 0;
  const out = [];
  for (let raw of lines) {
    let line = raw.replace(/\s+$/g, '');
    // ignore empty
    const trimmed = line.trim();
    if (!trimmed) { out.push(''); continue; }
    // detect closing brace for this line's indent level (do not mutate depth yet)
    const closeFirst = /^\}/.test(trimmed);
    const currentDepth = Math.max(0, depth - (closeFirst ? 1 : 0));
    const indent = ' '.repeat(currentDepth * indentSize);
    line = indent + trimmed;
    // adjust trailing separators: keep as-is
    out.push(line);
    // adjust depth after line
    const opens = (trimmed.match(/\{/g) || []).length;
    const closes = (trimmed.match(/\}/g) || []).length;
    depth += opens - closes;
    if (depth < 0) depth = 0;
  }
  return out.join('\n');
}

function toId(name) {
  return name.trim().toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '');
}

function roleTemplate(name, id) {
  return `role "${name}" id "${id}" {
  description "Describe the role";
  vars {
    "hp" = 30;
    "mp" = 5;
    "atk" = 5;
    "def" = 3;
    "range" = 1;
    "speed" = 1;
  }
  tags { "example" }
  skills {
    skill "Basic Attack" {
      range 1; targeting enemies; cooldown 1;
      deal physical 5 damage to target from caster;
      consume mp = 1;
    }
  }
}`;
}

const VAR_KEYS = [
  { key: 'hp', detail: 'int: current health' },
  { key: 'max_hp', detail: 'int: max health' },
  { key: 'mp', detail: 'int/double: current mana' },
  { key: 'max_mp', detail: 'int/double: max mana (defaults to mp if omitted)' },
  { key: 'atk', detail: 'int: physical attack' },
  { key: 'def', detail: 'int: physical defense' },
  { key: 'matk', detail: 'int: magic attack' },
  { key: 'mdef', detail: 'int: magic defense' },
  { key: 'resist_physical', detail: '0..1: physical damage resist' },
  { key: 'resist_magic', detail: '0..1: magic damage resist' },
  { key: 'range', detail: 'int: UI/selection range' },
  { key: 'speed', detail: 'int/double: turn/move speed' },
  { key: 'shield_value', detail: 'int/double: shield HP (decays per hit)' },
  { key: 'undying_turns', detail: 'int: >0 prevents death, set to 1 to keep at 1 HP' },
  { key: 'mp_regen_per_turn', detail: 'int/double: mana regen each turn' },
  { key: 'hp_regen_per_turn', detail: 'int/double: health regen each turn' },
  { key: 'stunned_turns', detail: 'int: stunned duration' },
  { key: 'silenced_turns', detail: 'int: silenced duration' },
  { key: 'rooted_turns', detail: 'int: rooted/frozen duration' },
  { key: 'status_immune_turns', detail: 'int: immune to status while >0' },
  { key: 'bleed_turns', detail: 'int' },
  { key: 'bleed_per_turn', detail: 'int' },
  { key: 'burn_turns', detail: 'int' },
  { key: 'burn_per_turn', detail: 'int' },
  { key: 'auto_heal_below_half', detail: 'int: auto-heal once when HP <= 1/2 (uses this amount)' },
  { key: 'auto_heal_below_half_used', detail: 'bool: internal flag' },
  { key: 'extra_strikes_range', detail: 'int: bonus strikes range threshold' },
  { key: 'extra_strikes_count', detail: 'int: bonus strikes count (>=2)' }
];

const TAGS = ['stunned', 'silenced', 'rooted', 'frozen', 'undying', 'duel', 'windrealm', 'charged'];

const DSL_DOCS = {
  'range': 'Meta: range N — skill targeting range',
  'cooldown': 'Meta: cooldown N — enforced by engine',
  'targeting': 'Meta: targeting any|enemies|allies|self|tile',
  'cost': 'Meta: cost mp N',
  'mp': 'Meta: used with cost mp',
  'deal': 'Actions: deal N damage to <unit> | deal physical N damage to <unit> [from <unit>] [ignore defense P%] | deal magic ... [ignore resist P%]',
  'heal': 'Actions: heal N to <unit>',
  'move': 'Actions: move <unit> to (x,y)',
  'dash': 'Actions: dash towards <unit> up to N',
  'line': 'Actions: line [physical|magic] P to <unit> length L [radius R] [ignore ... X%]',
  'add': 'Actions: add tag "tag" to <unit> | add global tag "tag"',
  'remove': 'Actions: remove tag "tag" from <unit> | remove global tag "tag"',
  'set': 'Actions: set unit(<unit>) var "k" = value | set tile(x,y) var "k" = value | set global var "k" = value',
  'consume': 'Actions: consume mp = N',
  'if': 'Control: if <cond> then <stmt> [else <stmt>]',
  'repeat': 'Control: repeat N times <stmt>',
  'parallel': 'Control: parallel { stmt; stmt; }',
  'for': 'Control: for each <selector> [in parallel] do { ... }',
  'min_range': 'Meta: min_range N — minimal distance to target',
  'sealed_until': 'Meta: sealed_until T — unusable before global turn T',
  'nearest': 'Selector: nearest [N] enemies|allies of caster|target|point',
  'farthest': 'Selector: farthest [N] enemies|allies of caster|target|point'
};

module.exports = {
  activate,
  deactivate
};
