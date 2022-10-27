// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Model;

internal class DefaultBumpStrategy : IBumpStrategy
{
    public bool TryBumpVersion(
        Product product,
        BuildContext context,
        [NotNullWhen( true )] out Version? oldVersion,
        [NotNullWhen( true )] out Version? newVersion )
    {
        var mainVersionFile = Path.Combine(
            context.RepoDirectory,
            product.MainVersionFilePath );

        if ( !File.Exists( mainVersionFile ) )
        {
            context.Console.WriteError( $"The file '{mainVersionFile}' does not exist." );

            oldVersion = null;
            newVersion = null;

            return false;
        }

        var currentMainVersionFile = product.ReadMainVersionFile( mainVersionFile );

        oldVersion = new Version( currentMainVersionFile.MainVersion );

        // Increment the version.
        newVersion = new Version(
            oldVersion.Major,
            oldVersion.Minor,
            oldVersion.Build + 1 );

        // Save the MainVersion.props with new version.
        if ( !TrySaveMainVersion( context, mainVersionFile, newVersion ) )
        {
            return false;
        }

        context.Console.WriteSuccess( $"Bumping the '{context.Product.ProductName}' version from '{oldVersion}' to '{newVersion}' was successful." );

        return true;
    }

    private static bool TrySaveMainVersion(
        BuildContext context,
        string mainVersionFile,
        Version version )
    {
        if ( !File.Exists( mainVersionFile ) )
        {
            context.Console.WriteError( $"Could not save '{mainVersionFile}': the file does not exist." );

            return false;
        }

        var document = XDocument.Load( mainVersionFile );
        var project = document.Root;
        var properties = project!.Element( "PropertyGroup" );
        var mainVersionElement = properties!.Element( "MainVersion" );
        var ourPatchVersionElement = properties.Element( "OurPatchVersion" );

        mainVersionElement!.Value = version.ToString();

        // Using settings to keep the indentation as well as encoding identical to original MainVersion.props.
        var xmlWriterSettings =
            new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "    ", Encoding = new UTF8Encoding( false ) };

        using ( var xmlWriter = XmlWriter.Create( mainVersionFile, xmlWriterSettings ) )
        {
            document.Save( xmlWriter );
        }

        context.Console.WriteMessage( $"Writing '{mainVersionFile}'." );

        return true;
    }
}