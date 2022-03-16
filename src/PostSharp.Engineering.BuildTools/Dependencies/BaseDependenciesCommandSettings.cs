using PostSharp.Engineering.BuildTools.Build;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Dependencies;

public class BaseDependenciesCommandSettings : CommonCommandSettings
{
    [Description( "Build configuration (Debug | Release | Public)" )]
    [CommandOption( "-c|--configuration" )]
    [Obsolete("Use the BuildConfiguration property. ")]
    public BuildConfiguration? BuildConfiguration { get; set; }

    public bool TryGetBuildConfiguration( BuildContext context, out BuildConfiguration configuration )
    {
#pragma warning disable CS0618
        if ( this.BuildConfiguration != null )
        {
            configuration = this.BuildConfiguration.Value;

            return true;
        }
#pragma warning restore CS0618

        var defaultConfiguration = context.Product.ReadDefaultConfiguration( context );

        if ( defaultConfiguration == null )
        {
            context.Console.WriteError( "The --configuration must be specified because there is no default configuration." );

            configuration = Build.BuildConfiguration.Debug;

            return false;
        }

        configuration = defaultConfiguration.Value;

        context.Console.WriteMessage( $"Using the prepared build configuration: {configuration}." );

        return true;
    }
}