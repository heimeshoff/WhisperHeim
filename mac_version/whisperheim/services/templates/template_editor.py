"""Template editor GUI — tkinter-based window for managing templates.

Provides CRUD operations on templates: list, add, edit, delete.
Accessible from the menu bar "Edit Templates..." menu item.
"""

import logging
import threading
import tkinter as tk
from tkinter import messagebox, simpledialog
from typing import Optional

from whisperheim.services.templates.template_service import TemplateService

logger = logging.getLogger(__name__)


class TemplateEditorWindow:
    """A tkinter window for managing voice templates."""

    def __init__(self, template_service: TemplateService):
        self._service = template_service
        self._window: Optional[tk.Tk] = None
        self._listbox: Optional[tk.Listbox] = None
        self._name_var: Optional[tk.StringVar] = None
        self._text_widget: Optional[tk.Text] = None
        self._group_var: Optional[tk.StringVar] = None
        self._editing_index: Optional[int] = None

    def show(self) -> None:
        """Open the template editor window in a new thread."""
        thread = threading.Thread(target=self._create_window, daemon=True)
        thread.start()

    def _create_window(self) -> None:
        """Create and run the tkinter editor window."""
        try:
            self._window = tk.Tk()
            self._window.title("WhisperHeim — Template Editor")
            self._window.geometry("700x500")
            self._window.minsize(500, 400)

            self._build_ui()
            self._refresh_list()
            self._window.mainloop()
        except Exception as e:
            logger.error("[TemplateEditor] Failed to create window: %s", e)

    def _build_ui(self) -> None:
        """Build the editor UI."""
        win = self._window

        # --- Left panel: template list ---
        left_frame = tk.Frame(win)
        left_frame.pack(side=tk.LEFT, fill=tk.BOTH, padx=(10, 5), pady=10)

        tk.Label(left_frame, text="Templates", font=("Helvetica", 14, "bold")).pack(
            anchor=tk.W
        )

        list_frame = tk.Frame(left_frame)
        list_frame.pack(fill=tk.BOTH, expand=True, pady=(5, 0))

        scrollbar = tk.Scrollbar(list_frame)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        self._listbox = tk.Listbox(
            list_frame,
            yscrollcommand=scrollbar.set,
            font=("Helvetica", 12),
            selectmode=tk.SINGLE,
        )
        self._listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.config(command=self._listbox.yview)
        self._listbox.bind("<<ListboxSelect>>", self._on_select)

        # Buttons under list
        btn_frame = tk.Frame(left_frame)
        btn_frame.pack(fill=tk.X, pady=(5, 0))

        tk.Button(btn_frame, text="New", command=self._on_new, width=8).pack(
            side=tk.LEFT, padx=(0, 5)
        )
        tk.Button(btn_frame, text="Delete", command=self._on_delete, width=8).pack(
            side=tk.LEFT
        )

        # --- Right panel: edit form ---
        right_frame = tk.Frame(win)
        right_frame.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=(5, 10), pady=10)

        tk.Label(right_frame, text="Edit Template", font=("Helvetica", 14, "bold")).pack(
            anchor=tk.W
        )

        # Name field
        name_frame = tk.Frame(right_frame)
        name_frame.pack(fill=tk.X, pady=(10, 5))
        tk.Label(name_frame, text="Name:", width=6, anchor=tk.W).pack(side=tk.LEFT)
        self._name_var = tk.StringVar()
        tk.Entry(name_frame, textvariable=self._name_var, font=("Helvetica", 12)).pack(
            side=tk.LEFT, fill=tk.X, expand=True
        )

        # Group field
        group_frame = tk.Frame(right_frame)
        group_frame.pack(fill=tk.X, pady=(0, 5))
        tk.Label(group_frame, text="Group:", width=6, anchor=tk.W).pack(side=tk.LEFT)
        self._group_var = tk.StringVar()
        tk.Entry(group_frame, textvariable=self._group_var, font=("Helvetica", 12)).pack(
            side=tk.LEFT, fill=tk.X, expand=True
        )

        # Text field
        tk.Label(right_frame, text="Template Text:", anchor=tk.W).pack(
            anchor=tk.W, pady=(5, 2)
        )
        tk.Label(
            right_frame,
            text="Placeholders: {date} = YYYY-MM-DD, {time} = HH:MM",
            font=("Helvetica", 10, "italic"),
            fg="gray",
        ).pack(anchor=tk.W)

        text_frame = tk.Frame(right_frame)
        text_frame.pack(fill=tk.BOTH, expand=True, pady=(2, 5))

        text_scrollbar = tk.Scrollbar(text_frame)
        text_scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        self._text_widget = tk.Text(
            text_frame,
            yscrollcommand=text_scrollbar.set,
            font=("Helvetica", 12),
            wrap=tk.WORD,
            height=8,
        )
        self._text_widget.pack(fill=tk.BOTH, expand=True)
        text_scrollbar.config(command=self._text_widget.yview)

        # Save button
        tk.Button(
            right_frame, text="Save", command=self._on_save, width=10
        ).pack(anchor=tk.E, pady=(5, 0))

    def _refresh_list(self) -> None:
        """Refresh the template listbox."""
        if not self._listbox:
            return

        self._listbox.delete(0, tk.END)
        templates = self._service.get_templates()
        for t in templates:
            display = t.name
            if t.group:
                display = f"[{t.group}] {t.name}"
            self._listbox.insert(tk.END, display)

    def _on_select(self, _event) -> None:
        """Template selected in the list — populate edit form."""
        if not self._listbox:
            return

        selection = self._listbox.curselection()
        if not selection:
            return

        index = selection[0]
        templates = self._service.get_templates()
        if index >= len(templates):
            return

        template = templates[index]
        self._editing_index = index
        self._name_var.set(template.name)
        self._group_var.set(template.group or "")
        self._text_widget.delete("1.0", tk.END)
        self._text_widget.insert("1.0", template.text)

    def _on_new(self) -> None:
        """Create a new template."""
        self._editing_index = None
        self._name_var.set("")
        self._group_var.set("")
        self._text_widget.delete("1.0", tk.END)
        self._text_widget.focus_set() if self._text_widget else None

    def _on_delete(self) -> None:
        """Delete the selected template."""
        if not self._listbox:
            return

        selection = self._listbox.curselection()
        if not selection:
            messagebox.showinfo("Delete", "Select a template to delete.")
            return

        index = selection[0]
        templates = self._service.get_templates()
        if index >= len(templates):
            return

        name = templates[index].name
        if messagebox.askyesno("Delete Template", f'Delete template "{name}"?'):
            self._service.remove_template(index)
            self._editing_index = None
            self._name_var.set("")
            self._group_var.set("")
            self._text_widget.delete("1.0", tk.END)
            self._refresh_list()

    def _on_save(self) -> None:
        """Save the current template (add or update)."""
        name = self._name_var.get().strip()
        text = self._text_widget.get("1.0", tk.END).strip()
        group = self._group_var.get().strip() or None

        if not name:
            messagebox.showwarning("Save", "Template name is required.")
            return
        if not text:
            messagebox.showwarning("Save", "Template text is required.")
            return

        if self._editing_index is not None:
            # Update existing
            self._service.update_template(self._editing_index, name, text)
            self._service.move_template_to_group(self._editing_index, group)
        else:
            # Add new
            self._service.add_template(name, text, group)

        self._refresh_list()

        # Select the saved item
        templates = self._service.get_templates()
        for i, t in enumerate(templates):
            if t.name == name:
                self._listbox.selection_clear(0, tk.END)
                self._listbox.selection_set(i)
                self._listbox.see(i)
                self._editing_index = i
                break

        logger.info('[TemplateEditor] Saved template "%s".', name)
