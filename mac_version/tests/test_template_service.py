"""Tests for the template service — storage, matching, CRUD operations."""

import json
import tempfile
from pathlib import Path

import pytest

from whisperheim.services.settings_service import SettingsService, TemplateItem
from whisperheim.services.templates.template_service import TemplateService


@pytest.fixture
def temp_settings():
    """Create a SettingsService with a temporary settings file."""
    with tempfile.TemporaryDirectory() as tmpdir:
        settings_path = Path(tmpdir) / "settings.json"
        service = SettingsService(settings_path=settings_path)
        yield service


@pytest.fixture
def template_service(temp_settings):
    """Create a TemplateService backed by temp settings."""
    return TemplateService(temp_settings)


class TestTemplateServiceCRUD:
    """Test template CRUD operations."""

    def test_add_template(self, template_service):
        template_service.add_template("greeting", "Hello, World!")
        templates = template_service.get_templates()
        assert len(templates) == 1
        assert templates[0].name == "greeting"
        assert templates[0].text == "Hello, World!"

    def test_update_template(self, template_service):
        template_service.add_template("greeting", "Hello!")
        template_service.update_template(0, "greeting", "Hello, World!")
        templates = template_service.get_templates()
        assert templates[0].text == "Hello, World!"

    def test_remove_template(self, template_service):
        template_service.add_template("greeting", "Hello!")
        template_service.add_template("farewell", "Goodbye!")
        template_service.remove_template(0)
        templates = template_service.get_templates()
        assert len(templates) == 1
        assert templates[0].name == "farewell"

    def test_templates_persist(self, temp_settings):
        """Templates should persist across service instances."""
        service1 = TemplateService(temp_settings)
        service1.add_template("greeting", "Hello!")

        # Reload settings
        service2 = TemplateService(temp_settings)
        templates = service2.get_templates()
        assert len(templates) == 1
        assert templates[0].name == "greeting"

    def test_add_template_with_group(self, template_service):
        template_service.add_template("greeting", "Hello!", group="Social")
        templates = template_service.get_templates()
        assert templates[0].group == "Social"

    def test_move_template_to_group(self, template_service):
        template_service.add_template("greeting", "Hello!")
        template_service.move_template_to_group(0, "Work")
        templates = template_service.get_templates()
        assert templates[0].group == "Work"


class TestTemplateServiceMatching:
    """Test fuzzy matching and expansion."""

    def test_match_exact(self, template_service):
        template_service.add_template("greeting", "Hello!")
        result = template_service.match_and_expand("greeting")
        assert result is not None
        assert result.template_name == "greeting"
        assert result.expanded_text == "Hello!"

    def test_match_fuzzy(self, template_service):
        template_service.add_template("greeting", "Hello!")
        result = template_service.match_and_expand("greating")
        assert result is not None
        assert result.template_name == "greeting"

    def test_no_match(self, template_service):
        template_service.add_template("greeting", "Hello!")
        result = template_service.match_and_expand("xyzabc")
        assert result is None

    def test_match_with_placeholder_expansion(self, template_service):
        template_service.add_template("today", "Date: {date}")
        result = template_service.match_and_expand("today")
        assert result is not None
        assert "{date}" not in result.expanded_text
        assert "Date: " in result.expanded_text

    def test_system_template_repeat(self, template_service):
        result = template_service.match_and_expand("repeat")
        assert result is not None
        assert result.is_system_template is True
        assert result.system_action_id == "system.repeat"

    def test_empty_spoken_returns_none(self, template_service):
        assert template_service.match_and_expand("") is None
        assert template_service.match_and_expand("   ") is None


class TestTemplateServiceGroups:
    """Test group management."""

    def test_default_ungrouped_exists(self, template_service):
        groups = template_service.get_groups()
        assert any(g.name == "Ungrouped" for g in groups)

    def test_add_group(self, template_service):
        template_service.add_group("Work")
        groups = template_service.get_groups()
        assert any(g.name == "Work" for g in groups)

    def test_no_duplicate_groups(self, template_service):
        template_service.add_group("Work")
        template_service.add_group("work")  # case-insensitive duplicate
        groups = template_service.get_groups()
        work_groups = [g for g in groups if g.name.lower() == "work"]
        assert len(work_groups) == 1

    def test_remove_empty_group(self, template_service):
        template_service.add_group("Empty")
        assert template_service.remove_group("Empty") is True
        groups = template_service.get_groups()
        assert not any(g.name == "Empty" for g in groups)

    def test_cannot_remove_group_with_templates(self, template_service):
        template_service.add_group("Work")
        template_service.add_template("greeting", "Hello!", group="Work")
        assert template_service.remove_group("Work") is False

    def test_cannot_remove_ungrouped(self, template_service):
        assert template_service.remove_group("Ungrouped") is False
