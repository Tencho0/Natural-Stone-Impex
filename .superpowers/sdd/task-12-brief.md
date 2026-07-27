### Task 12: Entry points — navigation, product detail button, home promo

**Files:**
- Modify: `src/NaturalStoneImpex.Client/Layout/MainLayout.razor` (nav list, ~line 26–35)
- Modify: `src/NaturalStoneImpex.Client/Pages/Public/ProductDetail.razor`
- Modify: `src/NaturalStoneImpex.Client/Pages/Public/Home.razor`

- [ ] **Step 1: Navigation link**

In `src/NaturalStoneImpex.Client/Layout/MainLayout.razor`, after the «Каталог» `<li>` add:

```razor
                <li class="nav-item">
                    <NavLink class="nav-link" href="/visualizer">
                        Визуализатор
                    </NavLink>
                </li>
```

- [ ] **Step 2: Product detail button**

In `src/NaturalStoneImpex.Client/Pages/Public/ProductDetail.razor`, locate the add-to-cart block (`AddToCart` button area, around line 150 of the current file) and add below it, inside the same markup section:

```razor
            @if (_product.IsVisualizerEnabled)
            {
                <a class="btn btn-outline-primary mt-2" href="/visualizer?productId=@_product.Id">
                    Виж как ще изглежда при вас
                </a>
            }
```

(`IsVisualizerEnabled` exists on the client `ProductDto` since Task 9.)

- [ ] **Step 3: Home page promo**

In `src/NaturalStoneImpex.Client/Pages/Public/Home.razor`, after the existing hero/CTA section (adapt placement to the file's current structure — it is a short page), add:

```razor
<section class="my-4">
    <div class="card bg-light">
        <div class="card-body d-flex flex-wrap align-items-center justify-content-between gap-3">
            <div>
                <h5 class="card-title mb-1">Вижте настилката във вашия двор</h5>
                <p class="card-text mb-0">
                    Качете снимка на вашата алея или двор и разгледайте как ще изглежда с нашите естествени камъни.
                </p>
            </div>
            <a class="btn btn-primary" href="/visualizer">Опитай визуализатора</a>
        </div>
    </div>
</section>
```

- [ ] **Step 4: Build, verify, commit**

`dotnet build`, run both projects: nav shows «Визуализатор» on desktop + mobile hamburger; product detail of an enabled product shows the button and preselects that product in the visualizer; home промо links correctly.

```powershell
git add -A
git commit -m "feat(visualizer): navigation, product detail and home entry points"
```

---

