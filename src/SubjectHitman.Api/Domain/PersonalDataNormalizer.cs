using System.Text;

namespace SubjectHitman.Api.Domain;

/// <summary>
/// Правила нормализации персональных данных субъекта (техническая спецификация, § 5.1–5.2).
/// Применяются и к входящим запросам, и к данным перед сохранением, чтобы хранимые значения
/// и вычисляемые хэши всегда были сравнимы.
/// </summary>
public static class PersonalDataNormalizer
{
    /// <summary>Заглушка, сохраняемая вместо отсутствующего компонента ФИО.</summary>
    public const string AbsentNameComponent = "-";

    private static readonly Dictionary<char, char> LatinToCyrillic = new()
    {
        ['A'] = 'А',
        ['B'] = 'В',
        ['C'] = 'С',
        ['E'] = 'Е',
        ['H'] = 'Н',
        ['K'] = 'К',
        ['M'] = 'М',
        ['O'] = 'О',
        ['P'] = 'Р',
        ['T'] = 'Т',
        ['X'] = 'Х',
        ['Y'] = 'У',
    };

    /// <summary>
    /// Нормализует компонент ФИО: НФД, обрезка краевых пробелов, схлопывание пробелов,
    /// приведение к верхнему регистру, транслитерация визуально идентичных латинских букв
    /// в кириллические (5791-У, Приложение 2, прим. 2).
    /// Отсутствующее или пустое значение заменяется на <see cref="AbsentNameComponent"/>.
    /// </summary>
    /// <param name="value">Исходный компонент имени; может быть <see langword="null"/>.</param>
    /// <returns>Нормализованный компонент, никогда не <see langword="null"/> и не пустая строка.</returns>
    public static string NormalizeNameComponent(string? value)
    {
        var basic = NormalizeBasic(value);
        if (basic.Length == 0)
        {
            return AbsentNameComponent;
        }

        var chars = basic.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (LatinToCyrillic.TryGetValue(chars[i], out var cyr))
            {
                chars[i] = cyr;
            }
        }

        return new string(chars);
    }

    /// <summary>
    /// Нормализует поле документа (код типа, серия, номер): НФД, обрезка краевых пробелов,
    /// схлопывание пробелов, приведение к верхнему регистру.
    /// Отсутствующие значения становятся пустой строкой (отсутствующая серия совпадает
    /// с отсутствующей серией — 5791-У, Приложение 2, прим. 4).
    /// </summary>
    /// <param name="value">Исходное значение поля; может быть <see langword="null"/>.</param>
    /// <returns>Нормализованное значение; пустая строка, если значение отсутствует.</returns>
    public static string NormalizeDocumentField(string? value) => NormalizeBasic(value);

    /// <summary>
    /// Нормализует ИНН или СНИЛС: удаляет все нецифровые символы (решение по спецификации D2).
    /// Литерал <c>"-"</c>, пустая строка или значение без цифр обозначают отсутствующий идентификатор.
    /// </summary>
    /// <param name="value">Исходное значение ИНН/СНИЛС; может быть <see langword="null"/>.</param>
    /// <returns>Строка, состоящая только из цифр, либо <see langword="null"/>, если идентификатор отсутствует.</returns>
    public static string? NormalizeDigitsOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
            }
        }

        return digits.Length == 0 ? null : digits.ToString();
    }

    /// <summary>
    /// Сериализует дату в каноническую форму <c>yyyy-MM-dd</c>, используемую в прообразах ключей.
    /// </summary>
    /// <param name="date">Дата для сериализации.</param>
    /// <returns>Каноничное строковое представление даты.</returns>
    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd");

    private static string NormalizeBasic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var nfc = value.Normalize(NormalizationForm.FormC).Trim();
        var sb = new StringBuilder(nfc.Length);
        var pendingSpace = false;
        foreach (var c in nfc)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }
}
