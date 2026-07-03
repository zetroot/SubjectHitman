# AGENTS.md

Компонент учёта бесплатных кредитных отчётов (ЦБ 5791-У). ASP.NET Core 10 minimal API + Wolverine 6 (saga, RabbitMQ) + EF Core / PostgreSQL.

## Язык и стиль

- Код-комментарии и XML-доки — **на русском**; commit-сообщения — на английском (conventional commits: `feat(saga): ...`).
- `TreatWarningsAsErrors` + `GenerateDocumentationFile` включены глобально (`Directory.Build.props`): каждый public-член обязан иметь XML-док, иначе сборка падает.

## Команды

```bash
dotnet build SubjectHitman.slnx
dotnet test SubjectHitman.slnx                       # 63 unit + 11 integration
dotnet test test/SubjectHitman.UnitTests             # быстрые (<1 сек)
dotnet test test/SubjectHitman.IntegrationTests      # требуют Docker (Testcontainers)
dotnet test test/SubjectHitman.IntegrationTests --filter "FullyQualifiedName~ИмяТеста"
```

- Интеграционные тесты сами поднимают PostgreSQL и RabbitMQ через Testcontainers; docker-compose для них не нужен. Зелёный прогон ~20 сек; при падениях саговые тесты ждут таймауты — до 2.5 мин.
- Миграции EF: `dotnet ef migrations add <Name> --project src/SubjectHitman.DataAccess --startup-project src/SubjectHitman.Api` (есть `DesignTimeDbContextFactory` в DataAccess). Именование БД — snake_case через `EFCore.NamingConventions`.

## Границы проектов

- `src/SubjectHitman.Abstractions` — контракты (сообщения, DTO, `IReportStatusClient`). **Не должен ссылаться на Wolverine** — это осознанное ограничение, не «забытая» зависимость.
- `src/SubjectHitman.Domain` — сущности (`Entities/`) и интерфейсы репозиториев (`Repositories/`). **Не ссылается на Wolverine**. Чистые модели без ссылок на EF Core.
- `src/SubjectHitman.DataAccess` — `AppDbContext`, `IEntityTypeConfiguration<>` (в `Configurations/`), миграции, реализации репозиториев. Ссылается на `SubjectHitman.Domain`. **Не ссылается на Wolverine**.
- `src/SubjectHitman.Api` — единственный хост: HTTP + консюмеры + сага в одном процессе. Ссылается на `Domain`, `DataAccess`, `Abstractions`. Регистрация контекста — через `AddDbContextWithWolverineIntegration<AppDbContext>`, репозиториев — через `builder.Services.AddDataAccess()`.
- `src/SubjectHitman.ReportStatusMock` — мок статус-API для docker-compose; в тестах вместо него стаб `StubReportStatusClient`.

## Wolverine saga — грабли (проверено на практике)

- Корреляция саги по свойству сообщения ищется по именам `SagaId`, `Id`, `{SagaTypeName}Id` — наши сообщения используют `ReportId`, что **не матчится**. Поэтому на параметре сообщения каждого saga-обработчика обязателен `[SagaIdentityFrom("ReportId")]`. Новое сообщение саги без этого атрибута молча не скоррелируется.
- `UseEntityFrameworkCoreTransactions()` + `AutoApplyTransactions()` **не флашат** пользовательские сущности `AppDbContext` из saga-обработчиков — нужен явный `await dbContext.SaveChangesAsync(ct)`. Симптом пропуска: `InvalidOperationException: Запись учёта ... не найдена` в `FinishAsync`.
- Статический `NotFound(ReportStatusCheckTimeout, ...)` — штатный путь (timeout всегда стреляет после завершения саги), лог `Debug`, не чинить.

## Документация

- `docs/technical-spec.md` — авторитетная спека; § 12 содержит as-built отклонения (Δ1–Δ7) и технические находки. При изменении поведения саги/API — обновлять её.
- `docs/development-task.md` — BA-требования (user stories, Q1–Q5), `task.md` — исходная постановка.
