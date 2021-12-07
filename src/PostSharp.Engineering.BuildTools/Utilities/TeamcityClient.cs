using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
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
            this._httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue( "Bearer", token );
        }

        public int? GetLatestBuildNumber( string buildTypeId, string branch, CancellationToken cancellationToken )
        {
            var url =
                $"https://tc.postsharp.net/app/rest/builds?locator=defaultFilter:false,state:finished,status:SUCCESS,buildType:{buildTypeId},branch:refs/heads/{branch}";

            var result = this._httpClient.GetAsync( url, cancellationToken ).Result.Content.ReadAsStringAsync( cancellationToken ).Result;
            var xmlResult = XDocument.Parse( result );
            var build = xmlResult.Root?.Elements( "build" ).FirstOrDefault();

            if ( build == null )
            {
                return null;
            }
            else
            {
                return int.Parse( build.Attribute( "number" )!.Value, CultureInfo.InvariantCulture );
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

        public void Dispose()
        {
            this._httpClient.Dispose();
        }
    }
}