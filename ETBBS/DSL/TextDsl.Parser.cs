using System.Text;

namespace ETBBS;

public static partial class TextDsl
{
    private sealed class Parser
    {
        private readonly string _src;
        private int _pos;

        public Parser(string src) { _src = src; _pos = 0; }

        public ProgramNode ParseProgram()
        {
            var prog = new ProgramNode();
            SkipWs();
            while (!Eof())
            {
                if (TryParseMeta(prog))
                {
                    SkipWs(); if (Peek() == ';') { _pos++; SkipWs(); }
                    continue;
                }
                var st = ParseStmt();
                prog.Statements.Add(st);
                SkipWs();
                if (!Eof() && Peek() == ';') { _pos++; SkipWs(); }
            }
            return prog;
        }

        private bool TryParseMeta(ProgramNode prog)
        {
            if (TryKeyword("cost"))
            {
                RequireKeyword("mp");
                prog.MpCost = ParseInt();
                return true;
            }
            if (TryKeyword("range"))
            {
                prog.Range = ParseInt();
                return true;
            }
            if (TryKeyword("cooldown"))
            {
                prog.Cooldown = ParseInt();
                return true;
            }
            if (TryKeyword("targeting"))
            {
                if (TryKeyword("any")) prog.Targeting = "any";
                else if (TryKeyword("enemies")) prog.Targeting = "enemies";
                else if (TryKeyword("allies")) prog.Targeting = "allies";
                else if (TryKeyword("self")) prog.Targeting = "self";
                else if (TryKeyword("tile") || TryKeyword("point")) prog.Targeting = "tile";
                else throw Error("unknown targeting mode");
                return true;
            }
            if (TryKeyword("min_range"))
            {
                prog.MinRange = ParseInt();
                return true;
            }
            if (TryKeyword("sealed_until"))
            {
                // Support: sealed_until <turn>  (legacy) OR sealed_until day <D> [phase <P>]
                var save = _pos;
                if (TryKeyword("day"))
                {
                    prog.SealedUntilDay = ParseInt();
                    if (TryKeyword("phase")) prog.SealedUntilPhase = ParseInt();
                    return true;
                }
                _pos = save;
                prog.SealedUntil = ParseInt();
                return true;
            }
            if (TryKeyword("ends_turn"))
            {
                prog.EndsTurn = true;
                return true;
            }
            return false;
        }

        private IStmt ParseStmt()
        {
            if (TryKeyword("if")) return ParseIf();
            if (TryKeyword("chance")) return ParseChance();
            if (TryKeyword("repeat")) return ParseRepeat();
            if (TryKeyword("for")) return ParseForEach();
            if (TryKeyword("parallel")) return ParseParallel();
            return ParseActionOrBlock();
        }

        private IStmt ParseActionOrBlock()
        {
            SkipWs();
            if (Peek() == '{') return ParseBlock();
            return ParseAction();
        }

        private BlockStmt ParseBlock()
        {
            Expect('{');
            var blk = new BlockStmt();
            SkipWs();
            while (!Eof() && Peek() != '}')
            {
                blk.Items.Add(ParseStmt());
                SkipWs();
                if (Peek() == ';') { _pos++; SkipWs(); }
            }
            Expect('}');
            return blk;
        }

        private IStmt ParseIf()
        {
            // already consumed 'if'
            var cond = ParseCond();
            RequireKeyword("then");
            var thenSt = ParseActionOrBlock();
            IStmt? elseSt = null;
            if (TryKeyword("else")) elseSt = ParseActionOrBlock();
            return new IfStmt { Cond = cond, Then = thenSt, Else = elseSt };
        }

        private IStmt ParseChance()
        {
            // chance <P>% then <stmt> [else <stmt>]
            var p = ParsePercent();
            RequireKeyword("then");
            var thenSt = ParseActionOrBlock();
            IStmt? elseSt = null;
            if (TryKeyword("else")) elseSt = ParseActionOrBlock();
            return new ChanceStmt { Probability = p, Then = thenSt, Else = elseSt };
        }

        private IStmt ParseRepeat()
        {
            // already consumed 'repeat'
            var n = ParseInt();
            RequireKeyword("times");
            var body = ParseActionOrBlock();
            return new RepeatStmt { Times = n, Body = body };
        }

        private IStmt ParseParallel()
        {
            // already consumed 'parallel'
            var blk = ParseBlock();
            var ps = new ParallelStmt();
            ps.Branches.AddRange(blk.Items);
            return ps;
        }

