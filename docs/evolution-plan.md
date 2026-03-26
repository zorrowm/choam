# Finance Tracker Evolution Plan

## Context

Chris is moving into a shared apartment with another person. The Finance Tracker needs to evolve from a basic CRUD app into a usable household finance application. The plan is structured in 5 phases, each building on the previous.

## Current State

- **Frontend**: Vue 3 + Quasar 2 + TypeScript + Vite 7 + TanStack Vue Query
  - Single page (TransactionsPage) with month filtering, CRUD for transactions/categories
  - Summary cards (Income, Expense, Investment, Net Cash Flow)
  - Responsive (table desktop, cards mobile), dark mode
- **Backend**: ASP.NET Core 10 + EF Core 10 + PostgreSQL 16, Clean Architecture
  - Domain: Transaction (Title, Amount, Description, Date, Type, CategoryId), Category (Name)
  - Application: TransactionService, CategoryService, DTOs
  - Infrastructure: EF Core, Repositories
  - Presentation: Controllers, ExceptionHandlingMiddleware
- **Deployment**: K3s on Raspberry Pi, Flux CD GitOps, Traefik, Cloudflare Tunnel

## User Decisions

| Decision | Choice |
|----------|--------|
| Multi-User | Real accounts via Keycloak (OpenID Connect) |
| Mobile | PWA (Progressive Web App) |
| Charts | Category breakdown + Monthly trend |
| AI Receipt Scanning | Ollama on private PC (RTX 5080, 16GB VRAM) with abstract AI service interface |

---

## Phase 1: Dashboard & Visualization

**Goal**: Add a Dashboard page with charts so users can see spending patterns at a glance.
**Complexity**: Small-Medium | **Backend changes**: None

### Chart Library

**Apache ECharts** via `vue-echarts` — handles dark mode theming natively, good mobile touch support, tree-shakeable.

```bash
npm install echarts vue-echarts
```

### New Files (Frontend)

| File | Purpose |
|------|---------|
| `src/pages/DashboardPage.vue` | Dashboard with MonthPicker, summary cards, charts |
| `src/components/charts/CategoryBreakdownChart.vue` | Donut chart — expenses grouped by category |
| `src/components/charts/MonthlyTrendChart.vue` | Bar chart — Income vs Expense vs Investment per month |
| `src/composables/useDashboard.ts` | Computed chart data from raw transaction list |
| `src/styles/pages/_dashboard-page.scss` | Dashboard-specific styles |

### Modified Files

| File | Change |
|------|--------|
| `src/router/index.ts` | Add `/` → Dashboard (new default), `/transactions` → TransactionsPage |
| `src/layouts/MainLayout.vue` | Add navigation tabs (Dashboard, Transactions). On mobile: bottom nav or icon buttons |

### Data Flow

Reuses existing `useTransactions()` composable (TanStack Query deduplicates). Chart data computed client-side in `useDashboard.ts`. Fine for household scale (<10K transactions).

---

## Phase 2: PWA Setup

**Goal**: Make the app installable on phones from homescreen.
**Complexity**: Small | **Can be done in parallel with Phase 1**

### Approach

**vite-plugin-pwa** (not Quasar PWA mode, since project uses `@quasar/vite-plugin` not Quasar CLI).

```bash
npm install vite-plugin-pwa -D
```

### Modified Files

| File | Change |
|------|--------|
| `vite.config.ts` | Add VitePWA plugin: `registerType: 'prompt'`, manifest config, workbox patterns |
| `index.html` | Add `<meta name="theme-color">`, apple-touch-icon link |
| `nginx.conf` | Add `Service-Worker-Allowed` header, no-cache for sw.js |

### New Files

| File | Purpose |
|------|---------|
| `public/icons/icon-192.png`, `icon-512.png` | PWA icons |
| `src/composables/usePwaUpdate.ts` | Service worker update detection, "New version available" banner |

### Offline Strategy

- Cache app shell (HTML, JS, CSS) — app loads instantly from homescreen
- `NetworkFirst` strategy for API calls (stale-while-revalidate)
- Show "You are offline" when API calls fail (existing error states handle this)
- No full offline-first with IndexedDB (over-engineered for this stage)

