# Техническая спецификация: компонент учёта бесплатных кредитных отчётов (SubjectHitman)

| Атрибут | Значение |
|---|---|
| Статус | **Implemented** (as-built, актуализировано по коду) |
| Автор | System Analyst |
| Дата | 2026-07-02, актуализация 2026-07-03 |
| Основание | `docs/development-task.md` (BA), `task.md` |
| Реализация | 74/74 тестов зелёные (63 unit + 11 integration); план работ T1–T10 выполнен (§ 12) |

Документ переводит бизнес-требования в техническое задание. Бизнес-контекст, user stories и допущения A1–A8 — см. `docs/development-task.md`; здесь они не дублируются, только уточняются.

---

## 1. Решения по открытым вопросам BA (Q1–Q5)

| # | Вопрос | Решение |
|---|---|---|
| Q1 | Конфликт скалярных ПДн при обновлении (ИНН/СНИЛС/ДР: в БД X, в запросе Y ≠ X) | **Игнорировать новое значение, логировать `Warning`** с subjectId и типом поля (без значений ПДн в логе). Заполнение — только из `NULL`. История значений не ведётся. |
| Q2 | `ReportCompleted`/`ReportFailed` раньше `ReportOrdered` | **Отбрасывать с логом `Warning`**. В Wolverine — saga not-found обрабатывается без исключения (см. § 7.4). Если `ReportOrdered` придёт позже — сага стартует штатно и завершится через timeout-ветку по статусу из основной системы. |
| Q3 | AuthN/AuthZ HTTP API | **Вне скоупа итерации.** Требование к коду: пайплайн должен позволять добавить аутентификацию без изменения обработчиков (стандартный middleware ASP.NET Core). |
| Q4 | Таблица латиница→кириллица | Зафиксирована в § 5.2. |
| Q5 | Детализация по cooldown-группам в ответе API | **Нет.** Только счётчик и границы периода. |

**Дополнительные решения SA (дельта к BA-документу):**

| # | Решение |
|---|---|
| D1 | `birthDate` и `document.issueDate` — **обязательные** поля запроса (приложение 1 к 5791-У: дата рождения и дата выдачи ДУЛ входят в состав запроса КО безусловно). BA-контракт ужесточён. |
| D2 | ИНН и СНИЛС **нормализуются до строки цифр** (удаляются все нецифровые символы) до валидации и хэширования. `"-"` и пустая строка после нормализации = отсутствие показателя. |
| D3 | Конкурентность upsert субъекта — через **PostgreSQL advisory locks** по хэшам ключей поиска (§ 5.6). |
| D4 | Даты `orderedAt`, `completedAt`, `failedAt` — `timestamptz` (UTC в БД), конвертация в `TimeZone` только при вычислении границ года/cooldown. |
| D5 | Мок статус-API — **отдельный minimal-API проект** в том же решении (`SubjectHitman.ReportStatusMock`), поднимается в docker-compose; в юнит/интеграционных тестах — стаб за интерфейсом `IReportStatusClient`. |

---

## 2. Архитектура

### 2.1. Контекст

```mermaid
flowchart LR
    EXT[Внешняя система\nзаказа отчётов] -- "POST /usage-query (sync)" --> API
    ORCH[Оркестратор общей саги\nобработки отчёта] -- "ReportOrdered /\nReportCompleted /\nReportFailed" --> MQ[(RabbitMQ)]
    MQ --> WORKER
    subgraph SubjectHitman
        API[HTTP API\nWolverine.HTTP + minimal API]
        WORKER[Message handlers + Saga\nWolverine]
        CORE[Domain: идентификация,\nключи, подсчёт]
        API --> CORE
        WORKER --> CORE
        CORE --> DB[(PostgreSQL\nEF Core)]
        WORKER -- "GET /reports/{id}/status\n(timeout-ветка)" --> STATUS[Статус-API основной системы\n=> МОК]
    end
```

HTTP API и обработчики сообщений хостятся **в одном процессе** (одно ASP.NET Core приложение с Wolverine). Разделение на отдельные хосты не требуется на этой итерации.

### 2.2. Структура решения

Фактическая структура (as-built). Отличия от первоначального проекта: контракты сообщений и общие DTO вынесены в отдельный проект **`SubjectHitman.Abstractions`** (без зависимости от Wolverine — см. § 7.6), слой хранения выделен в **`SubjectHitman.Domain`** + **`SubjectHitman.DataAccess`** (Δ8), формат решения — `.slnx`, каталог тестов — `test/`.

