# Agent instructions — FlaxMCP

**This file is the single source of truth for AI tools working on this repository.** It applies to Claude Code, Codex, Junie, and any other assistant.

| Document | Scope |
|---|---|
| [`CONVENTIONS.md`](CONVENTIONS.md) | Writing tasks/issues |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Branch/PR flow, commits, CI |
| `docs/plan-startu.md` | Full project plan, phase breakdown, spike findings — **not committed** (`.gitignore`), local working document only |
| [`README.md`](README.md) | Project pitch and usage — kept honest about current status, not aspirational |

---

## 1. Verify before you trust

This is an early-stage project. Two things follow from that:

- **Only `server_info` and `project_info` exist as working tools today.** Everything else described in `README.md`'s Roadmap or in a GitHub issue is planned, not implemented — check `src/FlaxMcp/Tools/` before assuming a tool exists.
- **`src/FlaxMcpBridge` (the live-editor plugin) is a verified spike, not production code.** It compiles, loads into a real `FlaxEditor.exe`, and answers `ping`/actor-listing correctly over its named pipe — but its unload path was never observed in a GUI session, and its protocol has no versioning yet. Don't extend it as if it were hardened; harden it first (see the `[Bridge]` issues on the project board).

The local Flax Engine source checkout (wherever `FLAX_ENGINE_PATH`/`Source/` points) contains the C++ engine headers but **not** the C# `FlaxEditor` source — the editor's C# tooling (`FlaxEditor.Modules`, `FlaxEditor.Windows`, etc.) isn't available to read locally. When you need to know an exact API shape there, two things actually work:

