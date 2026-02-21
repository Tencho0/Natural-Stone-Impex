# Epic 04: Product Management

## Description
Admin can create, view, edit, and soft-delete products. Each product belongs to a category and has prices (with/without ДДС), a unit of measurement, stock quantity, and an optional image. This is the most complex admin CRUD epic.

## Dependencies
- Epic 03 (Category Management) must be completed.

## Stories

---

### Story 4.1: Product Entity and Migration

**As** a developer, **I want** the Product entity in the database **so that** the shop can manage its inventory.

**Acceptance Criteria:**
- [ ] `Product` entity created with fields:
  - `Id` (int, PK)
  - `Name` (string, required, max 200)
  - `Description` (string, nullable, max 2000)
  - `CategoryId` (int, FK to Category)
  - `PriceWithoutVat` (decimal(18,2), required)
  - `VatAmount` (decimal(18,2), required)
  - `PriceWithVat` (decimal(18,2), required)
  - `Unit` (enum: Kg = 0, Sqm = 1)
  - `StockQuantity` (decimal(18,2), default 0)
  - `ImagePath` (string, nullable)
  - `IsActive` (bool, default true) — for soft delete
  - `CreatedAt` (DateTime)
  - `UpdatedAt` (DateTime)
- [ ] `UnitType` enum created in `Models/Entities/`
- [ ] Navigation property: `Product.Category`
- [ ] `DbSet<Product>` added to `AppDbContext`
- [ ] Unique constraint on `Name` + `CategoryId` combination
- [ ] EF Core migration created and applied
- [ ] Seed data: 5–8 sample products across different categories

**Tasks:**
- Create `Models/Entities/UnitType.cs` enum
- Create `Models/Entities/Product.cs`
- Configure entity relationships and constraints in `AppDbContext`
- Add seed products to `DbSeeder.cs`
- Create and apply migration

---

### Story 4.2: Product API Endpoints

**As** the admin, **I want** API endpoints to manage products **so that** I can perform all CRUD operations.

**Acceptance Criteria:**
- [ ] `GET /api/products` — list products (public, no auth)
  - Query params: `categoryId` (optional filter), `search` (optional name search), `page` (default 1), `pageSize` (default 12)
  - Returns: paginated response `{ items: [...], totalCount, page, pageSize, totalPages }`
  - Only returns active products (`IsActive = true`) for public requests
  - Admin requests (with valid token) see all products including inactive
- [ ] `GET /api/products/{id}` — get single product (public)
  - Returns full product details including category name
- [ ] `POST /api/products` — create product (admin only)
  - Request DTO with all product fields except Id, ImagePath, IsActive, timestamps
  - Validation: name required, category must exist, prices > 0, PriceWithVat == PriceWithoutVat + VatAmount
  - Returns: created product with HTTP 201
- [ ] `PUT /api/products/{id}` — update product (admin only)
  - Same validation as create
  - Returns: updated product with HTTP 200
- [ ] `DELETE /api/products/{id}` — soft-delete product (admin only)
  - Sets `IsActive = false`
  - Returns HTTP 204
- [ ] `POST /api/products/{id}/image` — upload product image (admin only)
  - Accepts `multipart/form-data` with image file
  - Validates: JPG/PNG only, max 5MB
  - Saves to `wwwroot/uploads/products/{id}_{filename}`
  - Updates product's `ImagePath`
  - Returns updated product with HTTP 200

**Tasks:**
- Create DTOs: `ProductDto`, `ProductListDto`, `CreateProductRequest`, `UpdateProductRequest`, `PaginatedResponse<T>`
- Create `Services/IProductService.cs` and `Services/ProductService.cs`
- Create `Controllers/ProductsController.cs`
- Implement image upload with file validation
- Create `wwwroot/uploads/products/` directory (ensure it's served as static files)
- Add pagination helper
- Test all endpoints via Swagger

---

### Story 4.3: Product List Page (Admin)

**As** the admin, **I want** to see all products in a table **so that** I can manage my inventory.

**Acceptance Criteria:**
- [ ] Page at `/admin/products` with title "Продукти"
- [ ] Table columns: Снимка (thumbnail 50x50), Име, Категория, Цена с ДДС, Мерна ед., Наличност, Действия
- [ ] Category filter dropdown above the table
- [ ] Search input for product name
- [ ] Pagination (20 items per page)
- [ ] "Добави продукт" button → navigates to `/admin/products/new`
- [ ] Edit button → navigates to `/admin/products/{id}/edit`
- [ ] Delete button → confirmation dialog, then soft-delete
- [ ] Inactive (deleted) products shown with a visual indicator (e.g., strikethrough or "Неактивен" badge) — or hidden with a toggle filter
- [ ] Stock quantity shown with color coding: red if ≤ 10, orange if ≤ 50, green otherwise
- [ ] Loading spinner while data loads

**Tasks:**
- Create `Services/IProductService.cs` and `Services/ProductService.cs` in Blazor client
- Create `Pages/Admin/Products.razor`
- Implement table with Bootstrap styling
- Add category filter dropdown (calls category service)
- Add search input with debounce
- Add pagination component
- Implement delete with confirmation
- Add stock quantity color coding

---

### Story 4.4: Product Form Page (Admin — Add/Edit)

**As** the admin, **I want** a form to add or edit products **so that** I can manage product details, prices, and images.

**Acceptance Criteria:**
- [ ] Add page at `/admin/products/new`, Edit page at `/admin/products/{id}/edit`
- [ ] Same form component used for both (pre-filled for edit)
- [ ] Form fields (all labels in Bulgarian):
  - Име на продукта (text input, required)
  - Категория (dropdown, required)
  - Описание (textarea, optional)
  - Цена без ДДС (decimal input, required)
  - ДДС (decimal input, required)
  - Цена с ДДС (decimal input, required, auto-calculated but editable)
  - Мерна единица (dropdown: кг / м²)
  - Налично количество (decimal input, required for new products)
  - Снимка (file input with preview)
- [ ] Auto-calculation: when "Цена без ДДС" or "ДДС" changes, "Цена с ДДС" auto-updates
- [ ] Validation: PriceWithVat must equal PriceWithoutVat + VatAmount (show error if not)
- [ ] Image preview: show current image (for edit) or selected file preview (for new)
- [ ] On save: redirect to product list with success message
- [ ] On cancel: redirect back to product list
- [ ] Loading state during save
- [ ] Validation errors displayed per field (Bootstrap `is-invalid` classes)

**Tasks:**
- Create `Pages/Admin/ProductForm.razor` (shared add/edit component)
- Implement `EditForm` with `DataAnnotationsValidator`
- Add auto-calculation logic for VAT
- Implement image upload (separate API call after product creation)
- Add image preview
- Handle both create and edit modes
- Wire up navigation and success/error messages
