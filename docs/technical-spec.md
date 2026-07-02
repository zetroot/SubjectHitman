# Техническая спецификация: компонент учёта бесплатных кредитных отчётов (SubjectHitman)

| Атрибут | Значение |
|---|---|
| Статус | Ready for development |
| Автор | System Analyst |
| Дата | 2026-07-02 |
| Основание | `docs/development-task.md` (BA), `task.md` |

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

```
SubjectHitman.sln
├── src/
│   ├── SubjectHitman.Api/              # хост: minimal API + Wolverine + EF Core
│   │   ├── Program.cs
│   │   ├── Endpoints/UsageQueryEndpoint.cs
│   │   ├── Sagas/ReportAccountingSaga.cs
│   │   ├── Messaging/                  # контракты сообщений (record'ы)
│   │   ├── Domain/
│   │   │   ├── SubjectIdentificationService.cs
│   │   │   ├── SearchKeyBuilder.cs     # нормализация + K1..K6 + SHA256
│   │   │   ├── FreeReportCounter.cs    # выборка + cooldown-группировка
│   │   │   └── Entities/               # Subject, SubjectName, SubjectDocument, SearchKey, ReportUsage
│   │   ├── Infrastructure/
│   │   │   ├── AppDbContext.cs + Migrations/
│   │   │   └── ReportStatusClient.cs   # IReportStatusClient + HTTP-реализация
│   │   └── appsettings.json
│   └── SubjectHitman.ReportStatusMock/ # мок статус-API (D5)
├── tests/
│   ├── SubjectHitman.UnitTests/
│   └── SubjectHitman.IntegrationTests/ # Testcontainers: PostgreSQL + RabbitMQ
├── docker-compose.yml                  # postgres + rabbitmq + mock
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

```csharp
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
public record ReportStatusCheckTimeout(Guid ReportId);   // внутреннее scheduled-сообщение
```

Сериализация — JSON (System.Text.Json), camelCase. Идентичность сообщения = `ReportId`.

### 7.3. Сага `ReportAccountingSaga`

```csharp
public class ReportAccountingSaga : Saga
{
    public Guid Id { get; set; }              // = ReportId
    public Guid SubjectId { get; set; }
    public bool IsFree { get; set; }
    public DateTimeOffset OrderedAt { get; set; }
    public int TimeoutCheckCount { get; set; }
}
```

| Обработчик | Логика |
|---|---|
| `Start(ReportOrdered)` | Идемпотентность: если `report_usages` уже содержит `reportId` — игнор (лог `Information`). Иначе: идентификация субъекта (§ 5, общая транзакция с insert'ом `report_usages` в статусе `Pending`), заполнение состояния саги, `schedule ReportStatusCheckTimeout через SagaTimeout`. |
| `Handle(ReportCompleted)` | `report_usages.status = Charged`, `finished_at = now()`, `MarkCompleted()`. |
| `Handle(ReportFailed)` | `report_usages.status = NotCharged`, `finished_at = now()`, `MarkCompleted()`. |
| `Handle(ReportStatusCheckTimeout)` | Вызов `IReportStatusClient.GetStatusAsync(reportId)`:<br>• `Success` → как `ReportCompleted`;<br>• `Failed` → как `NotCharged`;<br>• `Unknown` (в т.ч. любая ошибка/таймаут HTTP): если `TimeoutCheckCount + 1 >= MaxTimeoutRetries` → `NotCharged` + `MarkCompleted()`, лог `Warning`; иначе `TimeoutCheckCount++`, re-schedule через `SagaTimeout`. |
| Saga not found (`ReportCompleted`/`ReportFailed`/`ReportStatusCheckTimeout` без саги) | Лог `Warning`, сообщение подтверждается (Q2). В Wolverine — статический `NotFound(...)` обработчик. |

Обновление `report_usages` и завершение саги — в одной транзакции (Wolverine transactional middleware + EF Core).

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
- Обработка ошибок консюмера: встроенные политики Wolverine — retry с экспоненциальной задержкой (3 попытки), затем move to dead-letter queue.

---

## 10. План работ (порядок реализации)

| # | Задача | Зависимости | Результат |
|---|---|---|---|
| T1 | Скелет решения: проекты, docker-compose (postgres, rabbitmq), CI-совместимая сборка | — | `dotnet build` проходит |
| T2 | EF Core: сущности, `AppDbContext`, миграция схемы § 3 | T1 | миграция создаёт БД с нуля |
| T3 | `SearchKeyBuilder`: нормализация § 5.1–5.2, прообразы § 5.3, юнит-тесты | T1 | все кейсы DoD по ключам зелёные |
| T4 | `SubjectIdentificationService`: поиск, разрешение конфликтов, merge, advisory locks, retry; юнит + интеграционные тесты (включая конкурентный) | T2, T3 | |
| T5 | `FreeReportCounter`: выборка + cooldown; юнит-тесты границ | T2 | |
| T6 | HTTP endpoint US-1: валидация, ProblemDetails, интеграционный тест | T4, T5 | |
| T7 | Контракты сообщений, топология RabbitMQ, Wolverine durable inbox/outbox | T1 | |
| T8 | `ReportAccountingSaga`: основной поток + идемпотентность + not-found; интеграционные тесты | T4, T7 | |
| T9 | Timeout-ветка: `IReportStatusClient`, HTTP-реализация, мок-проект, тесты ретраев | T8 | |
| T10 | Наблюдаемость, health checks, README, финальный прогон DoD | T6, T9 | DoD § 11 BA-документа выполнен |

Оценка сложности — T4 и T8 самые ёмкие; T3 и T5 — чистая логика, делать первыми под юнит-тесты.

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
