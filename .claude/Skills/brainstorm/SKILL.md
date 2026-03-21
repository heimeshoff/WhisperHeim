# Skill: Brainstorm

Interactive ideation session that produces or refines the product vision.

## Invocation

```
/brainstorm
/brainstorm [topic or initial idea]
```

## Instructions

You are running an interactive brainstorming session. Your goal is to help the user clarify and articulate their product vision through Socratic dialogue.

### Startup

1. Read `.workflow/vision.md` to see what exists already.
2. Read `.workflow/research/_index.md` to see if there's relevant research.
3. If `vision.md` has content, summarize it and ask what the user wants to refine.
4. If `vision.md` is empty, start fresh.
5. If arguments were provided, use them as the starting point.

### Dialogue Phase

Engage the user in a structured conversation. Do NOT write code. Ask smart, probing questions one or two at a time. Cover these areas (adapt to what's already filled in):

1. **Problem Discovery** -- What problem are we solving? Who has it? How painful is it?
2. **Target Users** -- Who specifically will use this? What's their context?
3. **Value Proposition** -- What's unique about our approach? Why would users choose this?
4. **Key Features** -- What are the must-have capabilities? What makes this special?
5. **Non-Goals** -- What are we explicitly NOT building? Where are the boundaries?
6. **Success Criteria** -- How will we know this is working? What does success look like?

Keep the conversation focused and productive. Challenge assumptions. Suggest angles the user might not have considered. Draw on research findings if available.

### Output Phase

When the user indicates they're satisfied (or you've covered all areas):

1. Synthesize the conversation into a complete `vision.md` using the template structure.
2. Write the updated `.workflow/vision.md`.
3. Log the session to `.workflow/protocol.md` by **prepending** a new entry:

```markdown
## YYYY-MM-DD HH:MM -- Brainstorm: [Brief description]

**Type:** Brainstorm
**Summary:** [2-3 sentence summary of what was discussed and decided]
**Vision updated:** Yes/No
**Key decisions:**
- [Decision 1]
- [Decision 2]

---
```

Place the new entry right after the `---` on line 4 of `protocol.md` (before any existing entries).

### Important

- Do NOT write any code during brainstorming.
- Do NOT create tasks or modify the roadmap (that's what `/plan` is for).
- Keep the dialogue conversational and engaging.
- It's okay to have multiple brainstorming sessions -- each one refines the vision further.
