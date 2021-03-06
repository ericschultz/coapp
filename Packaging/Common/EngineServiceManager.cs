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

namespace CoApp.Packaging.Common {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.ServiceProcess;
    using System.Threading;
    using Exceptions;
    using Toolkit.Exceptions;
    using Toolkit.Extensions;
    using Toolkit.Logging;
    using Toolkit.Win32;
    using TimeoutException = System.TimeoutException;

    public static class EngineServiceManager {
        public const string CoAppServiceName = "CoApp";
        public const string CoAppDisplayName = "CoApp Service";

        public static bool IsServiceInstalled {
            get {
                return ServiceController.GetServices().Any(service => service.ServiceName == CoAppServiceName);
            }
        }

        private static readonly Lazy<ServiceController> Controller = new Lazy<ServiceController>(() => new ServiceController(CoAppServiceName));

        internal static void EnsureServiceAclsCorrect() {
            var psd = new byte[0];
            uint bufSizeNeeded;
            var ok = Advapi32.QueryServiceObjectSecurity(Controller.Value.ServiceHandle, SecurityInfos.DiscretionaryAcl, psd, 0, out bufSizeNeeded);
            if (!ok) {
                int err = Marshal.GetLastWin32Error();
                if (err == 122) {
                    // ERROR_INSUFFICIENT_BUFFER
                    // expected; now we know bufsize
                    psd = new byte[bufSizeNeeded];
                    ok = Advapi32.QueryServiceObjectSecurity(
                        Controller.Value.ServiceHandle, SecurityInfos.DiscretionaryAcl, psd, bufSizeNeeded, out bufSizeNeeded);
                } else {
                    throw new ApplicationException("error calling QueryServiceObjectSecurity() to get DACL for Service: error code=" + err);
                }
            }
            if (!ok) {
                throw new ApplicationException("error calling QueryServiceObjectSecurity(2) to get DACL for Service: error code=" + Marshal.GetLastWin32Error());
            }

            // get security descriptor via raw into DACL form so ACE
            // ordering checks are done for us.
            var rsd = new RawSecurityDescriptor(psd, 0);
            var dacl = new DiscretionaryAcl(false, false, rsd.DiscretionaryAcl);

            dacl.AddAccess(AccessControlType.Allow, new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), (int)ServiceAccess.ServiceCoapp, InheritanceFlags.None, PropagationFlags.None);

            // convert discretionary ACL back to raw form; looks like via byte[] is only way
            var rawdacl = new byte[dacl.BinaryLength];
            dacl.GetBinaryForm(rawdacl, 0);
            rsd.DiscretionaryAcl = new RawAcl(rawdacl, 0);

            // set raw security descriptor on service again
            var rawsd = new byte[rsd.BinaryLength];
            rsd.GetBinaryForm(rawsd, 0);

            ok = Advapi32.SetServiceObjectSecurity(Controller.Value.ServiceHandle, SecurityInfos.DiscretionaryAcl, rawsd);
            if (!ok) {
                throw new ApplicationException("error calling SetServiceObjectSecurity(); error code=" + Marshal.GetLastWin32Error());
            }
        }

        // some dumbass thought that they should just return the last value, forcing developers to 'refresh' to get the current value. 
        // Not quite sure WHY THIS EVER SOUNDED LIKE A GOOD IDEA!? IF I WANTED THE LAST F$(*&%^*ING VALUE, I'D HAVE CACHED IT MYSELF DUMBASS!
        public static ServiceControllerStatus Status {
            get {
                Controller.Value.Refresh();
                return Controller.Value.Status;
            }
        }

        public static bool CanStop {
            get {
                Controller.Value.Refresh();
                return Controller.Value.CanStop;
            }
        }

        public static bool IsServiceRunning {
            get {
                return IsServiceInstalled && Status == ServiceControllerStatus.Running;
            }
        }

        public static void TryToStopService() {
            try {
                if (IsServiceInstalled) {
                    if (Status != ServiceControllerStatus.Stopped && CanStop) {
                        Controller.Value.Stop();
                    }
                }
            } catch {
                // keep movin'
            }

            // kill any service processes that are currently running.
            KillServiceProcesses();
        }

