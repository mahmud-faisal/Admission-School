# AI usage

> **DRAFT. Please personalize before submitting.** Claude Code wrote this from `docs/AI_LOG.md`. Add your own
> view: what you reviewed yourself, what you changed, and anything you'd do differently. The sections marked
> _[you]_ are yours to fill in.

## Tools used

- **Claude Code** (Anthropic Claude Opus 5.5) in the terminal, driven by `instruction.md` in checkpoints that each
  ended with a report and my confirmation before continuing.
- Specialized Claude Code subagents, defined in `.claude/agents/`:
  - `system-architect`: wrote `docs/DESIGN.md`, `docs/PLAN.md` and the first `ASSUMPTIONS.md`.
  - `api-contract-auditor`: compared the live API with spec §1.4, field by field (checkpoints 3 and 7).
  - `functional-verifier`: ran the whole system with Docker and checked every rule of the spec (checkpoints 7 and 8).
  - `code-reviewer`: architecture and code-quality review before submission (checkpoint 8).

## What the AI did

| Part | AI role | My role  |
|---|---|---|
| Design (Clean Architecture, schema, concurrency design) | Drafted by the architect subagent; open questions were put to me |The fundamental Design instructin was given. Over that, the Desing was created by the AI. Additionally, During desing I have to Answered the 11 open questions (e.g. a CONFIRMED hold can't be cancelled; another student's hold → 403) |
| Backend (Domain, Application, Infrastructure, Api) | Written by Claude Code | Instructed follow the Desing prepared at phase-1, Reviewed code and Commit|
| Tests (unit, integration, concurrency, two-process) | Written by Claude Code | Reviewed The test code |
| Frontend (React + TS) | Written by Claude Code | I found a scope to enhance a feature. I added the feature and maintained that from Backend and Frontend |
| Docker / compose | Written by Claude Code | Freed port 3000 on my machine for verification |
| Docs (README, ASSUMPTIONS, this file) | Drafted by Claude Code | Checked those files and added necessary informations  |

## Where the AI made mistakes, and how they were caught

The full list is in `docs/AI_LOG.md`. The most important ones:

1. **Design that would not compile.** The architect put `HoldSnapshot` in the Application layer, but the Domain
   rules use it, and Domain may not reference Application. *Caught* while implementing; moved to Domain.
2. **Design that would fail at runtime.** The seat-count SQL used `(@classId IS NULL OR ...)`, but Npgsql cannot
   type an untyped NULL parameter. *Caught* in review before running; split into two queries.
3. **Clock rule violation in the design.** `Class.Create(..., createdAtUtc)` would have needed the app server's
   clock. *Caught* in implementation; `created_at` now uses the database default `now()`.
4. **Wrong error responses.** Errors were sent as `application/json` instead of `application/problem+json`, the
   header error key was rewritten to `x-Student-Id`, an unsupported API version returned the library's own code,
   and raw JSON parser messages reached the user. *Caught* by a curl smoke test of every error case.
5. **OpenAPI did not match the wire format** (M1-M6: numbers accepted as strings, missing `code` in error
   schemas, a required header shown as optional, ...). *Caught* by the `api-contract-auditor` subagent, then fixed
   and re-audited to 47/0.
6. **Database connection exhaustion under load.** The parallel tests hit PostgreSQL's 100-connection limit
   (`53300 too many clients`) and 500s appeared. The same could happen with two backend processes in production.
   *Caught* by the integration tests; fixed with `max_connections=500` and smaller test pools.
7. **Tests that could not fail or were racy.** An assertion that only counted requests sent, and a sweeper test
   that a third sweeper could have raced. *Caught* by re-reading tests after they passed.
8. **CRLF line endings in shell scripts.** On Windows they would have broken the nginx entrypoint inside the
   container, and the first fix silently did nothing. *Caught* by an explicit line-ending check before the
   Docker build.
9. **Licensing traps.** The newest MediatR (14.x) and FluentAssertions (8.x) need commercial licences. *Caught*
   during the version lookup; pinned MediatR 12.5.0 and FluentAssertions 7.2.2.
10. **A stale Docker test image.** `docker compose run` reuses an existing image, so after code changes the Docker
   test run silently ran the *old* tests (74 instead of 75). *Caught* by the final functional-verifier run, which
   reported a mismatch with the README count, and by comparing the test names. Fixed by adding `--build` to the
   README test commands.

## How I checked the AI's work _[you]_

- Each checkpoint stopped for my review: report, commits, and things to check.
- Independent subagents (auditor, verifier, reviewer) checked the work against the spec, not against the AI's
  own design.
- Checkpoint 7: functional-verifier 41 PASS / 0 FAIL and api-contract-auditor 58 PASS / 0 FAIL.
- Checkpoint 8: code review found 0 blockers and 4 should-fix items, all fixed. The most important was S1: a hold could
  last up to 1 s less than HOLD_DURATION_SECONDS. The final functional-verifier run found one more problem: the
  Docker test image was stale (see mistake 10). After the fix, all 143 backend tests (68 unit + 75 integration) pass and all spec rules hold
  from a clean Docker build.
- _[you: what you checked by hand, e.g. clicking through the UI or reading the concurrency SQL]_
