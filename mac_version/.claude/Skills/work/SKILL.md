# Skill: Work

Execute tasks from the todo queue. Supports parallel execution of independent tasks via subagents.

## Invocation

```
/work                   # Parallel mode (default) -- analyzes dependency graph, runs independent tasks concurrently
/work [task-id]         # Sequential mode -- execute a specific task only
/work --sequential      # Sequential mode -- process all tasks one at a time (legacy behavior)
```

## Instructions

You are the orchestrator. You coordinate task execution but **never do coding work yourself**. In all modes, you delegate the actual work to subagents and stay lean -- your job is to move files, log to protocol, commit, and dispatch. This keeps your context window small and prevents exhaustion.

---

## Parallel Mode (Default)

### Phase 1: Dependency Graph Analysis

1. **Scan `todo/`** -- Read all task files and extract their `Dependencies` fields.
2. **Scan `done/`** -- List all completed task IDs.
3. **Build the unblocked set** -- A task is unblocked if ALL of its dependencies are in `done/` (or it has no dependencies).
4. **If no tasks are unblocked** and `todo/` is non-empty, report that all remaining tasks are blocked and list the blocking dependencies. Stop.
5. **If `todo/` is empty**, provide a completion summary (see "Completion Summary" below).

### Phase 2: Conflict Detection

Before dispatching a batch, reduce conflicts between concurrent tasks:

1. **Scan task Details sections** for file paths, directory references, and shared resources.
2. **If two unblocked tasks reference the same file or directory**, demote the higher-numbered task to the next batch.
3. **Cap the batch at MAX_PARALLEL = 3** tasks. Select the lowest-numbered unblocked, non-conflicting tasks.

### Phase 3: Recovery Check

Before dispatching new work, check `in-progress/`:

- **If 0 tasks in `in-progress/`**: proceed normally.
- **If 1 task in `in-progress/`**: resume it as a single sequential task before starting parallel dispatch.
- **If 2+ tasks in `in-progress/`**: a previous parallel session was interrupted. Ask the user:
  - **"Resume all in parallel"** -- re-dispatch each as a subagent.
  - **"Resume one at a time"** -- process them sequentially.

### Phase 4: Batch Dispatch

For each batch of unblocked tasks:

1. **Move all batch task files** from `todo/` to `in-progress/` (the orchestrator does this before spawning subagents).

2. **Log "Batch Started"** to `.workflow/protocol.md` by prepending:

```markdown
## YYYY-MM-DD HH:MM -- Batch Started: [NNN, NNN, NNN]

**Type:** Batch Start
**Tasks:** NNN - [Title], NNN - [Title], NNN - [Title]
**Mode:** Parallel (batch of N)

---
```

3. **Spawn one subagent per task** using the Task tool. Launch all subagents in a single message (parallel tool calls). Each subagent receives the prompt defined in "Subagent Prompt Template" below.

4. **Wait for all subagents to complete.** As each subagent finishes:

   a. **Use the subagent's return message** (which contains a concise summary) for the protocol log. Do NOT re-read the task file -- trust the subagent's report.
   b. **Log "Task Completed"** to `.workflow/protocol.md` by prepending (the orchestrator does this, NOT the subagent):

   ```markdown
   ## YYYY-MM-DD HH:MM -- Task Completed: NNN - [Task Title]

   **Type:** Task Completion
   **Task:** NNN - [Title]
   **Summary:** [1-2 sentence summary extracted from subagent's work log]
   **Files changed:** [count] files

   ---
   ```

   c. **Auto-commit** the completed task's changes:
   ```
   git add -A && git commit -m "Task NNN: [task title]"
   ```

   d. If a subagent **failed**, leave the task in `in-progress/`, log the failure to `protocol.md`, and continue with other subagents. Do NOT let one failure block the batch.

5. **After the entire batch completes**, return to Phase 1 -- re-scan `todo/` and `done/` to find the next set of unblocked tasks.

### Subagent Prompt Template

Each subagent is spawned with the Task tool using `subagent_type: "general-purpose"`. The prompt MUST include:

