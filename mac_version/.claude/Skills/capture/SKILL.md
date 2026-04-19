# Skill: Capture

Capture ideas, refine them over time, and promote them to tasks when ready.

## Invocation

```
/capture
/capture [quick idea description]
/capture [existing-idea-name]
```

## Instructions

You help the user capture ideas, iteratively refine them, and turn them into trackable tasks in the workflow system.

### Mode Selection

**Smart argument matching:**

1. If an argument was provided, check if it matches an existing idea filename in `.workflow/ideas/` (match against the short-name portion, ignoring the date prefix and `.md` extension; fuzzy/partial matches count):
   - **Exact or unique match** → enter **Refine mode** for that idea
   - **Multiple matches** → list the matching ideas and let the user pick one, then enter Refine mode
   - **No match** → treat the argument as a new idea description. Ask the user:
     - **Quick capture** -- Just write it down as-is (creates an idea file)
     - **Deep capture** -- Let's discuss and refine this into a well-specified task

2. If no argument was provided:
   - Scan `.workflow/ideas/` for existing idea files (exclude subdirectories like `promoted/`)
   - If ideas exist, show them with their Status and offer three choices:
     - **Quick capture** -- Jot down a new idea
     - **Deep capture** -- Discuss and create a well-specified task
     - **Refine an idea** -- Pick an existing idea to refine or promote
   - If no ideas exist, ask the user what they want to capture and offer Quick or Deep mode

### Quick Capture Mode

1. Create a file in `.workflow/ideas/` with the format:
   - Filename: `YYYY-MM-DD-short-name.md`
   - Content:

```markdown
# Idea: [Short title]

**Captured:** YYYY-MM-DD
**Source:** User input
**Status:** Raw
**Last Refined:** --

## Description
[The idea as described by the user]

## Initial Thoughts
[Any immediate observations or connections to existing vision/research]

## Open Questions
- [Any obvious unknowns or things to figure out]

## Refinement Log
[Empty -- entries added during Refine mode]
```

2. Alternatively, if the user wants it as a task immediately, create it directly in `.workflow/tasks/backlog/` using the standard task file format. Determine the next available task ID by scanning all task directories.

3. Log to `.workflow/protocol.md` by prepending:

```markdown
## YYYY-MM-DD HH:MM -- Idea Captured: [Short title]

**Type:** Idea Capture
**Mode:** Quick
**Filed to:** ideas/YYYY-MM-DD-short-name.md (or tasks/backlog/NNN-name.md)

---
```

### Deep Capture Mode

1. Engage in a conversation to refine the idea:
   - What problem does this solve?
   - Who benefits?
   - How does it relate to the current vision?
   - What would the acceptance criteria be?
   - How big is this? (Small/Medium/Large)
   - Which milestone does it belong to (if any)?

2. Optionally research the idea using `WebSearch` if the user wants.

3. Create a well-specified task file:
   - In `.workflow/tasks/todo/` if it should be worked on soon
   - In `.workflow/tasks/backlog/` if it's for later
   - Use the standard task file format (see `/plan` skill for format)
   - Determine the next available task ID by scanning all task directories

4. Optionally update `.workflow/roadmap.md` if this affects milestones.

5. Log to `.workflow/protocol.md` by prepending:

```markdown
## YYYY-MM-DD HH:MM -- Idea Captured: [Short title]

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/[status]/NNN-name.md
**Summary:** [Brief description of the refined idea]

---
```

### Refine Mode

Refine mode is for iterating on an existing idea across one or more sessions. Read the idea file and present a summary showing its title, status, description, open questions, and refinement history.

Then offer three actions:

#### Continue Refining

1. Walk through the idea with the user:
   - Review and discuss open questions
   - Expand or sharpen the description
   - Add new observations or connections to vision/research
   - Resolve open questions (move them into the description or discard them)

2. Update the idea file:
   - Update **Status** progression: `Raw` → `Developing` → `Ready`
     - `Raw`: Just captured, not yet discussed
     - `Developing`: Has been refined at least once, still has open questions
     - `Ready`: Open questions resolved, description is clear, ready to promote
   - Update **Last Refined** to today's date
   - Append an entry to the **Refinement Log**:

```markdown
### YYYY-MM-DD
[Summary of what was discussed and changed in this refinement session]
```

3. Log to `.workflow/protocol.md` by prepending:

```markdown
## YYYY-MM-DD HH:MM -- Idea Refined: [Short title]

**Type:** Idea Refinement
**Idea:** ideas/YYYY-MM-DD-short-name.md
**Status:** [new status]
**Summary:** [What was refined]

---
```

#### Promote to Task

1. Confirm with the user:
   - Which task queue: `todo/` (work on soon) or `backlog/` (for later)?
   - Which milestone (if any)?
   - Task size (Small/Medium/Large)?
   - Any dependencies?

2. Determine the next available task ID by scanning all task directories (including `done/`).

3. Create a standard task file in the chosen directory, carrying over the refined description, acceptance criteria, and any other details from the idea.

4. Move the original idea file from `.workflow/ideas/` to `.workflow/ideas/promoted/`, preserving the full refinement history.

5. Add a back-reference to the top of the promoted idea file:

```markdown
> **Promoted to task:** [NNN-short-name.md] on YYYY-MM-DD
```

6. Log to `.workflow/protocol.md` by prepending:

```markdown
## YYYY-MM-DD HH:MM -- Idea Promoted: [Short title]

**Type:** Idea Promotion
**From:** ideas/YYYY-MM-DD-short-name.md
**To:** tasks/[status]/NNN-short-name.md
**Summary:** [Brief description of the resulting task]

---
```

#### Discard

1. Confirm with the user that they want to discard the idea.

2. Delete the idea file from `.workflow/ideas/`.

3. Log to `.workflow/protocol.md` by prepending:

```markdown
## YYYY-MM-DD HH:MM -- Idea Discarded: [Short title]

**Type:** Idea Discard
**Idea:** ideas/YYYY-MM-DD-short-name.md
**Reason:** [User's reason, if given]

---
```

### Important

- Capture should be fast and frictionless -- don't over-engineer the conversation for quick captures.
- Always acknowledge what was captured so the user has confirmation.
- Connect ideas to existing vision and milestones when possible.
- If the idea duplicates an existing task or idea, point that out instead of creating a duplicate.
- Refine mode should feel conversational, not like filling out a form. Guide the user naturally.
- When promoting, carry over as much context as possible from the idea file so nothing is lost.
