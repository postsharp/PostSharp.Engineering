using Spectre.Console.Cli;
using System;
using System.ComponentModel;

namespace PostSharp.Engineering.BuildTools.Build;

public class TeamCityCommandSettings : CommonCommandSettings
{
    [Description( "Bump the product before Deployment" )]
    [CommandOption( "--bump" )]
    public bool Bump { get; protected set; }

    [Description( "Set specific ProductName to deploy/bump." )]
    [CommandArgument( 0, "[ProductName]" )]
    public string? ProductName { get; protected set; }

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

    [Description( "Simulate a continuous integration build by setting the build ContinuousIntegrationBuild property to TRUE." )]
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