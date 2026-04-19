# Skill: Research

Research a topic and persist findings for the project.

## Invocation

```
/research [topic]
/research
```

## Instructions

You are conducting research to inform the project. You will investigate a topic, synthesize findings, and persist them for future reference.

### Startup

1. Read `.workflow/vision.md` for project context.
2. Read `.workflow/research/_index.md` to see existing research.
3. If a topic argument was provided, use it as the research focus.
4. If no argument was given:
   - Review `vision.md` and existing research
   - Suggest 3-5 research topics that would be most valuable right now
   - Ask the user which topic(s) to investigate
   - The user can also specify a completely different topic

### Research Phase

1. Use `WebSearch` to find relevant information on the topic.
2. Use `WebFetch` to read specific pages with valuable content.
3. For broad topics, you may use the `Task` tool to spawn parallel research agents for subtopics.
4. Focus on:
   - **Technical feasibility** -- Can we build this? What technologies exist?
   - **Domain knowledge** -- What do we need to understand about the problem space?
   - **Existing solutions** -- What's already out there? What can we learn from them?
   - **Best practices** -- What approaches are recommended by experts?
   - **Risks and pitfalls** -- What should we watch out for?

### Output Phase

1. Create a research file at `.workflow/research/{topic-slug}.md` with this format:

```markdown
# Research: [Topic Title]

**Date:** YYYY-MM-DD
**Status:** Complete | Partial (needs follow-up)
**Relevance:** [Which aspect of the vision this relates to]

## Summary
[2-3 paragraph executive summary of findings]

## Key Findings

### [Finding 1 Title]
[Details with source references]

### [Finding 2 Title]
[Details with source references]

## Implications for This Project
[How these findings should influence our approach]

## Open Questions
- [Questions that need further investigation]

## Sources
- [Source 1 with URL]
- [Source 2 with URL]
```

2. Update `.workflow/research/_index.md` by adding a row to the table:
   ```
   | YYYY-MM-DD | [Topic] | [filename.md](filename.md) | [1-line key finding] |
   ```

3. Log the research to `.workflow/protocol.md` by **prepending** a new entry:

```markdown
## YYYY-MM-DD HH:MM -- Research: [Topic]

**Type:** Research
**Topic:** [Full topic description]
**File:** research/{topic-slug}.md
**Key findings:**
- [Finding 1]
- [Finding 2]
- [Finding 3]

---
```

Place the new entry right after the `---` on line 4 of `protocol.md` (before any existing entries).

### Important

- Always cite sources with URLs.
- Be honest about confidence levels -- mark uncertain findings as such.
- If the topic is too broad, narrow it down with the user first.
- Connect findings back to the project vision whenever possible.
- Do NOT create tasks or modify the roadmap (that's what `/plan` is for).
