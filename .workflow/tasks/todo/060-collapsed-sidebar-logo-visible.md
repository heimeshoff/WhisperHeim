# Task: Show Full Logo in Collapsed Sidebar

**ID:** 060
**Milestone:** --
**Size:** Small
**Created:** 2026-03-22
**Dependencies:** --

## Objective
Ensure the logo is fully visible when the sidebar is collapsed, and only show the app name when the sidebar is expanded.

## Details

### Problem
When the sidebar collapses to 60px, the 32px logo with 16px margins on each side (= 64px total) gets clipped on the right. The logo should remain fully visible and centered in the collapsed sidebar.

### Solution
- Increase `SidebarCollapsedWidth` from 60px to 64px in `MainWindow.xaml.cs` (line ~91)
- When collapsed: center the logo horizontally within the sidebar, hide the app name (`BrandingTitle`)
- When expanded: show both logo and app name side by side (current behavior)
- Adjust the branding header margins when collapsed so the logo is properly centered in the 64px width

### Files
- `MainWindow.xaml` — branding header layout, logo margins
- `MainWindow.xaml.cs` — `SidebarCollapsedWidth` constant, `ApplySidebarCollapsedState` method

## Acceptance Criteria
- [ ] Logo is fully visible and centered when sidebar is collapsed
- [ ] App name ("WhisperHeim") is hidden when collapsed, visible when expanded
- [ ] No clipping of the logo at collapsed width
- [ ] Works correctly in both light and dark themes

## Work Log
<!-- Appended by /work during execution -->