```
SubjectHitman.slnx
├── Directory.Build.props
├── Directory.Packages.props         # NuGet Central Package Management (Δ9)
├── src/
│   ├── SubjectHitman.Abstractions/     # контракты: сообщения, DTO, IReportStatusClient
│   │   ├── Messages/                   # ReportOrdered, ReportCompleted, ReportFailed
│   │   ├── Api/                        # UsageQueryRequest/Response, ReportStatusResponse
│   │   ├── SubjectData.cs, IdentityDocumentData.cs, PersonNameData.cs
│   │   ├── IReportStatusClient.cs, ReportStatus.cs
│   │   └── (НЕ ссылается на Wolverine)
│   ├── SubjectHitman.Domain/           # сущности + интерфейсы репозиториев
│   │   ├── Entities/                   # Subject, SubjectName, SubjectDocument,
│   │   │                               # SearchKey, ReportUsage (+ enums)
│   │   ├── Repositories/               # ISubjectRepository, IReportUsageRepository
│   │   └── SearchKeyValue.cs           # значение поискового ключа
  │   ├── SubjectHitman.DataAccess/       # EF Core: DbContext, миграции, реализации репозиториев
  │   │   ├── AppDbContext.cs
  │   │   ├── DesignTimeDbContextFactory.cs
  │   │   ├── DataAccessServiceCollectionExtensions.cs
  │   │   ├── Configurations/             # IEntityTypeConfiguration<> классы
  │   │   ├── Repositories/               # SubjectRepository, ReportUsageRepository
  │   │   ├── Telemetry/                  # IDataAccessMetricsPublisher, метрики слоя данных (Δ10)
  │   │   └── Migrations/                 # InitialSchema
  │   ├── SubjectHitman.Api/              # хост: minimal API + Wolverine + EF Core
  │   │   ├── Program.cs
  │   │   ├── Endpoints/UsageQueryEndpoint.cs
  │   │   ├── Sagas/
  │   │   │   ├── ReportAccountingSaga.cs         # § 7.3
  │   │   │   ├── ReportStatusCheckTimeout.cs     # внутреннее scheduled-сообщение
  │   │   │   └── SagaOptions.cs
  │   │   ├── Telemetry/                  # IApiMetricsPublisher, метрики прикладного уровня (Δ10)
  │   │   ├── Domain/
│   │   │   ├── SubjectIdentificationService.cs
│   │   │   ├── PersonalDataNormalizer.cs       # нормализация § 5.1–5.2
│   │   │   ├── SearchKeyBuilder.cs             # K1..K6 + SHA256 (§ 5.3)
│   │   │   ├── NormalizedSubject.cs
│   │   │   ├── FreeReportCounter.cs            # выборка + cooldown (§ 6)
│   │   │   └── FreeReportsOptions.cs
│   │   ├── Infrastructure/
│   │   │   └── ReportStatusClient.cs           # HTTP-реализация IReportStatusClient
│   │   └── appsettings.json
│   └── SubjectHitman.ReportStatusMock/ # мок статус-API (D5)
├── test/
│   ├── SubjectHitman.UnitTests/        # 63 теста: нормализация, ключи, cooldown,
│   │                                   # разрешение конфликтов, валидация
│   └── SubjectHitman.IntegrationTests/ # 11 тестов: Testcontainers PostgreSQL + RabbitMQ,
│                                       # WebApplicationFactory, стаб IReportStatusClient
├── Dockerfile                          # образ SubjectHitman.Api
├── docker-compose.yml                  # postgres + rabbitmq + status-mock + api
└── README.md
```

---

## 3. База данных — DDL

Схема — `public`. Все `timestamptz` хранятся в UTC. EF Core migrations должны порождать эквивалентную схему.

```sql
CREATE TABLE subjects (
    id          uuid        PRIMARY KEY,
    birth_date  date        NULL,
    inn         varchar(12) NULL,             -- только цифры (D2); 12 цифр для ФЛ
    snils       varchar(11) NULL,             -- только цифры (D2); 11 цифр
    created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE subject_names (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    subject_id  uuid NOT NULL REFERENCES subjects(id) ON DELETE CASCADE,
    last_name   text NOT NULL,                -- нормализованное значение (§ 5.2), "-" если пусто
    first_name  text NOT NULL,
    middle_name text NOT NULL,
    UNIQUE (subject_id, last_name, first_name, middle_name)
);

CREATE TABLE subject_documents (
    id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    subject_id    uuid NOT NULL REFERENCES subjects(id) ON DELETE CASCADE,
    doc_type_code text NOT NULL,
    series        text NOT NULL DEFAULT '',   -- отсутствие серии = пустая строка
    number        text NOT NULL,
    issue_date    date NULL,                  -- NULL допустим только для «предыдущего ДУЛ» (D1: у текущего обязателен на уровне API)
    UNIQUE NULLS NOT DISTINCT (subject_id, doc_type_code, series, number, issue_date)
);

CREATE TABLE search_keys (
    id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    subject_id  uuid NOT NULL REFERENCES subjects(id) ON DELETE CASCADE,
    key_type    smallint NOT NULL CHECK (key_type BETWEEN 1 AND 6),
    hash        bytea NOT NULL,               -- 32 байта SHA-256
    UNIQUE (subject_id, key_type, hash)
);
CREATE INDEX ix_search_keys_hash ON search_keys (hash);  -- поиск по IN (…) списку хэшей

CREATE TABLE report_usages (
    report_id   uuid PRIMARY KEY,             -- идемпотентность
    subject_id  uuid NOT NULL REFERENCES subjects(id),
    is_free     boolean NOT NULL,
    status      smallint NOT NULL DEFAULT 0,  -- 0 Pending / 1 Charged / 2 NotCharged
    ordered_at  timestamptz NOT NULL,
    finished_at timestamptz NULL
);
-- покрывающий индекс для подсчёта (§ 6)
CREATE INDEX ix_report_usages_count
    ON report_usages (subject_id, ordered_at)
    WHERE is_free AND status = 1;
```

