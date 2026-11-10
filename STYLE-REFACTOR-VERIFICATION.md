# Layout and CSS refactor verification

The former `NavMenu.razor` and `NavMenu.razor.css` have been consolidated into `MainLayout.razor` and `MainLayout.razor.css`.

Global styles load through `wwwroot/app.css` in this order:
1. `css/foundation.css` (design tokens, base, links)
2. `css/components.css` (forms, buttons, cards, tables, badges)
3. `css/pages.css` (page utilities, dashboard, validation, print)
4. `css/overlays-responsive.css` (inventory alert, searchable select, mobile tables)

Other page-level `.razor.css` files remain component-scoped to avoid unexpected styling changes.

## Before merging
- [ ] Run `dotnet restore` and `dotnet build` on .NET 10.
- [ ] Confirm `app.css` imports resolve (no 404 responses).
- [ ] Confirm navigation renders correctly for administrator and restricted-permission accounts.
- [ ] Confirm anonymous and signed-out states do not show protected links.
- [ ] Test mobile menu open, backdrop close, and navigation-triggered close.
- [ ] Check desktop/mobile layout, English/Persian and RTL direction.
- [ ] Check inventory alert toast, searchable selects, responsive tables, and print view.
- [ ] Run any available automated tests.
