using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a Windows Unattend XML configuration with enhanced navigation capabilities
    /// </summary>
    public class UnattendXMLConfiguration
    {
        /// <summary>
        /// The underlying XML document
        /// </summary>
        public XmlDocument XmlDocument { get; set; } = new XmlDocument();

        /// <summary>
        /// Source file path if loaded from file
        /// </summary>
        public string SourceFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Whether the configuration has been modified
        /// </summary>
        public bool IsModified { get; set; } = false;

        /// <summary>
        /// XML namespace manager for XPath queries
        /// </summary>
        public XmlNamespaceManager NamespaceManager { get; private set; }

        /// <summary>
        /// Configuration passes found in the document
        /// </summary>
        public List<string> ConfigurationPasses
        {
            get
            {
                var passes = new List<string>();
                var settingsNodes = XmlDocument.SelectNodes("//unattend:settings", NamespaceManager);
                if (settingsNodes != null)
                {
                    foreach (XmlNode node in settingsNodes)
                    {
                        var pass = node.Attributes?["pass"]?.Value;
                        if (!string.IsNullOrEmpty(pass) && !passes.Contains(pass!))
                        {
                            passes.Add(pass!);
                        }
                    }
                }
                return passes.OrderBy(p => p).ToList();
            }
        }

        /// <summary>
        /// Components found in the document with XPath information
        /// </summary>
        public List<UnattendXMLComponent> Components
        {
            get
            {
                var components = new List<UnattendXMLComponent>();
                // Look for both namespaced and non-namespaced components
                var componentNodes = XmlDocument.SelectNodes("//unattend:component | //component", NamespaceManager);
                if (componentNodes != null)
                {
                    foreach (XmlNode node in componentNodes)
                    {
                        var component = new UnattendXMLComponent
                        {
                            Name = node.Attributes?["name"]?.Value ?? "",
                            ProcessorArchitecture = node.Attributes?["processorArchitecture"]?.Value ?? "",
                            PublicKeyToken = node.Attributes?["publicKeyToken"]?.Value ?? "",
                            Language = node.Attributes?["language"]?.Value ?? "",
                            VersionScope = node.Attributes?["versionScope"]?.Value ?? "",
                            Pass = node.ParentNode?.Attributes?["pass"]?.Value ?? "",
                            XmlNode = node,
                            XPath = GetXPath(node)
                        };
                        components.Add(component);
                    }
                }
                return components;
            }
        }

        /// <summary>
        /// All XML elements with their XPath for easy navigation
        /// </summary>
        public List<UnattendXMLElement> Elements
        {
            get
            {
                var elements = new List<UnattendXMLElement>();
                TraverseNodes(XmlDocument.DocumentElement, elements);
                return elements;
            }
        }

        /// <summary>
        /// Constructor that initializes the namespace manager
        /// </summary>
        public UnattendXMLConfiguration()
        {
            NamespaceManager = new XmlNamespaceManager(XmlDocument.NameTable);
            NamespaceManager.AddNamespace("unattend", "urn:schemas-microsoft-com:unattend");
            NamespaceManager.AddNamespace("wcm", "http://schemas.microsoft.com/WMIConfig/2002/State");
        }

        /// <summary>
        /// Gets the XPath for a given XML node
        /// </summary>
        public string GetXPath(XmlNode node)
        {
            if (node == null || node.NodeType == XmlNodeType.Document)
                return "";

            var xpath = "";
            while (node != null && node.NodeType == XmlNodeType.Element)
            {
                var index = 1;
                var sibling = node.PreviousSibling;
                while (sibling != null)
                {
                    if (sibling.NodeType == XmlNodeType.Element && sibling.LocalName == node.LocalName && sibling.NamespaceURI == node.NamespaceURI)
                        index++;
                    sibling = sibling.PreviousSibling;
                }

                var nodeName = node.LocalName;
                if (node.NamespaceURI == "urn:schemas-microsoft-com:unattend")
                    nodeName = "unattend:" + node.LocalName;
                else if (node.NamespaceURI == "http://schemas.microsoft.com/WMIConfig/2002/State")
                    nodeName = "wcm:" + node.LocalName;

                xpath = "/" + nodeName + (index > 1 ? $"[{index}]" : "") + xpath;
                node = node.ParentNode;
            }

            return xpath;
        }

        /// <summary>
        /// Finds an element by its friendly name (without complex XPath)
        /// </summary>
        public XmlNode? FindElement(string elementName, string? pass = null, string? componentName = null)
        {
            string xpath;

            if (!string.IsNullOrEmpty(pass) && !string.IsNullOrEmpty(componentName))
            {
                xpath = $"//unattend:settings[@pass='{pass}']//unattend:component[@name='{componentName}']//*[local-name()='{elementName}']";
            }
            else if (!string.IsNullOrEmpty(pass))
            {
                xpath = $"//unattend:settings[@pass='{pass}']//*[local-name()='{elementName}']";
            }
            else if (!string.IsNullOrEmpty(componentName))
            {
                xpath = $"//unattend:component[@name='{componentName}']//*[local-name()='{elementName}']";
            }
            else
            {
                xpath = $"//*[local-name()='{elementName}']";
            }

            return XmlDocument.SelectSingleNode(xpath, NamespaceManager);
        }

        /// <summary>
        /// Gets or creates an element by friendly name
        /// </summary>
        public XmlNode GetOrCreateElement(string elementName, string? pass = null, string? componentName = null)
        {
            var existing = FindElement(elementName, pass, componentName);
            if (existing != null)
                return existing;

            // Create the element structure if it doesn't exist
            // This is a simplified version - full implementation would handle the complete hierarchy
            var element = XmlDocument.CreateElement(elementName, "urn:schemas-microsoft-com:unattend");
            
            // Find appropriate parent or create one
            XmlNode parent;
            if (!string.IsNullOrEmpty(componentName) && !string.IsNullOrEmpty(pass))
            {
                parent = FindOrCreateComponent(componentName!, pass!);
            }
            else if (!string.IsNullOrEmpty(pass))
            {
                parent = FindOrCreateSettings(pass!);
            }
            else
            {
                parent = XmlDocument.DocumentElement ?? CreateRootElement();
            }

            parent.AppendChild(element);
            IsModified = true;
            return element;
        }

        /// <summary>
        /// Validates the XML configuration
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (XmlDocument.DocumentElement == null)
            {
                errors.Add("No root element found");
                return errors;
            }

            // Check for required namespace
            var xmlns = XmlDocument.DocumentElement?.GetAttribute("xmlns");
            if (string.IsNullOrEmpty(xmlns) || !xmlns!.Contains("microsoft.com"))
            {
                errors.Add("Missing or invalid xmlns attribute");
            }

            return errors;
        }

        /// <summary>
        /// Creates a deep copy of the configuration
        /// </summary>
        public UnattendXMLConfiguration Clone()
        {
            var clone = new UnattendXMLConfiguration();
            clone.XmlDocument.LoadXml(XmlDocument.OuterXml);
            clone.SourceFilePath = SourceFilePath;
            clone.IsModified = IsModified;
            return clone;
        }

        /// <summary>
        /// Saves the XML to a file with optional encoding
        /// </summary>
        public void SaveToFile(string filePath, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = encoding,
                OmitXmlDeclaration = false
            };

            using var writer = XmlWriter.Create(filePath, settings);
            XmlDocument.Save(writer);
        }

        private void TraverseNodes(XmlNode? node, List<UnattendXMLElement> elements)
        {
            if (node == null) return;

            if (node.NodeType == XmlNodeType.Element)
            {
                elements.Add(new UnattendXMLElement
                {
                    Name = node.LocalName,
                    FullName = node.Name,
                    Value = node.InnerText,
                    XPath = GetXPath(node),
                    XmlNode = node,
                    HasChildren = node.HasChildNodes && node.ChildNodes.Cast<XmlNode>().Any(n => n.NodeType == XmlNodeType.Element),
                    Attributes = node.Attributes?.Cast<XmlAttribute>().ToDictionary(a => a.Name, a => a.Value) ?? new Dictionary<string, string>()
                });
            }

            foreach (XmlNode child in node.ChildNodes)
            {
                TraverseNodes(child, elements);
            }
        }

        private XmlNode FindOrCreateComponent(string componentName, string pass)
        {
            var xpath = $"//unattend:settings[@pass='{pass}']//unattend:component[@name='{componentName}']";
            var component = XmlDocument.SelectSingleNode(xpath, NamespaceManager);
            
            if (component == null)
            {
                var settings = FindOrCreateSettings(pass);
                component = XmlDocument.CreateElement("component", "urn:schemas-microsoft-com:unattend");
                ((XmlElement)component).SetAttribute("name", componentName);
                settings.AppendChild(component);
                IsModified = true;
            }
            
            return component;
        }

        private XmlNode FindOrCreateSettings(string pass)
        {
            var xpath = $"//unattend:settings[@pass='{pass}']";
            var settings = XmlDocument.SelectSingleNode(xpath, NamespaceManager);
            
            if (settings == null)
            {
                var root = XmlDocument.DocumentElement ?? CreateRootElement();
                settings = XmlDocument.CreateElement("settings", "urn:schemas-microsoft-com:unattend");
                ((XmlElement)settings).SetAttribute("pass", pass);
                root.AppendChild(settings);
                IsModified = true;
            }
            
            return settings;
        }

        private XmlElement CreateRootElement()
        {
            var root = XmlDocument.CreateElement("unattend", "urn:schemas-microsoft-com:unattend");
            root.SetAttribute("xmlns", "urn:schemas-microsoft-com:unattend");
            XmlDocument.AppendChild(root);
            IsModified = true;
            return root;
        }
    }

    /// <summary>
    /// Represents a component in the Unattend XML with XPath information
    /// </summary>
    public class UnattendXMLComponent
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessorArchitecture { get; set; } = string.Empty;
        public string PublicKeyToken { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string VersionScope { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public string XPath { get; set; } = string.Empty;
        public XmlNode? XmlNode { get; set; }
    }

    /// <summary>
    /// Represents any XML element with navigation information
    /// </summary>
    public class UnattendXMLElement
    {
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string XPath { get; set; } = string.Empty;
        public bool HasChildren { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public XmlNode? XmlNode { get; set; }
    }
}
