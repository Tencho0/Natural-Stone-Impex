# API Endpoints — Natural Stone Impex

## Overview

Base URL: `https://localhost:5001/api`

All endpoints return JSON. All request bodies are JSON (`Content-Type: application/json`) unless specified otherwise (image upload uses `multipart/form-data`).

**Authentication**: Admin endpoints require `Authorization: Bearer {jwt_token}` header. Public endpoints require no authentication.

**Error Response Format** (consistent across all endpoints):
```json
{
  "error": "Bulgarian error message here"
}
```

**Pagination Response Format** (for all list endpoints):
```json
{
  "items": [],
  "totalCount": 100,
  "page": 1,
  "pageSize": 20,
  "totalPages": 5
}
```

**Date Format**: All dates are ISO 8601 in API (`"2026-02-18T14:30:00Z"`). The Blazor client formats them as `DD.MM.YYYY` for display.

**Decimal Format**: All monetary values and quantities are returned as numbers with 2 decimal places (e.g., `24.00`, not `24`).

---

## 1. Health Check

### GET /api/health

Returns API status. Used for connectivity verification.

**Auth**: None

**Response 200:**
```json
{
  "status": "ok",
  "timestamp": "2026-02-18T14:30:00Z"
}
```

---

## 2. Authentication

### POST /api/auth/login

Authenticates the admin user and returns a JWT token.

**Auth**: None

**Request Body:**
```json
{
  "username": "admin",
  "password": "Admin123!"
}
```

**Validation:**
- `username`: required
- `password`: required