        private IStmt ParseForEach()
        {
            // already consumed 'for'
            RequireKeyword("each");
            var sel = ParseSelector();
            var parallel = TryKeyword("in") && RequireKeyword("parallel");
            RequireKeyword("do");
            var body = ParseActionOrBlock();
            return new ForEachStmt { Selector = sel, Parallel = parallel, Body = body };
        }

        private SelectorExpr ParseSelector()
        {
            if (TryKeyword("enemies"))
            {
                var sel = new CombinedSelector { Kind = CombinedSelector.BaseKind.Enemies };
                if (TryKeyword("of")) sel.OfUnit = ParseUnitRef();
                // Disambiguate 'in range' vs. outer 'in parallel'
                {
                    var save = _pos;
                    if (TryKeyword("in"))
                    {
                        if (TryKeyword("range"))
                        {
                            sel.Range = ParseInt(); RequireKeyword("of"); if (TryKeyword("point")) sel.RangeFromPoint = true; else sel.RangeOrigin = ParseUnitRef();
                        }
                        else
                        {
                            _pos = save; // not an in-range clause; leave 'in' for caller (e.g., 'in parallel')
                        }
                    }
                }
                if (TryKeyword("with")) { RequireKeyword("tag"); sel.TagFilter = ParseString(); }
                if (TryKeyword("with")) { RequireKeyword("var"); sel.VarKey = ParseString(); sel.VarOp = ParseOp(); sel.VarValue = ParseInt(); }
                if (TryKeyword("order")) { RequireKeyword("by"); RequireKeyword("var"); sel.VarOrderKey = ParseString(); sel.VarOrderDesc = TryKeyword("desc"); if (!sel.VarOrderDesc) { sel.VarOrderDesc = false; TryKeyword("asc"); } }
                if (TryKeyword("limit")) sel.Limit = ParseInt();
                return sel;
            }
            if (TryKeyword("allies"))
            {
                var sel = new CombinedSelector { Kind = CombinedSelector.BaseKind.Allies };
                if (TryKeyword("of")) sel.OfUnit = ParseUnitRef();
                // Disambiguate 'in range' vs. outer 'in parallel'
                {
                    var save = _pos;
                    if (TryKeyword("in"))
                    {
                        if (TryKeyword("range"))
                        {
                            sel.Range = ParseInt(); RequireKeyword("of"); if (TryKeyword("point")) sel.RangeFromPoint = true; else sel.RangeOrigin = ParseUnitRef();
                        }
                        else
                        {
                            _pos = save;
                        }
                    }
                }
                if (TryKeyword("with")) { RequireKeyword("tag"); sel.TagFilter = ParseString(); }
                if (TryKeyword("with")) { RequireKeyword("var"); sel.VarKey = ParseString(); sel.VarOp = ParseOp(); sel.VarValue = ParseInt(); }
                if (TryKeyword("order")) { RequireKeyword("by"); RequireKeyword("var"); sel.VarOrderKey = ParseString(); sel.VarOrderDesc = TryKeyword("desc"); if (!sel.VarOrderDesc) { sel.VarOrderDesc = false; TryKeyword("asc"); } }
                if (TryKeyword("limit")) sel.Limit = ParseInt();
                return sel;
            }
            if (TryKeyword("units"))
            {
                var sel = new CombinedSelector { Kind = CombinedSelector.BaseKind.Units };
                if (TryKeyword("with"))
                {
                    if (TryKeyword("tag")) sel.TagFilter = ParseString();
                    else if (TryKeyword("var")) { sel.VarKey = ParseString(); sel.VarOp = ParseOp(); sel.VarValue = ParseInt(); }
                    else throw Error("expected tag|var after 'with'");
                }
                if (TryKeyword("order")) { RequireKeyword("by"); RequireKeyword("var"); sel.VarOrderKey = ParseString(); sel.VarOrderDesc = TryKeyword("desc"); if (!sel.VarOrderDesc) { sel.VarOrderDesc = false; TryKeyword("asc"); } }
                if (TryKeyword("limit")) sel.Limit = ParseInt();
                return sel;
            }
            if (TryKeyword("nearest"))
            {
                // allow optional whitespace before the count and keyword
                SkipWs();
                int? n = null; if (!Eof() && char.IsDigit(Peek())) n = ParseInt();
                SkipWs();
                if (TryKeyword("enemies"))
                {
                    var sel = new CombinedSelector { Kind = CombinedSelector.BaseKind.Enemies, OrderByDistance = true, OrderDesc = false };
                    RequireKeyword("of"); if (TryKeyword("point")) sel.DistanceFromPoint = true; else sel.DistanceOrigin = ParseUnitRef();
                    sel.Limit = n;
                    return sel;
                }
                if (TryKeyword("allies"))
                {
                    var sel = new CombinedSelector { Kind = CombinedSelector.BaseKind.Allies, OrderByDistance = true, OrderDesc = false };
                    RequireKeyword("of"); if (TryKeyword("point")) sel.DistanceFromPoint = true; else sel.DistanceOrigin = ParseUnitRef();
                    sel.Limit = n;
                    return sel;
                }
                throw Error("nearest expects allies|enemies");
            }
            if (TryKeyword("farthest"))
            {
                SkipWs();
                int? n = null; if (!Eof() && char.IsDigit(Peek())) n = ParseInt();
                SkipWs();
                if (TryKeyword("enemies"))
                {
                    var sel = new CombinedSelector { Kind = CombinedSelector.BaseKind.Enemies, OrderByDistance = true, OrderDesc = true };
                    RequireKeyword("of"); if (TryKeyword("point")) sel.DistanceFromPoint = true; else sel.DistanceOrigin = ParseUnitRef();
                    sel.Limit = n;
                    return sel;
                }
                if (TryKeyword("allies"))
                {
                    var sel = new CombinedSelector { Kind = CombinedSelector.BaseKind.Allies, OrderByDistance = true, OrderDesc = true };
                    RequireKeyword("of"); if (TryKeyword("point")) sel.DistanceFromPoint = true; else sel.DistanceOrigin = ParseUnitRef();
                    sel.Limit = n;
                    return sel;
                }
                throw Error("farthest expects allies|enemies");
            }
            throw Error("unknown selector");
        }

