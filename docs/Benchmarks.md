# Benchmarks

Project: `ETBBS.Benchmarks` (BenchmarkDotNet)

Benchmarks included:

- `LbrParseBenchmarks.ParseRole()` — Parse a `.lbr` role
- `DslBenchmarks.CompileSkill()` — Compile a simple DSL script
- `RuntimeBenchmarks.ApplyPhysical()` — Apply a physical damage atomic action

## Run

```bash
dotnet run -c Release --project ETBBS.Benchmarks
```

BenchmarkDotNet generates reports in the `BenchmarkDotNet.Artifacts` folder.

> Requires NuGet restore for `BenchmarkDotNet`.

