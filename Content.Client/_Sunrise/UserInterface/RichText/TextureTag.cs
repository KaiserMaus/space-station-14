﻿using System.Diagnostics.CodeAnalysis;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.UserInterface.RichText;

public sealed class TextureTag : BaseTextureTag
{
    public override string Name => "tex";

    public override bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!node.Attributes.TryGetValue("path", out var rawPath) || !rawPath.TryGetString(out var path))
            return false;

        if (!node.Attributes.TryGetValue("scale", out var scale) || !scale.TryGetLong(out var scaleValue))
            scaleValue = 1;

        if (!TryDrawIcon(path, scaleValue.Value, out var texture))
            return false;

        control = texture;
        return true;
    }
}
