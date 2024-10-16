﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using JetBrains.Annotations;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console.Cli;
using System;
using System.IO;
using System.Xml;

#pragma warning disable 8765

namespace PostSharp.Engineering.BuildTools.Csproj
{
    [UsedImplicitly]
    public class AddProjectReferenceCommand : Command<AddProjectReferenceSettings>
    {
        public override int Execute( CommandContext context, AddProjectReferenceSettings settings )
        {
            var console = new ConsoleHelper();

            foreach ( var project in Directory.EnumerateFiles(
                         Directory.GetCurrentDirectory(),
                         $"*{settings.Filter}*.csproj",
                         SearchOption.AllDirectories ) )
            {
                AddReference( console, project, settings.PreviousReference, settings.NewReference );
            }

            return 0;
        }

        private static void AddReference(
            ConsoleHelper console,
            string project,
            string existingReference,
            string newReference )
        {
            console.WriteImportantMessage( Path.GetFileName( project ) + ": " );

            var xml = new XmlDocument();
            xml.Load( project );

            var newReferenceFileName = Path.GetFileName( newReference );

            var newReferenceItem =
                xml.SelectSingleNode( $"//ProjectReference[contains(@Include,'{newReferenceFileName}')]" );

            if ( newReferenceItem != null )
            {
                console.WriteImportantMessage( "skipped - contains new reference" );

                return;
            }

            var existingReferenceItem =
                xml.SelectSingleNode( $"//ProjectReference[contains(@Include,'{existingReference}')]" );

            if ( existingReferenceItem == null )
            {
                console.WriteImportantMessage( $"skipped - doesn't reference {existingReference}" );

                return;
            }

            // https://stackoverflow.com/questions/1766748/how-do-i-get-a-relative-path-from-one-path-to-another-in-c-sharp
            var projectUri = new Uri( project );
            var newReferenceFullPath = Path.GetFullPath( newReference );
            var newReferenceUri = new Uri( newReferenceFullPath );
            var newReferenceRelativeUri = projectUri.MakeRelativeUri( newReferenceUri );

            var newReferenceRelativePath =
                Uri.UnescapeDataString( newReferenceRelativeUri.OriginalString )
                    .Replace( "/", "\\", StringComparison.OrdinalIgnoreCase );

            newReferenceItem = xml.CreateElement( "ProjectReference" );
            var newReferenceAttribute = xml.CreateAttribute( "Include" );
            newReferenceAttribute.Value = newReferenceRelativePath;
            newReferenceItem.Attributes!.Append( newReferenceAttribute );

            existingReferenceItem.ParentNode!.InsertAfter( newReferenceItem, existingReferenceItem );
            xml.Save( project );

            console.WriteImportantMessage( "modified" );
        }
    }
}