**Response 200 (success):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-02-18T22:30:00Z"
}
```

**Response 401 (invalid credentials):**
```json
{
  "error": "Невалидно потребителско име или парола."
}
```

---

## 3. Categories

### GET /api/categories

Returns all categories. Used by both public catalog (category filter) and admin panel.

**Auth**: None

**Response 200:**
```json
[
  {
    "id": 1,
    "name": "Натурален камък",
    "productCount": 12,
    "createdAt": "2026-01-15T10:00:00Z"
  },
  {
    "id": 2,
    "name": "Цимент",
    "productCount": 5,
    "createdAt": "2026-01-15T10:00:00Z"
  }
]
```

---

### POST /api/categories

Creates a new category.

**Auth**: Admin (Bearer token)

**Request Body:**
```json
{
  "name": "Тръби"
}
```

**Validation:**
- `name`: required, min 2 characters, max 100 characters, must be unique

**Response 201 (created):**
```json
{
  "id": 6,
  "name": "Тръби",
  "productCount": 0,
  "createdAt": "2026-02-18T14:30:00Z"
}
```

**Response 400 (validation error):**
```json
{
  "error": "Категория с това име вече съществува."
}
```

---

### PUT /api/categories/{id}

Updates an existing category.

**Auth**: Admin

**Request Body:**
```json
{
  "name": "Тръби и фитинги"
}
```

**Validation:**
- Same as POST
- Category with `{id}` must exist

**Response 200 (updated):**
```json
{
  "id": 6,
  "name": "Тръби и фитинги",
  "productCount": 0,
  "createdAt": "2026-01-15T10:00:00Z"
}
```

**Response 404:**
```json
{
  "error": "Категорията не е намерена."
}
```

---

### DELETE /api/categories/{id}

Deletes a category. Fails if the category contains products.

**Auth**: Admin

**Response 204 (deleted):** No content.

**Response 400 (has products):**
```json
{
  "error": "Категорията не може да бъде изтрита, защото съдържа продукти."
}
```

**Response 404:**
```json
{
  "error": "Категорията не е намерена."
}
```

---

## 4. Products

### GET /api/products

Returns a paginated list of products. Public access returns only active products. Admin access (with token) returns all products including inactive.

**Auth**: None (public) / Optional Admin (sees inactive too)

**Query Parameters:**
| Param      | Type   | Default | Notes                          |
|------------|--------|---------|--------------------------------|
| categoryId | int    | null    | Filter by category             |
| search     | string | null    | Search by product name (contains, case-insensitive) |
| page       | int    | 1       | Page number                    |
| pageSize   | int    | 12      | Items per page (max 50)        |

**Response 200:**
```json
{
  "items": [
    {
      "id": 1,
      "name": "Гранит сив",
      "categoryId": 1,
      "categoryName": "Натурален камък",
      "priceWithoutVat": 25.00,
      "vatAmount": 5.00,
      "priceWithVat": 30.00,
      "unit": 1,
      "unitDisplay": "м²",
      "stockQuantity": 150.00,
      "imagePath": "/uploads/products/1_granit.jpg",
      "isActive": true
    }
  ],
  "totalCount": 45,
  "page": 1,
  "pageSize": 12,
  "totalPages": 4
}
```

---

### GET /api/products/{id}

Returns full product details.

**Auth**: None

**Response 200:**
```json
{
  "id": 1,
  "name": "Гранит сив",
  "description": "Висококачествен сив гранит за облицовка и настилка.",
  "categoryId": 1,
  "categoryName": "Натурален камък",
  "priceWithoutVat": 25.00,
  "vatAmount": 5.00,
  "priceWithVat": 30.00,
  "unit": 1,
  "unitDisplay": "м²",
  "stockQuantity": 150.00,
  "imagePath": "/uploads/products/1_granit.jpg",
  "isActive": true,
  "createdAt": "2026-01-15T10:00:00Z",
  "updatedAt": "2026-02-10T08:00:00Z"
}
```

**Response 404:**
```json
{
  "error": "Продуктът не е намерен."
}
```

---

### POST /api/products

Creates a new product.

**Auth**: Admin

**Request Body:**
```json
{
  "name": "Гранит сив",
  "description": "Висококачествен сив гранит за облицовка и настилка.",
  "categoryId": 1,
  "priceWithoutVat": 25.00,
  "vatAmount": 5.00,
  "priceWithVat": 30.00,
  "unit": 1,
  "stockQuantity": 150.00
}
```

**Validation:**
- `name`: required, min 2, max 200
- `categoryId`: required, must reference existing category
- `priceWithoutVat`: required, > 0
- `vatAmount`: required, ≥ 0
- `priceWithVat`: required, > 0, must equal `priceWithoutVat + vatAmount`
- `unit`: required, 0 (Kg) or 1 (Sqm)
- `stockQuantity`: required, ≥ 0
- Unique constraint: `name` + `categoryId` combination must be unique

**Response 201:** Full product object (same shape as GET /api/products/{id}).

**Response 400 (validation):**
```json
{
  "error": "Цената с ДДС трябва да е равна на цената без ДДС + ДДС."
}
```

---

### PUT /api/products/{id}

Updates an existing product.

**Auth**: Admin

**Request Body:** Same shape as POST (all fields required).

**Response 200:** Updated product object.

**Response 404:**
```json
{
  "error": "Продуктът не е намерен."
}
```

---

### DELETE /api/products/{id}

Soft-deletes a product (sets `IsActive = false`).

**Auth**: Admin

**Response 204:** No content.

**Response 404:**
```json
{
  "error": "Продуктът не е намерен."
}
```

---

### POST /api/products/{id}/image

Uploads a product image. Replaces the existing image if one exists.

**Auth**: Admin

**Request**: `multipart/form-data` with a single file field named `image`.

**Validation:**
- File required
- Allowed types: `image/jpeg`, `image/png`
- Max size: 5MB (5,242,880 bytes)

**Response 200:**
```json
{
  "imagePath": "/uploads/products/1_granit.jpg"
}
```

**Response 400:**
```json
{
  "error": "Позволени са само JPG и PNG файлове до 5MB."
}
```

---

### GET /api/products/low-stock

Returns products with stock at or below a threshold. Used for admin dashboard alerts.

**Auth**: Admin

**Query Parameters:**
| Param     | Type | Default | Notes               |
|-----------|------|---------|---------------------|
| threshold | decimal | 10   | Stock level threshold |

**Response 200:**
```json
[
  {
    "id": 3,
    "name": "Цимент 25кг",
    "categoryName": "Цимент",
    "stockQuantity": 5.00,
    "unit": 0,
    "unitDisplay": "кг"
  }
]
```

---

## 5. Orders

### POST /api/orders

Places a new customer order. Public endpoint — no auth.

**Auth**: None

**Request Body:**
```json
{
  "customerType": 0,
  "deliveryMethod": 1,
  "customerInfo": {
    "fullName": "Иван Петров",
    "phone": "+359888123456",
    "address": "ул. Витоша 15, София"
  },
  "items": [
    {
      "productId": 1,
      "quantity": 5.00
    },
    {
      "productId": 3,
      "quantity": 100.00
    }
  ]
}
```

**Company customer example:**
```json
{
  "customerType": 1,
  "deliveryMethod": 1,
  "customerInfo": {
    "companyName": "Строй ЕООД",
    "eik": "123456789",
    "mol": "Георги Димитров",
    "contactPerson": "Мария Иванова",
    "contactPhone": "+359899987654",
    "address": "бул. България 100, Пловдив"
  },
  "items": [
    {
      "productId": 1,
      "quantity": 20.00
    }
  ]
}
```

**Validation:**
- `customerType`: required, 0 or 1
- `deliveryMethod`: required, 0 or 1
- `items`: required, at least 1 item
- Each item: `productId` must exist and be active, `quantity` > 0
- If `customerType == 0` (Individual): `fullName` and `phone` required
- If `customerType == 1` (Company): `companyName`, `eik`, `mol`, `contactPerson`, `contactPhone` required
- If `deliveryMethod == 1` (Delivery): `address` required
- `eik`: must be 9 or 13 digits

**Business Logic:**
- Status set to `Pending`
- OrderNumber auto-generated (format: `NSI-YYYYMMDD-XXXX`)
- Product prices **snapshotted** into OrderItem at current values
- Stock is **NOT** decremented (happens on confirmation)

**Response 201:**
```json
{
  "orderNumber": "NSI-20260218-0001",
  "message": "Вашата поръчка е приета успешно."
}
```

**Response 400:**
```json
{
  "error": "Продукт 'Гранит сив' не е наличен."
}
```

---

### GET /api/orders

Returns paginated list of all orders.

**Auth**: Admin

**Query Parameters:**
| Param    | Type   | Default | Notes                                  |
|----------|--------|---------|----------------------------------------|
| status   | int    | null    | Filter: 0=Pending, 1=Confirmed, 2=Completed |
| page     | int    | 1       |                                        |
| pageSize | int    | 20      | Max 50                                 |

**Response 200:**
```json
{
  "items": [
    {
      "id": 1,
      "orderNumber": "NSI-20260218-0001",
      "createdAt": "2026-02-18T14:30:00Z",
      "customerName": "Иван Петров",
      "customerType": 0,
      "customerTypeDisplay": "Физическо лице",
      "deliveryMethod": 1,
      "deliveryMethodDisplay": "Доставка",
      "status": 0,
      "statusDisplay": "Чакаща",
      "isCancelled": false,
      "totalWithVat": 750.00,
      "itemCount": 2
    }
  ],
  "totalCount": 25,
  "page": 1,
  "pageSize": 20,
  "totalPages": 2
}
```

**Note on `customerName`**: Returns `FullName` for individuals, `CompanyName` for companies.

---

### GET /api/orders/{id}

Returns full order details.

**Auth**: Admin

**Response 200:**
```json
{
  "id": 1,
  "orderNumber": "NSI-20260218-0001",
  "status": 0,
  "statusDisplay": "Чакаща",
  "customerType": 0,
  "customerTypeDisplay": "Физическо лице",
  "deliveryMethod": 1,
  "deliveryMethodDisplay": "Доставка",
  "deliveryFee": null,
  "isCancelled": false,
  "createdAt": "2026-02-18T14:30:00Z",
  "confirmedAt": null,
  "completedAt": null,
  "customerInfo": {
    "fullName": "Иван Петров",
    "phone": "+359888123456",
    "address": "ул. Витоша 15, София",
    "companyName": null,
    "eik": null,
    "mol": null,
    "contactPerson": null,
    "contactPhone": null
  },
  "items": [
    {
      "id": 1,
      "productId": 1,
      "productName": "Гранит сив",
      "quantity": 5.00,
      "unitPriceWithoutVat": 25.00,
      "vatAmount": 5.00,
      "unitPriceWithVat": 30.00,
      "unit": 1,
      "unitDisplay": "м²",
      "rowTotalWithoutVat": 125.00,
      "rowVatTotal": 25.00,
      "rowTotalWithVat": 150.00
    },
    {
      "id": 2,
      "productId": 3,
      "productName": "Цимент 25кг",
      "quantity": 100.00,
      "unitPriceWithoutVat": 5.00,
      "vatAmount": 1.00,
      "unitPriceWithVat": 6.00,
      "unit": 0,
      "unitDisplay": "кг",
      "rowTotalWithoutVat": 500.00,
      "rowVatTotal": 100.00,
      "rowTotalWithVat": 600.00
    }
  ],
  "subtotalWithoutVat": 625.00,
  "totalVat": 125.00,
  "subtotalWithVat": 750.00,
  "grandTotal": 750.00
}
```

**Note on `grandTotal`**: `subtotalWithVat + deliveryFee` (if deliveryFee is set). When deliveryFee is null, `grandTotal == subtotalWithVat`.

**Response 404:**
```json
{
  "error": "Поръчката не е намерена."
}
```

---

### PUT /api/orders/{id}/delivery-fee

Sets or updates the delivery fee for an order.

**Auth**: Admin

**Request Body:**
```json
{
  "deliveryFee": 25.00
}
```

**Validation:**
- `deliveryFee`: required, ≥ 0
- Order must exist
- Order `DeliveryMethod` must be `Delivery` (1)
- Order must not be cancelled

**Response 200:**
```json
{
  "message": "Цената за доставка е зададена.",
  "deliveryFee": 25.00,
  "grandTotal": 775.00
}
```

**Response 400:**
```json
{
  "error": "Цена за доставка може да се задава само за поръчки с доставка."
}
```

---

### PUT /api/orders/{id}/confirm

Confirms a pending order. **Decrements stock** for all order items.

**Auth**: Admin

**Request Body:** None (empty body or `{}`).

**Validation:**
- Order must exist
- Order status must be `Pending` (0)
- Order must not be cancelled
- **Stock check**: for each item, `Product.StockQuantity >= OrderItem.Quantity`. If any product has insufficient stock, return error with details.

**Business Logic (within a database transaction):**
1. Verify all stock levels are sufficient.
2. For each OrderItem: `Product.StockQuantity -= OrderItem.Quantity`
3. Set `Order.Status = Confirmed`
4. Set `Order.ConfirmedAt = DateTime.UtcNow`
5. Save all changes.

**Response 200:**
```json
{
  "message": "Поръчката е потвърдена.",
  "orderNumber": "NSI-20260218-0001",
  "status": 1,
  "statusDisplay": "Потвърдена"
}
```

**Response 400 (insufficient stock):**
```json
{
  "error": "Недостатъчна наличност за следните продукти:",
  "details": [
    {
      "productName": "Цимент 25кг",
      "ordered": 100.00,
      "available": 50.00,
      "unit": "кг"
    }
  ]
}
```

**Response 400 (wrong status):**
```json
{
  "error": "Само чакащи поръчки могат да бъдат потвърдени."
}
```

---

### PUT /api/orders/{id}/complete

Marks a confirmed order as completed.

**Auth**: Admin

**Request Body:** None.

**Validation:**
- Order must exist
- Order status must be `Confirmed` (1)
- Order must not be cancelled

**Business Logic:**
1. Set `Order.Status = Completed`
2. Set `Order.CompletedAt = DateTime.UtcNow`

**Response 200:**
```json
{
  "message": "Поръчката е маркирана като завършена.",
  "orderNumber": "NSI-20260218-0001",
  "status": 2,
  "statusDisplay": "Завършена"
}
```

**Response 400:**
```json
{
  "error": "Само потвърдени поръчки могат да бъдат завършени."
}
```

---

### PUT /api/orders/{id}/cancel

Cancels a pending order. Does NOT affect stock.

**Auth**: Admin

**Request Body:** None.

**Validation:**
- Order must exist
- Order status must be `Pending` (0) — cannot cancel confirmed orders (stock already decremented)
- Order must not be already cancelled

**Response 200:**
```json
{
  "message": "Поръчката е отказана.",
  "orderNumber": "NSI-20260218-0001"
}
```

**Response 400:**
```json
{
  "error": "Само чакащи поръчки могат да бъдат отказани."
}
```

---

### GET /api/orders/stats

Returns order count statistics for the admin dashboard.

**Auth**: Admin

**Response 200:**
```json
{
  "totalProducts": 45,
  "pendingOrders": 5,
  "confirmedOrders": 3,
  "completedOrders": 120
}
```

---

### GET /api/orders/recent

Returns the most recent orders for the admin dashboard.

**Auth**: Admin

**Query Parameters:**
| Param | Type | Default | Notes         |
|-------|------|---------|---------------|
| count | int  | 5       | Max 20        |

**Response 200:**
```json
[
  {
    "id": 25,
    "orderNumber": "NSI-20260218-0001",
    "createdAt": "2026-02-18T14:30:00Z",
    "customerName": "Иван Петров",
    "status": 0,
    "statusDisplay": "Чакаща",
    "totalWithVat": 750.00
  }
]
```

---

## 6. Invoices

### GET /api/invoices

Returns a paginated list of all invoices.

**Auth**: Admin

**Query Parameters:**
| Param    | Type | Default | Notes    |
|----------|------|---------|----------|
| page     | int  | 1       |          |
| pageSize | int  | 20      | Max 50   |

**Response 200:**
```json
{
  "items": [
    {
      "id": 1,
      "supplierName": "Гранит БГ ООД",
      "invoiceNumber": "F-2026-0145",
      "invoiceDate": "2026-02-15T00:00:00Z",
      "entryDate": "2026-02-16T09:00:00Z",
      "totalItems": 3,
      "totalQuantity": 500.00,
      "invoiceTotal": 8500.00
    }
  ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

---

### GET /api/invoices/{id}

Returns full invoice details.

**Auth**: Admin

**Response 200:**
```json
{
  "id": 1,
  "supplierName": "Гранит БГ ООД",
  "invoiceNumber": "F-2026-0145",
  "invoiceDate": "2026-02-15T00:00:00Z",
  "entryDate": "2026-02-16T09:00:00Z",
  "items": [
    {
      "id": 1,
      "productId": 1,
      "productName": "Гранит сив",
      "unit": 1,
      "unitDisplay": "м²",
      "quantity": 200.00,
      "purchasePrice": 15.00,
      "rowTotal": 3000.00
    },
    {
      "id": 2,
      "productId": 2,
      "productName": "Мрамор бял",
      "unit": 1,
      "unitDisplay": "м²",
      "quantity": 100.00,
      "purchasePrice": 30.00,
      "rowTotal": 3000.00
    },
    {
      "id": 3,
      "productId": 4,
      "productName": "Пясък фин",
      "unit": 0,
      "unitDisplay": "кг",
      "quantity": 200.00,
      "purchasePrice": 12.50,
      "rowTotal": 2500.00
    }
  ],
  "invoiceTotal": 8500.00
}
```

**Response 404:**
```json
{
  "error": "Доставката не е намерена."
}
```

---

### POST /api/invoices

Creates a new invoice and **automatically increments stock** for each item.

**Auth**: Admin

**Request Body:**
```json
{
  "supplierName": "Гранит БГ ООД",
  "invoiceNumber": "F-2026-0145",
  "invoiceDate": "2026-02-15",
  "items": [
    {
      "productId": 1,
      "quantity": 200.00,
      "purchasePrice": 15.00
    },
    {
      "productId": 2,
      "quantity": 100.00,
      "purchasePrice": 30.00
    }
  ]
}
```

**Validation:**
- `supplierName`: required, min 2, max 200
- `invoiceNumber`: required, min 1, max 50
- `invoiceDate`: required, cannot be in the future
- `items`: required, at least 1 item
- Each item: `productId` must exist and be active, `quantity` > 0, `purchasePrice` ≥ 0

**Business Logic (within a database transaction):**
1. Create Invoice record with `EntryDate = DateTime.UtcNow`
2. Create InvoiceItem records
3. For each item: `Product.StockQuantity += item.Quantity`
4. Save all changes

**Response 201:**
```json
{
  "id": 2,
  "message": "Доставката е записана. Наличностите са обновени.",
  "supplierName": "Гранит БГ ООД",
  "invoiceNumber": "F-2026-0145"
}
```

**Response 400:**
```json
{
  "error": "Продукт с ID 99 не е намерен."
}
```

---

## HTTP Status Code Summary

| Code | Meaning                          | Used For                                  |
|------|----------------------------------|-------------------------------------------|
| 200  | OK                               | Successful GET, PUT                       |
| 201  | Created                          | Successful POST (create)                  |
| 204  | No Content                       | Successful DELETE                         |
| 400  | Bad Request                      | Validation errors, business rule violations|
| 401  | Unauthorized                     | Missing or invalid JWT token              |
| 404  | Not Found                        | Entity not found by ID                    |
| 500  | Internal Server Error            | Unhandled exceptions (via middleware)      |
