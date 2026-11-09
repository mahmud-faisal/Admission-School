# AI Log

Every bug or wrong assumption found by a subagent, a test, or the user, and how it was caught.
This file feeds `AI_USAGE.md`.

| Date | Checkpoint | What was wrong | How it was caught | Fix |
|------|------------|----------------|-------------------|-----|
| 2026-11-09 | 0 | `.claude/settings.md` holds JSON permission deny rules meant to protect the spec, but Claude Code only reads `.claude/settings.json`, so the rules are not active. | Manual inspection by the main agent during bootstrap. | Raised with the user; awaiting decision on renaming. |
| 2026-11-09 | 0 | Git on Windows (`core.autocrlf`) warned it would convert LF to CRLF. CRLF would break shell scripts run inside Linux containers (e.g. the nginx `env.js` entrypoint). | git warning on the first commit. | Added `.gitattributes` with `* text=auto eol=lf`. |
