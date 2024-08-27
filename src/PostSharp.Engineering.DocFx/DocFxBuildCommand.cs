// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Docfx;
using JetBrains.Annotations;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using PostSharp.Engineering.BuildTools;
using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.DocFx.Markdig;

namespace PostSharp.Engineering.DocFx;

[UsedImplicitly]
internal class DocFxBuildCommand : BaseCommand<DocFxSettings>
{
    protected override bool ExecuteCore( BuildContext context, DocFxSettings settings )
    {
        var commandData = (DocFxCommandData) context.CommandData;

        var options = new BuildOptions
        {
            ConfigureMarkdig = markdig =>
            {
                // Disable HtmlBlockParser to let inline elemets in HTML blocks be parsed.
                // E.g. Xref links. (<xref:...>)
                var htmlBlockParser = markdig.BlockParsers.Find<HtmlBlockParser>();

                if ( htmlBlockParser != null )
                {
                    markdig.BlockParsers.Remove( htmlBlockParser );
                }

                // Enable HTML parsing in AutolinkInlineParser to prevent escaping of HTML tags.
                // (This has nothing to do with autolink parsing, but the AutoplinkInlineParser provides this feature.)
                var autolinkInlineParser = markdig.InlineParsers.Find<AutolinkInlineParser>()!;
                autolinkInlineParser.EnableHtmlParsing = true;

                markdig.Extensions.AddIfNotAlready<CommentBlockExtension>();

                commandData.Options.ConfigureMarkdig?.Invoke( markdig );

                return markdig;
            }
        };

        Docset.Build( settings.ConfigurationPath, options ).Wait();

        return true;
    }
}