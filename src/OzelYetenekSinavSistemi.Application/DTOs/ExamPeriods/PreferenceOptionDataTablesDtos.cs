using System.Text.Json.Serialization;

namespace OzelYetenekSinavSistemi.Application.DTOs.ExamPeriods;

/// <summary>Yönetici Tercih Seçenekleri DataTables satırı (salt okunur).</summary>
public sealed class PreferenceOptionTableRowDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("preferenceName")]
    public string PreferenceName { get; init; } = string.Empty;

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}

/// <summary>DataTables server-side JSON yanıtı.</summary>
public sealed class PreferenceOptionDataTablesResponse
{
    [JsonPropertyName("draw")]
    public int Draw { get; init; }

    [JsonPropertyName("recordsTotal")]
    public int RecordsTotal { get; init; }

    [JsonPropertyName("recordsFiltered")]
    public int RecordsFiltered { get; init; }

    [JsonPropertyName("data")]
    public IReadOnlyList<PreferenceOptionTableRowDto> Data { get; init; } = Array.Empty<PreferenceOptionTableRowDto>();

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}

/// <summary>DataTables istek parametreleri (normalize edilmiş).</summary>
public sealed class PreferenceOptionDataTablesQuery
{
    public Guid ExamPeriodId { get; init; }
    public int Draw { get; init; }
    public int Start { get; init; }
    public int Length { get; init; }
    public string? SearchValue { get; init; }
    public int OrderColumnIndex { get; init; }
    public string OrderDirection { get; init; } = "asc";
}

public sealed class PreferenceOptionPagedResult
{
    public int RecordsTotal { get; init; }
    public int RecordsFiltered { get; init; }
    public IReadOnlyList<PreferenceOptionTableRowDto> Rows { get; init; } = Array.Empty<PreferenceOptionTableRowDto>();
}
