﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Trace {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Xml.Serialization;
    using Extensions;
    using Scripting;

    public partial class File {
        private string fullPath;

        [XmlIgnore]
        public long Handle { get; set; }

        [XmlIgnore]
        public string FullPath {
            get { return fullPath ?? (fullPath = Path.Combine(folder, name)); }
            set {
                fullPath = value.ToLower().NormalizePath();
                if (Directory.Exists(fullPath)) {
                    folder = Path.GetDirectoryName(fullPath);
                    name = "";
                    extension = "";
                }
                else {
                    extension = Path.GetExtension(fullPath);
                    name = Path.GetFileName(fullPath);
                    folder = Path.GetDirectoryName(fullPath);
                }
            }
        }
    }
}