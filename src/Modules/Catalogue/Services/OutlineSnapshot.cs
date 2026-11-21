using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;

namespace Plastoids.Modules.Catalogue.Services;

public sealed record OutlineLesson(Guid Id, string Type, string Title, int Order, int DurationMinutes, bool IsPreview, Guid? MediaAssetId, string? PlaybackId, JsonElement? Content);

public sealed record OutlineModule(Guid Id, string Title, int Order, IReadOnlyList<OutlineLesson> Lessons);

/// <summary>Builds the outline (modules → lessons) from the draft tables; serialised into CourseVersion on publish.</summary>
internal sealed class OutlineSnapshot(CatalogueDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<OutlineModule>> BuildAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var modules = await db.Modules.AsNoTracking().Where(m => m.CourseId == courseId).OrderBy(m => m.Order).ToListAsync(cancellationToken);
        var lessons = await db.Lessons.AsNoTracking().Where(l => l.CourseId == courseId).OrderBy(l => l.Order).ToListAsync(cancellationToken);
        var assetIds = lessons.Where(l => l.MediaAssetId is not null).Select(l => l.MediaAssetId!.Value).ToList();
        var assets = await db.MediaAssets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);

        return modules.Select(m => new OutlineModule(
            m.Id,
            m.Title,
            m.Order,
            lessons.Where(l => l.ModuleId == m.Id).Select(l => new OutlineLesson(
                l.Id,
                l.Type,
                l.Title,
                l.Order,
                l.DurationMinutes,
                l.IsPreview,
                l.MediaAssetId,
                l.MediaAssetId is { } id && assets.TryGetValue(id, out var asset) && asset.Status == MediaStatus.Ready ? asset.PlaybackId : null,
                l.ContentJson is null ? null : JsonDocument.Parse(l.ContentJson).RootElement.Clone())).ToList())).ToList();
    }

    public static string Serialise(IReadOnlyList<OutlineModule> outline) => JsonSerializer.Serialize(outline, Json);

    public static IReadOnlyList<OutlineModule> Deserialise(string json) => JsonSerializer.Deserialize<List<OutlineModule>>(json, Json) ?? [];
}