        private CondExpr ParseCond()
        {
            // <unitRef> has tag "x" | <unitRef> mp OP int
            var u = ParseUnitRef();
            if (TryKeyword("has"))
            {
                RequireKeyword("tag"); var tag = ParseString();
                return new HasTagCond { Unit = u, Tag = tag };
            }
            if (TryKeyword("mp"))
            {
                var op = ParseOp(); var n = ParseInt();
                return new MpCompareCond { Unit = u, Op = op, Value = n };
            }
            throw Error("unsupported condition");
        }

        private ActionStmt ParseAction()
        {
            // deal N damage to <unit>
            if (TryKeyword("deal"))
            {
                if (TryKeyword("physical"))
                {
                    var n = ParseInt(); RequireKeyword("damage"); RequireKeyword("to"); var ur = ParseUnitRef();
                    var stmt = new ActionStmt { Kind = ActionKind.DealPhysicalDamage, IntArg = n, Target = ur, RatioArg = 0.0 };
                    if (TryKeyword("from")) stmt.FromUnit = ParseUnitRef(); else stmt.FromUnit = new UnitRefCaster();
                    if (TryKeyword("ignore")) { RequireKeyword("defense"); stmt.RatioArg = ParsePercent(); }
                    return stmt;
                }
                else if (TryKeyword("magic"))
                {
                    var n = ParseInt(); RequireKeyword("damage"); RequireKeyword("to"); var ur = ParseUnitRef();
                    var stmt = new ActionStmt { Kind = ActionKind.DealMagicDamage, IntArg = n, Target = ur, RatioArg = 0.0 };
                    if (TryKeyword("from")) stmt.FromUnit = ParseUnitRef(); else stmt.FromUnit = new UnitRefCaster();
                    if (TryKeyword("ignore")) { RequireKeyword("resist"); stmt.RatioArg = ParsePercent(); }
                    return stmt;
                }
                else
                {
                    var n = ParseInt(); RequireKeyword("damage"); RequireKeyword("to"); var ur = ParseUnitRef();
                    return new ActionStmt { Kind = ActionKind.DealDamage, IntArg = n, Target = ur };
                }
            }
            // heal N to <unit>
            if (TryKeyword("heal"))
            {
                var n = ParseInt(); RequireKeyword("to"); var ur = ParseUnitRef();
                return new ActionStmt { Kind = ActionKind.Heal, IntArg = n, Target = ur };
            }
            // add tag "x" to <unit>
            if (TryKeyword("add"))
            {
                if (TryKeyword("tag"))
                {
                    var tag = ParseString(); RequireKeyword("to"); var ur = ParseUnitRef();
                    return new ActionStmt { Kind = ActionKind.AddUnitTag, StrArg = tag, Target = ur };
                }
                if (TryKeyword("global"))
                {
                    RequireKeyword("tag"); var tag = ParseString();
                    return new ActionStmt { Kind = ActionKind.AddGlobalTag, StrArg = tag, Target = new UnitRefCaster() };
                }
            }
            // remove tag "x" from <unit> | remove global tag "x"
            if (TryKeyword("remove"))
            {
                if (TryKeyword("tag"))
                {
                    var tag = ParseString(); RequireKeyword("from"); var ur = ParseUnitRef();
                    return new ActionStmt { Kind = ActionKind.RemoveUnitTag, StrArg = tag, Target = ur };
                }
                if (TryKeyword("global"))
                {
                    if (TryKeyword("tag"))
                    {
                        var tag = ParseString();
                        return new ActionStmt { Kind = ActionKind.RemoveGlobalTag, StrArg = tag, Target = new UnitRefCaster() };
                    }
                    if (TryKeyword("var"))
                    {
                        var key = ParseString();
                        return new ActionStmt { Kind = ActionKind.RemoveGlobalVar, KeyArg = key, Target = new UnitRefCaster() };
                    }
                }
                if (TryKeyword("unit"))
                {
                    RequireKeyword("var"); var key = ParseString(); RequireKeyword("from"); var ur = ParseUnitRef();
                    return new ActionStmt { Kind = ActionKind.RemoveUnitVar, KeyArg = key, Target = ur };
                }
                if (TryKeyword("tile"))
                {
                    RequireKeyword("var"); var key = ParseString(); RequireKeyword("at"); var pos = ParseCoord();
                    return new ActionStmt { Kind = ActionKind.RemoveTileVar, KeyArg = key, PosArg = pos, Target = new UnitRefCaster() };
                }
            }
            // move <unit> to (x,y)
            if (TryKeyword("move"))
            {
                var ur = ParseUnitRef(); RequireKeyword("to"); var pos = ParseCoord();
                return new ActionStmt { Kind = ActionKind.MoveTo, Target = ur, PosArg = pos };
            }
            // dash towards <unit> up to N
            if (TryKeyword("dash"))
            {
                RequireKeyword("towards"); var ur = ParseUnitRef(); RequireKeyword("up"); RequireKeyword("to"); var n = ParseInt();
                return new ActionStmt { Kind = ActionKind.DashTowards, Target = ur, IntArg = n };
            }
            // line physical N to <unit> length L [radius R]
            if (TryKeyword("line"))
            {
                if (TryKeyword("physical"))
                {
                    var p = ParseInt(); RequireKeyword("to"); var ur = ParseUnitRef(); RequireKeyword("length"); var L = ParseInt(); int R = 0; double ignore = 0.0;
                    if (TryKeyword("radius")) R = ParseInt();
                    if (TryKeyword("ignore")) { RequireKeyword("defense"); ignore = ParsePercent(); }
                    return new ActionStmt { Kind = ActionKind.LinePhysicalAoe, IntArg = p, Target = ur, IntArg2 = L, IntArg3 = R, RatioArg = ignore };
                }
                if (TryKeyword("magic"))
                {
                    var p = ParseInt(); RequireKeyword("to"); var ur = ParseUnitRef(); RequireKeyword("length"); var L = ParseInt(); int R = 0; double ignore = 0.0;
                    if (TryKeyword("radius")) R = ParseInt();
                    if (TryKeyword("ignore")) { RequireKeyword("resist"); ignore = ParsePercent(); }
                    return new ActionStmt { Kind = ActionKind.LineMagicAoe, IntArg = p, Target = ur, IntArg2 = L, IntArg3 = R, RatioArg = ignore };
                }
                // true damage
                var pt = ParseInt(); RequireKeyword("to"); var urt = ParseUnitRef(); RequireKeyword("length"); var Lt = ParseInt(); int Rt = 0;
                if (TryKeyword("radius")) Rt = ParseInt();
                return new ActionStmt { Kind = ActionKind.LineTrueAoe, IntArg = pt, Target = urt, IntArg2 = Lt, IntArg3 = Rt };
            }
            // consume mp = N  (implies caster)
            if (TryKeyword("consume"))
            {
                RequireKeyword("mp"); Expect('='); var n = ParseNumber();
                return new ActionStmt { Kind = ActionKind.ConsumeMp, NumArg = n, Target = new UnitRefCaster() };
            }
            // set global var "k" = value
            if (TryKeyword("set"))
            {
                if (TryKeyword("global"))
                {
                    RequireKeyword("var"); var key = ParseString(); Expect('='); var val = ParseValue();
                    return new ActionStmt { Kind = ActionKind.SetGlobalVar, KeyArg = key, ValueArg = val, Target = new UnitRefCaster() };
                }
                if (TryKeyword("tile"))
                {
                    var pos = ParseCoord(); RequireKeyword("var"); var key = ParseString(); Expect('='); var val = ParseValue();
                    return new ActionStmt { Kind = ActionKind.SetTileVar, KeyArg = key, ValueArg = val, PosArg = pos, Target = new UnitRefCaster() };
                }
                if (TryKeyword("unit"))
                {
                    var ur = ParseUnitRefInParensOptional(); RequireKeyword("var"); var key = ParseString(); Expect('='); var val = ParseValue();
                    return new ActionStmt { Kind = ActionKind.SetUnitVar, KeyArg = key, ValueArg = val, Target = ur };
                }
            }
            throw Error("unknown action");
        }