Замечания для разработчика:

- `hash` ищется без `key_type` в предикате: во входящем запросе уже известно, какому типу соответствует каждый хэш (тип зашит префиксом в прообраз хэша, коллизии типов исключены). Индекс по `hash` достаточен; `key_type` читается из строки результата.
- `subject_documents.issue_date` — `NULL`-able: у текущего ДУЛ дата выдачи обязательна на уровне валидации API (D1), но «предыдущий ДУЛ» по 5791-У может прийти без даты выдачи. Такой документ сохраняется с `issue_date = NULL` и участвует в ключах K1, K2, K4; ключ K3 для него **не рассчитывается**. `UNIQUE NULLS NOT DISTINCT` (PostgreSQL 15+) предотвращает дубли строк с `NULL`-датой.
- Состояние саги — таблица Wolverine (генерируется её механизмом персистентности в PostgreSQL), вручную не создаётся.

### 3.1. ER-диаграмма

```mermaid
erDiagram
    subjects ||--o{ subject_names : has
    subjects ||--o{ subject_documents : has
    subjects ||--o{ search_keys : has
    subjects ||--o{ report_usages : has
    subjects {
        uuid id PK
        date birth_date "NULL"
        varchar inn "NULL, digits only"
        varchar snils "NULL, digits only"
        timestamptz created_at
    }
    search_keys {
        bigint id PK
        uuid subject_id FK
        smallint key_type "1..6"
        bytea hash "sha256"
    }
    report_usages {
        uuid report_id PK
        uuid subject_id FK
        boolean is_free
        smallint status "0/1/2"
        timestamptz ordered_at
        timestamptz finished_at "NULL"
    }
```

---

## 4. HTTP API — контракт

### 4.1. `POST /api/v1/free-reports/usage-query`

```yaml
openapi: 3.0.3
info: { title: SubjectHitman API, version: 1.0.0 }
paths:
  /api/v1/free-reports/usage-query:
    post:
      summary: Идентифицировать субъекта и вернуть число использованных бесплатных отчётов за текущий календарный год
      requestBody:
        required: true
        content:
          application/json:
            schema: { $ref: '#/components/schemas/UsageQueryRequest' }
      responses:
        '200':
          content:
            application/json:
              schema: { $ref: '#/components/schemas/UsageQueryResponse' }
        '400': { description: Ошибка валидации, ProblemDetails }
        '500': { description: Внутренняя ошибка, ProblemDetails }
components:
  schemas:
    PersonName:
      type: object
      required: [lastName, firstName]
      properties:
        lastName:   { type: string, minLength: 1, maxLength: 200 }
        firstName:  { type: string, minLength: 1, maxLength: 200 }
        middleName: { type: string, nullable: true, maxLength: 200 }
    IdentityDocument:
      type: object
      required: [typeCode, number]        # issueDate обязателен только для текущего ДУЛ (D1)
      properties:
        typeCode:  { type: string, minLength: 1, maxLength: 10 }
        series:    { type: string, nullable: true, maxLength: 20 }
        number:    { type: string, minLength: 1, maxLength: 50 }
        issueDate: { type: string, format: date, nullable: true }
    UsageQueryRequest:
      type: object
      required: [lastName, firstName, birthDate, document]
      properties:
        lastName:   { type: string, minLength: 1 }
        firstName:  { type: string, minLength: 1 }
        middleName: { type: string, nullable: true }
        birthDate:  { type: string, format: date }          # обязателен (D1)
        document:                                            # текущий ДУЛ, issueDate обязателен (D1)
          allOf: [ { $ref: '#/components/schemas/IdentityDocument' } ]
        previousName:     { $ref: '#/components/schemas/PersonName',       nullable: true }
        previousDocument: { $ref: '#/components/schemas/IdentityDocument', nullable: true }
        inn:   { type: string, nullable: true }   # "-", пусто или 12 цифр после нормализации
        snils: { type: string, nullable: true }   # "-", пусто или 11 цифр после нормализации
    UsageQueryResponse:
      type: object
      required: [subjectId, usedFreeReportsCount, periodStart, periodEnd]
      properties:
        subjectId:            { type: string, format: uuid }
        usedFreeReportsCount: { type: integer, minimum: 0 }
        periodStart:          { type: string, format: date-time }
        periodEnd:            { type: string, format: date-time }
```

### 4.2. Валидация (400, ProblemDetails с перечнем ошибок по полям)

| Поле | Правило |
|---|---|
| `lastName`, `firstName` | непустые после trim |
| `birthDate` | валидная дата, не в будущем |
| `document.typeCode`, `document.number` | непустые |
| `document.issueDate` | обязателен, валидная дата, не в будущем |
| `inn` | после D2-нормализации: пусто ИЛИ ровно 12 цифр |
| `snils` | после D2-нормализации: пусто ИЛИ ровно 11 цифр |
| `previousDocument` | если передан — `typeCode` и `number` непустые; `issueDate` опционален |
| `previousName` | если передан — `lastName`, `firstName` непустые |

Контрольные суммы ИНН/СНИЛС **не проверяются** (данные уже прошли валидацию в вышестоящей системе).

### 4.3. Sequence — US-1