---

## Phase 3: Receipt Scanning (AI)

**Goal**: Photo of receipt → AI extracts data → user reviews and confirms → transaction created.
**Complexity**: Large | **Depends on Phase 2 for mobile camera UX**

### AI Architecture

```
Phone (Camera) → Frontend (ImageCapture) → Backend (ReceiptController)
    → IAiService (abstract interface) → OllamaAiService → Ollama API (PC with RTX 5080)
    → ReceiptParseResult → ReceiptSuggestionDto → Frontend (Review Form)
    → User confirms → createTransaction()
```

**Abstract interface** allows swapping providers later (Ollama, Claude API, OpenAI, etc.).

### Ollama Setup

- **Model**: `llava:13b` (fits in 16GB VRAM, good vision quality)
- **Host**: Private PC with RTX 5080
- **Connectivity**: Expose via Cloudflare Tunnel (`ollama.chrispicloud.dev`) or LAN IP
- **Offline handling**: `GET /api/receipts/status` checks availability. If offline → user enters manually

### New Backend Files

| File | Layer | Purpose |
|------|-------|---------|
| `Domain/Interfaces/IAiService.cs` | Domain | Abstract AI interface: `ParseReceiptAsync(Stream, contentType)` |
| `Domain/Entities/ReceiptParseResult.cs` | Domain | Value object: StoreName, TotalAmount, Date, LineItems, Confidence |
| `Application/Interfaces/IReceiptService.cs` | Application | Receipt orchestration interface |
| `Application/Services/ReceiptService.cs` | Application | Calls IAiService, maps to suggestion DTO, matches categories |
| `Application/Dtos/ReceiptSuggestionDto.cs` | Application | Suggested transaction fields (all nullable) |
| `Infrastructure/AiServices/OllamaAiService.cs` | Infrastructure | Ollama HTTP client, base64 image, structured prompt, JSON parsing |
| `Infrastructure/AiServices/OllamaOptions.cs` | Infrastructure | Config: BaseUrl, Model, TimeoutSeconds |
| `Presentation/Controllers/ReceiptController.cs` | Presentation | `POST /api/receipts/parse` (multipart), `GET /api/receipts/status` |

### New Frontend Files

| File | Purpose |
|------|---------|
| `src/pages/ReceiptScanPage.vue` | Scan flow: capture → loading → review form → confirm |
| `src/components/ImageCapture.vue` | Camera/file upload with `<input capture="environment">` |
| `src/api/receiptService.ts` | `parseReceipt(File)` via FormData, `getAiStatus()` |
| `src/contracts/receipts.ts` | TypeScript types for ReceiptSuggestion, AiStatus |
| `src/composables/useReceipt.ts` | Receipt parsing mutation + AI status query |

### Modified Files

| File | Change |
|------|--------|
| `src/api/http.ts` | Add `apiFormData<T>()` helper (no Content-Type header) |
| `src/router/index.ts` | Add `/scan` → ReceiptScanPage |
| `src/layouts/MainLayout.vue` | Add "Scan" nav item (camera icon), prominent FAB on mobile |
| `appsettings.json` | Add `Ollama` config section |
| `Application/DependencyInjection.cs` | Register IReceiptService |
| `Infrastructure/DependencyInjection.cs` | Register IAiService → OllamaAiService, OllamaOptions |

### Infrastructure

- Add `Ollama__BaseUrl` env var to API deployment ConfigMap
- Optional: Cloudflare Tunnel on PC for Ollama API

---

## Phase 4: Authentication (Keycloak)

**Goal**: Real user accounts with login. Required before household features.
**Complexity**: Large

### Keycloak on K3s

- Image: `quay.io/keycloak/keycloak:latest` (ARM64 available)
- Realm: `financetracker`
- Clients: `financetracker-web` (public, PKCE), `financetracker-api` (bearer-only)
- Roles: `user`, `admin`
- RAM: 512MB-1GB (check Pi headroom)

### Backend Changes

