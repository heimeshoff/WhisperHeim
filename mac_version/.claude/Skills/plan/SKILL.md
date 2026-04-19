# Skill: Plan

Create or update the project roadmap and break milestones into actionable tasks.

## Invocation

```
/plan
/plan [specific area or milestone to plan]
```

## Instructions

You are running an interactive planning session. Your goal is to help the user define milestones and create concrete, actionable task files.

### Startup

1. Read `.workflow/vision.md` for product context.
2. Read `.workflow/research/_index.md` and any relevant research files.
3. Read `.workflow/roadmap.md` to see existing milestones.
4. Scan `.workflow/tasks/` across all status directories to see existing tasks.
5. Scan `.workflow/ideas/` for pending ideas that should be incorporated.
6. If an argument was provided, focus planning on that specific area.
7. Summarize the current state and ask the user what they want to plan.

### Planning Phase

Guide the user through an interactive planning conversation:

1. **Milestone Definition** -- What are the major milestones? What does each deliver?
2. **Task Breakdown** -- For each milestone, what are the concrete tasks?
3. **Ordering & Dependencies** -- What order should tasks be done in? What depends on what?
4. **Sizing** -- How big is each task? (Small/Medium/Large)
5. **Prioritization** -- What goes into `todo/` (ready to work) vs `backlog/` (later)?

Incorporate any pending ideas from `.workflow/ideas/`. Ask the user if they should become tasks.

### Determining Task IDs

When creating new tasks, scan all existing task files across all directories (`backlog/`, `todo/`, `in-progress/`, `done/`) to find the highest existing task ID number. New tasks start at the next number. Task IDs are three-digit zero-padded numbers (e.g., `001`, `002`, `042`).

### Output Phase

1. **Update `.workflow/roadmap.md`** with milestones:

```markdown
### M[N]: [Milestone Name]
**Status:** Not Started | In Progress | Done
**Target:** [description or date]

**Goals:**
- [Goal 1]
- [Goal 2]

**Tasks:** [NNN], [NNN], [NNN]
```

2. **Create task files** in `.workflow/tasks/todo/` (or `backlog/` for later work):

```markdown
# Task: [Short descriptive title]

**ID:** NNN
**Milestone:** M[N] - [Milestone Name]
**Size:** Small | Medium | Large
**Created:** YYYY-MM-DD
**Dependencies:** None | NNN-other-task, NNN-other-task

## Objective
[One sentence: what does "done" look like]

## Details
[Detailed specification of what to do]

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2

## Notes
[Additional context, references to research, etc.]

## Work Log
<!-- Appended by /work during execution -->
```

Filename format: `NNN-short-task-name.md` (e.g., `001-setup-project-structure.md`).

3. **Batch Task File Creation** -- When creating 5 or more task files, optionally use subagents to parallelize the work:
   - Group tasks by milestone.
   - Spawn one subagent per milestone group using the Task tool (parallel tool calls).
   - Each subagent creates its milestone's task files independently (no file conflicts since each task has a unique NNN-prefixed filename).
   - The orchestrator (you) handles `roadmap.md` and `protocol.md` updates after all subagents complete.
   - This is optional -- for fewer than 5 tasks, create them directly.

4. **If ideas were incorporated**, move or delete the processed idea files from `.workflow/ideas/`.

5. **Log to `.workflow/protocol.md`** by **prepending** a new entry:

```markdown
## YYYY-MM-DD HH:MM -- Planning: [Brief description]

**Type:** Planning
**Summary:** [What was planned]
**Milestones created/updated:** [list]
**Tasks created:** [list of NNN-name]
**Tasks moved to backlog:** [list, if any]
**Ideas incorporated:** [list, if any]

---
```

Place the new entry right after the `---` on line 4 of `protocol.md` (before any existing entries).

### Important

- Tasks must be concrete and actionable -- a developer should be able to pick one up and know exactly what to do.
- Acceptance criteria must be verifiable.
- Keep dependencies minimal -- prefer independent tasks that can be done in any order.
- Each task should ideally be completable in one work session.
- Do NOT execute any tasks (that's what `/work` is for).
