# Database Schema — Natural Stone Impex

## Overview

Database: **SQL Server** via **Entity Framework Core 8** (Code First approach).

All tables use `int` primary keys with identity (auto-increment). All monetary values use `decimal(18,2)`. All quantity values use `decimal(18,2)` to support fractional units (e.g., 2.5 кг, 3.75 м²).

---

## Entity Relationship Diagram

```
┌──────────────┐
│  AdminUser   │
└──────────────┘

┌──────────────┐       ┌──────────────┐
│   Category   │───1:N─│   Product    │
└──────────────┘       └──────┬───────┘
                              │
                    ┌─────────┼─────────┐
                    │ 1:N     │         │ 1:N
                    ▼         │         ▼
             ┌────────────┐   │   ┌─────────────┐
             │ OrderItem  │   │   │ InvoiceItem │
             └─────┬──────┘   │   └──────┬──────┘
                   │ N:1      │          │ N:1
                   ▼          │          ▼
             ┌────────────┐   │   ┌─────────────┐
             │   Order    │   │   │   Invoice   │
             └─────┬──────┘   │   └─────────────┘
                   │ 1:1
                   ▼
          ┌──────────────────┐
          │ OrderCustomerInfo│
          └──────────────────┘
```

**Relationships:**
- Category → Product: one-to-many
- Product → OrderItem: one-to-many (reference only — prices are snapshotted)
- Product → InvoiceItem: one-to-many
- Order → OrderItem: one-to-many
- Order → OrderCustomerInfo: one-to-one
- Invoice → InvoiceItem: one-to-many

---

## Enums

### UnitType
```csharp
public enum UnitType
{
    Kg = 0,    // кг (kilograms)
    Sqm = 1    // м² (square meters)
}
```
Stored as `int` in the database.

### CustomerType
```csharp
public enum CustomerType
{
    Individual = 0,  // Физическо лице
    Company = 1      // Фирма
}
```

### OrderStatus
```csharp
public enum OrderStatus
{
    Pending = 0,     // Чакаща
    Confirmed = 1,   // Потвърдена
    Completed = 2    // Завършена
}
```

### DeliveryMethod
```csharp
public enum DeliveryMethod
{
    Pickup = 0,    // Вземане от обекта
    Delivery = 1   // Доставка
}
```

---

## Tables

### AdminUser

Single admin account for the shop owner. Seeded on first run.

| Column       | Type           | Constraints                | Notes                     |
|--------------|----------------|----------------------------|---------------------------|
| Id           | int            | PK, Identity               |                           |
| Username     | nvarchar(50)   | Required, Unique           |                           |
| PasswordHash | nvarchar(500)  | Required                   | BCrypt hash               |
| CreatedAt    | datetime2      | Required, Default: UTC now |                           |

**EF Core Configuration:**
```csharp
entity.HasIndex(e => e.Username).IsUnique();
entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
```

---

### Category

Product categories managed by the admin.

| Column    | Type           | Constraints                | Notes                     |
|-----------|----------------|----------------------------|---------------------------|
| Id        | int            | PK, Identity               |                           |
| Name      | nvarchar(100)  | Required, Unique           | Bulgarian name            |
| CreatedAt | datetime2      | Required, Default: UTC now |                           |
| UpdatedAt | datetime2      | Required, Default: UTC now | Updated on every change   |

**EF Core Configuration:**
```csharp
entity.HasIndex(e => e.Name).IsUnique();
entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
```

**Seed Data:**
| Name              |
|-------------------|
| Натурален камък   |
| Цимент            |
| Пясък и чакъл     |
| Плочки            |
| Инструменти       |

---

### Product

Products in the shop catalog. Soft-deleted via `IsActive` flag.

| Column          | Type           | Constraints                       | Notes                              |
|-----------------|----------------|-----------------------------------|------------------------------------|
| Id              | int            | PK, Identity                      |                                    |
| Name            | nvarchar(200)  | Required                          |                                    |
| Description     | nvarchar(2000) | Nullable                          | Optional product description       |
| CategoryId      | int            | FK → Category.Id, Required        |                                    |
| PriceWithoutVat | decimal(18,2)  | Required, > 0                     | Price excluding ДДС in EUR         |
| VatAmount       | decimal(18,2)  | Required, ≥ 0                     | ДДС amount in EUR                  |
| PriceWithVat    | decimal(18,2)  | Required, > 0                     | Price including ДДС in EUR         |
| Unit            | int            | Required                          | Enum: 0 = Kg, 1 = Sqm             |
| StockQuantity   | decimal(18,2)  | Required, Default: 0, ≥ 0        | Current stock level                |
| ImagePath       | nvarchar(500)  | Nullable                          | Relative path to uploaded image    |
| IsActive        | bit            | Required, Default: true           | false = soft-deleted               |
| CreatedAt       | datetime2      | Required, Default: UTC now        |                                    |
| UpdatedAt       | datetime2      | Required, Default: UTC now        |                                    |

