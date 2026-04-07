# Design System Specification: High-End Editorial Dark Mode

## 1. Overview & Creative North Star
This design system is built to transform the utility of transcription into a high-end editorial experience. Moving away from the "industrial utility" look common in SaaS, we embrace the **"Digital Curator"** North Star.

The interface is treated like a premium obsidian-glass canvas. It rejects the rigid, boxy constraints of traditional grids in favor of **Tonal Layering** and **Asymmetric Breathing Room**. We do not use borders to define space; we use light and depth. The goal is to make long-form transcription text feel as prestigious as a digital broadsheet, ensuring the user feels they are managing valuable intellectual property rather than just "data."

---

## 2. Colors & Surface Architecture

### The Palette
The core utilizes a deep, multi-layered charcoal (`#060e20`) and slate foundation, punctuated by a vibrant, electric indigo (`#a4a5ff`).

### The "No-Line" Rule
**Strict Mandate:** Designers are prohibited from using 1px solid borders for sectioning or containment. 
Structure must be achieved through:
- **Tonal Shifts:** Placing a `surface-container-low` (`#091328`) card against a `surface` (`#060e20`) background.
- **Negative Space:** Using the spacing scale to create groupings.

### Surface Hierarchy & Nesting
Treat the UI as physical layers of frosted material.
1.  **Base Layer:** `surface` (`#060e20`) - The infinite canvas.
2.  **Section Layer:** `surface-container-low` (`#091328`) - Large grouping areas.
3.  **Component Layer:** `surface-container` (`#0f1930`) or `surface-container-high` (`#141f38`) - Interactive cards or transcription blocks.
4.  **Floating Layer:** `surface-bright` (`#1f2b49`) - Tooltips or active menus.

### The Glass & Gradient Rule
For primary actions and floating elements, use **Glassmorphism**. Combine `surface-variant` (`#192540`) with a 12px backdrop-blur and 60% opacity. For the main "Transcribe" or "Export" actions, utilize a subtle linear gradient from `primary` (`#a4a5ff`) to `primary-dim` (`#5e5eff`) at a 135-degree angle to provide a "lit from within" soul.

---

## 3. Typography

The system uses a dual-font strategy to balance editorial authority with functional clarity.

*   **Display & Headlines (Manrope):** Chosen for its geometric modernism. Used in `display-lg` through `headline-sm`. These should be set with tight tracking (-2%) to feel impactful.
*   **Reading & Interface (Inter):** The workhorse. Inter provides the legibility required for 5,000-word transcripts. 
    *   **Body-lg:** Used for the primary transcription text.
    *   **Title-md:** Used for card headings.
    *   **Label-sm:** Used for metadata like timestamps or "via Parakeet" tags.

**Hierarchy Note:** Brand identity is conveyed through the massive scale difference between `display-md` headers and `body-md` content, creating an "Editorial" layout feel.

---

## 4. Elevation & Depth

### The Layering Principle
Depth is achieved by stacking. A card should never have a border; instead, an active card moves from `surface-container` to `surface-container-highest` (`#192540`).

### Ambient Shadows
For floating elements (modals, dropdowns), use "Atmospheric Shadows":
- **Color:** `on-surface` (`#dee5ff`) at 6% opacity.
- **Blur:** 32px to 64px.
- **Spread:** -4px.
This creates a soft glow rather than a muddy dark shadow.

### The "Ghost Border" Fallback
If contrast is legally required for accessibility:
- Use `outline-variant` (`#40485d`) at **15% opacity**. It should be felt, not seen.

---

## 5. Components

### Cards & Transcription Blocks
- **Style:** No dividers. Use `surface-container-low` for the background. 
- **Rounding:** Use `xl` (0.75rem) for main cards to soften the dark aesthetic.
- **Interaction:** On hover, shift the background to `surface-container-high`.

### Primary Action (Button)
- **Background:** Gradient of `primary` to `primary-dim`.
- **Text:** `on-primary` (`#1300a3`).
- **Rounding:** `full` (9999px) for a "pill" look that stands out against the angular content.

### Input Fields
- **Background:** `surface-container-lowest` (`#000000`).
- **Focus State:** No thick border. Use a 1px `ghost border` of `primary` at 40% and a subtle `primary` outer glow (4px blur).
- **Typography:** `body-md`.

### Status Badges
- **Transcribing:** `secondary-container` (`#4a339c`) background with `secondary` text.
- **Error:** `error-container` (`#a70138`) background with `on-error-container` text.
- **Style:** Use `label-sm` caps for a "pro" metadata feel.

### Transcription Timeline (Unique Component)
Instead of a standard list, use an asymmetric layout where timestamps (`label-md`) are pinned to the left, and the transcript body (`body-lg`) flows in a wider column on the right, utilizing white space to denote speaker changes.

---

## 6. Do’s and Don’ts

### Do
- **Do** use `surface-tint` sparingly to highlight active text selections in transcripts.
- **Do** allow for generous margins (at least 48px between major sections).
- **Do** use `inter` for all numbers and timestamps to ensure tabular alignment.
- **Do** use backdrop blurs on sticky navigation headers to maintain context of the scroll.

### Don’t
- **Don’t** use `#000000` for anything other than the deepest input field backgrounds.
- **Don’t** use a divider line to separate list items. Use 16px of vertical padding and a tonal shift on hover instead.
- **Don’t** use high-contrast white (`#ffffff`) for body text. Use `on-surface-variant` (`#a3aac4`) for secondary text to reduce eye strain in dark mode.
- **Don’t** use standard "drop shadows" on cards; they should appear integrated into the surface.