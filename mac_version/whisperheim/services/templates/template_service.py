"""Template storage, retrieval, and fuzzy matching.

Ported from Windows WhisperHeim TemplateService.cs.
Manages user templates stored in settings and provides fuzzy matching
of spoken names to template text with placeholder expansion.
"""

import logging
from dataclasses import dataclass
from typing import Optional

from whisperheim.services.settings_service import SettingsService
from whisperheim.services.templates.fuzzy_matcher import FuzzyMatcher
from whisperheim.services.templates.placeholder_expander import TemplatePlaceholderExpander

logger = logging.getLogger(__name__)


# System template definitions (built-in, not persisted)
SYSTEM_REPEAT_ACTION_ID = "system.repeat"
SYSTEM_GROUP_NAME = "WhisperHeim"

SYSTEM_TEMPLATES = [
    {
        "name": "Repeat",
        "description": "Types the last dictated text again",
        "action_id": SYSTEM_REPEAT_ACTION_ID,
    },
]


@dataclass
class TemplateMatchResult:
    """Result of a template match operation."""
    template_name: str
    expanded_text: str
    score: float
    is_system_template: bool = False
    system_action_id: Optional[str] = None


class TemplateService:
    """Manages templates and provides fuzzy matching of spoken names."""

    UNGROUPED_NAME = "Ungrouped"

    def __init__(self, settings_service: SettingsService):
        self._settings = settings_service
        self._ensure_defaults()

    def match_and_expand(self, spoken_text: str) -> Optional[TemplateMatchResult]:
        """Match spoken text against template names and return expanded text.

        Tries system templates first (they take precedence), then user templates.
        Returns None if no match found above the threshold.
        """
        if not spoken_text or not spoken_text.strip():
            return None

        templates = self._settings.settings.templates.items
        all_candidates: list[tuple[str, bool, Optional[str]]] = []

        # System templates first (take precedence)
        for st in SYSTEM_TEMPLATES:
            if st["name"]:
                all_candidates.append((st["name"], True, st["action_id"]))

        # User templates
        for t in templates:
            if t.name:
                all_candidates.append((t.name, False, None))

        if not all_candidates:
            return None

        candidate_names = [c[0] for c in all_candidates]
        matched_name = FuzzyMatcher.find_best_match(spoken_text, candidate_names)

        if matched_name is None:
            logger.info(
                '[TemplateService] No match found for spoken text: "%s"', spoken_text
            )
            return None

        score = FuzzyMatcher.compute_similarity(
            spoken_text.strip().lower(), matched_name.strip().lower()
        )

        # Find the matched candidate
        matched = next(
            c for c in all_candidates if c[0].lower() == matched_name.lower()
        )

        if matched[1]:  # is_system_template
            logger.info(
                '[TemplateService] Matched "%s" -> system template "%s" '
                "(score=%.2f, action=%s)",
                spoken_text, matched_name, score, matched[2],
            )
            return TemplateMatchResult(
                template_name=matched_name,
                expanded_text="",
                score=score,
                is_system_template=True,
                system_action_id=matched[2],
            )

        # Find user template
        template = next(
            t for t in templates if t.name.lower() == matched_name.lower()
        )
        expanded_text = TemplatePlaceholderExpander.expand(template.text)

        logger.info(
            '[TemplateService] Matched "%s" -> template "%s" (score=%.2f)',
            spoken_text, matched_name, score,
        )
        return TemplateMatchResult(
            template_name=matched_name,
            expanded_text=expanded_text,
            score=score,
        )

    def get_templates(self) -> list:
        """Return all user templates."""
        return list(self._settings.settings.templates.items)

    def add_template(
        self, name: str, text: str, group: Optional[str] = None
    ) -> None:
        """Add a new template."""
        from whisperheim.services.settings_service import TemplateItem

        effective_group = None if (not group or group == self.UNGROUPED_NAME) else group
        self._settings.settings.templates.items.append(
            TemplateItem(name=name, text=text, group=effective_group)
        )
        self._settings.save()

    def update_template(self, index: int, name: str, text: str) -> None:
        """Update an existing template at the given index."""
        items = self._settings.settings.templates.items
        if 0 <= index < len(items):
            items[index].name = name
            items[index].text = text
            self._settings.save()

    def remove_template(self, index: int) -> None:
        """Remove a template at the given index."""
        items = self._settings.settings.templates.items
        if 0 <= index < len(items):
            items.pop(index)
            self._settings.save()

    def get_groups(self) -> list:
        """Return all template groups, ordered."""
        self._ensure_defaults()
        groups = self._settings.settings.templates.groups
        return sorted(
            groups,
            key=lambda g: (
                0 if g.name == self.UNGROUPED_NAME else (2 if g.name == SYSTEM_GROUP_NAME else 1),
                g.order,
                g.name.lower(),
            ),
        )

    def add_group(self, name: str) -> None:
        """Add a new template group."""
        if not name or not name.strip():
            return
        from whisperheim.services.settings_service import TemplateGroup

        groups = self._settings.settings.templates.groups
        if any(g.name.lower() == name.lower() for g in groups):
            return  # no duplicates

        max_order = max((g.order for g in groups), default=0)
        groups.append(TemplateGroup(name=name, is_expanded=True, order=max_order + 1))
        self._settings.save()

    def remove_group(self, name: str) -> bool:
        """Remove a group if it has no templates. Returns True if removed."""
        if name.lower() in (self.UNGROUPED_NAME.lower(), SYSTEM_GROUP_NAME.lower()):
            return False

        templates = self._settings.settings.templates.items
        if any(t.group and t.group.lower() == name.lower() for t in templates):
            return False

        groups = self._settings.settings.templates.groups
        before = len(groups)
        self._settings.settings.templates.groups = [
            g for g in groups if g.name.lower() != name.lower()
        ]
        if len(self._settings.settings.templates.groups) < before:
            self._settings.save()
            return True
        return False

    def move_template_to_group(
        self, template_index: int, group_name: Optional[str]
    ) -> None:
        """Move a template to a different group."""
        items = self._settings.settings.templates.items
        if 0 <= template_index < len(items):
            items[template_index].group = (
                None if (not group_name or group_name == self.UNGROUPED_NAME) else group_name
            )
            self._settings.save()

    def get_system_templates(self) -> list[dict]:
        """Return all system template definitions."""
        return list(SYSTEM_TEMPLATES)

    def _ensure_defaults(self) -> None:
        """Ensure default groups exist."""
        from whisperheim.services.settings_service import TemplateGroup

        groups = self._settings.settings.templates.groups
        if not any(g.name.lower() == self.UNGROUPED_NAME.lower() for g in groups):
            groups.insert(0, TemplateGroup(name=self.UNGROUPED_NAME, is_expanded=True, order=0))
