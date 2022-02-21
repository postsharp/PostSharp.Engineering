namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public record CiBuildId( int BuildNumber, string? BuildTypeId ) : ICiBuildSpec;