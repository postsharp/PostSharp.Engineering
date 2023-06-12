// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.ContinuousIntegration.Model;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration;

public abstract class VcsRepository
{
    public abstract string Name { get; }
    
    public abstract VcsProvider Provider { get; }
    
    public abstract string SshUrl { get; }

    public abstract string HttpUrl { get; }
    
    /// <summary>
    /// Gets the repository URL used by developers on their machines to access the repository.
    /// </summary>
    public abstract string DeveloperMachineRemoteUrl { get; }
    
    /// <summary>
    /// Gets the repository URL used by TeamCity server and agents to access the repository.
    /// </summary>
    public abstract string TeamCityRemoteUrl { get; }
    
    public abstract bool SshAgentRequired { get; }
    
    public abstract string DownloadTextFile( string branch, string path );

    /// <summary>
    /// Returns a string identifying the repository.
    /// </summary>
    /// <remarks>No matter which data is contained in the resulting string, the use of the result should be for UI and debugging only.</remarks>
    /// <returns>A string identifying the repository.</returns>
    public abstract override string ToString();
}