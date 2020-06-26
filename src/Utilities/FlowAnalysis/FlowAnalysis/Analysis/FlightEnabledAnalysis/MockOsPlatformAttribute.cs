// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace System.Runtime.Versioning
{
    public abstract class OSPlatformVersionAttribute : Attribute
    {
        protected OSPlatformVersionAttribute(string osPlatform,
                                              int major,
                                              int minor,
                                              int build,
                                              int revision)
        {
            PlatformIdentifier = osPlatform;
            Major = major;
            Minor = minor;
            Build = build;
            Revision = revision;
        }

        public string PlatformIdentifier { get; }
        public int Major { get; }
        public int Minor { get; }
        public int Build { get; }
        public int Revision { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
                    AllowMultiple = false, Inherited = true)]
    public sealed class AddedInOSPlatformVersionAttribute : OSPlatformVersionAttribute
    {
        public AddedInOSPlatformVersionAttribute(string osPlatform,
                                          int major,
                                          int minor,
                                          int build,
                                          int revision) : base(osPlatform, major, minor, build, revision)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
                    AllowMultiple = false, Inherited = true)]
    public sealed class RemovedInOSPlatformVersionAttribute : OSPlatformVersionAttribute
    {
        public RemovedInOSPlatformVersionAttribute(string osPlatform,
                                            int major,
                                            int minor,
                                            int build,
                                            int revision) : base(osPlatform, major, minor, build, revision)
        { }
    }

    [AttributeUsage(AttributeTargets.Assembly |
                    AttributeTargets.Class |
                    AttributeTargets.Constructor |
                    AttributeTargets.Event |
                    AttributeTargets.Method |
                    AttributeTargets.Module |
                    AttributeTargets.Property |
                    AttributeTargets.Struct,
                    AllowMultiple = false, Inherited = true)]
    public sealed class ObsoletedInOSPlatformVersionAttribute : OSPlatformVersionAttribute
    {
        public ObsoletedInOSPlatformVersionAttribute(string osPlatform,
                                              int major,
                                              int minor,
                                              int build,
                                              int revision) : base(osPlatform, major, minor, build, revision)
        { }
        public string? Url { get; set; }
    }
}

namespace System.Runtime.InteropServices
{
    public static class RuntimeInformationHelper
    {
#pragma warning disable CA1801, IDE0060 // Review unused parameters
        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major)
        {
            return RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor)
        {
            return RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build)
        {
            return RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformOrLater(OSPlatform osPlatform, int major, int minor, int build, int revision)
        {
            return RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major)
        {
            return !RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor)
        {
            return !RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build)
        {
            return !RuntimeInformation.IsOSPlatform(osPlatform);
        }

        public static bool IsOSPlatformEarlierThan(OSPlatform osPlatform, int major, int minor, int build, int revision)
        {
            return !RuntimeInformation.IsOSPlatform(osPlatform);
        }
#pragma warning restore CA1801, IDE0060 // Review unused parameters
    }
}
