"""Tests for the fuzzy matcher — ported matching logic."""

import pytest

from whisperheim.services.templates.fuzzy_matcher import FuzzyMatcher


class TestFuzzyMatcher:
    """Test fuzzy string matching behavior."""

    def test_exact_match_returns_candidate(self):
        result = FuzzyMatcher.find_best_match("hello", ["hello", "world"])
        assert result == "hello"

    def test_case_insensitive_match(self):
        result = FuzzyMatcher.find_best_match("Hello", ["hello", "world"])
        assert result == "hello"

    def test_containment_match(self):
        """If spoken text contains a candidate name, it should match."""
        result = FuzzyMatcher.find_best_match("say greeting", ["greeting", "farewell"])
        assert result == "greeting"

    def test_containment_reverse(self):
        """If candidate contains spoken text, it should match."""
        result = FuzzyMatcher.find_best_match("greet", ["greeting template", "farewell"])
        assert result == "greeting template"

    def test_fuzzy_match_with_typo(self):
        """Minor typos should still match above threshold."""
        result = FuzzyMatcher.find_best_match("greating", ["greeting", "farewell"])
        assert result == "greeting"

    def test_no_match_below_threshold(self):
        """Completely different strings should not match."""
        result = FuzzyMatcher.find_best_match("xyz", ["greeting", "farewell"])
        assert result is None

    def test_empty_spoken_returns_none(self):
        result = FuzzyMatcher.find_best_match("", ["hello"])
        assert result is None

    def test_whitespace_spoken_returns_none(self):
        result = FuzzyMatcher.find_best_match("   ", ["hello"])
        assert result is None

    def test_empty_candidates_returns_none(self):
        result = FuzzyMatcher.find_best_match("hello", [])
        assert result is None

    def test_ignores_punctuation_and_spaces(self):
        """Normalization strips non-alphanumeric characters."""
        result = FuzzyMatcher.find_best_match("my email", ["my-email", "phone"])
        assert result == "my-email"

    def test_best_match_selected(self):
        """Among multiple candidates, the best one should be selected."""
        result = FuzzyMatcher.find_best_match(
            "address", ["email address", "phone number", "mailing address"]
        )
        # Both contain "address", but exact containment should pick one
        assert result is not None
        assert "address" in result.lower()

    def test_compute_similarity_identical(self):
        assert FuzzyMatcher.compute_similarity("hello", "hello") == 1.0

    def test_compute_similarity_empty(self):
        assert FuzzyMatcher.compute_similarity("", "") == 1.0

    def test_compute_similarity_different(self):
        score = FuzzyMatcher.compute_similarity("abc", "xyz")
        assert score < 0.5

    def test_levenshtein_distance_basic(self):
        assert FuzzyMatcher._levenshtein_distance("kitten", "sitting") == 3
        assert FuzzyMatcher._levenshtein_distance("", "abc") == 3
        assert FuzzyMatcher._levenshtein_distance("abc", "") == 3
        assert FuzzyMatcher._levenshtein_distance("abc", "abc") == 0


class TestPlaceholderExpander:
    """Test placeholder expansion."""

    def test_date_placeholder(self):
        from whisperheim.services.templates.placeholder_expander import (
            TemplatePlaceholderExpander,
        )
        from datetime import datetime

        result = TemplatePlaceholderExpander.expand("Today is {date}")
        expected_date = datetime.now().strftime("%Y-%m-%d")
        assert result == f"Today is {expected_date}"

    def test_time_placeholder(self):
        from whisperheim.services.templates.placeholder_expander import (
            TemplatePlaceholderExpander,
        )
        from datetime import datetime

        result = TemplatePlaceholderExpander.expand("Time: {time}")
        expected_time = datetime.now().strftime("%H:%M")
        assert result == f"Time: {expected_time}"

    def test_case_insensitive_placeholders(self):
        from whisperheim.services.templates.placeholder_expander import (
            TemplatePlaceholderExpander,
        )

        result = TemplatePlaceholderExpander.expand("{DATE} and {Time}")
        assert "{DATE}" not in result
        assert "{Time}" not in result

    def test_no_placeholders(self):
        from whisperheim.services.templates.placeholder_expander import (
            TemplatePlaceholderExpander,
        )

        result = TemplatePlaceholderExpander.expand("plain text")
        assert result == "plain text"

    def test_empty_string(self):
        from whisperheim.services.templates.placeholder_expander import (
            TemplatePlaceholderExpander,
        )

        assert TemplatePlaceholderExpander.expand("") == ""
        assert TemplatePlaceholderExpander.expand(None) is None
