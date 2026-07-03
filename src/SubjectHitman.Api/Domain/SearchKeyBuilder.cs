using System.Security.Cryptography;
using System.Text;
using SubjectHitman.Domain;
using SubjectHitman.Domain.Entities;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// Вычисляет поисковые ключи K1..K6 субъекта на основе его нормализованных персональных данных
/// (техническая спецификация, § 5.3). Один и тот же код обрабатывает как входящие запросы,
/// так и хранимых субъектов, гарантируя сопоставимость хешей.
/// </summary>
public static class SearchKeyBuilder
{
    private const char Separator = '|';

    /// <summary>
    /// Строит все вычислимые поисковые ключи для заданных нормализованных данных субъекта.
    /// Ключи, для которых отсутствуют обязательные показатели, пропускаются. Правила множественности:
    /// K1 — каждое имя × каждый документ; K2 — каждая уникальная фамилия × каждый документ;
    /// K3, K4 — каждый документ (K3 только для документов с датой выдачи); K5, K6 — скалярные.
    /// </summary>
    /// <param name="subject">Нормализованные данные субъекта.</param>
    /// <returns>Уникальный набор вычисленных значений ключей.</returns>
    public static IReadOnlyCollection<SearchKeyValue> Build(NormalizedSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var keys = new HashSet<SearchKeyValue>();

        foreach (var doc in subject.Documents)
        {
            foreach (var name in subject.Names)
            {
                // K1: full name + document
                keys.Add(Compute(SearchKeyType.K1, name.LastName, name.FirstName, name.MiddleName, doc.TypeCode, doc.Series, doc.Number));
            }

            if (subject.BirthDate is { } birthDate)
            {
                foreach (var lastName in subject.Names.Select(n => n.LastName).Distinct())
                {
                    // K2: last name + birth date + document
                    keys.Add(Compute(SearchKeyType.K2, lastName, PersonalDataNormalizer.FormatDate(birthDate), doc.TypeCode, doc.Series, doc.Number));
                }
            }

            if (subject.Inn is not null && doc.IssueDate is { } issueDate)
            {
                // K3: document series + number + issue date + INN
                keys.Add(Compute(SearchKeyType.K3, doc.Series, doc.Number, PersonalDataNormalizer.FormatDate(issueDate), subject.Inn));
            }

            if (subject.Snils is not null)
            {
                // K4: document series + number + SNILS
                keys.Add(Compute(SearchKeyType.K4, doc.Series, doc.Number, subject.Snils));
            }
        }

        if (subject.BirthDate is { } bd)
        {
            if (subject.Snils is not null)
            {
                // K5: birth date + SNILS
                keys.Add(Compute(SearchKeyType.K5, PersonalDataNormalizer.FormatDate(bd), subject.Snils));
            }

            if (subject.Inn is not null)
            {
                // K6: birth date + INN
                keys.Add(Compute(SearchKeyType.K6, PersonalDataNormalizer.FormatDate(bd), subject.Inn));
            }
        }

        return keys;
    }

    private static SearchKeyValue Compute(SearchKeyType type, params string[] fields)
    {
        var preimage = new StringBuilder();
        preimage.Append('K').Append((int)type);
        foreach (var field in fields)
        {
            preimage.Append(Separator).Append(field);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(preimage.ToString()));
        return new SearchKeyValue(type, hash);
    }
}
