# Skill: Fix

Ad-hoc bug fix or quick change -- no task file needed. Just do it, commit it, log it.

## Invocation

```
/fix [description of what to fix]
/fix
```

## Instructions

You are the quick-fix executor. You handle ad-hoc work that doesn't need the full task ceremony -- small bug fixes, UI tweaks, one-liner refinements, minor adjustments.

### 1. Understand the Request

- **With argument:** The user provided a description of what to fix. Proceed directly.
- **Without argument:** Ask the user what needs fixing, then proceed.

### 2. Read and Understand

Read enough code to understand the fix. No task file, no milestone lookup, no dependency check. Just understand the problem and the relevant code.

### 3. Execute the Fix

- Make the change -- write code, fix the bug, adjust the UI, whatever is needed.
- Follow existing code patterns and project conventions.
- Run tests if applicable.
- Keep it focused. This is a quick fix, not a refactor.

### 4. Confirm with User

Briefly summarize what was changed:
- Which files were modified
- What the change does
- Any side effects or things to watch out for

Ask the user to confirm before committing.

### 5. Commit

Auto-commit all changes:
```
git add -A && git commit -m "Fix: [short description]"
```

The commit message format is: `Fix: [short description]` followed by a newline and `Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>`.

### 6. Log to Protocol

Prepend an entry to `.workflow/protocol.md`:

```markdown
## YYYY-MM-DD HH:MM -- Quick Fix: [Short description]

**Type:** Quick Fix
**Summary:** [What was changed and why]
**Files changed:** [list of files]

---
```

### Important

- **No task file.** This is the whole point of `/fix` -- skip the task ceremony for small, immediate work.
- **No milestone, no backlog, no dependencies.** Just do it.
- **Always commit.** Even small fixes should be tracked in git.
- **Always log to protocol.** The protocol is the project's memory. Every change matters.
- **Keep it small.** If the fix turns out to be bigger than expected, suggest using `/capture` or `/plan` instead.
- **Confirm before committing.** The user should see what changed before it's committed.
