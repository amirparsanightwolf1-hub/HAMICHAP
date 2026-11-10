# Sales-first app-wide design system: implementation and verification

## Intent
The existing **Sales Request and Quotation UI is the visual reference for the ERP application**. This work is not just a CSS file move: shared controls and screens throughout the product should use the Sales look where appropriate.

Use the Sales navy (#0d1741), light canvas (#f6f8fc), white card surfaces, soft borders, restrained shadows, consistent field labels, accessible focus rings and rounded 40-42px controls. This theme takes precedence over generic Bootstrap primary styling.

Preserve business semantics: success/warning/danger status colors and badges must remain recognizable. Preserve specialized layouts (Production Planning calendar, reconnect dialog, artwork tools, Home logo display) and responsive/RTL behavior.

## Implemented
- All **nine** remaining `.razor.css` stylesheets from the earlier layout branch were migrated into reusable `wwwroot/css` modules, with no isolated originals left.
- Global shared `sales-theme.css` is loaded last through the app.css entry point. It applies to navigation, page headers, cards, forms, labels, buttons, badges, tables and keyboard focus.
- Bootstrap primary utility/variant colors are mapped to Sales navy; green/red/yellow semantic variants stay intact.
- Inventory Documents, Items and Locations now explicitly use the shared `erp-page-header`. The Documents list also uses Sales-style search and table panels.
- Administration Users, Roles and Dashboard now explicitly use the shared page header; shared forms and tables are styled globally.
- Production Workflow uses the shared page header; existing Production Orders, Customers and Sales sections already use `erp-page-header` and gain its unified appearance.
- The navigation sidebar uses Sales navy; Sales pages themselves retain their existing page-specific rules where appropriate.
- Feature-specific `wwwroot/sheetbuilder/styles.css` remains separate to avoid breaking its specialized interface.

## Reusable markup
For regular management pages, use:
```razor
<div class="page-container">
    <div class="erp-page-header">
        <div>
            <h1 class="erp-page-title">Page title</h1>
            <p class="erp-page-subtitle">Optional description</p>
        </div>
        <button class="btn btn-primary">Primary action</button>
    </div>
    <div class="card">
        <div class="card-header">Section</div>
        <div class="card-body">...</div>
    </div>
</div>
```
Reuse `form-label`, `form-control`, `form-select`, `table`, and `btn` classes; do not introduce fresh hardcoded primary blues or new one-off card shadows.

## Verification
- [x] All ten shared CSS modules referenced in `app.css` exist.
- [x] No `.razor.css` files remain on the refactor branch.
- [x] CSS block delimiters are balanced in each module (static check).
- [ ] Build with the .NET 10 SDK and run automated tests.
- [ ] Compare Sales Requests create/edit/list and Quotations list/edit with screenshots before/after.
- [ ] Review Inventory Documents, Items, Locations, Stock, Customers, Administration Users/Roles, Production Orders/Workflow and Home.
- [ ] Verify Production Planning remains functional and visually clear.
- [ ] Verify anonymous identity forms, reconnect dialog, responsive sidebar, mobile card tables and sheet builder.
- [ ] Verify Persian RTL pages, focus visibility, keyboard navigation, disabled controls and reduced motion.
- [ ] Check browser console and network panel for stylesheet errors.
- [ ] Test responsive breakpoints at phone, tablet and desktop widths.
- [ ] Audit app-wide selector collisions caused by removing CSS isolation.

**Build and browser tests have not been run.** Keep the PR in draft until validated. This PR targets `refactor/unified-layout-styles`; merge/rebase that dependency before `main`.
