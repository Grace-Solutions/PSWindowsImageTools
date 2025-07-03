using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Xml;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Service for managing Windows Unattend XML configurations
    /// </summary>
    public class UnattendXMLService
    {
        private const string ServiceName = "UnattendXMLService";

        /// <summary>
        /// Loads an Unattend XML configuration from file
        /// </summary>
        public UnattendXMLConfiguration LoadConfiguration(string filePath, PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Loading Unattend XML configuration from {filePath}");

            var config = new UnattendXMLConfiguration();
            
            try
            {
                config.XmlDocument.Load(filePath);
                config.SourceFilePath = filePath;
                
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Successfully loaded Unattend XML configuration with {config.Components.Count} components");
                
                return config;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load Unattend XML from {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Saves an Unattend XML configuration to file
        /// </summary>
        public void SaveConfiguration(UnattendXMLConfiguration config, string filePath, PSCmdlet? cmdlet = null, string encoding = "UTF8")
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, $"Saving Unattend XML configuration to {filePath}");

            try
            {
                var textEncoding = GetTextEncoding(encoding);
                config.SaveToFile(filePath, textEncoding);
                
                LoggingService.WriteVerbose(cmdlet, ServiceName, 
                    $"Successfully saved Unattend XML configuration to {filePath}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save Unattend XML to {filePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Installs Unattend XML configuration to mounted Windows images
        /// </summary>
        public List<UnattendXMLApplicationResult> InstallConfiguration(
            MountedWindowsImage[] mountedImages, 
            UnattendXMLConfiguration config, 
            PSCmdlet? cmdlet = null,
            string encoding = "UTF8")
        {
            var results = new List<UnattendXMLApplicationResult>();

            foreach (var image in mountedImages)
            {
                var result = new UnattendXMLApplicationResult
                {
                    MountedImage = image,
                    Success = false,
                    ErrorMessage = string.Empty
                };

                try
                {
                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"Installing Unattend XML configuration to {image.ImageName}");

                    // Create the target directory structure
                    var pantherPath = Path.Combine(image.MountPath.FullName, "Windows", "Panther");
                    if (!Directory.Exists(pantherPath))
                    {
                        Directory.CreateDirectory(pantherPath);
                    }

                    var unattendPath = Path.Combine(pantherPath, "unattend.xml");
                    
                    // Save the configuration
                    SaveConfiguration(config, unattendPath, cmdlet, encoding);
                    
                    result.Success = true;
                    result.InstalledPath = unattendPath;
                    
                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"Successfully installed Unattend XML configuration to {image.ImageName}");
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = ex.Message;
                    LoggingService.WriteVerbose(cmdlet, ServiceName, 
                        $"Failed to install Unattend XML configuration to {image.ImageName}: {ex.Message}");
                }

                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Creates a basic Unattend XML configuration
        /// </summary>
        public UnattendXMLConfiguration CreateBasicConfiguration(string architecture = "amd64", string language = "neutral", PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Creating basic Unattend XML configuration template");

            var config = new UnattendXMLConfiguration();
            InitializeEmptyConfiguration(config, cmdlet);

            // Add basic structure
            EnsureConfigurationPass(config, "specialize", cmdlet);
            EnsureConfigurationPass(config, "oobeSystem", cmdlet);

            return config;
        }

        /// <summary>
        /// Creates an OOBE-focused Unattend XML configuration
        /// </summary>
        public UnattendXMLConfiguration CreateOOBEConfiguration(string architecture = "amd64", string language = "neutral", PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Creating OOBE Unattend XML configuration template");

            var config = new UnattendXMLConfiguration();
            InitializeEmptyConfiguration(config, cmdlet);

            EnsureConfigurationPass(config, "oobeSystem", cmdlet);

            return config;
        }

        /// <summary>
        /// Creates a Sysprep-focused Unattend XML configuration
        /// </summary>
        public UnattendXMLConfiguration CreateSysprepConfiguration(string architecture = "amd64", string language = "neutral", PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Creating Sysprep Unattend XML configuration template");

            var config = new UnattendXMLConfiguration();
            InitializeEmptyConfiguration(config, cmdlet);

            EnsureConfigurationPass(config, "generalize", cmdlet);
            EnsureConfigurationPass(config, "specialize", cmdlet);

            return config;
        }

        /// <summary>
        /// Creates a minimal Unattend XML configuration
        /// </summary>
        public UnattendXMLConfiguration CreateMinimalConfiguration(PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Creating minimal Unattend XML configuration template");

            var config = new UnattendXMLConfiguration();
            InitializeEmptyConfiguration(config, cmdlet);

            return config;
        }

        /// <summary>
        /// Initializes an empty Unattend XML configuration with proper structure
        /// </summary>
        public void InitializeEmptyConfiguration(UnattendXMLConfiguration config, PSCmdlet? cmdlet = null)
        {
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend"">
</unattend>";

            config.XmlDocument.LoadXml(xml);
            config.IsModified = true;
        }

        /// <summary>
        /// Ensures a configuration pass exists in the XML
        /// </summary>
        public void EnsureConfigurationPass(UnattendXMLConfiguration config, string pass, PSCmdlet? cmdlet = null)
        {
            var xpath = $"//unattend:settings[@pass='{pass}']";
            var settingsNode = config.XmlDocument.SelectSingleNode(xpath, config.NamespaceManager);
            
            if (settingsNode == null)
            {
                var root = config.XmlDocument.DocumentElement;
                if (root != null)
                {
                    settingsNode = config.XmlDocument.CreateElement("settings", "urn:schemas-microsoft-com:unattend");
                    ((XmlElement)settingsNode).SetAttribute("pass", pass);
                    root.AppendChild(settingsNode);
                    config.IsModified = true;
                    
                    LoggingService.WriteVerbose(cmdlet, ServiceName, $"Added configuration pass: {pass}");
                }
            }
        }

        /// <summary>
        /// Adds sample components for demonstration
        /// </summary>
        public void AddSampleComponents(UnattendXMLConfiguration config, PSCmdlet? cmdlet = null)
        {
            LoggingService.WriteVerbose(cmdlet, ServiceName, "Adding sample components to Unattend XML configuration");

            // Ensure we have the required passes
            EnsureConfigurationPass(config, "specialize", cmdlet);
            EnsureConfigurationPass(config, "oobeSystem", cmdlet);

            // Add sample Shell Setup component
            AddSampleShellSetupComponent(config, cmdlet);
        }

        private void AddSampleShellSetupComponent(UnattendXMLConfiguration config, PSCmdlet? cmdlet = null)
        {
            var shellSetupXml = @"
<component name=""Microsoft-Windows-Shell-Setup"" processorArchitecture=""amd64"" publicKeyToken=""31bf3856ad364e35"" language=""neutral"" versionScope=""nonSxS"" xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
    <OOBE>
        <HideEULAPage>true</HideEULAPage>
        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>
        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>
        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>
        <NetworkLocation>Work</NetworkLocation>
        <ProtectYourPC>1</ProtectYourPC>
    </OOBE>
</component>";

            // Find the oobeSystem settings
            var oobeSettings = config.XmlDocument.SelectSingleNode("//unattend:settings[@pass='oobeSystem']", config.NamespaceManager);
            if (oobeSettings != null)
            {
                var componentDoc = new XmlDocument();
                componentDoc.LoadXml(shellSetupXml);
                var importedNode = config.XmlDocument.ImportNode(componentDoc.DocumentElement!, true);
                oobeSettings.AppendChild(importedNode);
                config.IsModified = true;
                
                LoggingService.WriteVerbose(cmdlet, ServiceName, "Added sample Microsoft-Windows-Shell-Setup component");
            }
        }

        private Encoding GetTextEncoding(string encoding)
        {
            return encoding.ToUpperInvariant() switch
            {
                "UTF8" => System.Text.Encoding.UTF8,
                "UTF16" => System.Text.Encoding.Unicode,
                "UTF32" => System.Text.Encoding.UTF32,
                "ASCII" => System.Text.Encoding.ASCII,
                "UNICODE" => System.Text.Encoding.Unicode,
                "BIGENDIANUNICODE" => System.Text.Encoding.BigEndianUnicode,
                _ => System.Text.Encoding.UTF8
            };
        }
    }

    /// <summary>
    /// Result of installing Unattend XML configuration to a mounted image
    /// </summary>
    public class UnattendXMLApplicationResult
    {
        public MountedWindowsImage MountedImage { get; set; } = new MountedWindowsImage();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string InstalledPath { get; set; } = string.Empty;
    }
}
