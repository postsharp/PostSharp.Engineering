// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration
{
    public class TeamCityClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public TeamCityClient( string baseAddress, string token )
        {
            this._httpClient = new();
            this._httpClient.BaseAddress = new Uri( baseAddress );
            this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", token );
        }
        
        private static bool IsSuccessResponse( HttpResponseMessage response, ConsoleHelper? console, string? description, bool writeError = true )
        {
            if ( !response.IsSuccessStatusCode )
            {
                if ( writeError )
                {
                    if ( console == null )
                    {
                        throw new ArgumentNullException( nameof(console) );
                    }

                    if ( string.IsNullOrEmpty( description ) )
                    {
                        throw new ArgumentOutOfRangeException( nameof(description) );
                    }

                    console.WriteError( $"Failed to {description}." );
                    console.WriteError( $"{response.StatusCode}: {response.ReasonPhrase}" );
                    console.WriteError( response.Content.ReadAsString() );
                }

                return false;
            }

            return true;
        }

        private bool TryGet( string path, ConsoleHelper? console, string? description, out HttpResponseMessage response, bool writeError = true )
        {
            response = this._httpClient.GetAsync( path, ConsoleHelper.CancellationToken ).ConfigureAwait( false ).GetAwaiter().GetResult();

            return IsSuccessResponse( response, console, description, writeError );
        }

        private bool TryGet( string path, out HttpResponseMessage response ) => this.TryGet( path, null, null, out response, false );

        private bool TryPost( string path, string payload, ConsoleHelper console, string description, out HttpResponseMessage response )
        {
            var content = new StringContent( payload, Encoding.UTF8, "application/xml" );
            response = this._httpClient.PostAsync( path, content, ConsoleHelper.CancellationToken ).ConfigureAwait( false ).GetAwaiter().GetResult();

            return IsSuccessResponse( response, console, description );
        }

        public bool TryGetBranchFromBuildNumber( ConsoleHelper console, CiBuildId buildId, [NotNullWhen( true )] out string? branch )
        {
            var path =
                $"/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildId.BuildTypeId},number:{buildId.BuildNumber}";

            if ( !this.TryGet( path, console, $"determine the branch of '{buildId}'", out var response ) )
            {
                branch = null;

                return false;
            }

            var document = response.Content.ReadAsXDocument();
            var build = document.Root?.Elements( "build" ).FirstOrDefault();

            if ( build == null )
            {
                console.WriteError( $"Cannot determine the branch of '{buildId}': cannot find any build in '{path}'." );

                branch = null;

                return false;
            }

            branch = build.Attribute( "branchName" )!.Value;

            const string prefix = "refs/heads/";

            if ( branch.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
            {
                branch = branch.Substring( prefix.Length );
            }

            if ( string.IsNullOrEmpty( branch ) )
            {
                console.WriteError( $"Cannot determine the branch of '{buildId}': the branch name is empty." );
                
                branch = null;

                return false;
            }

            return true;
        }

        public CiBuildId? GetLatestBuildNumber( string buildTypeId, string branchName, bool isDefaultBranch )
        {
            // In some cases, the default branch is not set for the TeamCity build and we need to use the "default:true" locator.
            var branchLocator = isDefaultBranch ? "default:true" : $"refs/heads/{branchName}";

            var path = $"/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildTypeId},branch:{branchLocator}";

            if ( !this.TryGet( path, out var response ) )
            {
                return null;
            }

            var document = response.Content.ReadAsXDocument();
            var build = document.Root?.Elements( "build" ).FirstOrDefault();

            if ( build == null )
            {
                return null;
            }
            else
            {
                return new CiBuildId( int.Parse( build.Attribute( "number" )!.Value, CultureInfo.InvariantCulture ), buildTypeId );
            }
        }

        public void DownloadArtifacts( string buildTypeId, int buildNumber, string directory )
        {
            var path = $"/repository/downloadAll/{buildTypeId}/{buildNumber}";
            using var httpStream = this._httpClient.GetStream( path );
            using var memoryStream = new MemoryStream();
            httpStream.CopyTo( memoryStream );
            memoryStream.Seek( 0, SeekOrigin.Begin );

            using var zip = new ZipArchive( memoryStream, ZipArchiveMode.Read, false );
            zip.ExtractToDirectory( directory, true );
        }

        public void DownloadSingleArtifact( string buildTypeId, int buildNumber, string artifactPath, string saveLocation )
        {
            var path = $"/repository/download/{buildTypeId}/{buildNumber}/{artifactPath}";
            using var httpStream = this._httpClient.GetStream( path );
            using var fileStream = File.OpenWrite( saveLocation );
            httpStream.CopyTo( fileStream );
        }

        public string? ScheduleBuild( ConsoleHelper console, string buildTypeId, string comment, string? branchName = null )
        {
            var payload = $"<build buildTypeId=\"{buildTypeId}\"{(branchName == null ? "" : $" branchName=\"{branchName}\"")}><comment><text>{comment}</text></comment></build>";

            if ( !this.TryPost( "/app/rest/buildQueue", payload, console, "schedule build", out var response ) )
            {
                return null;
            }

            var document = response.Content.ReadAsXDocument();
            var build = document.Root;

            return build!.Attribute( "id" )!.Value;
        }

        public string PollRunningBuildStatus( string buildId, out string buildNumber )
        {
            var status = $"Build starting...";
            buildNumber = string.Empty;

            _ = this.TryGet( TeamCityHelper.TeamCityApiRunningBuildsPath, out var response );

            var document = response.Content.ReadAsXDocument();
            var builds = document.Root!;

            if ( !builds.Attribute( "count" )!.Value.Equals( "0", StringComparison.Ordinal ) )
            {
                var build = builds.Elements().ToArray().FirstOrDefault( e => e.Attribute( "id" )!.Value.Equals( buildId, StringComparison.Ordinal ) );

                if ( build != null && build.Attribute( "percentageComplete" ) != null )
                {
                    if ( build.Attribute( "number" ) != null )
                    {
                        buildNumber = build.Attribute( "number" )!.Value;
                    }

                    status = $"Build #{buildNumber} in progress: {build.Attribute( "percentageComplete" )!.Value}%";
                }

                return status;
            }

            return status;
        }

        public bool IsBuildQueued( ConsoleHelper console, string buildId )
        {
            if ( !this.TryGet( TeamCityHelper.TeamcityApiBuildQueuePath, console, "retrieve build queue", out var response ) )
            {
                return false;
            }

            var document = response.Content.ReadAsXDocument();
            var builds = document.Root!;

            if ( builds.Attribute( "count" )!.Value.Equals( "0", StringComparison.Ordinal ) )
            {
                return false;
            }

            var build = builds.Elements().ToArray().FirstOrDefault( e => e.Attribute( "id" )!.Value.Equals( buildId, StringComparison.Ordinal ) );

            if ( build == null )
            {
                return false;
            }

            return true;
        }

        public bool IsBuildRunning( ConsoleHelper console, string buildId )
        {
            if ( !this.TryGet( TeamCityHelper.TeamCityApiRunningBuildsPath, console, "retrieve running builds", out var response ) )
            {
                return false;
            }

            var document = response.Content.ReadAsXDocument();
            var builds = document.Root!;

            if ( builds.Attribute( "count" )!.Value.Equals( "0", StringComparison.Ordinal ) )
            {
                console.WriteError( "No running TeamCity builds found. This might be a TeamCity API problem." );

                return false;
            }

            var build = builds.Elements().ToArray().FirstOrDefault( e => e.Attribute( "id" )!.Value.Equals( buildId, StringComparison.Ordinal ) );

            if ( build == null )
            {
                return false;
            }

            return true;
        }

        public bool HasBuildFinishedSuccessfully( ConsoleHelper console, string buildId )
        {
            if ( !this.TryGet( TeamCityHelper.TeamCityApiFinishedBuildsPath, console, "retrieve finished builds", out var response ) )
            {
                return false;
            }

            var document = response.Content.ReadAsXDocument();
            var builds = document.Root!;

            if ( builds.Attribute( "count" )!.Value.Equals( "0", StringComparison.Ordinal ) )
            {
                console.WriteError( "No finished TeamCity builds found. This might be a TeamCity API problem." );

                return false;
            }

            var build = builds.Elements().ToArray().FirstOrDefault( e => e.Attribute( "id" )!.Value.Equals( buildId, StringComparison.Ordinal ) );

            if ( build == null )
            {
                console.WriteError( $"No successfully finished TeamCity build with ID '{buildId}' found." );

                return false;
            }

            if ( !build.Attribute( "status" )!.Value.Equals( "SUCCESS", StringComparison.OrdinalIgnoreCase ) )
            {
                return false;
            }

            return true;
        }

        public bool HasBuildFinished( ConsoleHelper console, string buildId )
        {
            if ( !this.TryGet( TeamCityHelper.TeamCityApiFinishedBuildsPath, console, "retrieve finished builds", out var response ) )
            {
                return false;
            }

            var document = response.Content.ReadAsXDocument();
            var builds = document.Root!;

            if ( builds.Attribute( "count" )!.Value.Equals( "0", StringComparison.Ordinal ) )
            {
                console.WriteError( "No finished TeamCity builds found. This might be a TeamCity API problem." );

                return false;
            }

            var build = builds.Elements().ToArray().FirstOrDefault( e => e.Attribute( "id" )!.Value.Equals( buildId, StringComparison.Ordinal ) );

            if ( build == null )
            {
                return false;
            }

            return true;
        }

        private bool TryGetDetails( ConsoleHelper console, string path, string description )
        {
            if ( !this.TryGet( path, console, description, out var response ) )
            {
                return false;
            }
            
            console.WriteMessage( response.Content.ReadAsXDocument().ToString() );

            return true;
        }

        public bool TryGetProjectDetails( ConsoleHelper console, string id )
            => this.TryGetDetails( console, $"/app/rest/projects/id:{id}", "retrieve project details" );
        
        public bool TryCreateProject( ConsoleHelper console, string name, string id, string parentId = "_Root" )
        {
            var payload = $@"<newProjectDescription id=""{id}"" name=""{name}"">
  <parentProject locator=""id:{parentId}"" />
</newProjectDescription>";

            return this.TryPost( "/app/rest/projects", payload, console, "create project", out _ );
        }

        public bool TrySetProjectVersionedSettings( ConsoleHelper console, string projectId, string vcsRootId )
        {
            var payload = $@"<projectFeature type=""versionedSettings"">
       <properties>
        <!-- The following settings come from the project versioned settings feature. -->
        <property name=""buildSettings"" value=""PREFER_VCS"" />
        <property name=""credentialsStorageType"" value=""credentialsJSON"" />
        <property name=""enabled"" value=""true"" />
        <property name=""format"" value=""kotlin"" />
        <property name=""rootId"" value=""{vcsRootId}"" />
        <property name=""showChanges"" value=""true"" />
        <property name=""twoWaySynchronization"" value=""false"" />
        <property name=""useRelativeIds"" value=""true"" />

        <!-- The following settings come from the project versioned settings configuration. -->
        <property name=""allowUIEditing"" value=""false"" />
        <property name=""buildSettingsMode"" value=""useFromVCS"" />
        <property name=""showSettingsChanges"" value=""true"" />
        <property name=""synchronizationMode"" value=""true"" />
      </properties>
</projectFeature>";

            return this.TryPost( $"/app/rest/projects/id:{projectId}/projectFeatures", payload, console, "set project versioned settings", out _ );
        }
        
        public bool TryGetProjectVersionedSettingsConfiguration( ConsoleHelper console, string projectId )
            => this.TryGetDetails( console, $"/app/rest/projects/id:{projectId}/versionedSettings/config", "get versioned settings configuration" );

        public bool TrySetProjectVersionedSettingsConfiguration( ConsoleHelper console, string projectId, string vcsRootId )
            => this.TryPost(
                $"/app/rest/projects/id:{projectId}/versionedSettings/config",
                $"<versionedSettingsConfig allowUIEditing=\"false\" buildSettingsMode=\"useFromVCS\" format=\"kotlin\" showSettingsChanges=\"true\" synchronizationMode=\"enabled\" vcsRootId=\"{vcsRootId}\" />",
                console,
                "set project versioned settings configuration",
                out _ );

        public bool TryLoadProjectVersionedSettings( ConsoleHelper console, string projectId )
            => this.TryPost(
                $"/app/rest/projects/id:{projectId}/versionedSettings/loadSettings",
                "",
                console,
                "load versioned settings",
                out _ );

        public bool TryGetVcsRootDetails( ConsoleHelper console, string id )
            => this.TryGetDetails( console, $"/app/rest/vcs-roots/id:{id}", "retrieve VCS root details" );

        public bool TryCreateVcsRoot(
            ConsoleHelper console,
            string url,
            string projectId,
            string version,
            [NotNullWhen( true )] out string? name,
            [NotNullWhen( true )] out string? id )
        {
            var properties = new List<(string Name, string Value)>();

            void AddProperty( string name, string value ) => properties!.Add( (name, value) );

            if ( AzureDevOpsRepoUrlParser.TryParse( url, out _, out _, out name ) )
            {
                AddProperty( "authMethod", "PASSWORD" );
                AddProperty( "username", "teamcity@postsharp.net" );
                AddProperty( "secure:password", "%SourceCodeWritingToken%" );
                AddProperty( "usernameStyle", "EMAIL" );
            }
            else if ( GitHubRepoUrlParser.TryParse( url, out _, out name ) )
            {
                AddProperty( "authMethod", "TEAMCITY_SSH_KEY" );
                AddProperty( "teamcitySshKey", "PostSharp.Engineering" );
                AddProperty( "usernameStyle", "USERID" );
            }
            else
            {
                console.WriteError( "Unknown VCS provider." );
                name = null;
                id = null;

                return false;
            }

            AddProperty( "url", url );
            AddProperty( "agentCleanFilesPolicy", "ALL_UNTRACKED" );
            AddProperty( "agentCleanPolicy", "ALWAYS" );
            AddProperty( "ignoreKnownHosts", "true" );
            AddProperty( "submoduleCheckout", "CHECKOUT" );
            AddProperty( "useAlternates", "USE_MIRRORS" );
            AddProperty( "branch", $"refs/heads/develop/{version}" );

            AddProperty(
                "teamcity:branchSpec",
                $"+:refs/heads/(topic/{version}/*)&#xA;+:refs/heads/(feature/{version}/*)&#xA;+:refs/heads/(experimental/{version}/*)&#xA;+:refs/heads/(develop/{version})&#xA;+:refs/heads/(release/{version})" );
            
            id = $"{projectId}_{name.Replace( ".", "", StringComparison.Ordinal )}";

            var payload = $@"<vcs-root id=""{id}"" name=""{name}"" vcsName=""jetbrains.git"">
   <project id=""{projectId}""/>
   <properties>
     {string.Join( Environment.NewLine, properties.Select( p => $"<property name=\"{p.Name}\" value=\"{p.Value}\" />" ) )}
   </properties>
</vcs-root>";

            return this.TryPost( "/app/rest/vcs-roots", payload, console, "create VCS root", out _ );
        }

        public void Dispose()
        {
            this._httpClient.Dispose();
        }
    }
}