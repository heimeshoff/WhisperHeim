"""Fuzzy string matching for matching spoken template names against stored names.

Ported from Windows WhisperHeim FuzzyMatcher.cs.
Uses Levenshtein distance normalized by the longer string length.
"""

import re
from typing import Optional


class FuzzyMatcher:
    """Provides fuzzy string matching for voice-triggered template lookup."""

    @staticmethod
    def find_best_match(
        spoken: str,
        candidates: list[str],
        threshold: float = 0.4,
    ) -> Optional[str]:
        """Find the best matching template name from candidates.

        Returns None if no candidate scores above the threshold.

        Args:
            spoken: The spoken/transcribed text to match.
            candidates: Available template names.
            threshold: Minimum similarity score (0.0 to 1.0). Default 0.4.
        """
        if not spoken or not spoken.strip():
            return None

        normalized_spoken = FuzzyMatcher._normalize(spoken)
        best_match: Optional[str] = None
        best_score: float = 0.0

        for candidate in candidates:
            if not candidate or not candidate.strip():
                continue

            normalized_candidate = FuzzyMatcher._normalize(candidate)

            # Try exact containment first (highest priority)
            if (normalized_candidate in normalized_spoken
                    or normalized_spoken in normalized_candidate):
                contain_score = 0.95
                if contain_score > best_score:
                    best_score = contain_score
                    best_match = candidate
                continue

            # Compute similarity via Levenshtein distance
            score = FuzzyMatcher.compute_similarity(
                normalized_spoken, normalized_candidate
            )
            if score > best_score:
                best_score = score
                best_match = candidate

        return best_match if best_score >= threshold else None

    @staticmethod
    def compute_similarity(a: str, b: str) -> float:
        """Compute a similarity score between 0.0 and 1.0 for two strings.

        1.0 = identical, 0.0 = completely different.
        """
        if a.lower() == b.lower():
            return 1.0

        max_len = max(len(a), len(b))
        if max_len == 0:
            return 1.0

        distance = FuzzyMatcher._levenshtein_distance(a.lower(), b.lower())
        return 1.0 - distance / max_len

    @staticmethod
    def _normalize(text: str) -> str:
        """Strip everything except letters and digits, then lowercase."""
        return re.sub(r"[^a-zA-Z0-9]", "", text).lower()

    @staticmethod
    def _levenshtein_distance(s: str, t: str) -> int:
        """Compute the Levenshtein edit distance between two strings."""
        n = len(s)
        m = len(t)

        if n == 0:
            return m
        if m == 0:
            return n

        # Single-row optimization
        previous_row = list(range(m + 1))
        current_row = [0] * (m + 1)

        for i in range(1, n + 1):
            current_row[0] = i
            for j in range(1, m + 1):
                cost = 0 if s[i - 1] == t[j - 1] else 1
                current_row[j] = min(
                    current_row[j - 1] + 1,      # insertion
                    previous_row[j] + 1,          # deletion
                    previous_row[j - 1] + cost,   # substitution
                )
            previous_row, current_row = current_row, previous_row

        return previous_row[m]
