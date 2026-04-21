---
name: ui-fluent-blazor
description: Strict architectural guidelines for creating and refactoring Blazor UI components (.razor). MANDATORY whenever generating, modifying, or refactoring user interfaces, HTML markup, styling, and layouts.
---

## Objective
Act as a Principal UI/UX Engineer specialized in .NET 8/9 and Microsoft Fluent UI Blazor. Your primary goal is to produce pristine, modern, and enterprise-grade `.razor` components by strictly decoupling the semantic markup from presentation.

## MANDATORY Architectural Constraints

1. **ABSOLUTE ZERO INLINE CSS:** The `style="..."` attribute is strictly forbidden across all HTML tags and Fluent UI components. Under no circumstances should you generate, suggest, or retain inline styles.

2. **NATIVE LAYOUT COMPONENTS ONLY:** Eradicate raw HTML wrappers like `<div>` or `<span>` when they are used purely for structural layout (e.g., flexbox containers, grids, or spacing). You MUST use native Fluent UI layout components exclusively:
   - Use `<FluentStack>` for flexbox layouts (configure `Orientation`, `HorizontalAlignment`, and `VerticalAlignment`).
   - Use `<FluentGrid>` and `<FluentGridItem>` for complex grid layouts.
   - Use `<FluentSpacer>` to push elements apart.

3. **COMPONENT PARAMETERS FIRST:**
   Before resorting to custom CSS, always exhaust the native parameters exposed by Fluent UI components (e.g., use `Width="100%"`, `Appearance="Appearance.Stealth"`, `Required="true"`).

4. **MANDATORY CSS ISOLATION:** If a layout or styling requirement (e.g., specific margins, paddings, absolute positioning, complex borders) cannot be achieved natively via component parameters, you MUST extract the styling into a dedicated CSS Isolation file (e.g., `ComponentName.razor.css`). Never inject `<style>` tags directly inside the `.razor` file.

5. **STRICT DESIGN TOKEN USAGE:** Inside `.razor.css` files, hardcoded color hex codes (e.g., `#FFFFFF`, `red`) and arbitrary pixel values for systemic spacings/radii are completely banned. You must EXCLUSIVELY use the project's injected CSS Custom Properties (Design Tokens) to ensure automatic, seamless Light/Dark mode compatibility. 
   - *Allowed examples:* `var(--pdm-primary)`, `var(--pdm-bg-surface)`, `var(--pdm-radius-md)`, `var(--pdm-text-muted)`, `var(--pdm-border)`.

6. **CLEAN & TARGETED CSS SELECTORS:** Maintain highly modular and specific CSS Isolation. Assign semantic, BEM-like CSS classes to your components (e.g., `Class="pdm-toolbar-actions"`) instead of writing broad tag selectors. Only use the `::deep` combinator when it is strictly necessary to pierce a Fluent UI component's internal structure or Shadow DOM (e.g., `::deep fluent-search::part(control)`).

## Execution
When asked to fix, create, or refactor a component:
1. Thoroughly analyze the current code.
2. Strip away all raw HTML/inline CSS clutter.
3. Embrace the pure Fluent Design system.
4. Return the rigidly separated `.razor` file and its corresponding `.razor.css` file.