```
You are a task executor for the Earthdawn Companion project.

## Your Task
Read the task file at: [absolute path to the task file in in-progress/]
If the task references research files, read those too from `.workflow/research/`.

## Project Context
- Working directory: [current working directory]
- This is part of a parallel batch -- other tasks are running concurrently.

## Rules -- CRITICAL
1. **Do NOT run `git add`, `git commit`, or any git commands.** The orchestrator handles all git operations.
2. **Do NOT modify `.workflow/protocol.md`.** The orchestrator handles all protocol logging.
3. **Do NOT modify any `.workflow/tasks/` files other than YOUR assigned task file.**
4. **DO** write code, create files, modify configurations -- whatever the task requires.
5. **DO** follow existing code patterns and project conventions.
6. **DO** run tests if applicable.
7. **DO** append a Work Log entry to the task file:

### YYYY-MM-DD HH:MM -- Work Completed

**What was done:**
- [Action 1]
- [Action 2]

**Acceptance criteria status:**
- [x] Criterion 1 -- [how it was verified]
- [x] Criterion 2 -- [how it was verified]

**Files changed:**
- [file1.ext] -- [what changed]
- [file2.ext] -- [what changed]

8. **DO** move the task file from `in-progress/` to `done/` when all acceptance criteria are met.
9. **If the task fails**, leave it in `in-progress/`, note the failure in the work log, and return a clear error message.

## Context Hygiene -- IMPORTANT
To avoid running out of context window, follow these rules:
- **Read only what you need.** Use targeted reads (offset/limit) for large files. Don't read entire files if you only need a few lines.
- **Don't echo file contents back.** After reading a file, work with it -- don't repeat it in your output.
- **Keep tool output concise.** When running commands, limit output (e.g., use head/tail flags, --quiet modes).
- **Don't re-read files you've already read** unless they've changed.

## Return Format -- IMPORTANT
When you are done, return ONLY a concise summary in this exact format (nothing else):

RESULT: [SUCCESS or FAILED]
SUMMARY: [1-2 sentences of what was done]
FILES_CHANGED: [count]
FILE_LIST: [comma-separated list of changed file paths]

Do NOT return the full work log, file contents, or verbose descriptions.
```

### Centralized Coordination Rules

These rules prevent git conflicts and protocol corruption during parallel execution:

| Action | Who does it |
|--------|-------------|
| Move task `todo/` → `in-progress/` | Orchestrator (before spawn) |
| Write/modify code files | Subagent |
| Append work log to task file | Subagent |
| Move task `in-progress/` → `done/` | Subagent |
| Write to `protocol.md` | Orchestrator only |
| Run `git add` / `git commit` | Orchestrator only |
| Decide next batch | Orchestrator only |

---

## Sequential Mode

Used when `/work [task-id]` or `/work --sequential` is invoked. Processes one task at a time, but still delegates the actual work to a subagent to protect the orchestrator's context window.

### Task Selection

1. **Check `in-progress/`** -- If a task file exists there, resume it (a previous session may have been interrupted).
2. **If a task ID was provided as argument**, find that task in `todo/` and use it.
3. **Otherwise, scan `todo/`** -- Sort files by name (lowest NNN prefix first) and pick the first one.
4. **If `todo/` is empty**, report that all tasks are done and provide a completion summary (see "Completion Summary" below).

### Dependency Check

Before starting a task, read its `Dependencies` field:
- If it lists other task IDs, check that those tasks exist in `done/`.
- If a dependency is NOT in `done/`, skip this task and move to the next one in `todo/`.
- If all tasks in `todo/` are blocked, report the situation and stop.

### Execution Loop

For each task:

1. **Move the task file** from `todo/` to `in-progress/` using a git mv (or file move).
2. **Log "Task Started"** to `.workflow/protocol.md` by prepending:

```markdown
## YYYY-MM-DD HH:MM -- Task Started: NNN - [Task Title]

**Type:** Task Start
**Task:** NNN - [Title]
**Milestone:** [milestone reference]

---
```

3. **Dispatch a subagent** to execute the task. Use the same Subagent Prompt Template from Parallel Mode (see above), but set the project context line to `This is a sequential task -- no other tasks are running concurrently.`

4. **When the subagent returns**, use its concise summary to:

   a. **Log "Task Completed"** to `.workflow/protocol.md` by prepending:

   ```markdown
   ## YYYY-MM-DD HH:MM -- Task Completed: NNN - [Task Title]

   **Type:** Task Completion
   **Task:** NNN - [Title]
   **Summary:** [1-2 sentence summary from the subagent's return message]
   **Files changed:** [count] files

   ---
   ```

   b. **Auto-commit** all changes (workflow state + code changes):
   ```
   git add -A && git commit -m "Task NNN: [task title]

   Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>"
   ```

   c. If the subagent **failed**, leave the task in `in-progress/`, log the failure to `protocol.md`, and ask the user how to proceed.

5. **Check for next task** -- Go back to step 1 (check `in-progress/`, then `todo/`).

---

## Completion Summary

When all tasks in `todo/` are done (and none remain in `in-progress/`):

1. Count tasks in `done/`.
2. Read `protocol.md` for a summary of all work done.
3. Report to the user:
   - Total tasks completed in this session
   - Brief summary of each completed task
   - Any remaining tasks in `backlog/`
   - Suggestions for next steps

Then **stop**. Do not loop indefinitely.

## Important

- **Always auto-commit after each task.** This ensures no work is lost and allows the user to track progress via git log.
- **In parallel mode, commit after each subagent completes** -- do not batch commits across multiple tasks.
- **If a task fails** (tests break, something doesn't work), log the failure in the work log, keep the task in `in-progress/`, and continue with other tasks in the batch. Only stop entirely if the user's intervention is needed.
- **Respect the user's ability to add tasks in parallel.** Always re-scan `todo/` after completing a batch -- the user may have added new tasks while you were working.
- The commit message format is: `Task NNN: [short task title]` followed by a newline and `Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>`.
