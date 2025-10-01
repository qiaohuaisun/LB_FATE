using ETBBS;
using System.Buffers;
using System.Text;
using System.Text.Json;

// Minimal LSP server for .lbr files: initialize, didOpen/didChange, completion, diagnostics
class Program
{
    static async Task Main()
    {
        var server = new LspServer();
        await server.RunAsync();
    }
}

sealed class LspServer
{
    private readonly Dictionary<string, string> _docs = new(StringComparer.OrdinalIgnoreCase);
    private Localizer _loc = new Localizer(Environment.GetEnvironmentVariable("ETBBS_LSP_LANG") ?? "en");

    public async Task RunAsync()
    {
        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();
        var reader = new StreamReader(stdin, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (true)
        {
            // Read headers
            string? line;
            int contentLength = 0;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    var s = line.Substring("Content-Length:".Length).Trim();
                    int.TryParse(s, out contentLength);
                }
            }
            if (contentLength <= 0) continue;
            // Read content
            var buffer = ArrayPool<byte>.Shared.Rent(contentLength);
            int read = 0;
            while (read < contentLength)
            {
                int n = await stdin.ReadAsync(buffer, read, contentLength - read);
                if (n == 0) break; read += n;
            }
            var json = Encoding.UTF8.GetString(buffer, 0, read);
            ArrayPool<byte>.Shared.Return(buffer);

            try { await HandleAsync(json, stdout); }
            catch { /* ignore */ }
        }
    }

    private async Task HandleAsync(string json, Stream stdout)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : null;
        var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
        if (method == "initialize")
        {
            try
            {
                var p = root.GetProperty("params");
                string? lang = null;
                if (p.TryGetProperty("locale", out var locEl)) lang = locEl.GetString();
                if (p.TryGetProperty("initializationOptions", out var io) && io.ValueKind == JsonValueKind.Object)
                {
                    if (io.TryGetProperty("lang", out var l2)) lang = l2.GetString();
                }
                if (!string.IsNullOrEmpty(lang)) _loc = new Localizer(lang!);
            }
            catch { }
            var result = new
            {
                jsonrpc = "2.0",
                id = JsonSerializer.Deserialize<object>(id ?? "1"),
                result = new
                {
                    capabilities = new
                    {
                        textDocumentSync = 1,
                        completionProvider = new { resolveProvider = false, triggerCharacters = new[] { "\"", ":", " ", "." } },
                        hoverProvider = true,
                        documentFormattingProvider = true,
                        codeActionProvider = true,
                        workspaceSymbolProvider = true
                    }
                }
            };
            await SendAsync(stdout, result);
            return;
        }
        if (method == "shutdown")
        {
            var res = new { jsonrpc = "2.0", id = JsonSerializer.Deserialize<object>(id ?? "1"), result = (object?)null };
            await SendAsync(stdout, res);
            return;
        }
        if (method == "exit") { Environment.Exit(0); }

        if (method == "textDocument/didOpen" || method == "textDocument/didChange")
        {
            var p = root.GetProperty("params");
            string uri; string text;
            if (method == "textDocument/didOpen")
            {
                var td = p.GetProperty("textDocument");
                uri = td.GetProperty("uri").GetString()!;
                text = td.GetProperty("text").GetString() ?? "";
            }
            else
            {
                uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
                var changes = p.GetProperty("contentChanges");
                text = changes.GetArrayLength() > 0 ? changes[changes.GetArrayLength() - 1].GetProperty("text").GetString() ?? "" : "";
            }
            _docs[uri] = text;
            var diagnostics = GetDiagnosticsFor(uri, text);
            var notif = new
            {
                jsonrpc = "2.0",
                method = "textDocument/publishDiagnostics",
                @params = new { uri, diagnostics }
            };
            await SendAsync(stdout, notif);
            return;
        }
        if (method == "textDocument/completion")
        {
            var p = root.GetProperty("params");
            var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
            int line = p.GetProperty("position").GetProperty("line").GetInt32();
            int ch = p.GetProperty("position").GetProperty("character").GetInt32();
            _docs.TryGetValue(uri, out var text);
            var items = CompletionForContext(text ?? string.Empty, line, ch);
            var res = new { jsonrpc = "2.0", id = JsonSerializer.Deserialize<object>(id ?? "1"), result = new { isIncomplete = false, items } };
            await SendAsync(stdout, res);
            return;
        }
        if (method == "textDocument/hover")
        {
            var p = root.GetProperty("params");
            var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
            int line = p.GetProperty("position").GetProperty("line").GetInt32();
            int ch = p.GetProperty("position").GetProperty("character").GetInt32();
            _docs.TryGetValue(uri, out var text);
            var contents = new { kind = "markdown", value = HoverForPosition(text ?? string.Empty, line, ch) };
            var res = new { jsonrpc = "2.0", id = JsonSerializer.Deserialize<object>(id ?? "1"), result = new { contents } };
            await SendAsync(stdout, res);
            return;
        }
        if (method == "textDocument/formatting")
        {
            var p = root.GetProperty("params");
            var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
            _docs.TryGetValue(uri, out var text);
            var formatted = FormatDocument(text ?? string.Empty);
            var (lines, lastLen) = CountLinesAndLastLen(formatted);
            var edit = new
            {
                range = new { start = new { line = 0, character = 0 }, end = new { line = Math.Max(0, lines - 1), character = lastLen } },
                newText = formatted
            };
            var res = new { jsonrpc = "2.0", id = JsonSerializer.Deserialize<object>(id ?? "1"), result = new[] { edit } };
            await SendAsync(stdout, res);
            return;
        }
        if (method == "textDocument/codeAction")
        {
            var p = root.GetProperty("params");
            var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
            _docs.TryGetValue(uri, out var text);
            var range = p.GetProperty("range");
            int line = range.GetProperty("start").GetProperty("line").GetInt32();
            var context = p.GetProperty("context");
            var actions = CodeActionsForContext(text ?? string.Empty, uri, line, context);
            var res = new { jsonrpc = "2.0", id = JsonSerializer.Deserialize<object>(id ?? "1"), result = actions };
            await SendAsync(stdout, res);
            return;
        }
        if (method == "workspace/symbol")
        {
            var symbols = new List<object>();
            foreach (var (uri, text) in _docs)
            {
                // roles
                var rid = FindRoleId(text);
                if (!string.IsNullOrEmpty(rid))
                {
                    var (ln, col) = FindFirst(text, "id \"");
                    symbols.Add(new { name = rid, kind = 23, location = new { uri, range = new { start = new { line = ln, character = col }, end = new { line = ln, character = col + rid!.Length } } } });
                }
                // skills
                foreach (var sk in FindSkillBodies(text))
                {
                    var (baseLine, baseCol) = GetLineCol(text, sk.BodyStart);
                    symbols.Add(new { name = sk.Name, kind = 14, location = new { uri, range = new { start = new { line = baseLine - 1, character = Math.Max(0, baseCol - 1) }, end = new { line = baseLine - 1, character = Math.Max(0, baseCol - 1 + sk.Name.Length) } } } });
                }
            }
            var res = new { jsonrpc = "2.0", id = JsonSerializer.Deserialize<object>(id ?? "1"), result = symbols.ToArray() };
            await SendAsync(stdout, res);
            return;
        }
    }

    private object[] CompletionForContext(string text, int line, int ch)
    {
        var list = new List<object>();
        int off = GetOffset(text, line, ch);
        var prefix = GetPrefix(text, off, 40);
        void Add(params string[] arr) { list.AddRange(arr.Select(k => new { label = k, kind = 14, insertText = k })); }
        // LBR top-level
        if (prefix.Contains("targeting ")) Add("any", "enemies", "allies", "self", "tile", "point");
        else if (prefix.EndsWith("range ") || prefix.EndsWith("min_range ")) Add("0", "1", "2", "3", "4", "5");
        else if (prefix.Contains("sealed_until ")) Add("day ");
        else if (prefix.EndsWith("for ")) Add("each ");
        else if (prefix.Contains("for each ")) Add("enemies", "allies", "units", "nearest", "farthest");
        else if (prefix.EndsWith("in ")) Add("circle ", "cross ", "line length ", "cone radius ");
        else if (prefix.EndsWith("deal ")) Add("physical ", "magic ", "5 damage to target", "10 damage to target");
        else if (prefix.EndsWith("set ")) Add("global var ", "unit var ", "tile (x,y) var ");
        else if (prefix.EndsWith("add ")) Add("tag ", "global tag ");
        else if (prefix.EndsWith("remove ")) Add("tag ", "global tag ", "global var ", "unit var ", "tile var ");
        else if (prefix.EndsWith("distance ")) Add("manhattan", "chebyshev", "euclidean");
        else Add("role", "description", "vars", "tags", "skills", "skill", "range", "min_range", "cooldown", "distance", "targeting", "ends_turn", "sealed_until", "for", "each", "deal", "heal", "set", "add", "remove", "move", "dash");

        // Contextual suggestions for point/tile modes
        if (prefix.Contains("targeting point"))
        {
            list.Add(new { label = "示例: for each enemies in range 3 of point", kind = 14, insertText = "for each enemies in range 3 of point do { deal 5 damage to it; }" });
            list.Add(new { label = "提示: 由调用方提供坐标 (cfg.TargetPos)", kind = 14, insertText = "# 提示: 调用方需提供坐标 (cfg.TargetPos)\n" });
        }
        if (prefix.EndsWith("in range "))
        {
            list.Add(new { label = "of point", kind = 14, insertText = "of point" });
        }
        if (prefix.Contains("targeting tile"))
        {
            list.Add(new { label = "示例: set tile (x,y) var \"mark\" = 1", kind = 14, insertText = "set tile (0,0) var \"mark\" = 1" });
        }
        // Quick hint to set distance metric
        if (prefix.Contains("distance ")) { /* covered above */ }
        else if (prefix.Contains("targeting ") || prefix.Contains("range "))
        {
            list.Add(new { label = "提示: distance manhattan|chebyshev|euclidean", kind = 14, insertText = "distance manhattan" });
        }
        return list.ToArray();
    }

    private object[] GetDiagnosticsFor(string uri, string text)
    {
        var diags = new List<object>();
        if (uri.EndsWith(".lbr", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var role = LbrLoader.Load(text);
                // semantic warnings
                if (string.IsNullOrWhiteSpace(role.Id)) diags.Add(MakeDiag(0, 0, 0, 1, "warning", _loc.EmptyRoleId));
                if (string.IsNullOrWhiteSpace(role.Name)) diags.Add(MakeDiag(0, 0, 0, 1, "warning", _loc.EmptyRoleName));

                // Analyze each skill body and emit DSL warnings with proper mapping
                foreach (var sk in FindSkillBodies(text))
                {
                    var warns = TextDsl.AnalyzeText(sk.Script);
                    foreach (var w in warns)
                    {
                        var (ln, col) = ExtractLineColFromWarning(w);
                        var (baseLine, baseCol) = GetLineCol(text, sk.EffectiveStart);
                        int gLine = baseLine + Math.Max(0, ln - 1);
                        int gCol = (ln <= 1) ? baseCol + Math.Max(0, col - 1) : col;
                        diags.Add(MakeDiag(Math.Max(0, gLine - 1), Math.Max(0, gCol - 1), Math.Max(0, gLine - 1), Math.Max(0, gCol), "warning", _loc.SkillPrefix(sk.Name) + _loc.TranslateAnalyzeWarning(w)));
                    }
                }
            }
            catch (FormatException ex)
            {
                // Try to determine if it's a DSL error and map into the skill body
                var (dslLn, dslCol) = ExtractLineCol(ex.Message);
                bool mapped = false;
                foreach (var sk in FindSkillBodies(text))
                {
                    try
                    {
                        // Re-parse the DSL to reproduce error and ensure body match
                        _ = TextDsl.FromTextUsingGlobals(sk.Name, sk.Script);
                    }
                    catch (FormatException)
                    {
                        var (baseLine, baseCol) = GetLineCol(text, sk.EffectiveStart);
                        int gLine = baseLine + Math.Max(0, dslLn - 1);
                        int gCol = (dslLn <= 1) ? baseCol + Math.Max(0, dslCol - 1) : dslCol;
                        diags.Add(MakeDiag(Math.Max(0, gLine - 1), Math.Max(0, gCol - 1), Math.Max(0, gLine - 1), Math.Max(0, gCol), "error", _loc.SkillPrefix(sk.Name) + _loc.LocalizeParseMessage(ex.Message)));
                        mapped = true; break;
                    }
                }
                if (!mapped)
                {
                    // Fallback: report at file start if mapping failed
                    diags.Add(MakeDiag(0, 0, 0, 1, "error", _loc.LocalizeParseMessage(ex.Message)));
                }
            }
        }
        return diags.ToArray();
    }

    private static (int line, int col) ExtractLineCol(string message)
    {
        // Expect: "... at line X, column Y: ..."
        var idx = message.IndexOf("line ", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            idx += 5; int end = message.IndexOf(',', idx); if (end < 0) end = idx;
            if (int.TryParse(message.Substring(idx, end - idx).Trim(), out var ln))
            {
                var cidx = message.IndexOf("column", end, StringComparison.OrdinalIgnoreCase);
                if (cidx >= 0)
                {
                    cidx += 6; int cend = message.IndexOf(':', cidx); if (cend < 0) cend = message.Length;
                    if (int.TryParse(message.Substring(cidx, cend - cidx).Trim(), out var col))
                        return (ln, col);
                }
                return (ln, 1);
            }
        }
        return (1, 1);
    }

    private static (int line, int col) ExtractLineColFromWarning(string text)
    {
        // Expect: optional prefix "line X, col Y: ..."; fallback to (1,1)
        var idx = text.IndexOf("line ", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            idx += 5; int end = text.IndexOf(',', idx); if (end < 0) end = idx;
            if (int.TryParse(text.Substring(idx, end - idx).Trim(), out var ln))
            {
                var cidx = text.IndexOf("col", end, StringComparison.OrdinalIgnoreCase);
                if (cidx >= 0)
                {
                    cidx += 3; int cend = text.IndexOf(':', cidx); if (cend < 0) cend = text.Length;
                    if (int.TryParse(text.Substring(cidx, cend - cidx).Trim(), out var col))
                        return (ln, col);
                }
                return (ln, 1);
            }
        }
        return (1, 1);
    }

    private sealed record SkillSpan(string Name, int BodyStart, int BodyEnd, string Script, int EffectiveStart);

    private static List<SkillSpan> FindSkillBodies(string text)
    {
        var result = new List<SkillSpan>();
        int skillsStart = text.IndexOf("skills", StringComparison.Ordinal);
        if (skillsStart < 0) return result;
        int blkStart = text.IndexOf('{', skillsStart);
        if (blkStart < 0) return result;
        int depth = 1; int i = blkStart + 1;
        while (i < text.Length && depth > 0)
        {
            int si = text.IndexOf("skill", i, StringComparison.Ordinal);
            if (si < 0) break;
            // find name between quotes
            int q1 = text.IndexOf('"', si);
            if (q1 < 0) break;
            int q2 = text.IndexOf('"', q1 + 1);
            if (q2 < 0) break;
            string name = text.Substring(q1 + 1, q2 - q1 - 1);
            int sb = text.IndexOf('{', q2);
            if (sb < 0) break;
            int d = 1; int j = sb + 1;
            while (j < text.Length && d > 0)
            {
                char ch = text[j];
                if (ch == '{') d++; else if (ch == '}') d--; j++;
            }
            int bodyStart = sb + 1; int bodyEnd = j - 1;
            if (bodyStart >= 0 && bodyEnd >= bodyStart)
            {
                var bodyRaw = text.Substring(bodyStart, bodyEnd - bodyStart);
                var bodyTrimmed = bodyRaw.Trim();
                int leadSkip = bodyRaw.Length - bodyRaw.TrimStart().Length;
                int effectiveStart = bodyStart + leadSkip;
                result.Add(new SkillSpan(name, bodyStart, bodyEnd, bodyTrimmed, effectiveStart));
            }
            i = j;
        }
        return result;
    }

    private static (int line, int col) GetLineCol(string src, int pos)
    {
        int line = 1, col = 1;
        for (int i = 0; i < src.Length && i < pos; i++)
        {
            var c = src[i];
            if (c == '\r') continue;
            if (c == '\n') { line++; col = 1; }
            else col++;
        }
        if (col < 1) col = 1; return (line, col);
    }

    private static object MakeDiag(int sl, int sc, int el, int ec, string severity, string message)
        => new
        {
            range = new { start = new { line = sl, character = sc }, end = new { line = el, character = ec } },
            severity = severity == "error" ? 1 : 2,
            source = "etbbs",
            message
        };

    private static async Task SendAsync(Stream stdout, object message)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
        await stdout.WriteAsync(header, 0, header.Length);
        await stdout.WriteAsync(bytes, 0, bytes.Length);
        await stdout.FlushAsync();
    }
    // NOTE: class continues with helpers below; do not close here
    private static (int line, int col) FindFirst(string src, string marker)
    {
        int pos = src.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (pos < 0) return (0, 0);
        return GetLineCol(src, pos + marker.Length);
    }

    private string HoverForPosition(string text, int line, int ch)
    {
        int off = GetOffset(text, line, ch);
        var word = GetWordAt(text, off);
        if (string.IsNullOrEmpty(word)) return "";
        if (_hover.TryGetValue(word, out var doc)) return _loc.TranslateHover(word, doc);
        return $"`{word}`";
    }

    private static string GetWordAt(string text, int off)
    {
        if (off < 0 || off >= text.Length) return string.Empty;
        int i = off; while (i > 0 && !char.IsLetterOrDigit(text[i - 1]) && text[i - 1] != '_') i--;
        int j = i; while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_')) j++;
        return text.Substring(i, Math.Max(0, j - i));
    }

    private static int GetOffset(string text, int line, int ch)
    {
        int curLine = 0; int idx = 0;
        while (curLine < line && idx < text.Length)
        {
            if (text[idx++] == '\n') curLine++;
        }
        return Math.Min(text.Length, idx + ch);
    }

    private static (int lines, int lastLen) CountLinesAndLastLen(string text)
    {
        int lines = 1, last = 0;
        foreach (var c in text)
        {
            if (c == '\n') { lines++; last = 0; } else last++;
        }
        return (lines, last);
    }

    private static string GetPrefix(string text, int offset, int max)
    {
        int start = Math.Max(0, offset - max);
        return text.Substring(start, Math.Min(text.Length - start, max));
    }

    private object[] CodeActionsForContext(string text, string uri, int line, JsonElement context)
    {
        var actions = new List<object>();
        foreach (var d in context.GetProperty("diagnostics").EnumerateArray())
        {
            var msg = d.GetProperty("message").GetString() ?? "";
            if (msg.Contains("min_range") && msg.Contains("exceeds range"))
            {
                // Replace min_range value with current range value in the document
                var rangeVal = FindNumberAfter(text, "range ");
                var minRangeLoc = FindSpanForKeyword(text, "min_range ");
                if (rangeVal is not null && minRangeLoc is not null)
                {
                    var (sl, sc, el, ec) = minRangeLoc.Value;
                    var edit = new { range = new { start = new { line = sl, character = sc }, end = new { line = el, character = ec } }, newText = $"min_range {rangeVal}" };
                    actions.Add(new { title = _loc.FixSetMinRange, kind = "quickfix", edit = new { changes = new Dictionary<string, object[]> { [uri] = new[] { edit } } } });
                }
            }
            if (msg.Contains("chance 0%") || msg.Contains("概率为 0%"))
            {
                var span = FindSpanForKeyword(text, "chance ");
                if (span is not null)
                {
                    var (sl, sc, el, ec) = span.Value;
                    var edit = new { range = new { start = new { line = sl, character = sc }, end = new { line = el, character = ec } }, newText = "chance 50%" };
                    actions.Add(new { title = _loc.FixChangeChance, kind = "quickfix", edit = new { changes = new Dictionary<string, object[]> { [uri] = new[] { edit } } } });
                }
            }
            if (msg.Contains("line dir not specified") || msg.Contains("cone dir not specified"))
            {
                var span = FindSpanForKeyword(text, "dir ");
                if (span is null)
                {
                    // Insert dir "$dir" near line start
                    var insertAt = new { line, character = 0 };
                    var edit = new { range = new { start = insertAt, end = insertAt }, newText = "dir \"$dir\" " };
                    actions.Add(new { title = "添加方向参数 dir \"$dir\"", kind = "quickfix", edit = new { changes = new Dictionary<string, object[]> { [uri] = new[] { edit } } } });
                }
            }
            if (msg.Contains("cone angle not specified") || msg.Contains("angle not specified"))
            {
                var span = FindSpanForKeyword(text, "angle ");
                if (span is null)
                {
                    var insertAt = new { line, character = 0 };
                    var edit = new { range = new { start = insertAt, end = insertAt }, newText = "angle 90 " };
                    actions.Add(new { title = "添加扇形角度 angle 90", kind = "quickfix", edit = new { changes = new Dictionary<string, object[]> { [uri] = new[] { edit } } } });
                }
            }
        }
        return actions.ToArray();
    }

    private static int? FindNumberAfter(string text, string marker)
    {
        int i = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null; i += marker.Length;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        int start = i; while (i < text.Length && char.IsDigit(text[i])) i++;
        if (i > start && int.TryParse(text.Substring(start, i - start), out var n)) return n;
        return null;
    }

    private static (int sl, int sc, int el, int ec)? FindSpanForKeyword(string text, string marker)
    {
        int i = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null; int j = i + marker.Length;
        var (sl, sc) = GetLineCol(text, i); var (el, ec) = GetLineCol(text, j);
        return (Math.Max(0, sl - 1), Math.Max(0, sc - 1), Math.Max(0, el - 1), Math.Max(0, ec - 1));
    }

    private static string FormatDocument(string text)
    {
        var sb = new StringBuilder();
        int indent = 0;
        for (int i = 0; i < text.Length;)
        {
            // read a line
            int j = i; while (j < text.Length && text[j] != '\n') j++;
            var line = text.Substring(i, j - i).Trim();
            bool dedent = line.StartsWith("}");
            if (dedent) indent = Math.Max(0, indent - 1);
            sb.Append(new string(' ', indent * 2)).Append(line).Append('\n');
            if (line.EndsWith("{")) indent++;
            if (j < text.Length && text[j] == '\n') j++;
            i = j;
        }
        return sb.ToString();
    }

    private sealed class Localizer
    {
        private readonly bool zh;
        public Localizer(string lang) { zh = (lang ?? "").StartsWith("zh", StringComparison.OrdinalIgnoreCase); }
        public string EmptyRoleId => zh ? "角色ID为空" : "Empty role id";
        public string EmptyRoleName => zh ? "角色名称为空" : "Empty role name";
        public string FixSetMinRange => zh ? "将 min_range 设为与 range 相同" : "Set min_range to range";
        public string FixChangeChance => zh ? "将概率修改为 50%" : "Change chance to 50%";
        public string SkillPrefix(string name) => zh ? $"技能‘{name}’：" : $"Skill '{name}': ";
        public string LocalizeParseMessage(string msg)
        {
            if (!zh) return msg;
            var (ln, col) = ExtractLineColLocal(msg);
            bool isDsl = msg.StartsWith("DSL parse error", StringComparison.OrdinalIgnoreCase);
            bool isLbr = msg.StartsWith("LBR parse error", StringComparison.OrdinalIgnoreCase);
            var ix = msg.IndexOf(':');
            string tail = ix >= 0 && ix + 1 < msg.Length ? msg.Substring(ix + 1).Trim() : msg;
            if (isDsl) return $"DSL 解析错误，第 {ln} 行，第 {col} 列：{tail}";
            if (isLbr) return $"LBR 解析错误，第 {ln} 行，第 {col} 列：{tail}";
            return msg;
        }
        public string TranslateHover(string key, string eng)
        {
            if (!zh) return eng;
            return key.ToLowerInvariant() switch
            {
                "role" => "顶层角色声明：`role \"Name\" id \"id\" { ... }`",
                "vars" => "定义角色变量：`vars { \"hp\" = 30 }`",
                "tags" => "定义角色标签：`tags { \"melee\" }`",
                "skills" => "定义技能块，包含 `skill \"Name\" { ... }`",
                "skill" => "声明一个包含 DSL 的技能体",
                "range" => "技能元信息：最大射程",
                "min_range" => "技能元信息：最小距离限制",
                "cooldown" => "技能元信息：冷却回合数",
                "targeting" => "技能元信息：选取目标模式 (any|enemies|allies|self|tile/point)",
                "ends_turn" => "技能元信息：施放后结束回合",
                "sealed_until" => "技能元信息：解封时间 (回合或日/阶段)",
                "deal" => "动作：对目标造成伤害（支持 physical/magic）",
                "heal" => "动作：为单位回复生命",
                "set" => "动作：设置全局/单位/地块变量",
                "add" => "动作：添加标签（单位/全局/地块）",
                "remove" => "动作：移除标签/变量（单位/全局/地块）",
                "for" => "循环：遍历选中的单位 `for each enemies ... do { ... }`",
                "each" => "选择要遍历的单位集合",
                "nearest" => "选择器：按距离最近排序",
                "farthest" => "选择器：按距离最远排序",
                _ => eng
            };
        }
        public string TranslateAnalyzeWarning(string w)
        {
            if (!zh) return w;
            var (ln, col) = ExtractLineColFromWarningLocal(w);
            var colon = w.IndexOf(':');
            string content = colon >= 0 && colon + 1 < w.Length ? w.Substring(colon + 1).Trim() : w;
            string trans = content;
            if (content.StartsWith("chance 0%")) trans = "概率为 0%：then 分支不可达";
            else if (content.StartsWith("chance 100%")) trans = "概率为 100%：else 分支不可达";
            else if (content.StartsWith("repeat") && content.EndsWith("times has no effect"))
            {
                var parts = content.Split(' ');
                var n = parts.Length > 1 ? parts[1] : "";
                trans = $"重复 {n} 次无效果";
            }
            else if (content.Contains("parallel block is empty")) trans = "并行块为空";
            else if (content.Contains("empty block has no effect")) trans = "空代码块无效果";
            else if (content.Contains("selector range is negative")) trans = "选择器范围为负数";
            else if (content.Contains("selector limit is negative")) trans = "选择器限制为负数";
            else if (content.StartsWith("min_range") && content.Contains("exceeds range")) trans = "min_range 大于 range：将无法选择目标";
            else if (content.StartsWith("targeting self")) trans = "targeting self 且 range 非零：range 将被忽略";
            if (ln > 0 && col > 0) return $"第 {ln} 行，第 {col} 列：{trans}";
            return trans;
        }
        private static (int line, int col) ExtractLineColLocal(string message)
        {
            var idx = message.IndexOf("line ", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                idx += 5; int end = message.IndexOf(',', idx); if (end < 0) end = idx;
                if (int.TryParse(message.Substring(idx, end - idx).Trim(), out var ln))
                {
                    var cidx = message.IndexOf("column", end, StringComparison.OrdinalIgnoreCase);
                    if (cidx >= 0)
                    {
                        cidx += 6; int cend = message.IndexOf(':', cidx); if (cend < 0) cend = message.Length;
                        if (int.TryParse(message.Substring(cidx, cend - cidx).Trim(), out var col))
                            return (ln, col);
                    }
                    return (ln, 1);
                }
            }
            return (1, 1);
        }
        private static (int line, int col) ExtractLineColFromWarningLocal(string text)
        {
            var idx = text.IndexOf("line ", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                idx += 5; int end = text.IndexOf(',', idx); if (end < 0) end = idx;
                if (int.TryParse(text.Substring(idx, end - idx).Trim(), out var ln))
                {
                    var cidx = text.IndexOf("col", end, StringComparison.OrdinalIgnoreCase);
                    if (cidx >= 0)
                    {
                        cidx += 3; int cend = text.IndexOf(':', cidx); if (cend < 0) cend = text.Length;
                        if (int.TryParse(text.Substring(cidx, cend - cidx).Trim(), out var col))
                            return (ln, col);
                    }
                    return (ln, 1);
                }
            }
            return (1, 1);
        }
    }

    private static string? FindRoleId(string text)
    {
        int idx = text.IndexOf("id \"", StringComparison.Ordinal);
        if (idx < 0) return null;
        idx += 4;
        int end = text.IndexOf('"', idx);
        if (end < 0) return null;
        return text.Substring(idx, end - idx);
    }

    private static readonly Dictionary<string, string> _hover = new(StringComparer.OrdinalIgnoreCase)
    {
        ["role"] = "Top-level LBR declaration of a role: `role \"Name\" id \"id\" { ... }`",
        ["vars"] = "Define role variables: `vars { \"hp\" = 30 }`",
        ["tags"] = "Define role tags: `tags { \"melee\" }`",
        ["skills"] = "Define skills block containing `skill \"Name\" { ... }`",
        ["skill"] = "Declare a skill with a DSL body",
        ["range"] = "Skill metadata: max range to target",
        ["min_range"] = "Skill metadata: minimum range constraint",
        ["distance"] = "Skill metadata: distance metric (manhattan|chebyshev|euclidean)",
        ["cooldown"] = "Skill metadata: cooldown turns",
        ["targeting"] = "Skill metadata: targeting mode (any|enemies|allies|self|tile/point). For point mode, provider must supply a coordinate via cfg.TargetPos; selectors can use `in range N of point`. Combine with `distance ...` to control metric.",
        ["point"] = "Point targeting: provider must pass a coordinate (cfg.TargetPos). You can use selectors like `in range N of point`.",
        ["tile"] = "Tile targeting: set/touch tile-scoped variables, e.g., `set tile (x,y) var \"k\" = 1`.",
        ["ends_turn"] = "Skill metadata: ends the turn when executed",
        ["sealed_until"] = "Skill metadata: locked until a turn or day/phase",
        ["deal"] = "Action: deal N (or physical/magic) damage to target",
        ["heal"] = "Action: heal N to unit",
        ["set"] = "Action: set global/unit/tile variable",
        ["add"] = "Action: add tag (unit/global) or tile tag",
        ["remove"] = "Action: remove tag/var (unit/global/tile)",
        ["for"] = "Loop over selected units: `for each enemies ... do { ... }`",
        ["each"] = "Select units to iterate",
        ["nearest"] = "Selector: nearest units by distance",
        ["farthest"] = "Selector: farthest units by distance"
    };
}
