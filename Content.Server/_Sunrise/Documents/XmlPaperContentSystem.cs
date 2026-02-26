using System.Text.RegularExpressions;
using Content.Shared.Paper;
using Robust.Shared.ContentPack;

namespace Content.Server._Sunrise.Documents;

public sealed class XmlPaperContentSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly DocumentFormatSystem _format = default!;

    private static readonly Regex DocRegex =
        new("<Document>(.*?)</Document>", RegexOptions.Singleline | RegexOptions.Compiled);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XmlPaperContentComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<XmlPaperContentComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<PaperComponent>(ent, out var paper))
            return;

        using var file = _resources.ContentFileReadText(ent.Comp.Path);
        var fileText = file.ReadToEnd();
        var match = DocRegex.Match(fileText);
        var content = match.Success ? match.Groups[1].Value.Trim() : fileText.Trim();
        content = _format.Format(content, ent);

        _paper.SetContent((ent.Owner, paper), content);

        if (ent.Comp.Header != null)
            _paper.SetImageContent((ent.Owner, paper), ent.Comp.Header);
    }
}
