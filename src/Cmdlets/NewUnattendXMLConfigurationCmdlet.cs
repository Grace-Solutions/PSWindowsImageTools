using System;
using System.Management.Automation;
using PSWindowsImageTools.Models;
using PSWindowsImageTools.Services;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Creates a new Windows Unattend XML configuration template
    /// </summary>
    [Cmdlet(VerbsCommon.New, "UnattendXMLConfiguration")]
    [OutputType(typeof(UnattendXMLConfiguration))]
    public class NewUnattendXMLConfigurationCmdlet : PSCmdlet
    {
        /// <summary>
        /// Type of template to create
        /// </summary>
        [Parameter(Position = 0)]
        [ValidateSet("Basic", "OOBE", "Sysprep", "Custom", "Minimal")]
        public string Template { get; set; } = "Basic";

        /// <summary>
        /// Processor architecture
        /// </summary>
        [Parameter]
        [ValidateSet("amd64", "x86", "arm64")]
        public string Architecture { get; set; } = "amd64";

        /// <summary>
        /// Language setting
        /// </summary>
        [Parameter]
        public string Language { get; set; } = "neutral";

        /// <summary>
        /// Include common configuration passes
        /// </summary>
        [Parameter]
        public string[] ConfigurationPasses { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Add sample components for demonstration
        /// </summary>
        [Parameter]
        public SwitchParameter IncludeSamples { get; set; }

        private UnattendXMLService _unattendService = new UnattendXMLService();

        protected override void ProcessRecord()
        {
            try
            {
                LoggingService.WriteVerbose(this, "General", 
                    $"Creating new Unattend XML configuration template: {Template}");

                UnattendXMLConfiguration configuration;

                switch (Template.ToLowerInvariant())
                {
                    case "basic":
                        configuration = _unattendService.CreateBasicConfiguration(Architecture, Language, this);
                        break;
                    case "oobe":
                        configuration = _unattendService.CreateOOBEConfiguration(Architecture, Language, this);
                        break;
                    case "sysprep":
                        configuration = _unattendService.CreateSysprepConfiguration(Architecture, Language, this);
                        break;
                    case "minimal":
                        configuration = _unattendService.CreateMinimalConfiguration(this);
                        break;
                    case "custom":
                        configuration = new UnattendXMLConfiguration(); // Empty template
                        _unattendService.InitializeEmptyConfiguration(configuration, this);
                        break;
                    default:
                        configuration = _unattendService.CreateBasicConfiguration(Architecture, Language, this);
                        break;
                }

                // Add requested configuration passes
                if (ConfigurationPasses.Length > 0)
                {
                    foreach (var pass in ConfigurationPasses)
                    {
                        _unattendService.EnsureConfigurationPass(configuration, pass, this);
                    }
                }

                // Add sample components if requested
                if (IncludeSamples.IsPresent)
                {
                    _unattendService.AddSampleComponents(configuration, this);
                }

                WriteObject(configuration);

                LoggingService.WriteVerbose(this, "General", "Unattend XML configuration template created successfully");
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "CreationError", ErrorCategory.NotSpecified, Template));
            }
        }

        protected override void EndProcessing()
        {
            _unattendService = null!;
        }
    }
}
