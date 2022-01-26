using PostSharp.Engineering.BuildTools.Utilities;
using System;
using System.Collections.Generic;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public class MsDeployPublisher : Publisher
    {
        public string SiteName { get; init; }

        public string SlotName { get; init; } = "staging";

        public string? VirtualDirectory { get; init; }

        public MsDeployPublisher( string wepPackageFile, string siteName )
            : base( Pattern.Create( wepPackageFile ) )
        {
            this.SiteName = siteName;
        }

        public override SuccessCode Execute( BuildContext context, PublishSettings settings, string file, BuildConfigurationInfo configuration )
        {
            var hasEnvironmentError = false;

            var userName = $"{this.SiteName}__{this.SlotName}";
            var passwordEnvironmentVariableName = $"{userName}_PASSWORD".ToUpperInvariant();

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
                $"-dest:auto,ComputerName='https://{this.SiteName}-{this.SlotName}.scm.azurewebsites.net:443/msdeploy.axd?site={this.SiteName}',UserName='${userName}',Password='%{passwordEnvironmentVariableName}%',AuthType='Basic'",
                "-enableRule:AppOffline",
                "-retryAttempts:6",
                "-retryInterval:10000",
            };

            if ( this.VirtualDirectory != null )
            {
                if ( !this.VirtualDirectory.StartsWith( '/' ) )
                {
                    throw new InvalidOperationException( "The virtual directory has to start with a forward slash ('/')." );
                }

                argsList.Add( $"-setParam:name='IIS Web Application Name',value='{this.SiteName}{this.VirtualDirectory}'" );
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
                    ? SuccessCode.Error
                    : SuccessCode.Fatal;
            }
        }
    }
}