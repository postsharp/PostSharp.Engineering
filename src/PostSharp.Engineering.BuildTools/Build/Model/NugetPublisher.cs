using PostSharp.Engineering.BuildTools.Utilities;
using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class NugetPublisher : Publisher
    {
        private readonly string _source;
        private readonly string _apiKey;
        private readonly bool _isPublic;

        public NugetPublisher( string source, string apiKey, bool isPublic )
        {
            this._source = source;
            this._apiKey = apiKey;
            this._isPublic = isPublic;
        }

        public override bool SupportsPublicPublishing => this._isPublic;

        public override bool SupportsPrivatePublishing => !this._isPublic;

        public override string Extension => ".nupkg";

        public override SuccessCode Execute( BuildContext context, PublishOptions options, string file, bool isPublic )
        {
            var hasEnvironmentError = false;

            context.Console.WriteMessage( $"Publishing {file}." );

            var server = Environment.ExpandEnvironmentVariables( this._source );

            // Check if environment variables have been defined.
            if ( string.IsNullOrEmpty( server ) )
            {
                context.Console.WriteError( $"The {this._source} environment variable is not defined." );
                hasEnvironmentError = true;
            }

            var apiKey = Environment.ExpandEnvironmentVariables( this._apiKey );

            if ( string.IsNullOrEmpty( apiKey ) )
            {
                context.Console.WriteError( $"The {this._apiKey} environment variable is not defined." );
                hasEnvironmentError = true;
            }

            if ( hasEnvironmentError )
            {
                return SuccessCode.Fatal;
            }

            // Note that we don't expand the ApiKey environment variable so we don't expose passwords to logs.
            var arguments =
                $"nuget push {file} --source {server} --api-key {this._apiKey} --skip-duplicate";

            if ( options.Dry )
            {
                context.Console.WriteImportantMessage( "Dry run: dotnet " + arguments );

                return SuccessCode.Success;
            }
            else
            {
                return ToolInvocationHelper.InvokeTool(
                    context.Console,
                    "dotnet",
                    arguments,
                    Environment.CurrentDirectory )
                    ? SuccessCode.Success
                    : SuccessCode.Error;
            }
        }
    }
}