# UOMapVibe Git Workflow

Standard git conventions for the UOMapVibe repository. Follow these rules for all commits and pull requests.

---

## Branch Naming

```
{type}/{YYYY-MM-DD}-{short-description}
```

**Types:**
| Type | Use When |
|------|----------|
| `feature` | Adding new functionality |
| `fix` | Fixing a bug |
| `chore` | Build config, dependencies, CI, tooling |
| `docs` | Documentation only |
| `hotfix` | Urgent production fix |

**Examples:**
```
feature/2026-03-09-terrain-writer
fix/2026-03-09-statics-seek-offset
chore/2026-03-09-update-dotnet-sdk
docs/2026-03-09-api-endpoint-docs
```

---

## Commit Messages

```
{type}({scope}): {description}
```

**Scopes:**
| Scope | Covers |
|-------|--------|
| `core` | UOMapVibe.Core — MUL readers/writers, models |
| `analysis` | Style analysis, classifiers, orientation detection, building metrics |
| `api` | UOMapVibe.Api — HTTP endpoints, Program.cs |
| `web` | Web app — HTML, CSS, JavaScript |
| `exporter` | TileExporter — tile generation, radar colors |
| `tests` | Test projects |
| `rollback` | Snapshot/rollback system |
| `ci` | GitHub Actions, CI/CD |

**Types (same as branches):** `feat`, `fix`, `chore`, `docs`, `hotfix`, `refactor`, `test`

**Examples:**
```
feat(core): add MapWriter for terrain editing
fix(analysis): correct wall orientation detection using neighbor adjacency
feat(api): add terrain_grid to prepare endpoint response
feat(web): add annotation label editing
chore(ci): add dotnet build + test workflow
docs(api): document execute command format
test(core): add StaticsWriter round-trip tests
refactor(analysis): split StyleAnalyzer into separate components
```

**Rules:**
- First line under 72 characters
- Use imperative mood ("add", "fix", "update" — not "added", "fixes", "updated")
- Body is optional — use it for context on WHY, not WHAT (the diff shows what)
- Reference issue numbers when applicable: `fix(core): handle empty statics block (#12)`

---

## Safety Rules

These rules prevent data loss and keep the repository clean.

### Never Do

1. **Never force-push to `main`** — this rewrites shared history and can destroy work
2. **Never use `git add -A` or `git add .`** — this can accidentally commit MUL files, snapshots, or secrets
3. **Never skip hooks** — no `--no-verify`, no `--no-gpg-sign`
4. **Never amend a commit after a hook failure** — the failed commit didn't happen, so `--amend` modifies the PREVIOUS commit. Create a new commit instead.
5. **Never commit MUL files** (`.mul`, `.uop`, `.idx`) — they are binary game data, not source code
6. **Never commit the `snapshots/` directory** — these are runtime edit backups
7. **Never commit `web/tiles/` or `web/data/tile_catalog.json`** — these are generated from MUL files

### Always Do

1. **Stage specific files by name** — `git add src/UOMapVibe.Core/MulFiles/StaticsWriter.cs`
2. **Review staged changes before committing** — `git diff --cached`
3. **Create new commits after hook failures** — fix the issue, re-stage, commit fresh
4. **Pull before pushing** — `git pull --rebase` to keep history clean
5. **Create a branch for non-trivial changes** — don't commit directly to `main` for features

---

## Pull Request Flow

### Creating a PR

1. Create a branch following the naming convention
2. Make your changes with properly formatted commits
3. Push the branch: `git push -u origin {branch-name}`
4. Create the PR with a clear title and description:

```bash
gh pr create --title "feat(analysis): improve road detection accuracy" --body "$(cat <<'EOF'
## Summary
- Rewrote road detection to use connected-component analysis
- Reduced false positives from scattered floor tiles
- Added edge/border tile detection for road boundaries

## Test plan
- [ ] Run style analysis on Britain area — verify road materials detected
- [ ] Run style analysis on Yew area — verify forest paths not flagged as roads
- [ ] Run full test suite: `dotnet test`
EOF
)"
```

### PR Title Format

Same as commit messages: `{type}({scope}): {description}`

Keep titles under 70 characters. Use the PR body for details.

### Before Merging

- All tests pass: `dotnet test UOMapVibe.slnx`
- Build succeeds: `dotnet build UOMapVibe.slnx`
- Changes reviewed (self-review at minimum)
- No MUL files, snapshots, or generated tiles in the diff

---

## Quick Reference

```bash
# Start a new feature
git checkout main && git pull
git checkout -b feature/2026-03-09-my-feature

# Stage specific files and commit
git add src/UOMapVibe.Core/SomeFile.cs
git commit -m "feat(core): add some feature"

# Push and create PR
git push -u origin feature/2026-03-09-my-feature
gh pr create --title "feat(core): add some feature" --body "Description here"

# After PR is merged
git checkout main && git pull
git branch -d feature/2026-03-09-my-feature
```
