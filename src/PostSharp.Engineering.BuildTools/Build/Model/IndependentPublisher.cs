using System;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class IndependentPublisher : Publisher
    {
        /// <summary>
        /// Executes the independent publisher.
        /// </summary>
        public abstract SuccessCode Execute(
            BuildContext context,
            PublishSettings settings,
            BuildInfo buildInfo,
            BuildConfigurationInfo configuration );
        
        protected override bool Publish(
            BuildContext context,
            PublishSettings settings,
            (string Private, string Public) directories,
            BuildConfigurationInfo configuration,
            BuildInfo buildInfo,
            bool isPublic,
            ref bool hasTarget,
            ref bool allFilesSucceeded )
        {
            var success = true;
            
            switch ( this.Execute( context, settings, buildInfo, configuration ) )
            {
                case SuccessCode.Success:
                    break;

                case SuccessCode.Error:
                    success = false;

                    break;

                case SuccessCode.Fatal:
                    return false;

                default:
                    throw new NotImplementedException();
            }

            if ( !success )
            {
                context.Console.WriteError( "Independent publishing has failed." );
            }

            return success;
        }
    }
}