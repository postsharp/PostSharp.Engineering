// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using PostSharp.Engineering.BuildTools.Build;
using PostSharp.Engineering.BuildTools.Dependencies.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.ContinuousIntegration
{
    public class TeamCityClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public TeamCityClient( string? token = null )
        {
            this._httpClient = new HttpClient();
            this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", token );
        }

        public bool TryGetBranchFromBuildNumber( ConsoleHelper console, CiBuildId buildId, [NotNullWhen( true )] out string? branch )
        {
            var cancellationToken = ConsoleHelper.CancellationToken;

            var url =
                $"{TeamCityHelper.TeamCityUrl}/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildId.BuildTypeId},number:{buildId.BuildNumber}";

            var result = this._httpClient.GetAsync( url, cancellationToken ).Result;

            if ( !result.IsSuccessStatusCode )
            {
                console.WriteError( $"Cannot determine the branch of '{buildId}': '{url}' returned {result.StatusCode} {result.ReasonPhrase}." );
                branch = null;

                return false;
            }

            var content = result.Content.ReadAsStringAsync( cancellationToken ).Result;
            var xmlResult = XDocument.Parse( content );
            var build = xmlResult.Root?.Elements( "build" ).FirstOrDefault();

            if ( build == null )
            {
                console.WriteError( $"Cannot determine the branch of '{buildId}': cannot find any build in '{url}'." );

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

        public CiBuildId? GetLatestBuildNumber( string buildTypeId, string branchName, bool isDefaultBranch, CancellationToken cancellationToken )
        {
            // In some cases, the default branch is not set for the TeamCity build and we need to use the "default:true" locator.
            var branchLocator = isDefaultBranch ? "default:true" : $"refs/heads/{branchName}";
            
            var url =
                $"{TeamCityHelper.TeamCityUrl}/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildTypeId},branch:{branchLocator}";

            var result = this._httpClient.GetAsync( url, cancellationToken ).Result;

            if ( !result.IsSuccessStatusCode )
            {
                return null;
            }

            var content = result.Content.ReadAsStringAsync( cancellationToken ).Result;
            var xmlResult = XDocument.Parse( content );
            var build = xmlResult.Root?.Elements( "build" ).FirstOrDefault();

            if ( build == null )
            {
                return null;
            }
            else
            {
                return new CiBuildId( int.Parse( build.Attribute( "number" )!.Value, CultureInfo.InvariantCulture ), buildTypeId );
            }
        }

        public void DownloadArtifacts( string buildTypeId, int buildNumber, string directory, CancellationToken cancellationToken )
        {
            var url = $"{TeamCityHelper.TeamCityUrl}/repository/downloadAll/{buildTypeId}/{buildNumber}";
            var httpStream = this._httpClient.GetStreamAsync( url, cancellationToken ).Result;
            var memoryStream = new MemoryStream();
            httpStream.CopyToAsync( memoryStream, cancellationToken ).Wait( cancellationToken );
            memoryStream.Seek( 0, SeekOrigin.Begin );

            var zip = new ZipArchive( memoryStream, ZipArchiveMode.Read, false );
            zip.ExtractToDirectory( directory, true );
        }

        public void DownloadSingleArtifact( string buildTypeId, int buildNumber, string artifactPath, string saveLocation, CancellationToken cancellationToken )
        {
            var url = $"{TeamCityHelper.TeamCityUrl}/repository/download/{buildTypeId}/{buildNumber}/{artifactPath}";
            var httpResponse = this._httpClient.GetAsync( url, cancellationToken ).Result;

            using ( var stream = httpResponse.Content.ReadAsStreamAsync( cancellationToken ).Result )
            {
                var fileInfo = new FileInfo( saveLocation );

                using ( var fileStream = fileInfo.OpenWrite() )
                {
                    stream.CopyToAsync( fileStream, cancellationToken );
                }
            }
        }

        // TODO: Remove the requestUri parameter in 2023.1
        public string? ScheduleBuild( ConsoleHelper console, string buildTypeId, string comment, string? branchName = null, string? requestUri = null )
        {
            requestUri = requestUri ?? TeamCityHelper.TeamcityApiBuildQueueUri;
            var payload = $"<build buildTypeId=\"{buildTypeId}\"{(branchName == null ? "" : $" branchName=\"{branchName}\"")}><comment><text>{comment}</text></comment></build>";
            var content = new StringContent( payload, Encoding.UTF8, "application/xml" );
            var httpResponseResult = this._httpClient.PostAsync( requestUri, content ).Result;

            if ( !httpResponseResult.IsSuccessStatusCode )
            {
                console.WriteError( "Failed to schedule build." );
                console.WriteError( $"{httpResponseResult.StatusCode} {httpResponseResult.ReasonPhrase}" );
                console.WriteError( httpResponseResult.Content.ReadAsStringAsync().ConfigureAwait( false ).GetAwaiter().GetResult() );
                
                return null;
            }

            var httpResponseMessageContentString = httpResponseResult.Content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).Result;
            var document = XDocument.Parse( httpResponseMessageContentString );
            var build = document.Root;

            return build!.Attribute( "id" )!.Value;
        }

        public string PollRunningBuildStatus( string buildId, out string buildNumber )
        {
            var status = $"Build starting...";
            buildNumber = string.Empty;

            var httpResponseResult = this._httpClient.GetAsync( TeamCityHelper.TeamCityApiRunningBuildsUri ).Result;

            var httpResponseMessageContentString = httpResponseResult.Content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).Result;

            var document = XDocument.Parse( httpResponseMessageContentString );
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

        public bool IsBuildQueued( BuildContext context, string buildId )
        {
            var httpResponseResult = this._httpClient.GetAsync( TeamCityHelper.TeamcityApiBuildQueueUri ).Result;

            if ( !httpResponseResult.IsSuccessStatusCode )
            {
                context.Console.WriteError(
                    $"Failed to retrieve TeamCity builds queue from API URI '{TeamCityHelper.TeamcityApiBuildQueueUri}': HTTP status code: {httpResponseResult.StatusCode}." );

                return false;
            }

            var httpResponseMessageContentString = httpResponseResult.Content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).Result;

            var document = XDocument.Parse( httpResponseMessageContentString );
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

        public bool IsBuildRunning( BuildContext context, string buildId )
        {
            var httpResponseResult = this._httpClient.GetAsync( TeamCityHelper.TeamCityApiRunningBuildsUri ).Result;

            if ( !httpResponseResult.IsSuccessStatusCode )
            {
                context.Console.WriteError(
                    $"Failed to retrieve TeamCity running builds from API URI '{TeamCityHelper.TeamCityApiRunningBuildsUri}': HTTP status code: {httpResponseResult.StatusCode}." );

                return false;
            }

            var httpResponseMessageContentString = httpResponseResult.Content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).Result;

            var document = XDocument.Parse( httpResponseMessageContentString );
            var builds = document.Root!;

            if ( builds.Attribute( "count" )!.Value.Equals( "0", StringComparison.Ordinal ) )
            {
                context.Console.WriteError( "No running TeamCity builds found. This might be a TeamCity API problem." );

                return false;
            }

            var build = builds.Elements().ToArray().FirstOrDefault( e => e.Attribute( "id" )!.Value.Equals( buildId, StringComparison.Ordinal ) );

            if ( build == null )
            {
                return false;
            }

            return true;
        }

        public bool HasBuildFinishedSuccessfully( BuildContext context, string buildId )
        {
            var httpResponseResult = this._httpClient.GetAsync( TeamCityHelper.TeamCityApiFinishedBuildsUri ).Result;

            if ( !httpResponseResult.IsSuccessStatusCode )
            {
                context.Console.WriteError(
                    $"Failed to retrieve TeamCity finished builds from API URI '{TeamCityHelper.TeamCityApiFinishedBuildsUri}': HTTP status code: {httpResponseResult.StatusCode}." );

                return false;
            }

            var httpResponseMessageContentString = httpResponseResult.Content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).Result;

            var document = XDocument.Parse( httpResponseMessageContentString );
            var builds = document.Root!;

            if ( builds.Attribute( "count" )!.Value.Equals( "0", StringComparison.Ordinal ) )
            {
                context.Console.WriteError( "No finished TeamCity builds found. This might be a TeamCity API problem." );

                return false;
            }

            var build = builds.Elements().ToArray().FirstOrDefault( e => e.Attribute( "id" )!.Value.Equals( buildId, StringComparison.Ordinal ) );

            if ( build == null )
            {
                context.Console.WriteError( $"No successfully finished TeamCity build with ID '{buildId}' found." );

                return false;
            }

            if ( !build.Attribute( "status" )!.Value.Equals( "SUCCESS", StringComparison.OrdinalIgnoreCase ) )
            {
                return false;
            }

            return true;
        }

        public bool HasBuildFinished( BuildContext context, string buildId )
        {
            var httpResponseResult = this._httpClient.GetAsync( TeamCityHelper.TeamCityApiFinishedBuildsUri ).Result;

            if ( !httpResponseResult.IsSuccessStatusCode )
            {
                context.Console.WriteError(
                    $"Failed to retrieve TeamCity finished builds from API URI '{TeamCityHelper.TeamCityApiFinishedBuildsUri}': HTTP status code: {httpResponseResult.StatusCode}." );

                return false;
            }

            var httpResponseMessageContentString = httpResponseResult.Content.ReadAsStringAsync( ConsoleHelper.CancellationToken ).Result;

            var document = XDocument.Parse( httpResponseMessageContentString );
            var builds = document.Root!;

            if ( builds.Attribute( "count" )!.Value.Equals( "0", StringComparison.Ordinal ) )
            {
                context.Console.WriteError( "No finished TeamCity builds found. This might be a TeamCity API problem." );

                return false;
            }

            var build = builds.Elements().ToArray().FirstOrDefault( e => e.Attribute( "id" )!.Value.Equals( buildId, StringComparison.Ordinal ) );

            if ( build == null )
            {
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            this._httpClient.Dispose();
        }
    }
}