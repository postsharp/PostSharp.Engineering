using System.IO;
using System.Linq;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    internal class TeamCityProject
    {
        private readonly TeamCityBuildConfiguration[] _configurations;

        public TeamCityProject( TeamCityBuildConfiguration[] configurations )
        {
            this._configurations = configurations;
        }

        public void GenerateTeamcityCode( TextWriter writer )
        {
            writer.WriteLine(
                @"// This file is automatically generated when you do `Build.ps1 prepare`.

import jetbrains.buildServer.configs.kotlin.v2019_2.*

// Both Swabra and swabra need to be imported
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.sshAgent
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.Swabra
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.swabra
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.powerShell
import jetbrains.buildServer.configs.kotlin.v2019_2.triggers.*

version = ""2021.2""

project {
" );

            foreach ( var configuration in this._configurations )
            {
                writer.WriteLine( $"   buildType({configuration.ObjectName})" );
            }

            var configurationsOrder = string.Join( ',', this._configurations.Select( c => c.ObjectName ) );
            writer.WriteLine( $"   buildTypesOrder = arrayListOf({configurationsOrder})" );

            writer.WriteLine( "}" );

            writer.WriteLine();

            foreach ( var configuration in this._configurations )
            {
                configuration.GenerateTeamcityCode( writer );
                writer.WriteLine();
            }
        }
    }
}