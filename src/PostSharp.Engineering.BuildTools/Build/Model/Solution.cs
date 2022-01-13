using System;
using System.IO;

namespace PostSharp.Engineering.BuildTools.Build.Model
{
    public abstract class Solution
    {
        public virtual string Name => Path.GetFileName( this.SolutionPath );

        public string SolutionPath { get; }

        public bool IsTestOnly { get; init; }

        public bool PackRequiresExplicitBuild { get; init; }

        public bool SupportsTestCoverage { get; init; }

        public bool CanFormatCode { get; init; }

        public string[] FormatExclusions { get; init; } = Array.Empty<string>();

        [Obsolete( "Use BuildMethod=Build" )]
        public bool CanPack
        {
            get => this.BuildMethod == Model.BuildMethod.Pack;
            init => this.BuildMethod = value ? Model.BuildMethod.Pack : Model.BuildMethod.Build;
        }

        public BuildMethod? BuildMethod { get; init; }

        public BuildMethod GetBuildMethod() => this.BuildMethod ?? (this.IsTestOnly ? Model.BuildMethod.Build : Model.BuildMethod.Pack);

        public abstract bool Build( BuildContext context, BuildSettings settings );

        public abstract bool Pack( BuildContext context, BuildSettings settings );

        public abstract bool Test( BuildContext context, BuildSettings settings );

        public abstract bool Restore( BuildContext context, BaseBuildSettings settings );

        protected Solution( string solutionPath )
        {
            this.SolutionPath = solutionPath;
        }
    }
}