        private UnitRef ParseUnitRefInParensOptional()
        {
            SkipWs();
            if (Peek() == '(') { Expect('('); var ur = ParseUnitRef(); Expect(')'); return ur; }
            return ParseUnitRef();
        }

        private UnitRef ParseUnitRef()
        {
            if (TryKeyword("caster")) return new UnitRefCaster();
            if (TryKeyword("target")) return new UnitRefTarget();
            if (TryKeyword("it")) return new UnitRefIt();
            if (TryKeyword("unit"))
            {
                RequireKeyword("id"); var id = ParseString();
                return new UnitRefById { Id = id };
            }
            throw Error("unknown unit reference");
        }

        private Coord ParseCoord()
        {
            Expect('('); var x = ParseInt(); Expect(','); var y = ParseInt(); Expect(')');
            return new Coord(x, y);
        }

        private object ParseValue()
        {
            // support simple additive expressions: <primary> (('+'|'-') <primary>)*
            object left = ParsePrimaryValue();
            while (true)
            {
                SkipWs();
                if (TryConsume("+"))
                {
                    var right = ParsePrimaryValue();
                    left = new ArithExpr { Left = left, Right = right, Op = '+' };
                    continue;
                }
                if (TryConsume("-"))
                {
                    var right = ParsePrimaryValue();
                    left = new ArithExpr { Left = left, Right = right, Op = '-' };
                    continue;
                }
                break;
            }
            return left;
        }