```mermaid
sequenceDiagram
    participant EXT as Внешняя система
    participant API as UsageQueryEndpoint
    participant IDN as SubjectIdentificationService
    participant DB as PostgreSQL
    EXT->>API: POST /usage-query
    API->>API: валидация (400 при ошибке)
    API->>IDN: Identify(subjectData)
    IDN->>IDN: нормализация + расчёт хэшей K1..K6
    IDN->>DB: BEGIN; pg_advisory_xact_lock(hash...) × N
    IDN->>DB: SELECT subject_id, key_type FROM search_keys WHERE hash IN (...)
    alt кандидатов нет
        IDN->>DB: INSERT subject + names + documents + search_keys
    else кандидаты есть
        IDN->>IDN: выбор победителя (max совпадений → сила → created_at)
        IDN->>DB: merge ПДн (§ 5.5) + полный пересчёт search_keys
    end
    IDN->>DB: COMMIT
    API->>DB: подсчёт (§ 6) по subject_id
    API-->>EXT: 200 { subjectId, usedFreeReportsCount, period }
```

---

## 5. Алгоритм идентификации — уточнения к § 5 BA-документа

### 5.1. Нормализация показателей

Порядок для каждого поля (применяется и к запросу, и к данным перед сохранением в БД — в БД хранятся **уже нормализованные** значения):

1. Unicode NFC → `trim` → схлопнуть повторные пробелы в один.
2. Верхний регистр (invariant culture).
3. Только для компонент ФИО: транслитерация латиницы в кириллицу по таблице § 5.2.
4. Пустая строка / null для компонент ФИО → `"-"`.
5. ИНН/СНИЛС: удалить все символы, кроме цифр (D2); пустой результат или исходное значение `"-"` → показатель отсутствует (`NULL` в БД).
6. Серия ДУЛ: null → пустая строка `""`.
7. Даты → `yyyy-MM-dd`.

### 5.2. Таблица транслитерации латиница → кириллица (Q4)

Применяется посимвольно к компонентам ФИО (после приведения к верхнему регистру):

| Лат. | A | B | C | E | H | K | M | O | P | T | X | Y |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Кир. | А | В | С | Е | Н | К | М | О | Р | Т | Х | У |

### 5.3. Прообразы хэшей (канонический формат)

`hash = SHA256(UTF8(prefix + "|" + f1 + "|" + f2 + ... ))`, поля строго в указанном порядке:

| Ключ | Прообраз | Не рассчитывается, если |
|---|---|---|
| K1 | `K1\|{last}\|{first}\|{middle}\|{docType}\|{series}\|{number}` | нет ФИО или ДУЛ |
| K2 | `K2\|{last}\|{birthDate}\|{docType}\|{series}\|{number}` | нет фамилии, ДР или ДУЛ |
| K3 | `K3\|{series}\|{number}\|{issueDate}\|{inn}` | нет ДУЛ, `issueDate` = NULL или нет ИНН |
| K4 | `K4\|{series}\|{number}\|{snils}` | нет ДУЛ или СНИЛС |
| K5 | `K5\|{birthDate}\|{snils}` | нет ДР или СНИЛС |
| K6 | `K6\|{birthDate}\|{inn}` | нет ДР или ИНН |

Множественность: ключи K1–K4 строятся для **каждой** комбинации, где участвуют наборы:
K1 — каждое ФИО × каждый ДУЛ; K2 — каждая фамилия (различные `last` из набора ФИО) × каждый ДУЛ; K3, K4 — каждый ДУЛ.
Для входящего запроса наборы = {текущие сведения} ∪ {предыдущие сведения, если переданы}.

### 5.4. Расчёт ключей запроса — пример

Запрос: текущее ФИО N1, предыдущее N2, текущий ДУЛ D1, предыдущий ДУЛ D2 (без issueDate), ДР, ИНН, СНИЛС нет.
Хэши запроса: K1(N1,D1), K1(N1,D2), K1(N2,D1), K1(N2,D2); K2(N1.last,D1), K2(N1.last,D2), K2(N2.last,D1), K2(N2.last,D2); K3(D1,inn) — K3(D2) не рассчитывается (нет issueDate); K6(ДР,inn). K4/K5 — нет (нет СНИЛС).

### 5.5. Merge при обновлении субъекта (уточнение Q1)

```
для каждого нового ФИО:   INSERT ... ON CONFLICT DO NOTHING
для каждого нового ДУЛ:   INSERT ... ON CONFLICT DO NOTHING
для birth_date, inn, snils:
    если в БД NULL и в запросе есть значение → записать
    если в БД X, в запросе Y ≠ X            → оставить X, log Warning (без значений ПДн)
если было ЛЮБОЕ изменение (вставка ФИО/ДУЛ или заполнение скаляра):
    DELETE FROM search_keys WHERE subject_id = @id
    пересчитать и вставить все ключи заново
```

### 5.6. Конкурентность (D3)

Проблема: два одновременных запроса с одинаковыми данными не должны создать двух субъектов.

Решение — advisory locks в рамках транзакции идентификации:

```
хэши запроса → для каждого: lockKey = первые 8 байт SHA256 как bigint
отсортировать lockKey по возрастанию (защита от дедлоков)
для каждого: SELECT pg_advisory_xact_lock(@lockKey)
затем — поиск/создание/merge как в § 4.3
```

