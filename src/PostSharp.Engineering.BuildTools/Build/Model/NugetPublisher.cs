using PostSharp.Engineering.BuildTools.Utilities;
using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class NugetPublisher : Publisher
    {
        private readonly string _source;
        private readonly string _apiKey;

        public NugetPublisher( Pattern files, string source, string apiKey ) : base( files )
        {
            this._source = source;
            this._apiKey = apiKey;
        }

        public override SuccessCode Execute( BuildContext context, PublishSettings settings, string file, VersionInfo version, BuildConfigurationInfo configuration )
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

            var exe = "dotnet";

            // Note that we don't expand the ApiKey environment variable so we don't expose passwords to logs.
            var args =
                $"nuget push {file} --source {server} --api-key {this._apiKey} --skip-duplicate";

            if ( settings.Dry )
            {
                context.Console.WriteImportantMessage( $"Dry run: {exe} {args}" );

                return SuccessCode.Success;
            }
            else
            {
                return ToolInvocationHelper.InvokeTool(
                    context.Console,
                    exe,
                    args,
                    Environment.CurrentDirectory )
                    ? SuccessCode.Success
                    : SuccessCode.Error;
            }
        }
    }
}