using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class MsDeployPublisher : Publisher
    {
        private readonly ImmutableArray<MsDeployConfiguration> _configurations;

        public MsDeployPublisher( IEnumerable<MsDeployConfiguration> configurations )
            : base( Pattern.Create( configurations.Select( c => c.PackageFileName ).ToArray() ) )
        {
            this._configurations = ImmutableArray.Create<MsDeployConfiguration>().AddRange( configurations );
        }

        public override SuccessCode Execute( BuildContext context, PublishSettings settings, string file, VersionInfo version, BuildConfigurationInfo configuration )
        {
            var fileName = Path.GetFileName( file );
            var packageConfiguration = this._configurations.Single( c => c.PackageFileName.ToString( version ) == fileName );

            var hasEnvironmentError = false;

            var userName = $"{packageConfiguration.SiteName}__{packageConfiguration.SlotName}";
            var passwordEnvironmentVariableName = $"{new string( userName.Select( c => char.IsLetterOrDigit( c ) ? char.ToUpperInvariant( c ) : '_' ).ToArray() )}_PASSWORD";

            if ( string.IsNullOrEmpty( Environment.GetEnvironmentVariable( passwordEnvironmentVariableName ) ) )
            {
                context.Console.WriteError( $"The {passwordEnvironmentVariableName} environment variable is not defined." );
                hasEnvironmentError = true;
            }

            if ( hasEnvironmentError )
            {
                return SuccessCode.Fatal;
            }

            var exe = @"C:\Program Files\IIS\Microsoft Web Deploy V3\msdeploy.exe";

            // The arguments are taken from the log of the [Azure DevOps] [Azure App Service deploy] [release pipeline] step.
            var argsList = new List<string>
            {
                "-verb:sync",
                $"-source:package='{file}'",
                $"-dest:auto,ComputerName='https://{packageConfiguration.SiteName}-{packageConfiguration.SlotName}.scm.azurewebsites.net:443/msdeploy.axd?site={packageConfiguration.SiteName}',UserName='${userName}',Password='%{passwordEnvironmentVariableName}%',AuthType='Basic'",
                "-enableRule:AppOffline",
                "-retryAttempts:6",
                "-retryInterval:10000",
            };

            if ( packageConfiguration.VirtualDirectory != null )
            {
                if ( !packageConfiguration.VirtualDirectory.StartsWith( '/' ) )
                {
                    throw new InvalidOperationException( "The virtual directory has to start with a forward slash ('/')." );
                }

                argsList.Add( $"-setParam:name='IIS Web Application Name',value='{packageConfiguration.SiteName}{packageConfiguration.VirtualDirectory}'" );
            }

            var args = string.Join( ' ', argsList );

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