Так конкурирующие запросы по одному человеку сериализуются на пересекающихся хэшах, а запросы по разным людям не блокируют друг друга. Уровень изоляции — `Read Committed` (достаточно при advisory locks). UNIQUE-констрейнты остаются последней линией защиты: при `unique_violation` — один retry всей операции идентификации.

Идентификация из HTTP-запроса (US-1) и из события `ReportOrdered` (US-2) обязана использовать **один и тот же** код (`SubjectIdentificationService`).

---

## 6. Подсчёт — уточнения

- Границы года: `[start, end)` — `start` = 1 января 00:00:00 текущего года в `TimeZone`, `end` = 1 января следующего года 00:00:00; сравнение по `ordered_at` после конвертации границ в UTC. В ответе API `periodEnd` показывается как `end - 1 сек` (соответствие BA-контракту).
- Выборка: `WHERE subject_id = @id AND is_free AND status = 1 AND ordered_at >= @startUtc AND ordered_at < @endUtc ORDER BY ordered_at` — работает по частичному индексу `ix_report_usages_count`.
- Cooldown-группировка — в памяти по псевдокоду § 6.2 BA-документа. Граница: разница **ровно** `CooldownPeriod` → тот же отчёт (новая группа только при строгом `>`). Объём данных на субъекта за год мал (единицы–десятки записей), выборка в память допустима.

---

## 7. Messaging и сага (Wolverine + RabbitMQ)

### 7.1. Топология RabbitMQ

| Объект | Имя | Назначение |
|---|---|---|
| Exchange | `report-processing` (topic, durable) | публикует оркестратор (в тестах — сами) |
| Queue | `subject-hitman.report-events` (durable) | вход компонента |
| Bindings | `report.ordered`, `report.completed`, `report.failed` → очередь | |

Wolverine: durable inbox/outbox на PostgreSQL (`PersistMessagesWithPostgresql`), auto-provision топологии при старте (`AutoProvision`). Scheduled messages (`ReportStatusCheckTimeout`) — локальное durable-расписание Wolverine, в RabbitMQ не публикуются.

### 7.2. Контракты сообщений (C# records)

Внешние контракты — в проекте `SubjectHitman.Abstractions` (namespace `SubjectHitman.Abstractions.Messages`), **без зависимости от Wolverine**. Внутреннее scheduled-сообщение `ReportStatusCheckTimeout` — в `SubjectHitman.Api.Sagas` (не покидает локальную durable-очередь).

```csharp
// SubjectHitman.Abstractions
public record SubjectData(
    string LastName, string FirstName, string? MiddleName,
    DateOnly BirthDate,
    IdentityDocumentData Document,
    PersonNameData? PreviousName,
    IdentityDocumentData? PreviousDocument,
    string? Inn, string? Snils);

public record IdentityDocumentData(string TypeCode, string? Series, string Number, DateOnly? IssueDate);
public record PersonNameData(string LastName, string FirstName, string? MiddleName);

public record ReportOrdered(Guid ReportId, DateTimeOffset OrderedAt, bool IsFree, SubjectData Subject);
public record ReportCompleted(Guid ReportId, DateTimeOffset CompletedAt);
public record ReportFailed(Guid ReportId, DateTimeOffset FailedAt, string? Reason);

// SubjectHitman.Api.Sagas — внутреннее scheduled-сообщение
public record ReportStatusCheckTimeout(Guid ReportId, int CheckCount);
```

Сериализация — JSON (System.Text.Json), camelCase. Идентичность сообщения = `ReportId`.

### 7.3. Сага `ReportAccountingSaga`

```csharp
public class ReportAccountingSaga : Saga
{
    [SagaIdentity]
    public Guid ReportId { get; set; }        // = ReportId сообщений
    public Guid SubjectId { get; set; }
    public bool IsFree { get; set; }
    public DateTimeOffset OrderedAt { get; set; }
    public int TimeoutCheckCount { get; set; }
}
```

**Корреляция саги (критично, выяснено при реализации).** Wolverine определяет saga identity по свойству сообщения, перебирая имена в порядке: `[SagaIdentityFrom]` на параметре обработчика → `[SagaIdentity]` на члене сообщения → `{SagaTypeName}Id` (`ReportAccountingSagaId`) → имя без "Saga" (`ReportAccountingId`) → `SagaId` → `Id` (case-insensitive). Свойство `ReportId` наших сообщений **не подходит ни под одно** из соглашений, а атрибут `[SagaIdentity]` на сообщениях невозможен (Abstractions не ссылается на Wolverine). Поэтому на параметре сообщения **каждого** обработчика саги стоит атрибут:

```csharp
public async Task<OutgoingMessages> Start(
    [SagaIdentityFrom("ReportId")] ReportOrdered message, ...)
```

Достаточно одного обработчика на тип сообщения; статическим `NotFound(...)` атрибут не нужен (Wolverine сканирует параметры всех обработчиков и берёт первый найденный атрибут).

