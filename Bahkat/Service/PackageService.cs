﻿using System;
using System.ComponentModel;
using System.IO;
using Bahkat.Util;
using Microsoft.Win32;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using Bahkat.Models;
using System.Reactive.Disposables;

namespace Bahkat.Service
{
    public enum PackageInstallStatus
    {
        NotInstalled,
        UpToDate,
        RequiresUpdate,
        VersionSkipped,
        ErrorNoInstaller,
        ErrorParsingVersion
    }

    public static class PackageInstallStatusExtensions
    {
        public static string Description(this PackageInstallStatus status)
        {
            switch (status)
            {
                case PackageInstallStatus.ErrorNoInstaller:
                    return Strings.ErrorNoInstaller;
                case PackageInstallStatus.ErrorParsingVersion:
                    return Strings.ErrorInvalidVersion;
                case PackageInstallStatus.RequiresUpdate:
                    return Strings.UpdateAvailable;
                case PackageInstallStatus.NotInstalled:
                    return Strings.NotInstalled;
                case PackageInstallStatus.UpToDate:
                    return Strings.Installed;
                case PackageInstallStatus.VersionSkipped:
                    return Strings.VersionSkipped;
            }

            return null;
        }
    }

    public struct PackageProgress
    {
        public Package Package;
        public DownloadProgressChangedEventHandler Progress;
    }

    public struct PackagePath
    {
        public Package Package;
        public string Path;
    }

    public interface IPackageService
    {
        PackageInstallStatus GetInstallStatus(Package package);
        void SkipVersion(Package package);
        IObservable<PackagePath> Download(PackageProgress[] packages, int maxConcurrent, CancellationToken cancelToken);
    }
    
    public class PackageService : IPackageService
    {
        public static class Keys
        {
            public const string UninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
            public const string DisplayVersion = "DisplayVersion";
            public const string SkipVersion = "SkipVersion";
        }
        
        private readonly IWindowsRegistry _registry;
        
        public PackageService(IWindowsRegistry registry)
        {
            _registry = registry;
        }

        private PackageInstallStatus CompareVersion<T>(Func<string, T> creator, string packageVersion, string registryVersion) where T: IComparable<T>
        {
            var ver = creator(packageVersion);
            if (ver == null)
            {
                return PackageInstallStatus.ErrorParsingVersion;
            }
            
            var parsedDispVer = creator(registryVersion);
            if (parsedDispVer == null)
            {
                return PackageInstallStatus.ErrorParsingVersion;
            }

            if (ver.CompareTo(parsedDispVer) > 0)
            {
                return PackageInstallStatus.RequiresUpdate;
            }
            else
            {
                return PackageInstallStatus.UpToDate;
            }
        }

        private IObservable<string> DownloadFileTaskAsync(Uri uri, string dest, DownloadProgressChangedEventHandler onProgress, CancellationToken cancelToken)
        {
            using (var client = new WebClient { Encoding = Encoding.UTF8 })
            {
                if (onProgress != null)
                {
                    client.DownloadProgressChanged += onProgress;
                }

                cancelToken.Register(() => client.CancelAsync());

                client.DownloadFileTaskAsync(uri, dest);

                return Observable.Create<string>(observer =>
                {
                    // TODO: turn this into reactive extension... extension
                    var watcher = Observable.FromEventPattern<AsyncCompletedEventHandler, AsyncCompletedEventArgs>(
                        x => client.DownloadFileCompleted += x,
                        x => client.DownloadFileCompleted -= x)
                    .Select(x => x.EventArgs)
                    .Subscribe(args =>
                    {
                        if (args.Error != null)
                        {
                            observer.OnError(args.Error);
                        }
                        else
                        {
                            observer.OnNext(dest);
                        }

                        observer.OnCompleted();
                    });

                    return new CompositeDisposable((IDisposable)observer, watcher);
                });
            }
        }

        private IObservable<PackagePath> Download(PackageProgress pd, CancellationToken cancelToken)
        {
            var inst = pd.Package.Installer;
            
            // Get file ending from URL
            var ext = Path.GetExtension(inst.Url.AbsoluteUri);
            
            // Make name package name + version
            var fileName = $"{pd.Package.Id}-{pd.Package.Version}{ext}";
            var path = Path.Combine(Path.GetTempPath(), fileName);

            return DownloadFileTaskAsync(inst.Url, path, pd.Progress, cancelToken)
                .Select(x => new PackagePath { Package = pd.Package, Path = x });
        }

        public bool RequiresUpdate(Package package)
        {
            return GetInstallStatus(package) == PackageInstallStatus.RequiresUpdate;
        }
        
        /// <summary>
        /// Checks the registry for the installed package. Uses the "DisplayVersion" value and parses that using
        /// either the Assembly versioning technique or the Semantic versioning technique. Attempts Assembly first
        /// as this tends to be more common on Windows than other platforms.
        /// </summary>
        /// <param name="package"></param>
        /// <returns>The package install status</returns>
        public PackageInstallStatus GetInstallStatus(Package package)
        {
            if (package.Installer == null)
            {
                return PackageInstallStatus.ErrorNoInstaller;
            }

            var installer = package.Installer;
            var hklm = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
            var path = $@"{Keys.UninstallPath}\{installer.ProductCode}";
            var instKey = hklm.OpenSubKey(path);

            if (instKey == null)
            {
                return PackageInstallStatus.NotInstalled;
            }
            
            var displayVersion = instKey.Get(Keys.DisplayVersion, "");
            if (displayVersion == "")
            {
                return PackageInstallStatus.ErrorParsingVersion;
            }

            var comp = CompareVersion(AssemblyVersion.Create, package.Version, displayVersion);
            if (comp != PackageInstallStatus.ErrorParsingVersion)
            {
                return comp;
            }

            if (GetSkipVersion(package) == package.Version)
            {
                return PackageInstallStatus.VersionSkipped;
            }
                
            comp = CompareVersion(SemanticVersion.Create, package.Version, displayVersion);
            if (comp != PackageInstallStatus.ErrorParsingVersion)
            {
                return comp;
            }

            return PackageInstallStatus.ErrorParsingVersion;
        }

        public void SkipVersion(Package package)
        {
            var hklm = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
            var path = $@"{AppConfigState.Keys.SubkeyId}\{package.Installer.ProductCode}";
            var instKey = hklm.CreateSubKey(path);
            
            instKey.Set(Keys.SkipVersion, package.Version, RegistryValueKind.String);
        }

        private string GetSkipVersion(Package package)
        {
            var hklm = _registry.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
            var path = $@"{AppConfigState.Keys.SubkeyId}\{package.Installer.ProductCode}";
            var instKey = hklm.OpenSubKey(path);

            return instKey?.Get<string>(Keys.SkipVersion);
        }

        /// <summary>
        /// Downloads the supplied packages. Each object should contain a unique progress handler so the UI can be
        /// updated effectively.
        /// </summary>
        /// <param name="packages"></param>
        /// <param name="maxConcurrent"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public IObservable<PackagePath> Download(PackageProgress[] packages, int maxConcurrent, CancellationToken cancelToken)
        {
            return packages
                .Select(pkg => Download(pkg, cancelToken))
                .Merge(maxConcurrent);
        }
    }
}