| File | Change |
|------|--------|
| `Program.cs` | Add JWT Bearer auth (`AddAuthentication`, `AddJwtBearer`) |
| `appsettings.json` | Add Keycloak Authority/Audience config |
| `Transaction.cs` | Add `UserId` property (string, Keycloak sub claim) |
| `TransactionController.cs` | Add `[Authorize]`, extract user ID from claims |
| `CategoryController.cs` | Add `[Authorize]` |
| `ITransactionRepository.cs` | Add user-scoped methods |
| `TransactionService.cs` | Filter transactions by user |
| New: `ICurrentUserService` | Extract user from HttpContext (Application layer) |
| New: EF Migration | Add UserId column to Transactions |

### Frontend Changes

| File | Change |
|------|--------|
| New: `src/auth/authService.ts` | OIDC client (`oidc-client-ts`), login/logout/token refresh |
| New: `src/composables/useAuth.ts` | Reactive auth state |
| New: `src/pages/CallbackPage.vue` | OIDC redirect handler |
| `src/api/http.ts` | Attach Bearer token to all requests |
| `src/router/index.ts` | Auth navigation guard, callback route |
| `src/layouts/MainLayout.vue` | User avatar/name, logout dropdown |

```bash
npm install oidc-client-ts
```

### Infrastructure

- Keycloak K8s manifests (Deployment, Service, IngressRoute)
- Keycloak PostgreSQL (separate DB or schema)
- Cloudflare Tunnel config for Keycloak endpoint
- K8s secrets for Keycloak admin + DB credentials

---

## Phase 5: Household Features

**Goal**: Two people sharing expenses, split costs, recurring transactions.
**Complexity**: Large | **Depends on Phase 4**

### New Domain Entities

| Entity | Fields |
|--------|--------|
| `Household` | Id, Name, InviteCode |
| `HouseholdMember` | Id, HouseholdId, UserId, DisplayName, Role (Owner/Member) |
| `RecurringTransaction` | Id, Title, Amount, Type, CategoryId, Frequency, DayOfMonth, StartDate, EndDate, UserId, HouseholdId |
| `TransactionSplit` | Id, TransactionId, UserId, Amount, IsPaid |

### Modified Entities

| Entity | New Fields |
|--------|-----------|
| `Transaction` | HouseholdId (nullable), SplitType (None/Equal/Custom) |

### New Backend Services & Controllers

- `IHouseholdService` / `HouseholdController` — CRUD, join via invite code, member management
- `IRecurringTransactionService` / `RecurringTransactionController` — Templates + auto-generation
- `GET /api/transactions/balances` — Per-person balance summary (who owes whom)

### New Frontend Pages & Components

| File | Purpose |
|------|---------|
| `src/pages/HouseholdPage.vue` | Manage household, members, invite link |
| `src/pages/RecurringPage.vue` | Manage recurring transaction templates |
| `src/components/SplitSelector.vue` | Split type + custom amounts UI |
| `src/components/BalanceSummary.vue` | "You owe X" / "X owes you" card |

### Modified Frontend

- `TransactionForm.vue` — Add "shared" toggle + split config when in household
- `DashboardPage.vue` — Add balance summary, personal vs household view toggle

---

## Phase Dependency Graph

```
Phase 1 (Dashboard)  ──┐
                       ├── Can be done in parallel
Phase 2 (PWA)        ──┘

Phase 3 (Receipt AI) ── Depends on Phase 2 for mobile camera
                        (backend work can start independently)

Phase 4 (Auth)       ── Independent, but gates Phase 5

Phase 5 (Household)  ── Depends on Phase 4
```

## Recommended Order

1. **Phase 1 + Phase 2** in parallel — immediate usability
2. **Phase 3** — receipt scanning (killer feature for daily use)
3. **Phase 4** — authentication before inviting roommate
4. **Phase 5** — household features once both users are onboarded

## Complexity Summary

| Phase | Backend | Frontend | Infra | Total |
|-------|---------|----------|-------|-------|
| 1 - Dashboard | None | Medium | None | Small-Medium |
| 2 - PWA | None | Small | Small | Small |
| 3 - Receipt AI | Large | Medium | Small | Large |
| 4 - Auth | Large | Medium | Large | Large |
| 5 - Household | Large | Large | Small | Large |
