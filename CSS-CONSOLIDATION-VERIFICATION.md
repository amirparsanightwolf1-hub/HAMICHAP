# Sales-first global CSS consolidation

## Scope
- Migrated all nine remaining isolated `.razor.css` files from `refactor/unified-layout-styles` into shared `wwwroot/css` modules and removed the isolated originals.
- `wwwroot/app.css` remains the sole public style entry point for application styles.
- The design palette comes from the Sales Request edit page's navy, soft borders, card elevation and form focus treatment, rather than Bootstrap defaults.
- Explicit Bootstrap variable overrides and shared component rules live in `sales-theme.css`, loaded last.
- `wwwroot/sheetbuilder/styles.css` remains separate for the specialized sheet builder feature, and is not a Razor isolated stylesheet.

## CSS order
1. foundation.css
2. components.css
3. pages.css
4. overlays-responsive.css
5. layout.css
6. home.css
7. production.css
8. sales.css
9. reconnect.css
10. sales-theme.css

## Verification
- [ ] Build with the .NET 10 SDK.
- [ ] Validate all CSS imports resolve in browser developer tools.
- [ ] Compare Sales Requests create/edit/list and Quotations list/edit with screenshots before/after.
- [ ] Check Home, Production Planning, Reconnect dialog, MainLayout and mobile navigation.
- [ ] Verify RTL Persian layouts, keyboard focus and reduced motion.
- [ ] Confirm any files generating scoped CSS bundles no longer refer to deleted `.razor.css` files.
- [ ] Verify Bootstrap variants and disabled buttons visually.
- [ ] Review stylesheet selectors for future cross-page collisions now that component isolation has been removed.

This branch is based on `refactor/unified-layout-styles`, not `main`, and contains no new security remediation commits.
