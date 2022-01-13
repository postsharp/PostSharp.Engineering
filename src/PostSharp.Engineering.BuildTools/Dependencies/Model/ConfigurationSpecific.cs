using PostSharp.Engineering.BuildTools.Build;
using System;

namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public class ConfigurationSpecific<T>
{
    public T Debug { get; init; }

    public T Release { get; init; }

    public T Public { get; init; }

    public ConfigurationSpecific( T debug, T release, T @public )
    {
        this.Debug = debug;
        this.Release = release;
        this.Public = @public;
    }

    public ConfigurationSpecific( T value )
    {
        this.Debug = value;
        this.Release = value;
        this.Public = value;
    }

    public ConfigurationSpecific( in (T Debug, T Release, T Public) values )
    {
        this.Debug = values.Debug;
        this.Release = values.Release;
        this.Public = values.Public;
    }

    public T this[ BuildConfiguration configuration ]
        => configuration switch
        {
            BuildConfiguration.Debug => this.Debug,
            BuildConfiguration.Public => this.Public,
            BuildConfiguration.Release => this.Release,
            _ => throw new ArgumentOutOfRangeException()
        };

    public T GetValue( BuildConfiguration configuration ) => this[configuration];

    public ConfigurationSpecific<T> WithValue( BuildConfiguration configuration, T value )
        => configuration switch
        {
            BuildConfiguration.Debug => new ConfigurationSpecific<T>( value, this.Release, this.Public ),
            BuildConfiguration.Public => new ConfigurationSpecific<T>( this.Debug, value, this.Public ),
            BuildConfiguration.Release => new ConfigurationSpecific<T>( this.Debug, this.Release, value ),
            _ => throw new ArgumentOutOfRangeException()
        };

    public ConfigurationSpecific<T> WithValue( BuildConfiguration configuration, Func<T, T> func )
        => this.WithValue( configuration, func( this.GetValue( configuration ) ) );

    public override string ToString() => $"Debug={{{this.Debug}}}, Release={{{this.Release}}}, Public={{{this.Public}}}";
}