| Обработчик | Логика |
|---|---|
| `Start(ReportOrdered)` | Идемпотентность: если `report_usages` уже содержит `reportId` — лог `Information`, `SubjectId` берётся из существующей записи, вставки нет. Иначе: идентификация субъекта (§ 5), insert `report_usages` в статусе `Pending`, **явный `SaveChangesAsync`**. В обоих случаях: заполнение состояния саги и `Schedule(ReportStatusCheckTimeout, now + SagaTimeout)` через возвращаемый `OutgoingMessages`. |
| `Handle(ReportCompleted)` | `FinishAsync(Charged)`, `MarkCompleted()`. |
| `Handle(ReportFailed)` | `FinishAsync(NotCharged)`, `MarkCompleted()`. |
| `Handle(ReportStatusCheckTimeout)` | Вызов `IReportStatusClient.GetStatusAsync(reportId)`:<br>• `Success` → `FinishAsync(Charged)` + `MarkCompleted()`;<br>• `Failed` → `FinishAsync(NotCharged)` + `MarkCompleted()`;<br>• `Unknown` (в т.ч. любая ошибка/таймаут HTTP): `TimeoutCheckCount++`; если `TimeoutCheckCount >= MaxTimeoutRetries` → `FinishAsync(NotCharged)` + `MarkCompleted()`, лог `Warning`; иначе re-schedule через `SagaTimeout`. |
| Saga not found (`ReportCompleted`/`ReportFailed` без саги) | Статический `NotFound(...)`: лог `Warning`, сообщение подтверждается (Q2). |
| Saga not found (`ReportStatusCheckTimeout` после завершения саги) | Статический `NotFound(...)`: лог `Debug`, штатная ситуация — timeout всегда остаётся в расписании после завершения саги по `ReportCompleted`/`ReportFailed`. |

Общий приватный метод `FinishAsync(status)`: `FindAsync` записи `report_usages` по `ReportId` (исключение, если нет — защита инварианта «сага существует ⇒ запись есть»), обновление `status`/`finished_at` **только из `Pending`** (защита от повторной финализации), **явный `SaveChangesAsync`**.

**Персистентность (критично, выяснено при реализации).** Saga-персистентность Wolverine (`CommitUnitOfWorkFrame`) сохраняет только состояние самой саги; изменения доменных сущностей (`ReportUsage`) в пользовательском `AppDbContext` **не флашатся автоматически**, несмотря на `UseEntityFrameworkCoreTransactions()` + `AutoApplyTransactions()`. Поэтому в `Start` и `FinishAsync` — явные вызовы `dbContext.SaveChangesAsync(ct)`.

### 7.4. Sequence — timeout-ветка

```mermaid
sequenceDiagram
    participant W as Wolverine scheduler
    participant S as ReportAccountingSaga
    participant ST as IReportStatusClient (мок)
    participant DB as PostgreSQL
    W->>S: ReportStatusCheckTimeout(reportId)
    S->>ST: GET /reports/{reportId}/status
    alt Success / Failed
        S->>DB: UPDATE report_usages SET status=Charged|NotCharged, finished_at=now()
        S->>S: MarkCompleted()
    else Unknown, retries < MaxTimeoutRetries
        S->>S: TimeoutCheckCount++
        S->>W: schedule ReportStatusCheckTimeout(+SagaTimeout)
    else Unknown, retries >= MaxTimeoutRetries
        S->>DB: UPDATE ... status=NotCharged, finished_at=now()
        S->>S: MarkCompleted()
    end
```

### 7.5. `IReportStatusClient`

```csharp
public enum ReportStatus { Success, Failed, Unknown }
public interface IReportStatusClient
{
    Task<ReportStatus> GetStatusAsync(Guid reportId, CancellationToken ct);
}
```

HTTP-реализация: `HttpClient` через `IHttpClientFactory`, timeout 5 сек, **без** retry внутри клиента (ретраи — на уровне саги). Любая ошибка (не-2xx, сеть, timeout, невалидный JSON) → `Unknown` + лог `Warning`. Мок-проект отвечает по контракту § 7.3 BA-документа и позволяет задавать статусы через `PUT /reports/{reportId}/status` (для интеграционных тестов и ручной отладки).

### 7.6. Ограничение: `SubjectHitman.Abstractions` без Wolverine

Проект контрактов не ссылается на Wolverine — сообщения переиспользуемы вышестоящей системой без транзитивной зависимости от брокер-фреймворка. Следствия:

- атрибуты Wolverine (`[SagaIdentity]` и т.п.) **нельзя** ставить на типы сообщений;
- корреляция саги решается на стороне `SubjectHitman.Api` атрибутом `[SagaIdentityFrom("ReportId")]` на параметрах обработчиков (§ 7.3);
- `IReportStatusClient` и `ReportStatus` также в Abstractions (интерфейс подменяется стабом в интеграционных тестах).

---