        private object ParsePrimaryValue()
        {
            SkipWs();
            if (Match("true")) return true; if (Match("false")) return false;
            if (TryKeyword("var"))
            {
                var key = ParseString(); RequireKeyword("of"); var ur = ParseUnitRef();
                return new VarRef { Key = key, Unit = ur };
            }
            if (Peek() == '"') return ParseString();
            return ParseNumber();
        }

        private string ParseOp()
        {
            SkipWs();
            if (TryConsume(">=")) return ">=";
            if (TryConsume("<=")) return "<=";
            if (TryConsume("==")) return "==";
            if (TryConsume("!=")) return "!=";
            if (TryConsume(">")) return ">";
            if (TryConsume("<")) return "<";
            throw Error("operator expected");
        }

        private int ParseInt()
        {
            SkipWs();
            int start = _pos; bool neg = false;
            if (!Eof() && Peek() == '-') { neg = true; _pos++; }
            if (Eof() || !char.IsDigit(Peek())) throw Error("number expected");
            int val = 0; while (!Eof() && char.IsDigit(Peek())) { val = val * 10 + (Peek() - '0'); _pos++; }
            return neg ? -val : val;
        }

        private double ParseNumber()
        {
            SkipWs();
            var start = _pos;
            if (!Eof() && Peek() == '-') { _pos++; }
            if (Eof() || !char.IsDigit(Peek())) throw Error("number expected");
            while (!Eof() && char.IsDigit(Peek())) _pos++;
            if (!Eof() && Peek() == '.') { _pos++; while (!Eof() && char.IsDigit(Peek())) _pos++; }
            var slice = _src.Substring(start, _pos - start);
            if (!double.TryParse(slice, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
                throw Error("invalid number");
            return d;
        }

        private double ParsePercent()
        {
            var n = ParseInt(); SkipWs(); if (!Eof() && Peek() == '%') { _pos++; }
            return Math.Clamp(n, 0, 100) / 100.0;
        }

        private string ParseString()
        {
            SkipWs(); Expect('"'); var sb = new StringBuilder();
            while (!Eof())
            {
                var c = Next();
                if (c == '"') break;
                if (c == '\\' && !Eof())
                {
                    var n = Next();
                    sb.Append(n switch { '\\' => '\\', '"' => '"', 'n' => '\n', 't' => '\t', _ => n });
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // --------------- lex helpers ---------------
        private void SkipWs()
        {
            while (!Eof())
            {
                if (char.IsWhiteSpace(Peek())) { _pos++; continue; }
                if (Peek() == '#') { while (!Eof() && Peek() != '\n') _pos++; continue; }
                if (Peek() == '/' && _pos + 1 < _src.Length)
                {
                    var n = _src[_pos + 1];
                    if (n == '/') { _pos += 2; while (!Eof() && Peek() != '\n') _pos++; continue; }
                    if (n == '*')
                    {
                        _pos += 2;
                        while (!Eof())
                        {
                            if (Peek() == '*')
                            {
                                _pos++;
                                if (!Eof() && Peek() == '/') { _pos++; break; }
                            }
                            else { _pos++; }
                        }
                        continue;
                    }
                }
                break;
            }
        }

        private bool TryKeyword(string kw)
        {
            SkipWs();
            var save = _pos;
            if (TryConsume(kw))
            {
                if (Eof() || !char.IsLetterOrDigit(Peek())) return true;
            }
            _pos = save; return false;
        }

        private bool RequireKeyword(string kw)
        {
            if (!TryKeyword(kw)) throw Error($"keyword '{kw}' expected");
            return true;
        }

        private bool Match(string text)
        {
            var save = _pos; if (TryConsume(text)) { _pos = save; return true; }
            return false;
        }

        private bool TryConsume(string text)
        {
            SkipWs();
            if (_pos + text.Length > _src.Length) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.ToLowerInvariant(_src[_pos + i]) != char.ToLowerInvariant(text[i])) return false;
            }
            _pos += text.Length; return true;
        }

        private void Expect(char ch)
        {
            SkipWs(); if (Eof() || Peek() != ch) throw Error($"'{ch}' expected"); _pos++;
        }

        private char Peek() => _src[_pos];
        private char Next() => _src[_pos++];
        private bool Eof() => _pos >= _src.Length;

        private (int line, int col, string lineText) GetLineCol(int pos)
        {
            int line = 1, col = 1;
            int i = 0, lineStart = 0;
            while (i < _src.Length && i < pos)
            {
                var c = _src[i++];
                if (c == '\r') { /* skip */ continue; }
                if (c == '\n') { line++; col = 1; lineStart = i; }
                else { col++; }
            }
            // Extract current line text
            int j = lineStart;
            var sb = new StringBuilder();
            while (j < _src.Length && _src[j] != '\n' && _src[j] != '\r') { sb.Append(_src[j++]); }
            var lineText = sb.ToString();
            // Normalize tabs for caret alignment
            var displayLine = lineText.Replace('\t', ' ');
            return (line, Math.Max(col, 1), displayLine);
        }

        private Exception Error(string message)
        {
            var (line, col, lineText) = GetLineCol(_pos);
            // Guard against over-long lines for display
            const int maxLen = 160;
            int displayCol = col;
            if (lineText.Length > maxLen)
            {
                int start = Math.Max(0, Math.Min(lineText.Length - maxLen, col - 40));
                int end = Math.Min(lineText.Length, start + maxLen);
                if (start > 0) lineText = "…" + lineText.Substring(start, end - start);
                else lineText = lineText.Substring(start, end - start);
                if (start > 0) displayCol = Math.Max(2, col - start + 1);
            }
            var caret = new string(' ', Math.Max(0, displayCol - 1)) + '^';
            var msg = $"DSL parse error at line {line}, column {col}: {message}\n  {lineText}\n  {caret}";
            return new FormatException(msg);
        }
    }
}
