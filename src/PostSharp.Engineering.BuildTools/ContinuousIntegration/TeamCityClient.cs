﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration
{
    public class TeamCityClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public TeamCityClient( string baseAddress, string token )
        {
            this._httpClient = new HttpClient();
            this._httpClient.BaseAddress = new Uri( baseAddress );
            this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", token );
        }

        private static void ReportHttpErrorIfAny( HttpResponseMessage response, ConsoleHelper? console )
        {
            if ( !response.IsSuccessStatusCode )
            {
                if ( console == null )
                {
                    throw new ArgumentNullException( nameof(console) );
                }

                console.WriteError(
                    $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} failed with code {response.StatusCode}. {response.ReasonPhrase}" );

                console.WriteMessage( string.Join( Environment.NewLine, response.Content.ReadAsString().Split( '\n', '\r' ).Select( x => "> " + x ) ) );
            }
        }

        private bool TryGet( string path, ConsoleHelper? console, out HttpResponseMessage response, bool writeError = true )
        {
            response = this._httpClient.GetAsync( path, ConsoleHelper.CancellationToken ).ConfigureAwait( false ).GetAwaiter().GetResult();

            if ( writeError )
            {
                ReportHttpErrorIfAny( response, console );
            }

            return response.IsSuccessStatusCode;
        }

        private bool TryGet( string path, out HttpResponseMessage response ) => this.TryGet( path, null, out response, false );

        private bool TryPost( string path, string payload, ConsoleHelper console, out HttpResponseMessage response )
        {
            var content = new StringContent( payload, Encoding.UTF8, "application/xml" );
            response = this._httpClient.PostAsync( path, content, ConsoleHelper.CancellationToken ).ConfigureAwait( false ).GetAwaiter().GetResult();

            ReportHttpErrorIfAny( response, console );

            return response.IsSuccessStatusCode;
        }

        public bool TryGetBranchFromBuildNumber( ConsoleHelper console, CiBuildId buildId, [NotNullWhen( true )] out string? branch )
        {
            var path =
                $"/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildId.BuildTypeId},number:{buildId.BuildNumber}";

            if ( !this.TryGet( path, console, out var response ) )
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

        public CiBuildId? GetLatestBuildId( ConsoleHelper console, string buildTypeId, string branchName )
        {
            var path = $"/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildTypeId},branch:{branchName}";

            if ( !this.TryGet( path, console, out var response ) )
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

        public bool TryDownloadArtifacts( ConsoleHelper console, string buildTypeId, int buildNumber, string artifactsPath, string artifactsDirectory )
        {
            var throttler = new SemaphoreSlim( 4, 4 );
            var cancellationToken = ConsoleHelper.CancellationToken;

            async Task<bool> DownloadFileAsync( ProgressTask progress, string url, string targetFilePath )
            {
                try
                {
                    progress.StartTask();
                    var response = await this._httpClient.GetAsync( url, HttpCompletionOption.ResponseHeadersRead, cancellationToken );

                    if ( !response.IsSuccessStatusCode )
                    {
                        progress.Description( $"{progress.Description} failed: {response.StatusCode} {response.ReasonPhrase}" );

                        return false;
                    }

                    await using var httpStream = await this._httpClient.GetStreamAsync( url, cancellationToken );
                    await using var fileStream = File.Open( targetFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None );

                    var buffer = new byte[4096];
                    int bytesRead;

                    while ( (bytesRead = await httpStream.ReadAsync( buffer, 0, buffer.Length, cancellationToken )) != 0 )
                    {
                        await fileStream.WriteAsync( buffer, 0, bytesRead, cancellationToken );
                        progress.Increment( bytesRead );
                    }

                    return true;
                }
                finally
                {
                    throttler!.Release();
                    progress.StopTask();
                }
            }

            async Task<bool> DownloadDirectoryAsync(
                ProgressContext totalProgress,
                IEnumerable<(string Name, string Path, long Length)> files,
                string targetDirectory )
            {
                List<Task<bool>> fileDownloads = new();

                Directory.CreateDirectory( targetDirectory );

                foreach ( var file in files )
                {
                    var targetFilePath = Path.Combine( targetDirectory, file.Name );
                    var targetFileRelativePath = Path.GetRelativePath( artifactsDirectory, targetFilePath );
                    var fileProgress = totalProgress.AddTask( targetFileRelativePath, false, file.Length );
                    await throttler!.WaitAsync( cancellationToken );
                    fileDownloads.Add( Task.Run( () => DownloadFileAsync( fileProgress, file.Path, targetFilePath ), cancellationToken ) );
                }

                await Task.WhenAll( fileDownloads );

                return fileDownloads.All( d => d.GetAwaiter().GetResult() );
            }

            List<Task<bool>> directoryDownloads = new();

            void StartDownloadTree( ProgressContext progress, string urlOrPath, string targetDirectory )
            {
                if ( !this.TryGet( urlOrPath, console, out var response ) )
                {
                    throw new InvalidOperationException( $"Failed to get '{urlOrPath}'." );
                }

                var document = response.Content.ReadAsXDocument();

                (string Name, XElement Element)[] artifacts = document.Root!.Elements( "file" )
                    .Select( f => (f.Attribute( "name" )?.Value ?? throw new InvalidOperationException( "Unknown name of an artifact." ), f) )
                    .ToArray();

                IEnumerable<(string Name, string Url, long Size)> files = artifacts
                    .Select(
                        a => (
                            a.Name,
                            a.Element.Element( "content" )?.Attribute( "href" )?.Value,
                            long.Parse( a.Element.Attribute( "size" )?.Value ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture )) )
                    .Where( a => a.Value != null )
                    .Select( a => (a.Name, a.Value!, a.Item3) );

                directoryDownloads.Add( DownloadDirectoryAsync( progress, files, targetDirectory ) );

                IEnumerable<(string Name, string Url)> directories = artifacts
                    .Select( a => (a.Item1, a.Element.Element( "children" )?.Attribute( "href" )?.Value) )
                    .Where( a => a.Value != null )
                    .Select( a => (a.Item1, a.Value!) );

                foreach ( var directory in directories )
                {
                    var childTargetDirectory = Path.Combine( targetDirectory, directory.Name );
                    StartDownloadTree( progress, directory.Url, childTargetDirectory );
                }
            }

            async Task<bool> DownloadAllAsync( ProgressContext progress )
            {
                var basePath =
                    $"/app/rest/builds/defaultFilter:false,buildType:{buildTypeId},number:{buildNumber}/artifacts/children/{artifactsPath.Replace( '\\', '/' )}";

                var baseTargetDirectory = Path.Combine( artifactsDirectory, artifactsPath.Replace( '/', Path.DirectorySeparatorChar ) );

                StartDownloadTree( progress, basePath, baseTargetDirectory );

                await Task.WhenAll( directoryDownloads );

                return directoryDownloads.All( d => d.GetAwaiter().GetResult() );
            }

            var success = Task.Run(
                    () => AnsiConsole.Progress()
                        .Columns(
                            new TaskDescriptionColumn(),
                            new ProgressBarColumn(),
                            new DownloadedColumn(),
                            new TransferSpeedColumn(),
                            new RemainingTimeColumn(),
                            new ElapsedTimeColumn(),
                            new SpinnerColumn() )
                        .StartAsync( DownloadAllAsync ),
                    cancellationToken )
                .GetAwaiter()
                .GetResult();

            if ( !success )
            {
                console.WriteError( "Failed to fetch artifacts. Check the descriptions above." );
            }
            
            return success;
        }

        public string? ScheduleBuild( ConsoleHelper console, string buildTypeId, string comment, string? branchName = null )
        {
            var payload =
                $"<build buildTypeId=\"{buildTypeId}\"{(branchName == null ? "" : $" branchName=\"{branchName}\"")}><comment><text>{comment}</text></comment></build>";

            if ( !this.TryPost( "/app/rest/buildQueue", payload, console, out var response ) )
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

                    status = $"Build #{buildNumber} {build.Attribute( "state" )!.Value} ({build.Attribute( "percentageComplete" )!.Value}%)";
                }

                return status;
            }

            return status;
        }

        public bool IsBuildQueued( ConsoleHelper console, string buildId )
        {
            if ( !this.TryGet( TeamCityHelper.TeamcityApiBuildQueuePath, console, out var response ) )
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
            if ( !this.TryGet( TeamCityHelper.TeamCityApiRunningBuildsPath, console, out var response ) )
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
            if ( !this.TryGet( TeamCityHelper.TeamCityApiFinishedBuildsPath, console, out var response ) )
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
            if ( !this.TryGet( TeamCityHelper.TeamCityApiFinishedBuildsPath, console, out var response ) )
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

        private bool TryGetDetails( ConsoleHelper console, string path )
        {
            if ( !this.TryGet( path, console, out var response ) )
            {
                return false;
            }

            console.WriteMessage( response.Content.ReadAsXDocument().ToString() );

            return true;
        }

        public bool TryGetProjectDetails( ConsoleHelper console, string id ) => this.TryGetDetails( console, $"/app/rest/projects/id:{id}" );

        public bool TryGetOrderedSubprojectsRecursively( ConsoleHelper console, string projectId, [NotNullWhen( true )] out IReadOnlyList<(string Id, string Name)>? subprojects )
        {
            subprojects = null;
            
            if ( !this.TryGet( $"/app/rest/projects/id:{projectId}", console, out var projectResponse ) )
            {
                return false;
            }

            var projectRootElement = projectResponse.Content.ReadAsXDocument().Root!;

            var expectedCount = int.Parse(
                projectRootElement.Element( "projects" )!.Attribute( "count" )!.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture );
            
            var subprojectsList = new List<(string, string)>();

            if ( expectedCount > 0 )
            {
                var nextSubprojectsPath = $"/app/rest/projects/id:{projectId}/order/projects";

                do
                {
                    if ( !this.TryGet( nextSubprojectsPath, console, out var subprojectsResponse ) )
                    {
                        return false;
                    }

                    var subprojectsRootsElement = subprojectsResponse.Content.ReadAsXDocument().Root!;

                    var newExpectedCount = int.Parse( subprojectsRootsElement.Attribute( "count" )!.Value, NumberStyles.Integer, CultureInfo.InvariantCulture );

                    if ( newExpectedCount == 0 )
                    {
                        console.WriteError(
                            $"No ordered subprojects retrieved for '{projectId}' project. Expected {expectedCount}. Custom order may not be set for the project." );

                        return false;
                    }
                    else if ( expectedCount != newExpectedCount )
                    {
                        throw new InvalidOperationException( "Inconsistent subprojects count" );
                    }

                    foreach ( var subprojectElement in subprojectsRootsElement.Elements( "project" ) )
                    {
                        var subprojectId = subprojectElement.Attribute( "id" )!.Value;
                        var subprojectName = subprojectElement.Attribute( "name" )!.Value;
                        subprojectsList.Add( (subprojectId, subprojectName) );

                        if ( !this.TryGetOrderedSubprojectsRecursively( console, subprojectId, out var subProjectsSubprojects ) )
                        {
                            return false;
                        }

                        expectedCount += subProjectsSubprojects.Count;
                        subprojectsList.AddRange( subProjectsSubprojects );
                    }

                    nextSubprojectsPath = subprojectsRootsElement.Attribute( "nextHref" )?.Value;
                }
                while ( nextSubprojectsPath != null );
            }

            if ( subprojectsList.Count != expectedCount )
            {
                throw new InvalidOperationException( "Not all subprojects have been retrieved." );
            }
            
            subprojects = subprojectsList.ToImmutableArray();

            return true;
        }

        public bool TryGetProjectsBuildConfigurations( ConsoleHelper console, string projectId, [NotNullWhen( true )] out ImmutableArray<string>? buildConfigurations )
        {
            int? expectedCount = null;
            buildConfigurations = null;
            var buildConfigurationsList = new List<string>();

            var nextBuildConfigurationsPath = $"/app/rest/projects/id:{projectId}/buildTypes";

            do
            {
                if ( !this.TryGet( nextBuildConfigurationsPath, console, out var buildConfigurationsResponse ) )
                {
                    return false;
                }

                var buildConfigurationsRootsElement = buildConfigurationsResponse.Content.ReadAsXDocument().Root!;

                var newExpectedCount = int.Parse( buildConfigurationsRootsElement.Attribute( "count" )!.Value, NumberStyles.Integer, CultureInfo.InvariantCulture );

                if ( expectedCount == null )
                {
                    expectedCount = newExpectedCount;
                }
                else if ( expectedCount != newExpectedCount )
                {
                    throw new InvalidOperationException( "Inconsistent build configurations count" );
                }

                foreach ( var buildConfigurationsElement in buildConfigurationsRootsElement.Elements( "buildType" ) )
                {
                    var buildConfigurationId = buildConfigurationsElement.Attribute( "id" )!.Value;
                    buildConfigurationsList.Add( buildConfigurationId );
                }

                nextBuildConfigurationsPath = buildConfigurationsRootsElement.Attribute( "nextHref" )?.Value;
            }
            while ( nextBuildConfigurationsPath != null );

            buildConfigurations = buildConfigurationsList.ToImmutableArray();

            if ( expectedCount == null )
            {
                throw new InvalidOperationException( "Unknown expected count." );
            }
            else if ( buildConfigurations.Value.Length != expectedCount )
            {
                throw new InvalidOperationException( "Not all build configurations have been retrieved." );
            }

            return true;
        }
        
        public bool TryGetBuildConfigurationsSnapshotDependencies( ConsoleHelper console, string buildConfigurationId, [NotNullWhen( true )] out ImmutableArray<string>? snapshotDependencies )
        {
            int? expectedCount = null;
            snapshotDependencies = null;
            var snapshotDependenciesList = new List<string>();

            var nextSnapshotDependenciesPath = $"/app/rest/buildTypes/id:{buildConfigurationId}/snapshot-dependencies";

            do
            {
                if ( !this.TryGet( nextSnapshotDependenciesPath, console, out var snapshotDependenciesResponse ) )
                {
                    return false;
                }

                var snapshotDependenciesRootsElement = snapshotDependenciesResponse.Content.ReadAsXDocument().Root!;

                var newExpectedCount = int.Parse( snapshotDependenciesRootsElement.Attribute( "count" )!.Value, NumberStyles.Integer, CultureInfo.InvariantCulture );

                if ( expectedCount == null )
                {
                    expectedCount = newExpectedCount;
                }
                else if ( expectedCount != newExpectedCount )
                {
                    throw new InvalidOperationException( "Inconsistent snapshot dependencies count" );
                }

                foreach ( var snapshotDependenciesElement in snapshotDependenciesRootsElement.Elements( "snapshot-dependency" ) )
                {
                    var snapshotDependencyId = snapshotDependenciesElement.Attribute( "id" )!.Value;
                    snapshotDependenciesList.Add( snapshotDependencyId );
                }

                nextSnapshotDependenciesPath = snapshotDependenciesRootsElement.Attribute( "nextHref" )?.Value;
            }
            while ( nextSnapshotDependenciesPath != null );

            snapshotDependencies = snapshotDependenciesList.ToImmutableArray();

            if ( expectedCount == null )
            {
                throw new InvalidOperationException( "Unknown expected count." );
            }
            else if ( snapshotDependencies.Value.Length != expectedCount )
            {
                throw new InvalidOperationException( "Not all snapshot dependencies have been retrieved." );
            }

            return true;
        }
        
        public bool TryCreateProject( ConsoleHelper console, string name, string id, string? parentId = null )
        {
            parentId ??= "_Root";

            var payload = $@"<newProjectDescription id=""{id}"" name=""{name}"">
  <parentProject locator=""id:{parentId}"" />
</newProjectDescription>";

            return this.TryPost( "/app/rest/projects", payload, console, out _ );
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

            return this.TryPost( $"/app/rest/projects/id:{projectId}/projectFeatures", payload, console, out _ );
        }

        public bool TryGetProjectVersionedSettingsConfiguration( ConsoleHelper console, string projectId )
            => this.TryGetDetails( console, $"/app/rest/projects/id:{projectId}/versionedSettings/config" );

        public bool TrySetProjectVersionedSettingsConfiguration( ConsoleHelper console, string projectId, string vcsRootId )
            => this.TryPost(
                $"/app/rest/projects/id:{projectId}/versionedSettings/config",
                $"<versionedSettingsConfig allowUIEditing=\"false\" buildSettingsMode=\"useFromVCS\" format=\"kotlin\" showSettingsChanges=\"true\" synchronizationMode=\"enabled\" vcsRootId=\"{vcsRootId}\" />",
                console,
                out _ );

        public bool TryLoadProjectVersionedSettings( ConsoleHelper console, string projectId )
            => this.TryPost(
                $"/app/rest/projects/id:{projectId}/versionedSettings/loadSettings",
                "",
                console,
                out _ );

        public bool TryGetVcsRootDetails( ConsoleHelper console, string id ) => this.TryGetDetails( console, $"/app/rest/vcs-roots/id:{id}" );

        public bool TryGetVcsRoots( ConsoleHelper console, string projectId, [NotNullWhen( true )] out ImmutableArray<(string Id, string Url)>? vcsRoots )
        {
            int? expectedCount = null;
            vcsRoots = null;
            var vcsRootsList = new List<(string Id, string Url)>();

            var nextVcsRootsPath = $"/app/rest/vcs-roots?locator=project:(id:{projectId})";

            do
            {
                if ( !this.TryGet( nextVcsRootsPath, console, out var vcsRootsResponse ) )
                {
                    return false;
                }

                var vcsRootsElement = vcsRootsResponse.Content.ReadAsXDocument().Root!;

                var newExpectedCount = int.Parse( vcsRootsElement.Attribute( "count" )!.Value, NumberStyles.Integer, CultureInfo.InvariantCulture );

                if ( expectedCount == null )
                {
                    expectedCount = newExpectedCount;
                }
                else if ( expectedCount != newExpectedCount )
                {
                    throw new InvalidOperationException( "Inconsistent VCS roots count" );
                }

                foreach ( var partialVcsRootElement in vcsRootsElement.Elements( "vcs-root" ) )
                {
                    var vcsRootPath = partialVcsRootElement.Attribute( "href" )!.Value;

                    if ( !this.TryGet( vcsRootPath, console, out var vcsRootResponse ) )
                    {
                        return false;
                    }

                    var vcsRootElement = vcsRootResponse.Content.ReadAsXDocument().Root!;
                    var vcsRootId = vcsRootElement.Attribute( "id" )!.Value;

                    var vcsRootUrl = vcsRootElement
                        .Element( "properties" )
                        !.Elements( "property" )
                        .Single( p => p.Attribute( "name" )!.Value == "url" )
                        .Attribute( "value" )!
                        .Value;

                    vcsRootsList.Add( (vcsRootId, vcsRootUrl) );
                }

                nextVcsRootsPath = vcsRootsElement.Attribute( "nextHref" )?.Value;
            }
            while ( nextVcsRootsPath != null );

            vcsRoots = vcsRootsList.ToImmutableArray();

            if ( expectedCount == null )
            {
                throw new InvalidOperationException( "Unknown expected count." );
            }
            else if ( vcsRoots.Value.Length != expectedCount )
            {
                throw new InvalidOperationException( "Not all VCS roots have been retrieved." );
            }

            return true;
        }

        public bool TryCreateVcsRoot(
            ConsoleHelper console,
            string url,
            string? projectId,
            string defaultBranch,
            IEnumerable<string> branchSpecification,
            [NotNullWhen( true )] out string? name,
            [NotNullWhen( true )] out string? id )
        {
            projectId ??= "_Root";

            var properties = new List<(string Name, string Value)>();

            void AddProperty( string name, string value ) => properties.Add( (name, value) );

            if ( AzureDevOpsRepository.TryParse( url, out var azureDevOpsRepository ) )
            {
                name = azureDevOpsRepository.Name;
                AddProperty( "authMethod", "PASSWORD" );
                AddProperty( "username", "teamcity@postsharp.net" );
                AddProperty( "secure:password", "%SourceCodeWritingToken%" );
                AddProperty( "usernameStyle", "EMAIL" );
            }
            else if ( GitHubRepository.TryParse( url, out var gitHubRepository ) )
            {
                name = gitHubRepository.Name;
                AddProperty( "authMethod", "TEAMCITY_SSH_KEY" );
                AddProperty( "teamcitySshKey", "PostSharp.Engineering" );
                AddProperty( "usernameStyle", "USERID" );
            }
            else
            {
                console.WriteError( $"Unknown VCS provider: {url}" );
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
            AddProperty( "branch", defaultBranch );
            AddProperty( "teamcity:branchSpec", string.Join( "&#xA;", branchSpecification ) );

            id = $"{(projectId == "_Root" ? "Root" : projectId)}_{name.Replace( ".", "", StringComparison.Ordinal )}";

            var payload = $@"<vcs-root id=""{id}"" name=""{name}"" vcsName=""jetbrains.git"">
   <project id=""{projectId}""/>
   <properties>
     {string.Join( Environment.NewLine, properties.Select( p => $"<property name=\"{p.Name}\" value=\"{p.Value}\" />" ) )}
   </properties>
</vcs-root>";

            return this.TryPost( "/app/rest/vcs-roots", payload, console, out _ );
        }

        public bool TryGetBuildTypeConfiguration( ConsoleHelper console, string buildTypeId, [NotNullWhen( true )] out XDocument? configuration )
        {
            if ( !this.TryGet( $"/app/rest/buildTypes/id:{buildTypeId}", console, out var response ) )
            {
                configuration = null;

                return false;
            }

            configuration = response.Content.ReadAsXDocument();

            return true;
        }

        public void Dispose()
        {
            this._httpClient.Dispose();
        }
    }
}