using Spectre.Console.Cli;
using System;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build;

/// <summary>
/// Base for <see cref="BuildSettings"/> and <see cref="PublishSettings"/>. Defines a <see cref="ExplicitBuildConfiguration"/>
/// option that resolves to the configuration of the latest build if any was define, otherwise to Debug.
/// </summary>
public class BaseBuildSettings : CommonCommandSettings
{
    private BuildConfiguration? _resolvedConfiguration;

    [Obsolete( "Use the BuildConfiguration property. " )]
    [Description( "Sets the build configuration (Debug | Release | Public)" )]
    [CommandOption( "-c|--configuration" )]
    public BuildConfiguration? ExplicitBuildConfiguration { get; set; }

#pragma warning disable CS0618

    public BuildConfiguration BuildConfiguration
    {
        get
            => this._resolvedConfiguration ?? this.ExplicitBuildConfiguration
                ?? throw new InvalidOperationException( "Call the Initialize method or set the BuildConfiguration first ." );
        set
        {
            this._resolvedConfiguration = value;
            this.ExplicitBuildConfiguration = value;
        }
    }

    [Obsolete( "Use the BuildConfiguration property." )]
    public BuildConfiguration ResolvedBuildConfiguration => this.BuildConfiguration;

    [Description( "Simulate a continuous integration build by setting the build ContinuousIntegrationBuild properyt to TRUE." )]
    [CommandOption( "--ci" )]
    public bool ContinuousIntegration { get; set; }

    public override void Initialize( BuildContext context )
    {
        if ( this.ExplicitBuildConfiguration != null )
        {
            this._resolvedConfiguration = this.ExplicitBuildConfiguration.Value;
        }
        else
        {
            var defaultConfiguration = context.Product.ReadDefaultConfiguration( context );

            if ( defaultConfiguration == null )
            {
                context.Console.WriteMessage( $"Using the default configuration Debug." );

                this._resolvedConfiguration = BuildConfiguration.Debug;
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