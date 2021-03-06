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
    using System.Collections.Generic;
    using System.Xml;
    using System.Xml.Serialization;
    using Toolkit.Extensions;

    [XmlRoot(ElementName = "Composition", Namespace = "http://coapp.org/atom-package-feed-1.0")]
    public class Composition {
        [XmlArray(IsNullable = false)]
        public List<CompositionRule> CompositionRules { get; set; }

        [XmlArray(IsNullable = false)]
        public List<DeveloperLibrary> DeveloperLibraries { get; set; }

        [XmlArray(IsNullable = false)]
        public List<WebApplication> WebApplications { get; set; }

        [XmlArray(IsNullable = false)]
        public List<Service> Services { get; set; }

        [XmlArray(IsNullable = false)]
        public List<SourceCode> SourceCodes { get; set; }

        [XmlArray(IsNullable = false)]
        public List<Driver> Drivers { get; set; }

        // soak up anything we don't recognize
        [XmlAnyAttribute, NotPersistable]
        public XmlAttribute[] UnknownAttributes;

        [XmlAnyElement, NotPersistable]
        public XmlElement[] UnknownElements;
    }
}