## 8. Конфигурация

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=subject_hitman;Username=app;Password=***",
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  },
  "FreeReports": {
    "CooldownPeriod": "1.00:00:00",
    "TimeZone": "Europe/Moscow"
  },
  "Saga": {
    "Timeout": "00:30:00",
    "MaxTimeoutRetries": 5
  },
  "ReportStatusApi": {
    "BaseUrl": "http://localhost:5100",
    "RequestTimeout": "00:00:05"
  }
}
```

Биндинг через `IOptions<T>` с валидацией на старте (`ValidateOnStart`): `CooldownPeriod > 0`, `Timeout > 0`, `MaxTimeoutRetries >= 1`, `TimeZone` — валидный IANA id, `BaseUrl` — абсолютный URI.

---

## 9. Наблюдаемость и обработка ошибок

- Структурированное логирование (стандартный `ILogger`): создание субъекта (`subjectId`, число ключей), выбор при конфликте кандидатов (`subjectId` победителя, счётчики совпадений — **без значений ПДн**), Q1-конфликты, старт/исходы саги, timeout-ретраи, saga-not-found.
- ProblemDetails для 400/500 (стандартный `AddProblemDetails` + exception handler). Тексты ошибок не содержат ПДн.
- Health checks: `GET /health` — liveness; readiness — доступность PostgreSQL.
- **Prometheus-метрики (Δ10):** `GET /metrics` через `OpenTelemetry.Exporter.Prometheus.AspNetCore`. Два класса-публикатора (singleton через интерфейс):
  - `IApiMetricsPublisher` (Meter `SubjectHitman.Api`): счётчики `saga.started`, `saga.duplicate_orders`, `saga.finished{status,via}`, `saga.timeout_rechecks`, `saga.orphaned_events{event_type}`, `identification.completed{outcome}`, `identification.conflicts_resolved`, `identification.pd_conflicts{field}`; гистограммы `identification.duration` (ms), `report_status.duration` (ms); счётчик `report_status.requests{result}`.
  - `IDataAccessMetricsPublisher` (Meter `SubjectHitman.DataAccess`): счётчики `subject_repository.unique_violation_retries`, `subject_repository.advisory_locks.acquired`; гистограмма `subject_repository.transaction.duration` (ms).
- Обработка ошибок консюмера: встроенные политики Wolverine — retry с экспоненциальной задержкой (3 попытки), затем move to dead-letter queue.

---

## 10. План работ (порядок реализации)

Все задачи выполнены (статус — см. § 12).

| # | Задача | Зависимости | Результат |
|---|---|---|---|
| T1 | Скелет решения: проекты, docker-compose (postgres, rabbitmq), CI-совместимая сборка | — | ✅ `dotnet build` проходит |
| T2 | EF Core: сущности, `AppDbContext`, миграция схемы § 3 | T1 | ✅ миграция `InitialSchema` создаёт БД с нуля |
| T3 | `SearchKeyBuilder`: нормализация § 5.1–5.2, прообразы § 5.3, юнит-тесты | T1 | ✅ `PersonalDataNormalizer` + `SearchKeyBuilder` |
| T4 | `SubjectIdentificationService`: поиск, разрешение конфликтов, merge, advisory locks, retry; юнит + интеграционные тесты (включая конкурентный) | T2, T3 | ✅ |
| T5 | `FreeReportCounter`: выборка + cooldown; юнит-тесты границ | T2 | ✅ |
| T6 | HTTP endpoint US-1: валидация, ProblemDetails, интеграционный тест | T4, T5 | ✅ 4 интеграционных теста |
| T7 | Контракты сообщений, топология RabbitMQ, Wolverine durable inbox/outbox | T1 | ✅ контракты в `Abstractions` |
| T8 | `ReportAccountingSaga`: основной поток + идемпотентность + not-found; интеграционные тесты | T4, T7 | ✅ см. § 7.3, § 12 |
| T9 | Timeout-ветка: `IReportStatusClient`, HTTP-реализация, мок-проект, тесты ретраев | T8 | ✅ |
| T10 | Наблюдаемость, health checks, README, финальный прогон DoD | T6, T9 | ✅ 74/74 тестов |

---

## 11. Трассировка требований

| Требование BA | Разделы спеки |
|---|---|
| US-1 (запрос счётчика) | § 4, § 5, § 6, T3–T6 |
| US-2 (сага, основной поток) | § 7.1–7.3, T7–T8 |
| US-3 (timeout) | § 7.3–7.5, T9 |
| A4/G4 (cooldown) | § 6, T5 |
| A6 (моки) | § 7.5, D5, T9 |
| Q1–Q5 | § 1 |
| NFR § 9 BA | § 8, § 9, § 3 |

---

## 12. Статус реализации (as-built, 2026-07-03)

### 12.1. Итог

Компонент реализован полностью, все задачи T1–T10 закрыты. Тесты: **63 unit + 11 integration = 74/74 зелёные**. Интеграционные тесты — Testcontainers (`postgres:17-alpine`, `rabbitmq:4-management-alpine`) + `WebApplicationFactory<Program>`; `IReportStatusClient` подменяется управляемым стабом, `Saga:Timeout` в тестах — 2 сек, `MaxTimeoutRetries` — 2.

### 12.2. Отклонения от первоначальной спеки

| # | Спека (было) | Реализация (стало) | Причина |
|---|---|---|---|
| Δ1 | Контракты сообщений в `SubjectHitman.Api/Messaging/` | Отдельный проект `SubjectHitman.Abstractions` без ссылки на Wolverine (§ 7.6) | Переиспользование контрактов вышестоящей системой |
| Δ2 | Свойство саги `Id` | Свойство `ReportId` + `[SagaIdentity]` на саге + `[SagaIdentityFrom("ReportId")]` на параметрах всех обработчиков | Соглашения Wolverine по имени свойства не покрывают `ReportId` (§ 7.3); `[SagaIdentity]` на сообщениях невозможен из-за Δ1 |
| Δ3 | «Обновление `report_usages` и завершение саги — в одной транзакции (transactional middleware)» | Явные `SaveChangesAsync` в `Start` и `FinishAsync` | Saga-персистентность Wolverine не флашит пользовательские сущности EF Core автоматически (§ 7.3) |
| Δ4 | `ReportStatusCheckTimeout(Guid ReportId)` | `ReportStatusCheckTimeout(Guid ReportId, int CheckCount)` | Диагностика: номер проверки виден в сообщении |
| Δ5 | Ретраи консюмера: экспоненциальная задержка, 3 попытки | `RetryWithCooldown(1s, 5s, 15s)` → move to error queue | Эквивалентная политика штатными средствами Wolverine |
| Δ6 | `SubjectHitman.sln`, каталог `tests/` | `SubjectHitman.slnx`, каталог `test/` | Современный формат решения |
| Δ7 | Health readiness «доступность PostgreSQL» | `AddDbContextCheck<AppDbContext>` на `GET /health` | Одна проверка покрывает liveness+readiness на этой итерации |
| Δ8 | `AppDbContext` и миграции в проекте Api; конфигурация в `OnModelCreating` | Выделены `SubjectHitman.Domain` (сущности + интерфейсы репозиториев) и `SubjectHitman.DataAccess` (DbContext, `IEntityTypeConfiguration<>`, миграции, реализации репозиториев). Потребители Api переключены на `ISubjectRepository` / `IReportUsageRepository`. | Dependency inversion: слой Api не зависит от деталей хранения. |
| Δ9 | Версии пакетов в `PackageReference` каждого `.csproj` | NuGet Central Package Management: все версии (19 пакетов) централизованы в `Directory.Packages.props`; атрибут `Version` удалён из `PackageReference` в 4 проектах. | Единый источник версий, упрощение обновлений. |
| Δ10 | Только структурированные логи | Добавлены Prometheus-метрики через OpenTelemetry: 14 счётчиков + 3 гистограммы в двух Meter'ах (`SubjectHitman.Api`, `SubjectHitman.DataAccess`), `GET /metrics`. Используется `OpenTelemetry.Exporter.Prometheus.AspNetCore` (prerelease 1.16.0-beta.1). | Мониторинг саг, идентификации, статус-API и слоя данных в проде.

Функциональные требования (Q1–Q5, D1–D5, US-1..US-3) реализованы без отклонений.

### 12.3. Технические находки (для сопровождения)

1. **Корреляция саг Wolverine.** Порядок разрешения saga identity в `SagaChain.DetermineSagaIdMember`: `[SagaIdentityFrom]` на параметре обработчика → `[SagaIdentity]` на члене сообщения → свойство `{SagaTypeName}Id` → имя без «Saga» → `SagaId` → `Id`. При добавлении нового сообщения саги **обязательно** ставить `[SagaIdentityFrom("ReportId")]` на его параметр, иначе корреляция молча не сработает.
2. **Флаш доменных сущностей.** Несмотря на `UseEntityFrameworkCoreTransactions()` + `Policies.AutoApplyTransactions()`, изменения в `AppDbContext` из обработчиков саги требуют явного `SaveChangesAsync`. Симптом при пропуске: `InvalidOperationException: Запись учёта для отчёта … не найдена` в `FinishAsync`.
3. **`NotFound` для timeout — штатный путь.** Scheduled-сообщение `ReportStatusCheckTimeout` всегда срабатывает после завершения саги по `ReportCompleted`/`ReportFailed`; обрабатывается статическим `NotFound` с логом `Debug` (не `Warning` — это не аномалия).
4. **Двойная защита идемпотентности финализации.** `FinishAsync` меняет статус только из `Pending`; повторные `ReportCompleted`/`ReportFailed` после завершения саги попадают в `NotFound` (Q2).
5. **Слой доступа к данным.** Выделены `SubjectHitman.Domain` (сущности + `ISubjectRepository`/`IReportUsageRepository`) и `SubjectHitman.DataAccess` (DbContext, `IEntityTypeConfiguration<>`, миграции, реализации). `SubjectIdentificationService` передаёт бизнес-логику в `ISubjectRepository.ExecuteIdentificationAsync` через коллбэк; транзакции, advisory-блокировки и ретраи инкапсулированы в `SubjectRepository` (DataAccess).
6. **Публикаторы метрик.** Метрики живут в классах `ApiMetricsPublisher` и `DataAccessMetricsPublisher` (singleton через интерфейс `IApiMetricsPublisher`/`IDataAccessMetricsPublisher`), создающих `Meter` через `IMeterFactory`. Новые метрики добавляются только через публикаторы; прямое создание `Meter` в бизнес-коде запрещено. DataAccess-публикатор не тянет OpenTelemetry — только `Microsoft.Extensions.Diagnostics.Abstractions`.

### 12.4. Возможные улучшения (вне скоупа итерации)

- AuthN/AuthZ HTTP API (Q3) — стандартный middleware, обработчики менять не потребуется.
- Раздельные liveness/readiness probes при выкладке в оркестратор (Δ7).
