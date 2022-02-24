// Copyright (c) SharpCrafters s.r.o.All rights reserved.Released under the MIT license.

using Amazon;
using PostSharp.Engineering.BuildTools.Build.Model;

namespace PostSharp.Engineering.BuildTools.AWS.S3.Publishers
{
    public class S3PublisherConfiguration
    {
        public ParametricString PackageFileName { get; init; }

        public RegionEndpoint RegionEnpoint { get; init; }

        public string BucketName { get; init; }

        public string KeyName { get; init; }

        public S3PublisherConfiguration(
            ParametricString packageFileName,
            RegionEndpoint regionEnpoint,
            string bucketName,
            string keyName )
        {
            this.PackageFileName = packageFileName;
            this.RegionEnpoint = regionEnpoint;
            this.BucketName = bucketName;
            this.KeyName = keyName;
        }
    }
}