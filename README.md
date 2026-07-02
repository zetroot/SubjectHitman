# SubjectHitman

Компонент учёта бесплатных кредитных отчётов и идентификации субъектов КИ по алгоритму на основе указания Банка России 5791-У.

## Документация

- `task.md` — исходная постановка задачи
- `docs/development-task.md` — задание бизнес-аналитика (user stories, допущения, AC)
- `docs/technical-spec.md` — техническая спецификация системного аналитика (DDL, контракты, алгоритм)

## Стек

ASP.NET Core 10 (minimal API), PostgreSQL, EF Core, Wolverine 6, RabbitMQ.

## Быстрый старт

```bash
docker compose up -d
```

API доступен на `http://localhost:8080`.

### Локальная разработка

1. Поднять PostgreSQL и RabbitMQ:
   ```bash
   docker compose up -d postgres rabbitmq status-mock
   ```
2. Запустить приложение:
   ```bash
   dotnet run --project src/SubjectHitman.Api
   ```

### Тесты

```bash
dotnet test SubjectHitman.slnx
```

Интеграционные тесты используют Testcontainers (автоматический подъём PostgreSQL и RabbitMQ).

## API

### `POST /api/v1/free-reports/usage-query`

```bash
curl -X POST http://localhost:8080/api/v1/free-reports/usage-query \
  -H 'Content-Type: application/json' \
  -d '{
    "lastName": "Иванов",
    "firstName": "Иван",
    "middleName": "Иванович",
    "birthDate": "1990-01-15",
    "document": { "typeCode": "21", "series": "4510", "number": "123456", "issueDate": "2010-05-20" },
    "inn": null,
    "snils": null
  }'
```

Ответ: `200 OK` с `subjectId` и `usedFreeReportsCount`.

### Конфигурация

| Параметр | По умолчанию | Описание |
|---|---|---|
| `FreeReports:CooldownPeriod` | `1.00:00:00` | Интервал дедупликации бесплатных отчётов |
| `FreeReports:TimeZone` | `Europe/Moscow` | Таймзона календарного года |
| `Saga:Timeout` | `00:30:00` | Интервал проверки статуса отчёта |
| `Saga:MaxTimeoutRetries` | `5` | Максимум проверок перед учётом «не списан» |

### Сообщения (RabbitMQ)

Компонент слушает очередь `subject-hitman.report-events`, привязанную к exchange `report-processing`:
- `report.ordered` → `ReportOrdered` (старт учёта)
- `report.completed` → `ReportCompleted` (учёт как списан)
- `report.failed` → `ReportFailed` (учёт как не списан)
