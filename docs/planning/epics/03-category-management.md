# Epic 03: Category Management

## Description
Admin can create, view, edit, and delete product categories. Categories are used to organize products in the catalog. This is a simple CRUD epic that serves as a warm-up for the more complex product management.

## Dependencies
- Epic 02 (Authentication) must be completed.

## Stories

---

### Story 3.1: Category Entity and Migration

**As** a developer, **I want** the Category entity in the database **so that** products can be organized into categories.

**Acceptance Criteria:**
- [ ] `Category` entity created: `Id` (int, PK), `Name` (string, required, max 100, unique), `CreatedAt` (DateTime), `UpdatedAt` (DateTime)
- [ ] `DbSet<Category>` added to `AppDbContext`
- [ ] EF Core migration created and applied
- [ ] Seed data: 3–5 sample categories (e.g., "Натурален камък", "Цимент", "Пясък и чакъл", "Плочки", "Инструменти")

**Tasks:**
- Create `Models/Entities/Category.cs`
- Configure entity in `AppDbContext` (unique constraint on Name)
- Add seed categories to `DbSeeder.cs`
- Create and apply migration

---

### Story 3.2: Category API Endpoints

**As** the admin, **I want** API endpoints to manage categories **so that** the Blazor client can perform CRUD operations.

**Acceptance Criteria:**
- [ ] `GET /api/categories` — returns all categories (public, no auth required)
- [ ] `POST /api/categories` — creates a new category (admin only)
  - Request: `{ "name": "..." }`
  - Validation: name required, min 2 chars, unique
  - Returns: created category with HTTP 201
- [ ] `PUT /api/categories/{id}` — updates a category (admin only)
  - Request: `{ "name": "..." }`
  - Validation: same as create + category must exist
  - Returns: updated category with HTTP 200
- [ ] `DELETE /api/categories/{id}` — deletes a category (admin only)
  - If category has products: return HTTP 400 with error "Категорията не може да бъде изтрита, защото съдържа продукти."
  - If no products: delete and return HTTP 204
- [ ] All write endpoints require `[Authorize]`
- [ ] Proper error responses for validation failures

**Tasks:**
- Create `Models/DTOs/CategoryDto.cs`, `CreateCategoryRequest.cs`, `UpdateCategoryRequest.cs`
- Create `Services/ICategoryService.cs` and `Services/CategoryService.cs`
- Create `Controllers/CategoriesController.cs`
- Add input validation with DataAnnotations
- Test all endpoints via Swagger

---

### Story 3.3: Category Management Page (Admin)

**As** the admin, **I want** a page to manage categories **so that** I can add, edit, and delete categories from the browser.

**Acceptance Criteria:**
- [ ] Page at `/admin/categories` with title "Категории"
- [ ] Table displays all categories: №, Име, Брой продукти, Действия
- [ ] "Добави категория" button above the table
- [ ] Add/Edit uses a Bootstrap modal with a single "Име" input field
- [ ] Edit button (per row) opens the modal pre-filled with current name
- [ ] Delete button (per row) shows confirmation dialog: "Сигурни ли сте, че искате да изтриете категория '{name}'?"
- [ ] If delete fails (has products), show error alert: "Категорията не може да бъде изтрита, защото съдържа продукти."
- [ ] Success messages shown after add/edit/delete (Bootstrap alert, auto-dismiss)
- [ ] Table refreshes after any operation
- [ ] Loading spinner shown while data is loading

**Tasks:**
- Create `Services/ICategoryService.cs` and `Services/CategoryService.cs` in Blazor client
- Create `Pages/Admin/Categories.razor`
- Implement category table with Bootstrap styling
- Create modal component for add/edit form
- Implement delete with confirmation dialog
- Add loading states and error handling
- Wire up all CRUD operations to API
