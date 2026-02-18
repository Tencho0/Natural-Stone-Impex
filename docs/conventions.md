# Conventions & Strict Rules

## DO NOT

- Do NOT install NuGet packages that are not listed in the tech stack without asking first.
- Do NOT create new database tables or columns not defined in `docs/database-schema.md` without asking first.
- Do NOT create API endpoints not defined in `docs/api-endpoints.md` without asking first.
- Do NOT use English text in any UI element — all user-facing text must be in Bulgarian.
- Do NOT expose EF Core entities directly in API responses — always use DTOs.
- Do NOT use `float` or `double` for monetary values — always use `decimal`.
- Do NOT use `.Result` or `.Wait()` — always use `async/await`.
- Do NOT hard-delete products — use soft delete (`IsActive = false`).
- Do NOT edit or delete invoices after creation — they are immutable.
- Do NOT decrement stock on order placement — only on order confirmation.
- Do NOT add authentication to public endpoints (catalog, product detail, place order).
- Do NOT create additional CSS files unless absolutely necessary — use Bootstrap 5 utility classes.
- Do NOT skip form validation — every form must validate inputs and show Bulgarian error messages.
- Do NOT leave TODO comments or placeholder implementations — complete every feature fully.

## ALWAYS

- Always store prices as `decimal(18,2)`.
- Always store stock quantities as `decimal(18,2)`.
- Always validate that `PriceWithVat == PriceWithoutVat + VatAmount` in product creation/editing.
- Always snapshot product prices into OrderItem when creating an order.
- Always use database transactions for operations that modify multiple tables (order confirmation, invoice creation).
- Always return consistent error format: `{ "error": "Bulgarian message" }`.
- Always include loading states and error handling in Blazor pages.
- Always format dates as DD.MM.YYYY for display.
- Always format currency as `XX.XX €` (space before €).
- Always add `[Authorize]` attribute to admin API endpoints.
- Always update `UpdatedAt` timestamp when modifying entities.