        public static void TryToStartService(bool secondAttempt = false) {
            if (!IsServiceInstalled) {
                InstallAndStartService();
                return;
            }

            Logger.Message("==[Trying to start Win32 Service]==");

            switch (Status) {
                case ServiceControllerStatus.ContinuePending:
                    // Logger.Message("==[State:Continuing]==");
                    // wait for it to continue.
                    try {
                        Controller.Value.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 10));
                    } catch (TimeoutException) {
                        throw new UnableToStartServiceException(
                            "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                    }
                    if (Status != ServiceControllerStatus.Running) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and was expected to be in the 'Running' state.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;
                case ServiceControllerStatus.PausePending:
                    //Logger.Message("==[State:Pausing]==");
                    try {
                        Controller.Value.WaitForStatus(ServiceControllerStatus.Paused, new TimeSpan(0, 0, 0, 10));
                    } catch (TimeoutException) {
                        throw new UnableToStartServiceException(
                            "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                    }
                    if (Status == ServiceControllerStatus.Paused) {
                        TryToStartService();
                    }
                    if (Status != ServiceControllerStatus.Running) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and was expected to be in the 'Running' state.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;
                case ServiceControllerStatus.Paused:
                    //Logger.Message("==[State:Paused]==");
                    Controller.Value.Continue();
                    try {
                        Controller.Value.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 10));
                    } catch (TimeoutException) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;

                case ServiceControllerStatus.Running:
                    //Logger.Message("==[State:Running]==");
                    // duh!
                    break;

                case ServiceControllerStatus.StartPending:
                    Logger.Message("==[State:Starting]==");
                    try {
                        Controller.Value.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 10));
                    } catch (TimeoutException) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;

