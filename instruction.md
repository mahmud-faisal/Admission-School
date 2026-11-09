# Execution Instructions — Checkpointed Build

These instructions drive the whole project. Read `CLAUDE.md` and
`School_Admission_System_Assignment_EN.md` first. The spec file is READ-ONLY. Never modify it,
or this file (`instruction.md`).

## How to run these instructions

1. Work through Checkpoints 0–8 **in order, one at a time**.
2. Within a checkpoint, do all its tasks without stopping (unless a rule below says to stop).
3. At the end of each checkpoint:
   a. Make sure everything builds and the checkpoint's tests pass.
   b. Commit (Conventional Commit messages; several small commits per checkpoint is good).
   c. Update `docs/PROGRESS.md` (format below) and commit it.
   d. Print the **Checkpoint Report** (format below).
   e. **STOP and ask for confirmation.** Use the AskUserQuestion tool if available, with options:
      - "Continue to Checkpoint N+1"
      - "Make changes first" (then wait for my change requests, apply them, re-report, ask again)
      - "Stop here"
      If the tool isn't available, ask in plain text and wait.
4. **Never start the next checkpoint without my explicit confirmation.** Silence is not confirmation.
5. Stop early and ask me (mid-checkpoint) only if: a decision is irreversible, the spec and my
   preferences conflict, or you are blocked after two genuine attempts. Otherwise decide, record it
   in `ASSUMPTIONS.md`, and continue.

### Resuming
At the start of any session (or after `/clear`), read `docs/PROGRESS.md` and `git log --oneline -20`.
Continue from the first checkpoint not marked DONE. If a checkpoint is IN PROGRESS, inspect the repo
to see what is finished, then complete only the rest. Do not redo finished work.

### docs/PROGRESS.md format
```
| # | Checkpoint            | Status       | Last commit | Notes |
|---|-----------------------|--------------|-------------|-------|
| 0 | Bootstrap             | DONE         | a1b2c3d     | ...   |
| 1 | Design & plan         | IN PROGRESS  |             | ...   |
...
```
Statuses: NOT STARTED / IN PROGRESS / AWAITING CONFIRMATION / DONE.
Mark a checkpoint DONE only after I confirm.

### Checkpoint Report format
```
## ✅ Checkpoint N — <name> complete

**Done**
- <what was built/changed, 3–8 bullets>

**Verification**
- Build: PASS/FAIL · Tests: X passed / Y failed · Subagent results (if any)

**Commits**
- <hash> <message> (all commits from this checkpoint)

**Decisions & assumptions added**
- <new entries in ASSUMPTIONS.md, or "none">

**Known issues / risks**
- <anything incomplete or worth my attention, or "none">

**What I'd like you to check**
- <1–3 concrete things for me to look at, with file paths or URLs>

**Next:** Checkpoint N+1 — <name>: <one-line summary>
```

---

## Checkpoint 0 — Bootstrap
- If not a git repo, `git init`. Add `.gitignore` for .NET, Node, IDE and OS files.
- First commit: the spec, `CLAUDE.md`, `instruction.md`, `.claude/`.
- Create `docs/PROGRESS.md` and `docs/AI_LOG.md`.
- Report which tools exist locally (`docker --version`, `docker compose version`,
  `dotnet --version`, `node --version`). If .NET 10 SDK or Node is missing, say that you will build
  inside Docker.
- No application code in this checkpoint.

## Checkpoint 1 — System design & plan
- Use the **system-architect** subagent to produce `docs/DESIGN.md`, `docs/PLAN.md` and a draft
  `ASSUMPTIONS.md`.
- In the report, include the architect's summary, the concurrency approach in 3–5 lines, the API
  versioning approach, and **all open questions** for me.
- When I answer, update the design docs and commit before asking to continue.

## Checkpoint 2 — Backend core
- Implement Domain, Application (MediatR commands/queries, FluentValidation validators, pipeline
  behaviors) and Infrastructure (EF Core + Npgsql, repositories, Unit of Work, migrations,
  hold-expiry BackgroundService) exactly as in `docs/DESIGN.md`.
- Follow the concurrency design precisely (DB-level locking/atomic SQL, DB clock).
- Build after each layer; commit after each layer.

## Checkpoint 3 — API
- Api project: all 8 endpoints at the exact spec paths; API versioning that works with no version
  parameter; ProblemDetails errors with a `code` field; X-Student-Id validation (17 digits); CORS;
  Swagger UI at `/swagger` and Scalar at `/scalar`, enabled in all environments; migrations on startup
  with retry.
- Run the **api-contract-auditor** subagent; fix every mismatch; re-run until clean.
- Report must include the audit result.

## Checkpoint 4 — Backend tests
- Unit tests for handlers and domain rules.
- Integration tests against real PostgreSQL:
  (a) concurrency test — many parallel hold requests, exactly `totalSeats` succeed, DB state verified;
  (b) idempotent confirm; (c) expiry releases seats without an API call; (d) duplicate application
  rejected; (e) bonus: two app instances on the same DB never oversell.
- Tests runnable with only Docker installed (compose `test` profile).
- Run all tests, fix failures. Report test names and results.

## Checkpoint 5 — Frontend
- React + TypeScript + Vite per `docs/DESIGN.md`: student ID field (17-digit validation, saved in
  localStorage); Create class; Class list with live seat counts (polling) and Apply; Application page
  with countdown, EXPIRED without reload, disabled Pay button, Cancel; My applications.
- Clear user messages for every error (seats full, expired, invalid input, server down); error boundary.
- API base URL from env at build time plus runtime override via `/env.js`.
- Vitest + React Testing Library tests for key flows. Small commits.

## Checkpoint 6 — Docker
- `backend/Dockerfile`, `frontend/Dockerfile` (nginx), root `docker-compose.yml`: PostgreSQL,
  backend on 8080, frontend; healthchecks; named volume; `test` profile; second-backend profile
  for the bonus.
- Verify from clean: `docker compose down -v && docker compose up --build`. Verify persistence with
  `down` (no `-v`) then `up`.
- Report the URLs: frontend, API, `/swagger`, `/scalar`.

## Checkpoint 7 — Verification loop
- Run **functional-verifier** and **api-contract-auditor** subagents in parallel.
- Fix every FAIL/mismatch (one commit per fix), re-run both. Repeat until all pass, or until a
  remaining item needs my decision — then report and ask.
- Report must include the final pass/fail table summary and what was fixed.

## Checkpoint 8 — Review & documentation
- Run the **code-reviewer** subagent; fix all Blocker and Should-fix items.
- Write `README.md` (run with Docker, frontend URL, Swagger/Scalar URLs, how to run backend and
  frontend tests, how to run the two-instance bonus).
- Finalize `ASSUMPTIONS.md` (short; include the multi-process explanation).
- Draft `AI_USAGE.md` from `docs/AI_LOG.md`, marked as a draft for me to personalize.
- Run **functional-verifier** one final time.
- Confirm the spec is unchanged: `git diff --stat <first-commit> HEAD -- School_Admission_System_Assignment_EN.md`
  must print nothing.
- Final report: the full deliverables checklist from spec §1.7 with ✅/❌ for each item.