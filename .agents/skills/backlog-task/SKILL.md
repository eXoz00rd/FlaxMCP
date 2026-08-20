---
name: backlog-task
description: Pick up and implement the next backlog task for FlaxMCP (repo eXoz00rd/FlaxMCP) — select an open issue, branch from main, implement it, validate it, and stop for review before committing. Use whenever the user says things like "pick up the next task", "what's next on the backlog", "let's do the next issue", or asks to work through backlog items one at a time, without repeating the full branch/commit/PR instructions each time.
---

# Backlog task workflow — FlaxMCP

Mirrored from `eXoz00rd/HEngine`'s `.Codex/skills/backlog-task/SKILL.md` and adapted to this
repo's board, branch, and build conventions. If the two ever meaningfully diverge in spirit
(not repo-specific IDs), prefer whichever is stricter about not committing/pushing without
explicit sign-off — that rule comes from the user's global instructions, not this skill.

## 0. Project board

Issues are tracked on the **FlaxMCP** GitHub Projects (v2) board, owner `eXoz00rd`, project
number `3` (project ID `PVT_kwHOBpFBus4BgtAv`). The `Status` field
(`PVTSSF_lAHOBpFBus4BgtAvzhfrjOY`) has these options:

| Status | Option ID |
|---|---|
| Todo | `f75ad846` |
| In Progress | `47fc9ee4` |
| Done | `98236657` |

Move the issue to **In Progress** once picked, before implementing. Get the item ID:

```bash
gh project item-list 3 --owner eXoz00rd --format json --limit 50 \
  | jq -r '.items[] | select(.content.number == <issue-number>) | .id'
```

Then set the status:

```bash
gh project item-edit --id <item-id> --project-id PVT_kwHOBpFBus4BgtAv \
  --field-id PVTSSF_lAHOBpFBus4BgtAvzhfrjOY --single-select-option-id <option-id>
```

`Done` is not set manually here — closing the issue (`Closes #<n>` in the eventual PR, or a
direct close) is what should move it, mirroring HEngine's default project workflow. Don't set
**In Progress → Done** by hand without the issue actually closing.

## 1. Check session state first

- If the current branch already has commits ahead of `main` for a task that looks finished,
  check whether its PR is merged (`gh pr list --head <branch> --state all`). A merged PR means
  the branch is stale — switch back to `main` rather than resuming it.
- Don't resume someone else's in-flight branch without checking `git log` / `gh pr list` first.

## 2. Select the next task

```bash
gh issue list --repo eXoz00rd/FlaxMCP --state open --limit 30 --json number,title,labels,createdAt
```

- Cross-check against `docs/plan-startu.md`'s phase breakdown (§5, "Fazy realizacji") when it
  exists locally — issues generally map to the next unfinished phase, and the phase text often
  states which sub-item comes first within it. `docs/` is gitignored, so it may not exist in
  every checkout; fall back to label/title triage if it's absent.
- Prefer `bug` label over `enhancement`/`architecture`-only issues when both are available —
  correctness fixes first.
- Read the full issue body (`gh issue view <number>`) before starting. `AGENTS.md` is the
  always-present entrypoint for repo conventions.
- Pick one task that fits in **≤400 changed lines / ≤15 files** (see `CONTRIBUTING.md`). If an
  issue is bigger than that, scope down to a coherent slice and say so, or ask the user before
  splitting.
- A `needs live editor check` label means the DoD can't be closed by a unit test alone — it
  needs a real (ideally GUI, not headless) Flax Editor session. Flag this to the user before
  driving a GUI session on their machine (see §4a).
- If several issues are similarly ranked and it's not obvious which to do, ask the user rather
  than guessing.
- Once picked, move the issue's board status to **In Progress** (see §0) before starting.

## 3. Branch

Per `CONTRIBUTING.md`: new branch from `main`, named `fix/...` or `feat/...` matching the change.

```bash
git checkout main
git pull --ff-only
git checkout -b fix/short-description
```

Never commit task work directly on `main` or leave it on a stale/merged branch from a previous
task.

## 4. Implement

- Read the relevant source before editing; confirm the actual bug/gap matches the issue
  description (don't assume the issue text is 100% precise).
- Follow `AGENTS.md` / `CONTRIBUTING.md`: no new comments unless the logic is genuinely
  non-obvious, no new build warnings, guard expensive logging
  (`if (_logger.IsEnabled(LogLevel.X))`), constructor injection with interfaces and `readonly`
  fields, **never primary constructors for services with injected dependencies**.
- `src/FlaxMcpBridge` is a Flax plugin project, not part of `FlaxMcp.slnx` — changes there can't
  be validated with `dotnet build`; see §4a and `AGENTS.md` §2.
- Add or update targeted tests in `tests/FlaxMcp.Tests/` (mirrors `src/FlaxMcp/` structure) for
  anything under `src/FlaxMcp`.

### 4a. Live-editor verification (when the issue needs it)

For issues touching `src/FlaxMcpBridge` or labeled `needs live editor check`, a unit test proves
nothing about GUI-only behavior (headless can't allocate a render target, unload/reload was only
ever exercised headless during Spike S1 — see `AGENTS.md` §1 and `docs/plan-startu.md`). Before
launching a real `FlaxEditor.exe` GUI session or driving it with computer-use automation, tell
the user what you're about to do and on which project (Mournfall is the integration pilot) —
this opens a real window on their desktop and may need manual steps (e.g. Ctrl+Alt+R for script
reload). Ask if they'd rather drive that step themselves and report back.

## 5. Validate

```bash
dotnet build FlaxMcp.slnx
dotnet test FlaxMcp.slnx
dotnet format --verify-no-changes
```

- No new build warnings — `TreatWarningsAsErrors` in `Directory.Build.props` fails the build
  locally on any, not just in CI.
- For `src/FlaxMcpBridge` changes, the only real compile check is a real engine launch (see
  `AGENTS.md` §2) — `dotnet build` on its generated `.csproj` silently does nothing.

## 6. Stop for review — do not commit yet

The user's global convention is explicit: **do not commit until the work has been reviewed and
you get an explicit go-ahead.** This applies here even though HEngine's mirrored skill commits
and opens a PR automatically at this point — don't carry that part over.

Report back: which issue, what changed, validation results (build/test/format, and how any
live-editor step was actually observed if applicable). Then wait.

## 7. Commit (only after explicit approval)

- One coherent commit (or a few, if the change is naturally staged), imperative mood summary
  line, short bullet body if needed.
- **Never add AI self-attribution** (no `Co-Authored-By: Codex` trailers, no "Generated with
  Codex" lines) — repo and user convention both forbid it.

## 8. Push and PR (only after a separate explicit signal to push/open a PR)

```bash
git push -u origin <branch>
```

PR body structure, per `CONTRIBUTING.md`:

```
## Summary
1-3 bullets: what changed and why.

## Test plan
Bulleted checklist of what was actually run/observed.
```

- Reference the issue with `Closes #<n>` when the PR fully resolves it.
- No self-attribution in the PR body either.
- Move the issue's board status to **In Progress → (stays)** — it flips to **Done** on its own
  once the linked issue closes; don't force it to Done manually before that.

## 9. Report back

Summarize in ≤100 words: which issue, what changed, validation results, PR link if one was
opened (the tool already surfaces the PR card — don't repeat the URL/number in text).