**EF Core Configuration:**
```csharp
entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
entity.Property(e => e.Description).HasMaxLength(2000);
entity.Property(e => e.PriceWithoutVat).HasPrecision(18, 2);
entity.Property(e => e.VatAmount).HasPrecision(18, 2);
entity.Property(e => e.PriceWithVat).HasPrecision(18, 2);
entity.Property(e => e.StockQuantity).HasPrecision(18, 2);
entity.Property(e => e.ImagePath).HasMaxLength(500);

entity.HasOne(e => e.Category)
      .WithMany(c => c.Products)
      .HasForeignKey(e => e.CategoryId)
      .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete

entity.HasIndex(e => new { e.Name, e.CategoryId }).IsUnique();
entity.HasIndex(e => e.IsActive); // For filtering active products
```

**Validation Rule:** `PriceWithVat == PriceWithoutVat + VatAmount` — enforced in the service layer, not the database.

**Seed Data:**
| Name               | Category        | PriceWithoutVat | VatAmount | PriceWithVat | Unit | StockQuantity |
|--------------------|-----------------|-----------------|-----------|--------------|------|---------------|
| Гранит сив         | Натурален камък | 25.00           | 5.00      | 30.00        | Sqm  | 150.00        |
| Мрамор бял         | Натурален камък | 40.00           | 8.00      | 48.00        | Sqm  | 80.00         |
| Цимент 25кг        | Цимент          | 5.00            | 1.00      | 6.00         | Kg   | 500.00        |
| Пясък фин          | Пясък и чакъл   | 0.08            | 0.02      | 0.10         | Kg   | 2000.00       |
| Гранитогрес 60x60  | Плочки          | 15.00           | 3.00      | 18.00        | Sqm  | 200.00        |

---

### Order

Customer orders placed through the storefront.

| Column         | Type           | Constraints                       | Notes                              |
|----------------|----------------|-----------------------------------|------------------------------------|
| Id             | int            | PK, Identity                      |                                    |
| OrderNumber    | nvarchar(20)   | Required, Unique                  | Format: NSI-YYYYMMDD-XXXX         |
| CustomerType   | int            | Required                          | Enum: 0 = Individual, 1 = Company |
| Status         | int            | Required, Default: 0              | Enum: 0 = Pending, 1 = Confirmed, 2 = Completed |
| DeliveryMethod | int            | Required                          | Enum: 0 = Pickup, 1 = Delivery    |
| DeliveryFee    | decimal(18,2)  | Nullable                          | Set by admin, null if not yet set  |
| IsCancelled    | bit            | Required, Default: false          | Separate from status flow          |
| CreatedAt      | datetime2      | Required, Default: UTC now        | When order was placed              |
| ConfirmedAt    | datetime2      | Nullable                          | When admin confirmed               |
| CompletedAt    | datetime2      | Nullable                          | When admin marked completed        |
| UpdatedAt      | datetime2      | Required, Default: UTC now        |                                    |

**EF Core Configuration:**
```csharp
entity.HasIndex(e => e.OrderNumber).IsUnique();
entity.Property(e => e.OrderNumber).HasMaxLength(20).IsRequired();
entity.Property(e => e.DeliveryFee).HasPrecision(18, 2);

entity.HasIndex(e => e.Status);        // For filtering by status
entity.HasIndex(e => e.CreatedAt);      // For sorting
```

**Order Number Generation Logic (Service Layer):**
```csharp
// Format: NSI-YYYYMMDD-XXXX
// Example: NSI-20260218-0001
// XXXX resets daily, sequential within the day
var today = DateTime.UtcNow.ToString("yyyyMMdd");
var prefix = $"NSI-{today}-";
var lastOrder = await _context.Orders
    .Where(o => o.OrderNumber.StartsWith(prefix))
    .OrderByDescending(o => o.OrderNumber)
    .FirstOrDefaultAsync();

int nextSequence = 1;
if (lastOrder != null)
{
    var lastSeq = int.Parse(lastOrder.OrderNumber.Substring(prefix.Length));
    nextSequence = lastSeq + 1;
}
var orderNumber = $"{prefix}{nextSequence:D4}";
```

---

### OrderCustomerInfo

Customer information associated with an order. One-to-one with Order. Fields are nullable because different customer types use different fields.

