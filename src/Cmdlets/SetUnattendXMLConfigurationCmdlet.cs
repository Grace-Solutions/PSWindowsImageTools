using System;
using System.Management.Automation;
using System.Xml;
using System.Xml.XPath;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Modifies Windows Unattend XML configuration using XPath or friendly element names
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "UnattendXMLConfiguration")]
    [OutputType(typeof(UnattendXMLConfiguration))]
    public class SetUnattendXMLConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Unattend XML configuration to modify
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNull]
        public UnattendXMLConfiguration Configuration { get; set; } = new UnattendXMLConfiguration();

        /// <summary>
        /// XPath expression to locate the element to modify (supports namespaces)
        /// </summary>
        [Parameter(ParameterSetName = "XPath", Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string XPath { get; set; } = string.Empty;

        /// <summary>
        /// Friendly element name (without complex XPath syntax)
        /// </summary>
        [Parameter(ParameterSetName = "ElementName", Mandatory = true, Position = 1)]
        [ValidateNotNullOrEmpty]
        public string ElementName { get; set; } = string.Empty;

        /// <summary>
        /// Configuration pass (e.g., "specialize", "oobeSystem")
        /// </summary>
        [Parameter(ParameterSetName = "ElementName")]
        public string Pass { get; set; } = string.Empty;

        /// <summary>
        /// Component name (e.g., "Microsoft-Windows-Shell-Setup")
        /// </summary>
        [Parameter(ParameterSetName = "ElementName")]
        public string ComponentName { get; set; } = string.Empty;

        /// <summary>
        /// Value to set for the element
        /// </summary>
        [Parameter(Position = 2)]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Attribute name to set (if modifying an attribute)
        /// </summary>
        [Parameter]
        public string AttributeName { get; set; } = string.Empty;

        /// <summary>
        /// Remove the element or attribute instead of setting a value
        /// </summary>
        [Parameter]
        public SwitchParameter Remove { get; set; }

        /// <summary>
        /// Create the element if it doesn't exist
        /// </summary>
        [Parameter]
        public SwitchParameter CreateIfNotExists { get; set; }

        /// <summary>
        /// Return the modified configuration
        /// </summary>
        [Parameter]
        public SwitchParameter PassThru { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", "Modifying Unattend XML configuration");

                // Create a copy to avoid modifying the original
                var modifiedConfig = Configuration.Clone();

                XmlNode? targetNode = null;

                // Determine which method to use for finding the element
                if (ParameterSetName == "XPath")
                {
                    LoggingService.WriteVerbose(this, "General", $"Using XPath: {XPath}");
                    targetNode = FindElementByXPath(modifiedConfig, XPath);
                }
                else if (ParameterSetName == "ElementName")
                {
                    LoggingService.WriteVerbose(this, "General", $"Using ElementName: {ElementName}, Pass: {Pass}, Component: {ComponentName}");
                    
                    if (CreateIfNotExists.IsPresent)
                    {
                        targetNode = modifiedConfig.GetOrCreateElement(ElementName, 
                            string.IsNullOrEmpty(Pass) ? null : Pass,
                            string.IsNullOrEmpty(ComponentName) ? null : ComponentName);
                    }
                    else
                    {
                        targetNode = modifiedConfig.FindElement(ElementName,
                            string.IsNullOrEmpty(Pass) ? null : Pass,
                            string.IsNullOrEmpty(ComponentName) ? null : ComponentName);
                    }
                }

                if (targetNode == null)
                {
                    var identifier = ParameterSetName == "XPath" ? XPath : ElementName;
                    if (CreateIfNotExists.IsPresent && ParameterSetName == "XPath")
                    {
                        WriteWarning($"Element not found at XPath: {identifier}. CreateIfNotExists with XPath requires manual XML manipulation.");
                        return;
                    }
                    else if (!CreateIfNotExists.IsPresent)
                    {
                        WriteError(new ErrorRecord(
                            new InvalidOperationException($"Element not found: {identifier}. Use -CreateIfNotExists to create it."),
                            "ElementNotFound",
                            ErrorCategory.ObjectNotFound,
                            identifier));
                        return;
                    }
                }

                // Perform the operation
                if (Remove.IsPresent)
                {
                    RemoveElementOrAttribute(targetNode!, modifiedConfig);
                }
                else
                {
                    SetElementOrAttribute(targetNode!, Value, modifiedConfig);
                }

                modifiedConfig.IsModified = true;

                LoggingService.WriteVerbose(this, "General", "Unattend XML configuration modification completed");

                if (PassThru.IsPresent)
                {
                    WriteObject(modifiedConfig);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "ConfigurationModificationError", ErrorCategory.NotSpecified, Configuration));
            }
        }

        private XmlNode? FindElementByXPath(UnattendXMLConfiguration config, string xpath)
        {
            try
            {
                return config.XmlDocument.SelectSingleNode(xpath, config.NamespaceManager);
            }
            catch (XPathException ex)
            {
                WriteError(new ErrorRecord(ex, "InvalidXPath", ErrorCategory.InvalidArgument, xpath));
                return null;
            }
        }

        private void RemoveElementOrAttribute(XmlNode node, UnattendXMLConfiguration config)
        {
            if (!string.IsNullOrEmpty(AttributeName))
            {
                // Remove attribute
                if (node.Attributes?[AttributeName] != null)
                {
                    node.Attributes.RemoveNamedItem(AttributeName);
                    LoggingService.WriteVerbose(this, "General", $"Removed attribute {AttributeName} from element");
                }
                else
                {
                    WriteWarning($"Attribute {AttributeName} not found on element");
                }
            }
            else
            {
                // Remove element
                if (node.ParentNode != null)
                {
                    var xpath = config.GetXPath(node);
                    node.ParentNode.RemoveChild(node);
                    LoggingService.WriteVerbose(this, "General", $"Removed element at XPath: {xpath}");
                }
                else
                {
                    WriteWarning("Cannot remove root element");
                }
            }
        }

        private void SetElementOrAttribute(XmlNode node, string value, UnattendXMLConfiguration config)
        {
            if (!string.IsNullOrEmpty(AttributeName))
            {
                // Set attribute value
                if (node is XmlElement element)
                {
                    element.SetAttribute(AttributeName, value);
                    LoggingService.WriteVerbose(this, "General", $"Set attribute {AttributeName} = {value}");
                }
                else
                {
                    WriteError(new ErrorRecord(
                        new InvalidOperationException("Cannot set attributes on non-element nodes"),
                        "InvalidNodeType",
                        ErrorCategory.InvalidOperation,
                        node));
                }
            }
            else
            {
                // Set element value
                node.InnerText = value;
                var xpath = config.GetXPath(node);
                LoggingService.WriteVerbose(this, "General", $"Set element value = {value} at XPath: {xpath}");
            }
        }
    }
}
