"""Template placeholder expansion.

Ported from Windows WhisperHeim TemplatePlaceholderExpander.cs.
Supported placeholders: {date}, {time}.
"""

import re
from datetime import datetime


class TemplatePlaceholderExpander:
    """Expands placeholders in template text."""

    @staticmethod
    def expand(template_text: str) -> str:
        """Expand all known placeholders in the given template text.

        Placeholders are case-insensitive:
        - {date} -> current date as YYYY-MM-DD
        - {time} -> current time as HH:MM
        """
        if not template_text:
            return template_text

        now = datetime.now()

        # {date} -> YYYY-MM-DD (case-insensitive)
        result = re.sub(
            r"\{date\}", now.strftime("%Y-%m-%d"), template_text, flags=re.IGNORECASE
        )

        # {time} -> HH:MM (case-insensitive)
        result = re.sub(
            r"\{time\}", now.strftime("%H:%M"), result, flags=re.IGNORECASE
        )

        return result
