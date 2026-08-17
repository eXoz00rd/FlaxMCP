# FlaxMCP

MCP server for [Flax Engine](https://flaxengine.com) projects.

Status: early scaffold (Phase 0). See `docs/plan-startu.md` (not tracked in git) for the full
project plan.

## Building

```bash
dotnet build
dotnet test
```

## Running

```bash
dotnet run --project src/FlaxMcp
```

Configuration is provided via environment variables prefixed with `FLAX_`, most importantly
`FLAX_PROJECT_PATH` pointing at a `.flaxproj` file or its containing directory.
