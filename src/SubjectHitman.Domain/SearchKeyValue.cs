namespace SubjectHitman.Domain;

using SubjectHitman.Domain.Entities;

/// <summary>
/// Вычисленное значение поискового ключа: его тип и SHA-256 хеш канонического прообраза.
/// </summary>
/// <param name="KeyType">Тип поискового ключа (K1..K6).</param>
/// <param name="Hash">32-байтовый SHA-256 хеш.</param>
public readonly record struct SearchKeyValue(SearchKeyType KeyType, byte[] Hash)
{
    /// <summary>Сравнивает по типу ключа и содержимому хеша (не по ссылке).</summary>
    /// <param name="other">Другое значение.</param>
    /// <returns><see langword="true"/>, если тип и байты хеша равны.</returns>
    public bool Equals(SearchKeyValue other)
        => KeyType == other.KeyType && Hash.AsSpan().SequenceEqual(other.Hash);

    /// <summary>Хеш-код, вычисленный из типа ключа и префикса хеша.</summary>
    /// <returns>Хеш-код.</returns>
    public override int GetHashCode()
        => HashCode.Combine(KeyType, Hash.Length >= 4 ? BitConverter.ToInt32(Hash, 0) : 0);
}
