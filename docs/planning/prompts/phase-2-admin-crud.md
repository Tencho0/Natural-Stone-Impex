# Phase 2: Admin CRUD — Exact Claude Code Prompts

## Prerequisites

- Phase 1 completed and committed
- Both projects build and run
- Admin login working
- Fresh Claude Code session (don't continue from Phase 1 session)

---

## Session 2.1 — Category Entity + API (Epic 03, Stories 3.1–3.2)

### Prompt 1 (Plan)
```
Read docs/conventions.md, docs/database-schema.md (Category section only),
docs/api-endpoints.md (Categories section only), and
planning/epics/03-category-management.md.

I want to implement Stories 3.1 and 3.2 — Category entity with seed data
and all Category API endpoints.

Before writing any code, tell me:
- What will the Category entity look like?
- What seed categories will you create?
- What DTOs will you create?
- What validation will each endpoint have?
- What error messages (in Bulgarian) will be returned?

Don't write any code yet.
```

> **Wait for response. Verify it matches database-schema.md and api-endpoints.md exactly. Key things to check:**
> - Entity has: Id, Name (unique, max 100), CreatedAt, UpdatedAt
> - Seed data includes: Натурален камък, Цимент, Пясък и чакъл, Плочки, Инструменти
> - GET /api/categories returns productCount
> - DELETE returns error if category has products
> - Error messages are in Bulgarian
>
> **Correct anything wrong, then proceed:**

### Prompt 2 (Execute)
```
Proceed with the implementation. Specifically:

Story 3.1 — Category Entity:
- Create Models/Entities/Category.cs: Id (int PK), Name (nvarchar 100,
  required, unique), CreatedAt (datetime2), UpdatedAt (datetime2)
- Add navigation property: ICollection<Product> Products (empty for now,
  will be used later — just declare it)
- Add DbSet<Category> to AppDbContext
- Configure in OnModelCreating: unique index on Name, max length 100
- Create migration named "AddCategory" and apply it
- Seed 5 categories in DbSeeder.cs: "Натурален камък", "Цимент",
  "Пясък и чакъл", "Плочки", "Инструменти"
- Skip seeding if categories already exist

Story 3.2 — Category API Endpoints:
- Create Models/DTOs/CategoryDto.cs: { Id, Name, ProductCount, CreatedAt }
- Create Models/DTOs/CreateCategoryRequest.cs: { Name } with DataAnnotations
  (Required, MinLength(2), MaxLength(100))
- Create Models/DTOs/UpdateCategoryRequest.cs: same as Create
- Create Services/ICategoryService.cs and Services/CategoryService.cs with methods:
  GetAllAsync(), GetByIdAsync(id), CreateAsync(request), UpdateAsync(id, request),
  DeleteAsync(id)
- ProductCount should be calculated in the query (Count of related products)
- Create Controllers/CategoriesController.cs:
  - GET /api/categories — public, no auth
  - POST /api/categories — [Authorize]
  - PUT /api/categories/{id} — [Authorize]
  - DELETE /api/categories/{id} — [Authorize]
- Error messages in Bulgarian:
  - Duplicate name: "Категория с това име вече съществува."
  - Not found: "Категорията не е намерена."
  - Has products: "Категорията не може да бъде изтрита, защото съдържа продукти."
- Register CategoryService in Program.cs DI container
```

### Verify
```bash
dotnet ef database update --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Api
```

Test via Swagger:
- [ ] GET /api/categories → returns 5 seeded categories with productCount: 0
- [ ] POST /api/categories with `{"name":"Тръби"}` → returns 201 with new category
- [ ] POST /api/categories with `{"name":"Тръби"}` again → returns 400 duplicate error in Bulgarian
- [ ] POST /api/categories with `{"name":"A"}` → returns 400 validation error (too short)
- [ ] PUT /api/categories/6 with `{"name":"Тръби и фитинги"}` → returns 200
- [ ] PUT /api/categories/999 → returns 404 in Bulgarian
- [ ] DELETE /api/categories/6 → returns 204
- [ ] DELETE /api/categories/999 → returns 404 in Bulgarian
- [ ] All write endpoints without token → return 401

### Commit
```bash
git add .
git commit -m "Epic 03: Stories 3.1-3.2 — Category entity, seed data, API endpoints"
```

---

## Session 2.2 — Category Admin Page (Epic 03, Story 3.3)

### Prompt 1
```
Read docs/conventions.md and planning/epics/03-category-management.md.

Implement Story 3.3 — Category Management Admin Page.

Create the Blazor client-side service and admin page:

1. Client Services:
- Create Services/ICategoryService.cs and Services/CategoryService.cs
  in the Blazor client project
- Methods: GetAllAsync(), CreateAsync(name), UpdateAsync(id, name),
  DeleteAsync(id)
- All methods call the API endpoints via HttpClient
- Handle error responses and return Bulgarian error messages

2. Admin Page at /admin/categories:
- Page title: "Категории"
- @attribute [Authorize]
- Table with columns: №, Име, Брой продукти, Действия
- "Добави категория" button above the table — opens a Bootstrap modal
- Modal contains: "Име" text input, "Запази" button, "Отказ" button
- Edit button per row — opens same modal pre-filled with current name
- Delete button per row — shows Bootstrap confirmation modal:
  "Сигурни ли сте, че искате да изтриете категория '{name}'?"
- If delete fails (has products): show Bootstrap alert-danger with the
  error message from the API
- Success operations: show Bootstrap alert-success that auto-dismisses
  after 3 seconds
- Table refreshes after every operation
- Loading spinner while data is loading initially

All text in Bulgarian. Use Bootstrap 5 classes for all styling —
no custom CSS.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Login → navigate to /admin/categories
- [ ] Table shows 5 seeded categories with product count 0
- [ ] Click "Добави категория" → modal opens
- [ ] Enter "Тръби" → save → table refreshes, new category appears, success alert shown
- [ ] Try adding "Тръби" again → error alert: duplicate name
- [ ] Click edit on "Тръби" → modal opens with "Тръби" pre-filled
- [ ] Change to "Тръби и фитинги" → save → table updated
- [ ] Click delete on "Тръби и фитинги" → confirmation modal appears
- [ ] Confirm delete → category removed, success alert
- [ ] Loading spinner appears on page load
- [ ] Without login, /admin/categories redirects to login

### Commit
```bash
git add .
git commit -m "Epic 03: Story 3.3 — Category admin page with full CRUD"
```

---

## Session 2.3 — Product Entity + Seed Data (Epic 04, Story 4.1)

### Prompt 1 (Plan)
```
Read docs/conventions.md, docs/database-schema.md (Product section and
Enums section), and planning/epics/04-product-management.md.

I want to implement Story 4.1 — Product entity, UnitType enum, migration,
and seed data.

Before writing any code, tell me:
- What will the UnitType enum look like?
- What will the Product entity look like (all fields)?
- What EF Core configuration will you add in OnModelCreating?
- What seed products will you create?
- How will Product relate to Category?

Don't write any code yet.
```

> **Wait for response. Key things to verify:**
> - UnitType enum: Kg = 0, Sqm = 1
> - All fields match database-schema.md exactly (especially decimal(18,2) precision)
> - Unique constraint on Name + CategoryId
> - FK to Category with DeleteBehavior.Restrict
> - Index on IsActive
> - Seed products span multiple categories
>
> **Correct if needed, then:**

### Prompt 2 (Execute)
```
Proceed with the implementation:

- Create Models/Entities/UnitType.cs enum: Kg = 0, Sqm = 1
- Create Models/Entities/Product.cs with ALL fields from docs/database-schema.md:
  Id, Name (max 200), Description (max 2000, nullable), CategoryId (FK),
  PriceWithoutVat (decimal 18,2), VatAmount (decimal 18,2),
  PriceWithVat (decimal 18,2), Unit (UnitType enum), StockQuantity (decimal 18,2,
  default 0), ImagePath (max 500, nullable), IsActive (bool, default true),
  CreatedAt, UpdatedAt
- Navigation property: Category
- Add ICollection<Product> Products to Category entity if not already there
- Add DbSet<Product> to AppDbContext
- Configure in OnModelCreating:
  - All decimal fields: HasPrecision(18, 2)
  - Unique index on (Name, CategoryId)
  - Index on IsActive
  - FK to Category with DeleteBehavior.Restrict
- Create migration named "AddProduct" and apply it
- Seed 5 products in DbSeeder.cs (from database-schema.md):
  - Гранит сив (Натурален камък, 25/5/30 €, Sqm, 150)
  - Мрамор бял (Натурален камък, 40/8/48 €, Sqm, 80)
  - Цимент 25кг (Цимент, 5/1/6 €, Kg, 500)
  - Пясък фин (Пясък и чакъл, 0.08/0.02/0.10 €, Kg, 2000)
  - Гранитогрес 60x60 (Плочки, 15/3/18 €, Sqm, 200)
- Seed products only if none exist yet. Look up CategoryId dynamically
  from seeded categories.
```

### Verify
```bash
dotnet ef database update --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Api
```

Check database:
- [ ] Products table exists with all columns and correct types
- [ ] 5 seed products exist with correct prices and stock quantities
- [ ] CategoryId correctly references the right categories
- [ ] Unique index on (Name, CategoryId) exists

Check that category endpoint still works:
- [ ] GET /api/categories → productCount now shows correct numbers (2 for Натурален камък, etc.)

### Commit
```bash
git add .
git commit -m "Epic 04: Story 4.1 — Product entity, UnitType enum, migration, seed data"
```

---

## Session 2.4 — Product API Endpoints (Epic 04, Story 4.2)

### Prompt 1
```
Read docs/conventions.md, docs/api-endpoints.md (Products section), and
planning/epics/04-product-management.md.

Implement Story 4.2 — all Product API endpoints.

Create the following DTOs in Models/DTOs/:
- ProductDto: Id, Name, Description, CategoryId, CategoryName,
  PriceWithoutVat, VatAmount, PriceWithVat, Unit (int), UnitDisplay (string —
  "кг" for Kg, "м²" for Sqm), StockQuantity, ImagePath, IsActive,
  CreatedAt, UpdatedAt
- ProductListDto: same as ProductDto but without Description
  (used in paginated list)
- CreateProductRequest: Name, Description, CategoryId, PriceWithoutVat,
  VatAmount, PriceWithVat, Unit, StockQuantity — with DataAnnotations validation
- UpdateProductRequest: same fields as Create
- PaginatedResponse<T>: Items (List<T>), TotalCount, Page, PageSize, TotalPages
  — make this generic and reusable

Create Services/IProductService.cs and Services/ProductService.cs:
- GetAllAsync(categoryId?, search, page, pageSize, includeInactive)
  - Public calls: only active products (IsActive = true)
  - Admin calls: all products
  - Returns PaginatedResponse<ProductListDto>
- GetByIdAsync(id) → ProductDto
- CreateAsync(request) → ProductDto
  - Validate: PriceWithVat == PriceWithoutVat + VatAmount, else return
    error "Цената с ДДС трябва да е равна на цената без ДДС + ДДС."
  - Validate: category exists
  - Validate: Name + CategoryId unique, else "Продукт с това име вече
    съществува в тази категория."
- UpdateAsync(id, request) → ProductDto (same validations)
- DeleteAsync(id) — soft delete, set IsActive = false
- UploadImageAsync(id, file) → string (imagePath)
  - Validate: JPG/PNG only, max 5MB
  - Save to wwwroot/uploads/products/{id}_{originalFilename}
  - Delete old image if exists
  - Error: "Позволени са само JPG и PNG файлове до 5MB."

Create Controllers/ProductsController.cs:
- GET /api/products — public (check if request has valid auth token to
  decide whether to include inactive products)
- GET /api/products/{id} — public
- POST /api/products — [Authorize]
- PUT /api/products/{id} — [Authorize]
- DELETE /api/products/{id} — [Authorize]
- POST /api/products/{id}/image — [Authorize], accepts multipart/form-data
- GET /api/products/low-stock?threshold=10 — [Authorize]

Make sure wwwroot/uploads/products/ directory exists and static files are
configured to serve from wwwroot in Program.cs.

Register ProductService in DI container.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
```

Test via Swagger:
- [ ] GET /api/products → returns 5 products with pagination (page 1, pageSize 12)
- [ ] GET /api/products?categoryId=1 → returns only Натурален камък products
- [ ] GET /api/products?search=гранит → returns Гранит сив and Гранитогрес
- [ ] GET /api/products/1 → returns full product detail with CategoryName and UnitDisplay
- [ ] POST /api/products with valid data → returns 201
- [ ] POST /api/products with PriceWithVat != PriceWithoutVat + VatAmount → returns 400 with Bulgarian error
- [ ] POST /api/products with duplicate Name+CategoryId → returns 400 with Bulgarian error
- [ ] PUT /api/products/1 with valid data → returns 200
- [ ] DELETE /api/products/1 → returns 204, product IsActive is now false
- [ ] GET /api/products → deleted product NOT in results (public)
- [ ] POST /api/products/1/image with JPG file → returns 200 with imagePath
- [ ] POST /api/products/1/image with .exe file → returns 400 with Bulgarian error
- [ ] GET /api/products/low-stock?threshold=100 → returns products with stock ≤ 100
- [ ] All write endpoints without token → 401

### Commit
```bash
git add .
git commit -m "Epic 04: Story 4.2 — Product API endpoints with image upload"
```

---

## Session 2.5 — Product List Admin Page (Epic 04, Story 4.3)

### Prompt 1
```
Read docs/conventions.md and planning/epics/04-product-management.md.

Implement Story 4.3 — Product List Page in the admin panel.

1. Client Services:
- Create Services/IProductService.cs and Services/ProductService.cs in
  the Blazor client
- Methods: GetAllAsync(categoryId?, search, page, pageSize),
  GetByIdAsync(id), CreateAsync(request), UpdateAsync(id, request),
  DeleteAsync(id), UploadImageAsync(id, file)
- For image upload, use MultipartFormDataContent with HttpClient

2. Reusable Pagination Component:
- Create Components/Pagination.razor
- Props: CurrentPage, TotalPages, OnPageChanged (EventCallback<int>)
- Bootstrap pagination styling
- Shows: Previous, page numbers, Next
- Disables Previous on page 1, Next on last page

3. Product List Page at /admin/products:
- Page title: "Продукти"
- @attribute [Authorize]
- "Добави продукт" button → navigates to /admin/products/new
- Filter row above table:
  - Category dropdown (loads from CategoryService): "Всички категории" + list
  - Search input for product name with 300ms debounce
- Table columns:
  - Снимка (50x50 thumbnail, placeholder if no image)
  - Име
  - Категория
  - Цена с ДДС (formatted as "XX.XX €")
  - Мерна ед. ("кг" or "м²")
  - Наличност — color coded:
    - Red text (text-danger) if ≤ 10
    - Orange text (text-warning) if ≤ 50
    - Green text (text-success) if > 50
  - Действия: Edit button (navigates to /admin/products/{id}/edit),
    Delete button
- Delete button: confirmation modal "Сигурни ли сте, че искате да
  деактивирате продукт '{name}'?" — on confirm, calls soft-delete
- Pagination (20 per page) using Pagination component
- Loading spinner while data loads
- Empty state: "Няма намерени продукти." if no results

All text in Bulgarian. Use Bootstrap 5 classes only.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:
- [ ] Login → /admin/products shows product table
- [ ] 5 seed products visible with correct data
- [ ] Category dropdown filter works (selecting "Цимент" shows only cement products)
- [ ] Search works (typing "гранит" filters to matching products)
- [ ] Stock color coding correct (2000 green, 80 green, etc.)
- [ ] Prices formatted as "XX.XX €"
- [ ] Units show "кг" or "м²"
- [ ] Delete shows confirmation → product disappears from list
- [ ] "Добави продукт" button navigates to /admin/products/new (placeholder OK for now)
- [ ] Pagination works if you have enough products

### Commit
```bash
git add .
git commit -m "Epic 04: Story 4.3 — Product list admin page with filters and pagination"
```

---

## Session 2.6 — Product Form Page (Epic 04, Story 4.4)

### Prompt 1
```
Read docs/conventions.md and planning/epics/04-product-management.md.

Implement Story 4.4 — Product Add/Edit Form Page.

Create Pages/Admin/ProductForm.razor that handles BOTH add and edit:
- Route /admin/products/new → add mode (empty form)
- Route /admin/products/{id:int}/edit → edit mode (pre-filled form)
- @attribute [Authorize]
- Page title: "Нов продукт" (add) or "Редактиране на продукт" (edit)

Form fields (all labels in Bulgarian, use EditForm with DataAnnotationsValidator):
- Име на продукта (text input, required, min 2 chars)
- Категория (dropdown populated from CategoryService, required)
- Описание (textarea, optional, max 2000 chars)
- Цена без ДДС (decimal input, required, min 0.01)
- ДДС (decimal input, required, min 0)
- Цена с ДДС (decimal input, required, min 0.01)
  — AUTO-CALCULATE: when "Цена без ДДС" or "ДДС" changes, automatically
  set "Цена с ДДС" = PriceWithoutVat + VatAmount
  — The field is still editable, but if the user manually changes it to
  a value != PriceWithoutVat + VatAmount, show validation error:
  "Цената с ДДС трябва да е равна на цената без ДДС + ДДС."
- Мерна единица (dropdown: "кг" / "м²", required)
- Налично количество (decimal input, required, min 0)
- Снимка (file input):
  - In edit mode: show current image if exists
  - On file select: show preview of selected file
  - Accept only .jpg, .png
  - Upload happens as a separate API call after product save

Form buttons:
- "Запази" (primary button) — save the product
- "Отказ" → navigate back to /admin/products

Save flow:
1. Validate all fields
2. If add mode: POST /api/products → get new product ID
3. If edit mode: PUT /api/products/{id}
4. If image selected: POST /api/products/{id}/image
5. On success: navigate to /admin/products with success message
6. On error: show Bootstrap alert-danger with error from API

Loading state on save button. Validation errors shown per field using
Bootstrap is-invalid classes and validation-message styling.

All text in Bulgarian. Bootstrap 5 classes only.
```

### Verify
```bash
dotnet run --project src/NaturalStoneImpex.Api
dotnet run --project src/NaturalStoneImpex.Client
```

Test in browser:

**Add product:**
- [ ] Navigate to /admin/products → click "Добави продукт"
- [ ] Form shows all fields, category dropdown populated
- [ ] Enter Цена без ДДС: 10, ДДС: 2 → Цена с ДДС auto-fills to 12
- [ ] Try to submit empty form → validation errors shown in Bulgarian
- [ ] Fill all fields, select image → click "Запази"
- [ ] Redirected to product list, new product appears
- [ ] Image visible as thumbnail in the list

**Edit product:**
- [ ] Click edit on a product → form pre-filled with all data
- [ ] Current image shown
- [ ] Change the name → save → redirected to list, name updated
- [ ] Upload new image → save → image updated

**Validation:**
- [ ] Set Цена с ДДС to wrong value → error shown
- [ ] Try duplicate Name + Category → error from API shown
- [ ] Cancel button → back to product list without saving

### Commit
```bash
git add .
git commit -m "Epic 04: Story 4.4 — Product add/edit form with image upload and VAT calculation"
```

---

## Phase 2 Complete ✅

At this point you should have:
- ✅ Category CRUD (entity, API, admin page with modal)
- ✅ Product CRUD (entity, API, admin list with filters, add/edit form)
- ✅ Image upload working
- ✅ VAT auto-calculation working
- ✅ Soft delete for products
- ✅ Pagination component (reusable)
- ✅ 5 seed categories + 5 seed products in database

Update planning/overview.md:
```markdown
| 03 | Category Management         | ✅ Completed  | Epic 02           |
| 04 | Product Management          | ✅ Completed  | Epic 03           |
```

```bash
git add planning/overview.md
git commit -m "Update planning status: Phase 2 complete"
```

**Next**: Phase 3 — Public Catalog, Cart, and Checkout. Start a fresh Claude Code session.

---

## Troubleshooting

### If category dropdown in product form is empty:
```
The category dropdown in ProductForm.razor is empty. Make sure it calls
the CategoryService to load all categories on initialization (OnInitializedAsync)
and populates the dropdown options. The API endpoint GET /api/categories
is public and should not require authentication.
```

### If image upload fails:
```
Image upload is failing. Check:
1. Is Program.cs configured to serve static files from wwwroot? (app.UseStaticFiles())
2. Does wwwroot/uploads/products/ directory exist?
3. Is the multipart form data being sent correctly from the Blazor client?
4. Is CORS configured to allow the request?
Show me the current image upload implementation in both the API controller
and the Blazor client service.
```

### If VAT auto-calculation doesn't work:
```
The VAT auto-calculation is not working. When I change "Цена без ДДС"
or "ДДС", the "Цена с ДДС" field should auto-update to their sum.
Use a property setter or an OnInput event handler to trigger the
recalculation. Show me the current implementation.
```

### If decimal inputs don't accept decimals:
```
The decimal input fields are not accepting decimal values (e.g., 25.50).
Make sure the input type is "number" with step="0.01" and the model
property is decimal. In Blazor, use InputNumber<decimal> component.
```

### If product list shows deleted products:
```
The product list is showing soft-deleted products (IsActive = false).
The public GET /api/products endpoint should only return active products.
Check that the ProductService filters by IsActive == true for public
requests. The admin list may optionally show inactive products with
a visual indicator.
```