| Column        | Type           | Constraints                | Notes                              |
|---------------|----------------|----------------------------|------------------------------------|
| Id            | int            | PK, Identity               |                                    |
| OrderId       | int            | FK → Order.Id, Required, Unique | One-to-one relationship       |
| FullName      | nvarchar(200)  | Nullable                   | Individual: required               |
| Phone         | nvarchar(20)   | Nullable                   | Individual: required               |
| Address       | nvarchar(500)  | Nullable                   | Required if delivery selected      |
| CompanyName   | nvarchar(200)  | Nullable                   | Company: required                  |
| Eik           | nvarchar(13)   | Nullable                   | Company: required (9 or 13 digits) |
| Mol           | nvarchar(200)  | Nullable                   | Company: required                  |
| ContactPerson | nvarchar(200)  | Nullable                   | Company: required                  |
| ContactPhone  | nvarchar(20)   | Nullable                   | Company: required                  |

**EF Core Configuration:**
```csharp
entity.HasOne(e => e.Order)
      .WithOne(o => o.CustomerInfo)
      .HasForeignKey<OrderCustomerInfo>(e => e.OrderId)
      .OnDelete(DeleteBehavior.Cascade);

entity.HasIndex(e => e.OrderId).IsUnique();
entity.Property(e => e.FullName).HasMaxLength(200);
entity.Property(e => e.Phone).HasMaxLength(20);
entity.Property(e => e.Address).HasMaxLength(500);
entity.Property(e => e.CompanyName).HasMaxLength(200);
entity.Property(e => e.Eik).HasMaxLength(13);
entity.Property(e => e.Mol).HasMaxLength(200);
entity.Property(e => e.ContactPerson).HasMaxLength(200);
entity.Property(e => e.ContactPhone).HasMaxLength(20);
```

**Validation (Service Layer):**
- If `CustomerType == Individual`: `FullName` and `Phone` are required.
- If `CustomerType == Company`: `CompanyName`, `Eik`, `Mol`, `ContactPerson`, and `ContactPhone` are required.
- If `DeliveryMethod == Delivery`: `Address` is required regardless of customer type.

---

### OrderItem

Line items within an order. Stores a **snapshot** of product data at the time the order was placed — prices and product name are copied, not referenced.

| Column             | Type           | Constraints                       | Notes                              |
|--------------------|----------------|-----------------------------------|------------------------------------|
| Id                 | int            | PK, Identity                      |                                    |
| OrderId            | int            | FK → Order.Id, Required           |                                    |
| ProductId          | int            | FK → Product.Id, Required         | Reference for tracking, not pricing|
| ProductName        | nvarchar(200)  | Required                          | Snapshot — copied from product     |
| Quantity           | decimal(18,2)  | Required, > 0                     |                                    |
| UnitPriceWithoutVat| decimal(18,2)  | Required                          | Snapshot — copied from product     |
| VatAmount          | decimal(18,2)  | Required                          | Snapshot — copied from product     |
| UnitPriceWithVat   | decimal(18,2)  | Required                          | Snapshot — copied from product     |
| Unit               | int            | Required                          | Snapshot — enum value at order time|

**EF Core Configuration:**
```csharp
entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
entity.Property(e => e.Quantity).HasPrecision(18, 2);
entity.Property(e => e.UnitPriceWithoutVat).HasPrecision(18, 2);
entity.Property(e => e.VatAmount).HasPrecision(18, 2);
entity.Property(e => e.UnitPriceWithVat).HasPrecision(18, 2);

entity.HasOne(e => e.Order)
      .WithMany(o => o.Items)
      .HasForeignKey(e => e.OrderId)
      .OnDelete(DeleteBehavior.Cascade);

entity.HasOne(e => e.Product)
      .WithMany()
      .HasForeignKey(e => e.ProductId)
      .OnDelete(DeleteBehavior.Restrict); // Don't delete products with orders
```

**Computed Values (not stored, calculated in DTOs/service):**
- `RowTotalWithVat = Quantity × UnitPriceWithVat`
- `RowTotalWithoutVat = Quantity × UnitPriceWithoutVat`
- `RowVatTotal = Quantity × VatAmount`

---

### Invoice

Records of incoming stock deliveries from suppliers. Immutable after creation.

| Column       | Type           | Constraints                | Notes                              |
|--------------|----------------|----------------------------|------------------------------------|
| Id           | int            | PK, Identity               |                                    |
| SupplierName | nvarchar(200)  | Required                   | Name of the supplier               |
| InvoiceNumber| nvarchar(50)   | Required                   | Supplier's invoice number          |
| InvoiceDate  | datetime2      | Required                   | Date on the supplier's invoice     |
| EntryDate    | datetime2      | Required, Default: UTC now | When this was entered in the system|
| CreatedAt    | datetime2      | Required, Default: UTC now |                                    |

