//-----------------------------------------------------------------------
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

namespace CoApp.Packaging.Common.Model {
    using System;
    using System.Xml.Serialization;
    using Toolkit.Extensions;

    [XmlRoot(ElementName = "License", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class License {
        [XmlElement(IsNullable = false)]
        public LicenseId LicenseId { get; set; }

        [XmlElement(IsNullable = false)]
        public string Name { get; set; }

        [XmlElement(IsNullable = false)]
        public string Text { get; set; }

        [XmlElement(ElementName = "LicenseUrl")]
        public string _licenseUrl {
            get {
                return Location.AbsoluteUri;
            }
            set {
                Location = value.ToUri();
            }
        }

        [XmlIgnore]
        public Uri Location { get; set; }
    }
}