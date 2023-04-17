// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using HtmlAgilityPack;
using System;
using System.Text.RegularExpressions;

namespace PostSharp.Engineering.BuildTools.Search.Crawlers;

public static class DocFxApiArticleHelper
{
    public static bool IsNextParagraphIgnored( HtmlNode paragraphInitialNode )
    {
        var nodeName = paragraphInitialNode.Name.ToLowerInvariant();

        if ( !Regex.IsMatch( nodeName, @"^h\d$" ) )
        {
            throw new InvalidOperationException( $"Expected h1-h6 instead of '{nodeName}'." );
        }
        
        var level = paragraphInitialNode.Name[1] - '0';

        string GetId() => paragraphInitialNode.GetAttributeValue<string>( "id", "" );

        switch ( level )
        {
            case 1: // For overview - eg: https://doc-production.metalama.net/api/metalama_framework_eligibility_describedobject-1
            case 4: // For method overloads - eg: https://doc-production.metalama.net/api/metalama_framework_eligibility_eligibilityextensions_mustbe
                return GetId().Length == "ID0EUAAC".Length; // Ignore list of members - eg: https://doc-production.metalama.net/api/metalama_framework_eligibility
            
            case 2: // For multi-paragraph description - eg: https://doc-production.metalama.net/api/metalama_framework_eligibility
                switch ( GetId() )
                {
                    case "conceptual-documentation":
                    case "overview":
                        return false;
                    
                    default:
                        return true;
                }
                
            default:
                return true;
        }
    }
}