**EF Core Configuration:**
```csharp
entity.Property(e => e.SupplierName).HasMaxLength(200).IsRequired();
entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();

entity.HasIndex(e => e.EntryDate); // For sorting
```

**Note:** `InvoiceNumber` is NOT unique in the database — different suppliers may use the same invoice numbers. The combination of `SupplierName + InvoiceNumber` should be unique in practice, but this is not enforced at the DB level to avoid edge cases.

---

### InvoiceItem

Line items within an invoice. Each item references a product and records the delivered quantity and purchase price.

| Column        | Type           | Constraints                       | Notes                              |
|---------------|----------------|-----------------------------------|------------------------------------|
| Id            | int            | PK, Identity                      |                                    |
| InvoiceId     | int            | FK → Invoice.Id, Required         |                                    |
| ProductId     | int            | FK → Product.Id, Required         |                                    |
| Quantity      | decimal(18,2)  | Required, > 0                     | Quantity delivered                  |
| PurchasePrice | decimal(18,2)  | Required, ≥ 0                     | Per-unit price from supplier (EUR) |

**EF Core Configuration:**
```csharp
entity.Property(e => e.Quantity).HasPrecision(18, 2);
entity.Property(e => e.PurchasePrice).HasPrecision(18, 2);

entity.HasOne(e => e.Invoice)
      .WithMany(i => i.Items)
      .HasForeignKey(e => e.InvoiceId)
      .OnDelete(DeleteBehavior.Cascade);

entity.HasOne(e => e.Product)
      .WithMany()
      .HasForeignKey(e => e.ProductId)
      .OnDelete(DeleteBehavior.Restrict); // Don't delete products referenced in invoices
```

**Stock Update Logic (Service Layer):**
```csharp
// When creating an invoice, within a single transaction:
foreach (var item in request.Items)
{
    var product = await _context.Products.FindAsync(item.ProductId);
    product.StockQuantity += item.Quantity;
    product.UpdatedAt = DateTime.UtcNow;
}
await _context.SaveChangesAsync();
```

---

## Indexes Summary

| Table             | Index                              | Type   | Purpose                    |
|-------------------|------------------------------------|--------|----------------------------|
| AdminUser         | Username                           | Unique | Login lookup               |
| Category          | Name                               | Unique | Prevent duplicate names    |
| Product           | (Name, CategoryId)                 | Unique | Prevent duplicates per category |
| Product           | IsActive                           | Index  | Filter active products     |
| Order             | OrderNumber                        | Unique | Order lookup               |
| Order             | Status                             | Index  | Filter by status           |
| Order             | CreatedAt                          | Index  | Sort by date               |
| OrderCustomerInfo | OrderId                            | Unique | One-to-one enforcement     |
| Invoice           | EntryDate                          | Index  | Sort by date               |

---

## Cascade Delete Rules

| Parent → Child                  | On Delete       | Reason                                           |
|---------------------------------|-----------------|--------------------------------------------------|
| Category → Product              | Restrict        | Cannot delete category with products             |
| Product → OrderItem             | Restrict        | Cannot delete product with order history          |
| Product → InvoiceItem           | Restrict        | Cannot delete product with invoice history        |
| Order → OrderItem               | Cascade         | Deleting order removes its items                  |
| Order → OrderCustomerInfo       | Cascade         | Deleting order removes customer info              |
| Invoice → InvoiceItem           | Cascade         | Deleting invoice removes its items                |

**Note:** In practice, orders and invoices should never be hard-deleted. Products are soft-deleted. These cascade rules are a safety net.

---

## Timestamps Convention

All entities with `CreatedAt` fields:
- `CreatedAt` is set to `DateTime.UtcNow` on creation and never modified.
- `UpdatedAt` (where present) is set to `DateTime.UtcNow` on creation and updated on every modification.
- Timestamps are stored as `datetime2` in SQL Server (UTC).
- Display to users in Bulgarian format: `DD.MM.YYYY` (date only) or `DD.MM.YYYY HH:mm` (with time).

---

## Migration History (Planned)

| Migration Name            | Epic  | Entities Added/Modified                   |
|---------------------------|-------|-------------------------------------------|
| InitialCreate             | 01    | Empty (database creation)                 |
| AddAdminUser              | 02    | AdminUser                                 |
| AddCategory               | 03    | Category                                  |
| AddProduct                | 04    | Product                                   |
| AddOrderEntities          | 06    | Order, OrderCustomerInfo, OrderItem       |
| AddInvoiceEntities        | 08    | Invoice, InvoiceItem                      |

**Note:** This is the planned sequence. Actual migration names may vary. Each migration should be created when the corresponding epic is implemented and should only include the entities for that epic.
