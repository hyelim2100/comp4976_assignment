---
applyTo: "**/*.cshtml"
description: "Razor view conventions (Bootstrap 5, accessibility, images, print)"
---

# Razor view instructions

- Use Bootstrap 5 utility classes and the grid; pages must work at phone width
  (candidate grids use `row-cols-2 row-cols-md-3 row-cols-lg-4`).
- Use tag helpers (`asp-controller`, `asp-action`, `asp-route-*`, `asp-for`, `asp-items`) instead of
  hard-coded URLs. Tag-helper attribute values must not contain double quotes — cast ViewData into a
  local variable in the `@{ }` block first.
- Images: `Images.GetUrl(fileName) ?? "/images/placeholder-photo.svg"`; always set meaningful `alt` text.
- Reuse partials: `_CandidateCard`, `_MunicipalityCard`, `_GuideButton`.
- Every POST form gets the antiforgery token automatically via the form tag helper; keep it.
- Format dates explicitly, e.g. `@m.ElectionDate.ToString("dddd, MMMM d, yyyy")`.
- The printable voting guide uses `_PrintLayout` and `wwwroot/css/print.css`; elements that must not
  print get the `print-toolbar` class (hidden in `@media print`).
- Label every form control (`<label asp-for>` or `visually-hidden` labels for compact filters).
