﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Toolkit.Exceptions {
    using System;

    public class PathIsNotSymlinkException : CoAppException {
        public string Path { get; set; }

        public PathIsNotSymlinkException(string path) {
            Path = path;
        }
    }
}