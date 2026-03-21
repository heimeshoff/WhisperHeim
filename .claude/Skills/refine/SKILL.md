# Skill: Refine

Refine backlog tasks through conversation and promote them to todo when ready.

## Invocation

```
/refine                 # List all backlog tasks and pick one
/refine [task-id]       # Jump straight to refining a specific task
/refine [keyword]       # Fuzzy-match against backlog task titles
```

## Instructions

You help the user take rough backlog tasks, sharpen them through interactive discussion, and promote them to the todo queue when they're ready to be worked on.

### Startup

1. Read `.workflow/vision.md` for product context.
2. Read `.workflow/roadmap.md` to understand milestones.
3. Scan `.workflow/tasks/backlog/` for all backlog task files.
4. If no backlog tasks exist, tell the user and suggest `/plan` or `/capture` instead.

### Task Selection

**If an argument was provided:**
- If it's a three-digit number (e.g., `007`), find the matching task ID in `backlog/`.
- Otherwise, fuzzy-match the argument against backlog task filenames and titles.
  - **Unique match** → select that task.
  - **Multiple matches** → list them and let the user pick.
  - **No match** → show all backlog tasks and let the user pick.

**If no argument was provided:**
- List all backlog tasks in a compact summary:

```
Backlog tasks:
  004 - Input system cleanup [Medium]
  005 - Message bus signal cleanup [Medium]
  ...
```

- Ask the user which one to refine.

### Refinement Phase

Once a task is selected:

1. **Read the full task file** and present a summary: title, objective, current details, acceptance criteria, dependencies, and size.

2. **Walk through refinement questions** (adapt to what's already filled in -- skip what's already clear):

   - **Scope clarity:** Is the objective precise enough? Does "done" have a single clear meaning?
   - **Details completeness:** Are the implementation steps specific enough for someone to start working immediately? Are exact file paths and line numbers still accurate?
   - **Acceptance criteria:** Are they verifiable? Can each one be checked off objectively?
   - **Size check:** Is this still the right size? Should it be split into smaller tasks?
   - **Dependencies:** Are there new dependencies based on what's changed since planning?
   - **Risk/unknowns:** Anything that needs research or investigation first?
   - **Priority vs. other backlog items:** Should this move ahead of or behind other tasks?

3. **If the task needs splitting**, help the user break it into smaller tasks:
   - Create new task files in `backlog/` with the next available IDs.
   - Update the original task to reference the new subtasks, or delete it if fully replaced.
   - Update `roadmap.md` to reflect the new task IDs.

4. **If the task needs research**, suggest using `/capture` in deep mode or reading existing research files.

5. **Update the task file** with any refinements:
   - Sharpen the objective, details, and acceptance criteria.
   - Update the size if the scope changed.
   - Add or update the Notes section with context from the discussion.
   - Append a refinement entry to the Work Log section:

```markdown
### YYYY-MM-DD -- Refined

**Changes:**
- [What was clarified, added, or changed]
```

### Promotion

When the user says the task is ready (or you've confirmed all sections are solid):

1. **Confirm with the user** that they want to promote to todo.

2. **Move the task file** from `backlog/` to `todo/`:
   ```
   mv .workflow/tasks/backlog/NNN-name.md .workflow/tasks/todo/NNN-name.md
   ```

3. **Log to `.workflow/protocol.md`** by prepending:

```markdown
## YYYY-MM-DD HH:MM -- Task Promoted: NNN - [Task Title]

**Type:** Task Promotion
**From:** backlog
**To:** todo
**Summary:** [1-2 sentence summary of what was refined and why it's ready]

---
```

Place the new entry right after the `---` on line 4 of `protocol.md`.

### Batch Promotion

If the user wants to promote multiple tasks at once without deep refinement:

1. List the backlog tasks.
2. Let the user select multiple tasks (by ID or keyword).
3. For each selected task, do a quick readiness check (objective clear? acceptance criteria present?).
4. Move all selected tasks from `backlog/` to `todo/`.
5. Log a single batch entry to `protocol.md`:

```markdown
## YYYY-MM-DD HH:MM -- Batch Promoted: [NNN, NNN, NNN]

**Type:** Batch Promotion
**Tasks:** NNN - [Title], NNN - [Title], NNN - [Title]
**Summary:** [Brief note on why these were promoted together]

---
```

### Important

- This command is about **sharpening existing tasks**, not creating new ones. If the user describes something new, suggest `/capture` or `/plan`.
- Keep refinement conversational -- don't turn it into a form-filling exercise.
- The goal is to make tasks immediately actionable for `/work`. A refined task should need zero clarification when picked up.
- If a task's audit references (file paths, line numbers) are stale because code has changed, update them during refinement.
- Do NOT execute any tasks -- that's what `/work` is for.
