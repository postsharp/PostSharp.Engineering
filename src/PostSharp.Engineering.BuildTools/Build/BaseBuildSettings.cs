using Spectre.Console.Cli;
using System;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build;

/// <summary>
/// Base for <see cref="BuildSettings"/> and <see cref="PublishSettings"/>. Defines a <see cref="BuildConfiguration"/>
/// option that resolves to the configuration of the latest build if any was define, otherwise to Debug.
/// </summary>
public class BaseBuildSettings : CommonCommandSettings
{
    private BuildConfiguration? _resolvedConfiguration;

    [Description( "Sets the build configuration (Debug | Release | Public)" )]
    [CommandOption( "-c|--configuration" )]

    public BuildConfiguration? BuildConfiguration
    {
        [Obsolete( "Use ResolvedBuildConfiguration in consuming code." )]
        get;
        set;
    }

#pragma warning disable CS0618

    public BuildConfiguration ResolvedBuildConfiguration
        => this._resolvedConfiguration ?? this.BuildConfiguration
            ?? throw new InvalidOperationException( "Call the Initialize method or set the BuildConfiguration first ." );

    public override void Initialize( BuildContext context )
    {
        if ( this.BuildConfiguration != null )
        {
            this._resolvedConfiguration = this.BuildConfiguration.Value;
        }
        else
        {
            var defaultConfiguration = context.Product.ReadDefaultConfiguration( context );

            if ( defaultConfiguration == null )
            {
                context.Console.WriteMessage( $"Using the default configuration Debug." );

                this._resolvedConfiguration = Build.BuildConfiguration.Debug;
            }
            else
            {
                context.Console.WriteMessage( $"Using the prepared build configuration: {defaultConfiguration.Value}." );

                this._resolvedConfiguration = defaultConfiguration.Value;
            }
        }
    }
#pragma warning restore CS0618
}