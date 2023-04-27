// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using System;

namespace PostSharp.Engineering.BuildTools.Build;

/// <summary>
/// Base for <see cref="BuildSettings"/> and <see cref="PublishSettings"/>. Defines a <see cref="BuildConfiguration"/>
/// option that resolves to the configuration of the latest build if any was define, otherwise to Debug.
/// </summary>
public class BaseBuildSettings : CommonCommandSettings
{
    private BuildConfiguration? _resolvedConfiguration;

    public BuildConfiguration BuildConfiguration
    {
        get
            => this._resolvedConfiguration
               ?? throw new InvalidOperationException( "Call the Initialize method or set the BuildConfiguration first ." );
        set => this._resolvedConfiguration = value;
    }

    public override void Initialize( BuildContext context )
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