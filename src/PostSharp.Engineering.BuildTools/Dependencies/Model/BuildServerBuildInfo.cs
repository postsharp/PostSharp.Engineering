namespace PostSharp.Engineering.BuildTools.Dependencies.Model;

public interface ICiBuildSpec { }

public record CiBuildId( int BuildNumber, string? BuildTypeId ) : ICiBuildSpec;

public record CiBranch( string Name ) : ICiBuildSpec;