﻿using PostSharp.Engineering.BuildTools.Dependencies.Model;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Xml.Linq;

namespace PostSharp.Engineering.BuildTools.Utilities
{
    public class TeamcityClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public TeamcityClient( string token )
        {
            this._httpClient = new HttpClient();
            this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Bearer", token );
        }

        public string? GetBranchFromBuildNumber( CiBuildId buildId, CancellationToken cancellationToken )
        {
            var url =
                $"https://tc.postsharp.net/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildId.BuildTypeId},number:{buildId.BuildNumber}";

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

            var branch = build.Attribute( "branchName" )!.Value;

            const string prefix = "refs/heads/";

            if ( !branch.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
            {
                return null;
            }

            return branch.Substring( prefix.Length );
        }

        public CiBuildId? GetLatestBuildNumber( string buildTypeId, string branch, CancellationToken cancellationToken )
        {
            var url =
                $"https://tc.postsharp.net/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildTypeId},branch:refs/heads/{branch}";

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
            var url = $"https://tc.postsharp.net/repository/downloadAll/{buildTypeId}/{buildNumber}";
            var httpStream = this._httpClient.GetStreamAsync( url, cancellationToken ).Result;
            var memoryStream = new MemoryStream();
            httpStream.CopyToAsync( memoryStream, cancellationToken ).Wait( cancellationToken );
            memoryStream.Seek( 0, SeekOrigin.Begin );

            var zip = new ZipArchive( memoryStream, ZipArchiveMode.Read, false );
            zip.ExtractToDirectory( directory, true );
        }

        public void DownloadSingleArtifact( string buildTypeId, int buildNumber, string artifactPath, string saveLocation, CancellationToken cancellationToken )
        {
            var url = $"https://tc.postsharp.net/repository/download/{buildTypeId}/{buildNumber}/{artifactPath}";
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

        public void Dispose()
        {
            this._httpClient.Dispose();
        }
    }
}