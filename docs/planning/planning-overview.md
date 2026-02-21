# Planning Overview — Natural Stone Impex

## Build Order & Epic Status

Epics are listed in recommended implementation order. Each epic should be completed before starting the next (dependencies flow downward).

| #  | Epic                        | Status      | Dependencies      |
|----|-----------------------------|-------------|--------------------|
| 01 | Project Setup & Scaffolding | ⬜ Not Started | None              |
| 02 | Authentication              | ⬜ Not Started | Epic 01           |
| 03 | Category Management         | ⬜ Not Started | Epic 02           |
| 04 | Product Management          | ⬜ Not Started | Epic 03           |
| 05 | Public Catalog & Product Detail | ⬜ Not Started | Epic 04       |
| 06 | Cart & Checkout             | ⬜ Not Started | Epic 05           |
| 07 | Order Management (Admin)    | ⬜ Not Started | Epic 06           |
| 08 | Invoice & Delivery Management | ⬜ Not Started | Epic 04         |
| 09 | Landing Page & Contacts     | ⬜ Not Started | Epic 01           |
| 10 | Receipt Printing            | ⬜ Not Started | Epic 07           |

## Status Legend
- ⬜ Not Started
- 🔧 In Progress
- ✅ Completed
- ⏸️ Blocked

## Notes
- Epic 08 (Invoices) depends on Epic 04 (Products) but NOT on Epic 06/07 (Orders). It can be built in parallel with Epics 05–07 if desired.
- Epic 09 (Landing & Contacts) is independent and can be built anytime after Epic 01.
- Epic 10 (Receipt) requires the order detail view from Epic 07.

## How to Use with Claude Code
1. Open a Claude Code session.
2. Say: "Read CLAUDE.md and planning/epics/XX-epic-name.md. Implement Story X.X."
3. Complete one story at a time. Test before moving to the next.
4. Update the status in this file and check off acceptance criteria in the epic file as you go.
