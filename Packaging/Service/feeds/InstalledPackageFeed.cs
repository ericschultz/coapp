﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Packaging.Service.Feeds {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using PackageFormatHandlers;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Tasks;

    internal class InstalledPackageFeed : PackageFeed {
        internal static string CanonicalLocation = "CoApp://InstalledPackages";
        internal static InstalledPackageFeed Instance = new InstalledPackageFeed();

        /// <summary>
        ///   contains the list of packages in the direcory. (may be recursive)
        /// </summary>
        private readonly List<Package> _packageList = new List<Package>();

        private InstalledPackageFeed() : base(CanonicalLocation) {
        }

        internal void PackageRemoved(Package package) {
            lock (_packageList) {
                if (_packageList.Contains(package)) {
                    _packageList.Remove(package);
                }
            }
        }

        internal void PackageInstalled(Package package) {
            lock (_packageList) {
                if (!_packageList.Contains(package)) {
                    _packageList.Add(package);
                }
            }
        }

        protected void ScanInstalledMSIs() {
            Task.Factory.StartNew(() => {
                var systemInstalledFiles = MSIBase.InstalledMSIFilenames.ToArray();
                foreach (var each in systemInstalledFiles) {
                    var lookup = File.GetCreationTime(each).Ticks + each.GetHashCode();
                    if (!Cache.Contains(lookup)) {
                        var pkg = Package.GetPackageFromFilename(each);
                        if (pkg != null ) {
                            if (pkg.IsInstalled) {
                                PackageInstalled(pkg);
                            }
                        } else {
                            // doesn't appear to be a coapp package
                            lock (Cache) {
                                Cache.Add(lookup);
                                SaveCache();
                            }
                        }
                    }
                }
            }, TaskCreationOptions.LongRunning).AutoManage();
        }

        protected void Scan() {
            lock (this) {
                if (!Scanned || Stale) {
                    Logger.Message("Starting 'installed packages' scan");
                    LastScanned = DateTime.Now;
                    _packageList.Clear();
                    ScanInstalledMSIs(); // kick off the system package task. It's ok if this doesn't get done in a hurry.


                    Logger.Message("Getting List of cached package files");
                    // add the cached package directory, 'cause on backlevel platform, they taint the MSI in the installed files folder.
                    var coAppInstalledFiles = PackageManagerSettings.CoAppPackageCache.FindFilesSmarter("*.msi").ToArray();

                    Logger.Message("There are '{0}' cached package files", coAppInstalledFiles.Length );
                    coAppInstalledFiles.AsParallel().ForAll(each => {
                    // foreach (var each in coAppInstalledFiles) {
                        Logger.Message("Checking cached package file '{0}'", each);
                        var lookup = File.GetCreationTime(each).Ticks + each.GetHashCode();
                        if (!Cache.Contains(lookup)) {
                            var pkg = Package.GetPackageFromFilename(each);

                            if (pkg != null ) {
                                if (pkg.IsInstalled) {
                                    PackageInstalled(pkg);
                                }
                            } else {
                                // doesn't appear to be a coapp package
                                lock (Cache) {
                                    Cache.Add(lookup);
                                    SaveCache();
                                }
                            }
                        }
                    });
                    // }

                    Logger.Message("COMPLETED 'installed packages' scan================================================");

                    Scanned = true;
                    Stale = false;
                }
            }
        }

        /// <summary>
        ///   Finds packages based on the cosmetic name of the package. Supports wildcard in pattern match.
        /// </summary>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        internal override IEnumerable<Package> FindPackages(CanonicalName canonicalName) {
            Scan();
            return _packageList.Where(each => each.CanonicalName.Matches(canonicalName));
        }
    }
}