1. **Reflection over the real assembly** — load `FlaxEngine.CSharp.dll` (from `Binaries/Editor/Win64/<Config>/`) with `System.Reflection` and inspect it directly. This is how every API surface used in `src/FlaxMcpBridge` was confirmed, not guessed.
2. **The official docs and [`FlaxEngine/ExamplePlugin`](https://github.com/FlaxEngine/ExamplePlugin)** — for plugin project structure and lifecycle conventions specifically.

Don't guess an API shape from memory or general engine familiarity — verify it one of those two ways first.

## 2. Build and test

- Never ask the user to perform manual verification.
- Never perform manual verification or control the user's computer.
- Use automated tests and MCP tools only. If they cannot verify a requirement, report the limitation without asking for or attempting a manual check.

```bash
dotnet build FlaxMcp.slnx
dotnet test FlaxMcp.slnx
dotnet format --verify-no-changes
```

`src/FlaxMcpBridge` is a **Flax plugin project**, not a plain .NET SDK project — `dotnet build` on `FlaxMcp.slnx` doesn't touch it, and `dotnet build` on its Flax-generated `.csproj` (under `Cache/Projects/`) silently does nothing (its `CustomAfterMicrosoftCommonTargets` short-circuits the `Build` target). The only real compile check is launching the actual engine:

```bash
"<engine>/Binaries/Editor/Win64/Development/FlaxEditor.exe" -project "<some-project-referencing-the-plugin>" -headless
```

then reading that project's `Logs/*.txt` (UTF-16LE, no BOM — decode explicitly, e.g. `iconv -f UTF-16LE -t UTF-8` for a manual check). `-std` does **not** reliably redirect that log to a captured stdout when the process is launched detached — don't rely on it.

Two other things learned building the Spike S1 bridge and worth not re-discovering:

- Flax's game/editor scripting modules have a trimmed .NET reference set. Anything beyond `FlaxEngine.CSharp`/`Newtonsoft.Json` (e.g. `System.IO.Pipes`, `System.Text.Json`) needs an explicit `options.ScriptingAPI.SystemReferences.Add("...")` in the module's `*.Build.cs`, or compilation fails with `CS1069`/`CS0234`.
- A game project picks up a plugin's `EditorPlugin` automatically just by listing the plugin's `.flaxproj` in its own `References` array — no `PrivateDependencies`/`Modules.Add` needed on the consumer side. Confirmed, not assumed.

## 3. Layout

```
src/FlaxMcp/           the MCP server — stdio transport, tools, configuration
  Configuration/        options, validation, toolset registration
  Flax/                 domain: reading .flaxproj, engine auto-detection
  Tools/                [McpServerToolType] classes
src/FlaxMcpBridge/      Flax plugin project (own .flaxproj) — the live-editor bridge
  Source/FlaxMcpBridge/        runtime module (GamePlugin placeholder)
  Source/FlaxMcpBridgeEditor/  editor module — the actual EditorPlugin + pipe server
tests/FlaxMcp.Tests/    xunit.v3, mirrors src/FlaxMcp/ structure
```

Rider MCP tools are preferred over raw console commands for `.NET`/C# work when available; fall back to the console when they fail.

## 4. Code rules

- No new build warnings — `TreatWarningsAsErrors` in `Directory.Build.props` means this fails the build locally, not just CI.
- Guard expensive logging: `if (_logger.IsEnabled(LogLevel.X))`.
- Async tests using a `CancellationTokenSource` pass `TestContext.Current.CancellationToken`.
- Constructor injection with interfaces; `readonly` fields for injected dependencies.
- **Never use primary constructors for services with injected dependencies**, even if tooling suggests it or reports it as a warning.
- Don't add EF Core migrations, feature flags, or backwards-compatibility shims — none of that applies here, and if it ever does, ask first.
- Default to no comments. Add one only when the *why* is genuinely non-obvious (a hidden constraint, a workaround for a specific engine quirk).

**Never add self-attribution** to commits or PR bodies — no `Co-Authored-By: Claude` trailers, no "Generated with Claude Code", nothing identifying AI involvement.

## 5. Wiring new tools

A tool is done when it's **registered and reachable through a real MCP `tools/call`** — not when its class exists and has a green unit test. Verify with an actual stdio round-trip (`initialize` → `tools/list` → `tools/call`) against a real Flax project, the same way every tool in `src/FlaxMcp/Tools/` was checked before being committed.

- Register new tools in `Toolsets` under the right area (see `CONVENTIONS.md`'s `[System]` table) — a tool that exists but isn't in the registry isn't reachable.
- Respect `FLAX_READ_ONLY`: give write-capable tools `ReadOnly = false` (or omit it) on `[McpServerTool]` so the filter actually excludes them.
- A tool that depends on the live bridge (anything in the `editor` toolset) should fail with a clear "bridge not connected" error when no editor session is running, not throw an unhandled exception.

## 6. Scope discipline

- One task at a time; finish it before starting another.
- Write Polish documents in Polish, not translated from English (§7).
- Keep pull requests small: roughly ≤400 changed lines and ≤15 files.
- A request to create or open a pull request explicitly authorizes pushing the required branch to this repository's configured GitHub remote. Do not ask for separate push approval.
- Do not commit until the work has been reviewed and you get an explicit go-ahead.
- Ask when a requirement or expected behaviour is unclear rather than assuming.

## 7. Writing documents

The working plan (`docs/plan-startu.md`) is written in Polish and stays that way — it's an internal document, not committed. Code, comments, commits, pull requests and issues stay in English (`CONVENTIONS.md`).

A Polish document must read as if it were written in Polish, not translated from an English draft:

- **Never calque a term.** Name the thing by what it does, or keep the established English term and gloss it once, rather than inventing a literal translation no one else uses.
- **Expand every abbreviation on first use**, in parentheses — including ones that feel obvious in context: MCP, DI, CI, GUID.
- **Keep an English term untranslated when it's the name actually used in the field** (bridge, headless, named pipe, plugin). Gloss each one once.
- **One term, one meaning.** If a word is doing two jobs in the document, one of them needs a different word.

Test before committing a document: could someone who knows C# but has never read the English sources behind these ideas read each paragraph once and understand it?
