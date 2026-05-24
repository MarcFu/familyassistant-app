# Contributing to FamilyAssist

Thanks for considering a contribution! This document explains how to get started.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A Home Assistant instance (for testing HA integration)
- Optional: [Ollama](https://ollama.ai/) (for AI icon generation testing)

## Getting Started

```bash
# Clone the repo
git clone https://github.com/your-repo/familyassist.git
cd familyassist

# Restore and run
cd src/FamilyAssist
dotnet run
```

The app starts at `http://localhost:5115`.

### Configuration for Development

Use .NET User Secrets for sensitive config:

```bash
cd src/FamilyAssist
dotnet user-secrets init
dotnet user-secrets set "HomeAssistant:BaseUrl" "http://your-ha-instance:8123"
dotnet user-secrets set "HomeAssistant:Token" "your-long-lived-access-token"
```

## Running Tests

```bash
dotnet test
```

Tests use an in-memory database (no HA instance required).

## Project Structure

```
src/FamilyAssist/          # Main application
  Components/              # Blazor pages, dialogs, layout
  Data/                    # EF Core context
  Models/                  # Domain models
  Services/                # Business logic + HA integration
  Migrations/              # EF Core migrations
tests/FamilyAssist.Tests/  # Unit tests (xUnit)
```

## Coding Conventions

See [AGENTS.md](AGENTS.md) for detailed project rules. Key points:

- **Enums** stored in DB must have explicit numeric values
- **New enum values** always appended at the end (never inserted)
- **Debug endpoints** must be wrapped in `if (app.Environment.IsDevelopment())`
- **Antiforgery disabled** only when technically required (document the reason)
- **UI pattern:** Outlined cards (no elevation), pill-shaped buttons, FABs only on list pages
- **Bug tracking:** Sequential IDs (`BUG-001`), commit format `fix(BUG-003): description`

## Making Changes

1. Create a feature branch from `main`
2. Make your changes
3. Run tests: `dotnet test`
4. Run build: `dotnet build`
5. If you changed models, create a migration: `dotnet ef migrations add <Name>`
6. Submit a pull request

## What to Contribute

- Bug fixes (see issue tracker)
- Translations / i18n support
- Additional schedule types
- Test coverage improvements
- Documentation improvements

## Code of Conduct

Be respectful and constructive. This is a family project.
