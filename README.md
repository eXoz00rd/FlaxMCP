# FlaxMCP

[![CI](https://github.com/eXoz00rd/FlaxMCP/actions/workflows/ci.yml/badge.svg)](https://github.com/eXoz00rd/FlaxMCP/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

MCP (Model Context Protocol) server for **[Flax Engine](https://flaxengine.com)** projects, built
with **.NET 10 / C#** on top of the official [ModelContextProtocol C# SDK](https://www.nuget.org/packages/ModelContextProtocol).

> **Status:** early development — not yet published. Run from source (see below).

## Why

Flax projects mix a JSON-based project/content format with a native C++/C# engine and editor.
FlaxMCP reads that project directly — `.flaxproj`, content, scenes, build output, logs — and, once
an `EditorPlugin` companion is installed, talks to a *running* editor session over a local named
pipe: live scene state, selection, and viewport screenshots. It deliberately leaves C# code
navigation and refactoring to a dedicated IDE-integrated MCP (e.g. Rider), and focuses on what
those tools can't see: Flax project structure, content, scenes, engine builds, and the live editor.

## Tools

2 tools across 2 areas (more land as later phases of the project ship):

| Area | Tools |
|---|---|
| Server | `server_info` |
| Project | `project_info` |

`server_info` reports the server name and version. `project_info` reads the `.flaxproj` file
directly: name, version, build targets, referenced projects (including the engine and any
plugins), and the default scene.

### Trimming the tool list

- **`FLAX_TOOLSETS=project`** exposes only the areas a client needs; `server_info` is always
  available regardless of this setting.
- **`FLAX_READ_ONLY=true`** removes every write tool. Every tool in the current tool set is
  already read-only, so this has no visible effect yet — it starts mattering once build/editor
  tools (which write) land.

An unknown toolset name fails at startup with the list of valid names instead of silently
exposing the wrong tools.

## Requirements

- .NET 10 SDK
- [Flax Engine](https://flaxengine.com) installed locally
- A Flax project (`.flaxproj`) to point the server at

## Setting up from scratch on a new machine

### 1. Install prerequisites

- **.NET 10 SDK** — `winget install Microsoft.DotNet.SDK.10` on Windows, or download from
  [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0); verify with
  `dotnet --list-sdks`
- **Flax Engine** — installed via the [Flax Launcher](https://flaxengine.com/download/)

### 2. Get the server

```bash
git clone https://github.com/eXoz00rd/FlaxMCP.git
cd FlaxMCP
dotnet build FlaxMcp.slnx
dotnet test FlaxMcp.slnx
```

### 3. Configure your MCP client

Use one of the configurations from [Usage](#usage) below, pointing `FLAX_PROJECT_PATH` at your
Flax project.

### 4. Verify

Ask the agent to call `server_info`, or "show me the project info". The server refuses to start
when `FLAX_PROJECT_PATH` doesn't resolve to a `.flaxproj` file, or when the Flax Engine install
(or its `FlaxEditor.exe`) can't be found, and logs the exact reason to stderr — so a misconfigured
client fails fast with a clear message.

## Usage

Not yet published, so run from source. Example MCP client configuration (Claude Code, VS Code,
etc.):

```json
{
  "mcpServers": {
    "flax": {
      "command": "dotnet",
      "args": ["run", "--project", "D:/Projects/FlaxMCP/src/FlaxMcp"],
      "env": {
        "FLAX_PROJECT_PATH": "D:/Projects/YourFlaxProject"
      }
    }
  }
}
```

### Configuration

| Variable | Required | Description |
|---|---|---|
| `FLAX_PROJECT_PATH` | yes | Path to a `.flaxproj` file, or a directory containing exactly one |
| `FLAX_ENGINE_PATH` | no | Flax Engine install directory. Auto-detected via the Flax Launcher's `Versions.txt` when omitted |
| `FLAX_EDITOR_CONFIG` | no | Editor build to use: `Development` (default), `Debug`, or `Release` |
| `FLAX_TOOLSETS` | no | Comma-separated toolsets to expose: `project`. `server` is always available |
| `FLAX_READ_ONLY` | no | Set to `true` to expose only read-only tools |
| `FLAX_BRIDGE` | no | Reserved for the live-editor bridge (`auto` / `off`); not yet wired up |
| `FLAX_ALLOW_CODE_EXECUTION` | no | Reserved for an arbitrary-C#-execution tool, off by default; not yet implemented |
| `FLAX_LOG_LEVEL` | no | Minimum level of logs written to stderr (defaults to `Warning`) |

## Security

- No credentials are involved today — the server only reads local project files and, once the
  bridge lands, talks to a local named pipe
- `FLAX_READ_ONLY=true` restricts the server to read-only tools
- The planned arbitrary-C#-execution tool (`FLAX_ALLOW_CODE_EXECUTION`) will be off by default and
  blocked by `FLAX_READ_ONLY`, because it amounts to full control of the machine running the editor

## Building from source

```bash
git clone https://github.com/eXoz00rd/FlaxMCP.git
cd FlaxMCP
dotnet build FlaxMcp.slnx
dotnet test FlaxMcp.slnx
dotnet format --verify-no-changes
```

CI runs the same steps on every push and pull request.

## Roadmap

FlaxMCP is being built in phases: project/content/scene introspection from files, engine build and
log tooling, then a live editor bridge (an `EditorPlugin` talking over a local named pipe) for
scene graph, selection, screenshots, and play mode. See the commit history and open pull requests
for current progress.

## License

[MIT](LICENSE)
