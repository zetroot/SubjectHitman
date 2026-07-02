namespace SubjectHitman.Api.Domain.Entities;

/// <summary>
/// Предвычисленный ключ поиска субъекта: SHA-256 хэш над нормализованной конкатенацией
/// подмножества персональных данных субъекта (см. техническую спецификацию, § 5.3).
/// </summary>
public class SearchKey
{
    /// <summary>Суррогатный идентификатор.</summary>
    public long Id { get; set; }

    /// <summary>Идентификатор субъекта-владельца.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Тип ключа (K1..K6).</summary>
    public SearchKeyType KeyType { get; set; }

    /// <summary>32-байтовый SHA-256 хэш канонического прообраза ключа.</summary>
    public byte[] Hash { get; set; } = [];
}
