using PostSharp.Engineering.BuildTools.Build.Model;
using PostSharp.Engineering.BuildTools.Utilities;
using System;

namespace PostSharp.Engineering.BuildTools.Build.Publishers
{
    /// <summary>
    /// Publishes VSIX packages to Visual Studio Marketplace. 
    /// </summary>
    public class VsixPublisher : ArtifactPublisher
    {
        public VsixPublisher( Pattern files ) : base( files ) { }

        public override SuccessCode PublishFile(
            BuildContext context,
            PublishSettings settings,
            string file,
            BuildInfo buildInfo,
            BuildConfigurationInfo configuration )
        {
            var hasEnvironmentError = false;

            if ( string.IsNullOrEmpty( Environment.GetEnvironmentVariable( "VSSDKINSTALL" ) ) )
            {
                context.Console.WriteError( $"The VSSDKINSTALL environment variable is not defined." );
                hasEnvironmentError = true;
            }

            if ( string.IsNullOrEmpty( Environment.GetEnvironmentVariable( "VS_MARKETPLACE_ACCESS_TOKEN" ) ) )
            {
                context.Console.WriteError( $"The VS_MARKETPLACE_ACCESS_TOKEN environment variable is not defined." );
                hasEnvironmentError = true;
            }

            if ( hasEnvironmentError )
            {
                return SuccessCode.Fatal;
            }

            var vsSdkDir = Environment.GetEnvironmentVariable( "VSSDKINSTALL" );

            var exe = $@"{vsSdkDir}\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe";

            var args =
                $" publish -payload \"{file}\" -publishManifest \"{file}.json\" -personalAccessToken \"%VS_MARKETPLACE_ACCESS_TOKEN%\"";

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