                case ServiceControllerStatus.StopPending:
                    Logger.Message("==[State:Stopping]==");
                    try {
                        Controller.Value.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 0, 10));
                    } catch (TimeoutException) {
                        throw new UnableToStartServiceException(
                            "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                    }
                    if (Status == ServiceControllerStatus.Stopped) {
                        TryToStartService();
                    }
                    if (Status != ServiceControllerStatus.Running) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and was expected to be in the 'Running' state.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;

                case ServiceControllerStatus.Stopped:
                    //Logger.Message("==[State:Stopped]==");
                    Controller.Value.Start();
                    try {
                        Controller.Value.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 0, 10));
                    } catch (TimeoutException) {
                        if (secondAttempt) {
                            throw new UnableToStartServiceException(
                                "Service is in the '{0}' state, and didn't respond before timing out.".format(Status.ToString()));
                        }
                        TryToStartService(true);
                    }
                    break;
            }
        }

        public static bool Available {
            get {
                lock (typeof (EngineServiceManager)) {
                    try {
                        using (var ewh = EventWaitHandle.OpenExisting("Global\\CoAppAvailable", EventWaitHandleRights.Synchronize)) {
                            return ewh.WaitOne(0);
                        }
                    } catch {
                    }
                    return false;
                }
            }
        }

        public static bool StartingUp {
            get {
                lock (typeof (EngineServiceManager)) {
                    try {
                        using (var ewh = EventWaitHandle.OpenExisting("Global\\CoAppStartingUp", EventWaitHandleRights.Synchronize)) {
                            return ewh.WaitOne(0);
                        }
                    } catch {
                    }
                    return false;
                }
            }
        }

        public static bool ShuttingDown {
            get {
                lock (typeof (EngineServiceManager)) {
                    try {
                        using (var ewh = EventWaitHandle.OpenExisting("Global\\CoAppShuttingDown", EventWaitHandleRights.Synchronize)) {
                            return ewh.WaitOne(0);
                        }
                    } catch {
                    }
                    return false;
                }
            }
        }

        public static bool ShutdownRequested {
            get {
                lock (typeof (EngineServiceManager)) {
                    try {
                        using (var ewh = EventWaitHandle.OpenExisting("Global\\CoAppShutdownRequested", EventWaitHandleRights.Synchronize)) {
                            return ewh.WaitOne(0);
                        }
                    } catch {
                    }
                    return false;
                }
            }
        }

        public static int EngineStartupStatus {
            get {
                return PackageManagerSettings.CoAppInformation["StartupPercentComplete"].IntValue;
            }
        }

        public static bool IsEngineResponding {
            get {
                return Available;
            }
        }

        public static void EnsureServiceIsResponding() {
            if (Available) {
                // looks good to me!
                return;
            }

            if (!StartingUp && !ShuttingDown && !Available && AreAnyServiceExesRunning) {
                // Just...wait a moment to see if anything good is going to happen.
                Thread.Sleep(800);
            }

            var count = 1200; // 10 minutes.
            if (StartingUp) {
                while (StartingUp && 0 < count--) {
                    // it's just getting started. It should be fine in a few moments
                    Thread.Sleep(500);
                }

                if (!StartingUp) {
                    // try again now that it's out of this state.
                    EnsureServiceIsResponding();
                    return;
                }

                // um. it's still starting up? What's gone wrong?
                throw new CoAppException("CoApp Engine appears stuck in starting up state.");
            }

            count = 10;
            while (ShuttingDown && count > 0) {
                if (IsServiceRunning) {
                    // looks like we caught the win32 service shutting down.
                    // let's try to start it back up (it'll safely stop first, so no worries)
                    TryToStartService();
                    EnsureServiceIsResponding();
                    return;
                }
                // looks like an interactive version is shutting down. Let's wait a few seconds and check again.
                Thread.Sleep(2000);
                count--;
            }

            if (ShuttingDown) {
                // hmm. looks like it's stuck shutting down an interactive version of the serivce
                // he's had long enough, let's kill him.
                KillServiceProcesses();
                TryToStartService();
                EnsureServiceIsResponding();
                return;
            }

            if (IsServiceRunning) {
                // hmm, we're not available, startingup or shutting down. 
                // try to stop it and restart it then.
                TryToStopService();
                TryToStartService();
                EnsureServiceIsResponding();
                return;
            }

            // we're not running at all!
            if (!IsServiceInstalled) {
                InstallAndStartService();
                EnsureServiceIsResponding();
                return;
            }

            // hmm. just try to start it I guess.
            TryToStartService();
            EnsureServiceIsResponding();
        }

        private static void KillServiceProcesses() {
            foreach (var proc in Process.GetProcessesByName("coapp.service").Where(each => each.Id != Process.GetCurrentProcess().Id).ToArray()) {
                try {
                    proc.Kill();
                } catch {
                }
            }
        }

        private static bool AreAnyServiceExesRunning {
            get {
                return Process.GetProcessesByName("coapp.service").Any(each => each != Process.GetCurrentProcess());
            }
        }

        public static string CoAppServiceExecutablePath {
            get {
                string result = null;

                var root = PackageManagerSettings.CoAppRootDirectory;
                var binDirectory = Path.Combine(root, "bin");

                // look for $COAPP\bin\coapp.service.exe 
                // this will happen when the service has been installed and configureed at least once.
                if (Directory.Exists(binDirectory)) {
                    var serviceExes = binDirectory.FindFilesSmarter(@"**\coapp.service.exe").OrderByDescending(Version);
                    result = serviceExes.FirstOrDefault(each => !Symlink.IsSymlink(each) || File.Exists(Symlink.GetActualPath(each)));
                    if (result != null) {
                        return result;
                    }
                }

                // Look in %program files%/outercurve...
                // this will happen when the service is installed via msi, but not initialized.
                var searchDirectory = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "outercurve foundation");

                if (Directory.Exists(searchDirectory)) {
                    // get all of the coapp.service.exes and pick the one with the highest version
                    var serviceExes = searchDirectory.FindFilesSmarter(@"**\coapp.service.exe").OrderByDescending(Version);

                    // ah, so we found some did we? Should never be a symlink in this case.
                    result = serviceExes.FirstOrDefault(each => !Symlink.IsSymlink(each));
                }
                return result;
            }
        }

        private static ulong Version(string path) {
            try {
                var info = FileVersionInfo.GetVersionInfo(path);
                var fv = info.FileVersion;
                if (!String.IsNullOrEmpty(fv)) {
                    fv = fv.Substring(0, fv.PositionOfFirstCharacterNotIn("0123456789.".ToCharArray()));
                }

                if (String.IsNullOrEmpty(fv)) {
                    return 0;
                }

                var vers = fv.Split('.');
                var major = vers.Length > 0 ? vers[0].ToInt32() : 0;
                var minor = vers.Length > 1 ? vers[1].ToInt32() : 0;
                var build = vers.Length > 2 ? vers[2].ToInt32() : 0;
                var revision = vers.Length > 3 ? vers[3].ToInt32() : 0;

                return (((UInt64)major) << 48) + (((UInt64)minor) << 32) + (((UInt64)build) << 16) + (UInt64)revision;
            } catch {
                return 0;
            }
        }

        public static void InstallAndStartService() {
            // we're going to try and install the service
            // make sure no other service processes are trying to run right now.
            if (AreAnyServiceExesRunning) {
                TryToStopService();
            }

            // basically, we just need to find *any* coapp.service.exe and tell it to --auto-install
            // and it will do a better search than we could do anyway.
            var exe = CoAppServiceExecutablePath;
            if (exe == null) {
                throw new CoAppException("Unable to locate Installed CoApp Service.");
            }

            var processStartInfo = new ProcessStartInfo(exe) {
                Arguments = "--auto-install",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
            };

            Process.Start(processStartInfo).WaitForExit();

            if (IsServiceInstalled) {
                TryToStartService(); // make sure it is started too.
                return;
            }

            // uh, if we got here, we're not going to be able start Coapp...
            throw new CoAppException("Unable to start CoApp Service; the service executable is: '{0}'".format(exe));
        }
    }
}