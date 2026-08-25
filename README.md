# Library Lending

A small library management web app built with ASP.NET Core Razor Pages: browse a catalogue of books/DVDs, borrow and reserve items as a member, and manage the catalogue, loans, reservations, and users as an admin.

## Features

- **Catalogue browsing** — search and filter by title/author/ISBN/publisher, type, year range, and availability
- **Borrowing** — members can hold up to 3 active loans (configurable), with a 14-day loan period
- **Reservations** — reserve an out-of-stock item for pickup; holds expire automatically after 3 days via a background service
- **Loan history** — current loans, past loans, and an overdue view
- **Admin area** — catalogue CRUD, active loans / overdue report with "mark returned", pending reservations with fulfil/cancel, and user management (create users, toggle admin role, lock/unlock accounts)
- Cookie-based auth via ASP.NET Core Identity, rate-limited login/register, common-password rejection, and an accessible UI (skip link, focus rings, keyboard-friendly nav)

## Tech stack

- .NET 10 / ASP.NET Core Razor Pages
- PostgreSQL via EF Core (Npgsql)
- ASP.NET Core Identity
- Tailwind CSS v4 (compiled with the standalone CLI — no Node required)
- xUnit, integration tests run against a real Postgres container via Testcontainers

## Running locally

### Option A: Docker Compose (app + database)

```bash
cp .env.example .env   # fill in POSTGRES_* and optionally ADMIN_EMAIL / ADMIN_PASSWORD
docker compose up
```

The app is served at `http://localhost:8080`. Set `ADMIN_EMAIL` / `ADMIN_PASSWORD` in `.env` to seed an initial admin account on startup (Development environment only).

### Option B: Database in Docker, app on the host

```bash
docker compose up -d db
cd src/LendingLibrary.Web
dotnet run
```

Uses the connection string in `appsettings.Development.json` (`localhost:5432`). Migrations and admin seeding run automatically in Development. To seed an admin, set the `Admin__Email` / `Admin__Password` environment variables before `dotnet run`.

Tailwind CSS is compiled automatically as part of `dotnet build` (`Styles/app.css` → `wwwroot/css/app.css`) using the standalone CLI in `tools/`.

## Configuration

| Section | Key | Default | Purpose |
|---|---|---|---|
| `ConnectionStrings` | `Default` | — | Postgres connection string |
| `Lending` | `MaxActiveLoans` | 3 | Max simultaneous active loans per member |
| `Lending` | `LoanPeriodDays` | 14 | Loan duration |
| `Lending` | `ReservationHoldDays` | 3 | How long a reservation is held before auto-expiring |
| env | `Admin__Email` / `Admin__Password` | — | Bootstraps an admin account on first run (Development only); leave unset to skip |

## Tests

```bash
dotnet test tests/LendingLibrary.UnitTests
dotnet test tests/LendingLibrary.IntegrationTests   # requires Docker (spins up Postgres via Testcontainers)
```

## Project layout

```
src/LendingLibrary.Web/
  Pages/            Public Razor Pages (home, catalogue, loans, reservations, account)
  Areas/Admin/       Admin-only Razor Pages
  Domain/            Entities and enums
  Services/          Business logic (catalogue, lending, reservations, user admin)
  Infrastructure/    Cross-cutting concerns (seeding, rate limiting support, paging, reservation expiry)
  Data/              EF Core DbContext and migrations
  Styles/app.css     Tailwind source (compiled to wwwroot/css/app.css at build time)
tests/
  LendingLibrary.UnitTests/         Domain/logic unit tests
  LendingLibrary.IntegrationTests/  Full-stack tests against a real Postgres instance
```

## Deployment

The app is fully containerized (`src/LendingLibrary.Web/Dockerfile`), so it can be deployed anywhere that runs Docker images plus a Postgres database — e.g. Railway, Fly.io, or Azure App Service + Azure Database for PostgreSQL. Run the image once with `--migrate` to apply pending migrations before starting the app in a non-Development environment (see the `migrate` service in `docker-compose.yml`).
