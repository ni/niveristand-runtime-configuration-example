using NationalInstruments.VeriStand;
using NationalInstruments.VeriStand.ClientAPI;
using NationalInstruments.VeriStand.SystemDefinitionAPI;
using NationalInstruments.VeriStand.SystemStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RuntimeConfigurationExecutionAPIExample
{
    class RuntimeConfigurationDemo
    {

        static void ApplyRuntimeConfigurationDemo()
        {
            // Use the repo path of your local system here
            string repoPath = @"C:\dev\Runtime-Configuration-Support-Example";
            string assetsPath = $@"{repoPath}\Examples\Assets";
            string systemDefinitionPath = $@"{assetsPath}\RuntimeConfiguationDemo.nivssdf";
            string runtimeConfigurableSectionPath = "Targets/Controller/Custom Devices/Runtime Configuration Support Demo/RuntimeConfiguration";
            string configFilePath_Subset1 = $@"{assetsPath}\Runtime Configuration - Subset1.nivsruntimeconfig";
            string configFilePath_Subset2 = $@"{assetsPath}\Runtime Configuration - Subset2.nivsruntimeconfig";
            List<string> configuredInputChannelNames_Subset1 = GetInputChannelNames(configFilePath_Subset1, runtimeConfigurableSectionPath);
            List<string> configuredOutputChannelNames_Subset1 = GetOutputChannelNames(configFilePath_Subset1, runtimeConfigurableSectionPath);
            List<string> configuredInputChannelNames_Subset2 = GetInputChannelNames(configFilePath_Subset2, runtimeConfigurableSectionPath);
            List<string> configuredOutputChannelNames_Subset2 = GetOutputChannelNames(configFilePath_Subset2, runtimeConfigurableSectionPath);

            // Start Gateway
            StartGateway();

            // Deploy SDF
            bool isDeploymentSuccessful = DeploySDF(systemDefinitionPath);
            if (!isDeploymentSuccessful) return;

            // Create Factory and Workspace objects
            Factory FacRef = new Factory();
            IWorkspace2 Workspace = FacRef.GetIWorkspace2("localhost");
            Error err;

            // Attempt to retrieve channel values before configuration - expected to fail
            err = Workspace.GetSingleChannelValue(configuredInputChannelNames_Subset1[0], out double channelVal);
            if (err.IsError) Console.WriteLine("As expected, unable to get channel value before applying runtime configuration.");
            else Console.WriteLine("Should not be able to fetch the channel value before applying runtime configuration. Check if the runtime configuration is already applied.");
            Console.WriteLine();

            // Apply runtime configuration

            // Use the Newly added IRuntimeConfigurationManager interface to apply the configuration
            IRuntimeConfigurationManager RuntimeConfigurationManager = FacRef.GetIRuntimeConfigurationManager("localhost");
            err = RuntimeConfigurationManager.ApplyConfigurationToCustomDevice(runtimeConfigurableSectionPath, configFilePath_Subset1);
            ErrChk(err, "ApplyConfigurationToCustomDevice - Subset1");

            // Set and retrieve multiple channel values using the Client API - channels are now configured
            double[] InputValues = { 1, 2, 3, 4, 5 };
            Workspace.SetMultipleChannelValues(configuredInputChannelNames_Subset1.ToArray(), InputValues);
            Thread.Sleep(1000); // Wait for a second
            // Get and display first 5 values of both input and output channels
            Workspace.GetMultipleChannelValues(configuredInputChannelNames_Subset1.Take(5).ToArray(), out double[] inputValues_Subset1);
            Workspace.GetMultipleChannelValues(configuredOutputChannelNames_Subset1.Take(5).ToArray(), out double[] outputValues_Subset1);
            DisplayChannelValues(configuredInputChannelNames_Subset1.Take(5).ToArray(), inputValues_Subset1, "Input Values - Subset1");
            DisplayChannelValues(configuredOutputChannelNames_Subset1.Take(5).ToArray(), outputValues_Subset1, "Output Values - Subset1");

            // Get and print all the aliases
            GetAndPrintAliases(Workspace);

            // Reapply the configuration - Removes previously applied configuration and applies the new configuration
            err = RuntimeConfigurationManager.ApplyConfigurationToCustomDevice(runtimeConfigurableSectionPath, configFilePath_Subset2);
            ErrChk(err, "ApplyConfigurationToCustomDevice - Subset2");

            // Attempt to set or retrieve previously configured channel values using Client API - expected to fail
            err = Workspace.GetSingleChannelValue(configuredInputChannelNames_Subset1[0], out channelVal);
            if (err.IsError) Console.WriteLine("As expected, unable to get channel value of previously applied runtime configuration.");
            else Console.WriteLine("Should not be able to fetch the channel value of previously applied runtime configuration.");

            // Set and retrieve newly configured channel values using Client API - expected to succeed
            Workspace.SetMultipleChannelValues(configuredInputChannelNames_Subset2.ToArray(), InputValues);
            Thread.Sleep(1000); // Wait for a second
            // Get and display first 5 values of both input and output channels
            Workspace.GetMultipleChannelValues(configuredInputChannelNames_Subset2.Take(5).ToArray(), out double[] inputValues_Subset2);
            Workspace.GetMultipleChannelValues(configuredOutputChannelNames_Subset2.Take(5).ToArray(), out double[] outputValues_Subset2);
            DisplayChannelValues(configuredInputChannelNames_Subset2.Take(5).ToArray(), inputValues_Subset2, "Input Values - Subset2");
            DisplayChannelValues(configuredOutputChannelNames_Subset2.Take(5).ToArray(), outputValues_Subset2, "Output Values - Subset2");

            // Remove the current configuration
            err = RuntimeConfigurationManager.RemoveConfigurationFromCustomDevice(runtimeConfigurableSectionPath);
            ErrChk(err, "RemoveConfigurationFromCustomDevice");

            // Attempt to set or retrieve old channel values using Client API - expected to fail
            err = Workspace.GetSingleChannelValue(configuredInputChannelNames_Subset2[0], out channelVal);
            if (err.IsError) Console.WriteLine("As expected, unable to get channel value of after removing runtime configuration.");
            else Console.WriteLine("Should not be able to fetch the channel value of previously applied runtime configuration.");
            Console.WriteLine();

            // Undeploy SDF
            UndeploySDF();
        }

        #region Utility Methods for querying channel names from Runtime configuration file

        /// Reads Runtime Configuration File and queries all the channel names of type either inport or outport
        static List<string> GetCustomDeviceChannelNames(BaseNode[] nodes, string parentPath, CDChannel_Type type)
        {
            List<string> channelNames = new List<string>();

            bool isChannelTypeNeedsToBeWriteable = (type == CDChannel_Type.Input);

            foreach (var node in nodes)
            {
                if (node.BaseNodeType.GetNodeType() == NodeType.K_CHANNEL)
                {
                    var channelType = node.BaseNodeType as ChannelType;
                    if (channelType.IsWritable == isChannelTypeNeedsToBeWriteable)
                        channelNames.Add($"{parentPath}/{node.Name}");
                }
                else if (node.BaseNodeType.GetNodeType() == NodeType.K_SECTION)
                {
                    channelNames.AddRange(GetCustomDeviceChannelNames(node.GetChildren(), $"{parentPath}/{node.Name}", type));
                }
            }

            return channelNames;
        }

        static List<string> GetInputChannelNames(string configurationFilePath, string runtimeConfigurableSectionPath)
        {
            RuntimeConfiguration runtimeConfiguration = new RuntimeConfiguration(configurationFilePath);
            return GetCustomDeviceChannelNames(runtimeConfiguration.GetChildNodes(), runtimeConfigurableSectionPath, CDChannel_Type.Input);
        }

        static List<string> GetOutputChannelNames(string configurationFilePath, string runtimeConfigurableSectionPath)
        {
            RuntimeConfiguration runtimeConfiguration = new RuntimeConfiguration(configurationFilePath);
            return GetCustomDeviceChannelNames(runtimeConfiguration.GetChildNodes(), runtimeConfigurableSectionPath, CDChannel_Type.Output);
        }

        #endregion

        #region Utility Methods for Veristand Execution and Querying information from Veristand ClientAPI

        static void StartGateway()
        {
            Console.WriteLine("Starting Gateway...");
            Factory FacRef = new Factory();
            ServerCreateOptions svropt = new ServerCreateOptions();
            svropt.ShutDownServerOnProcessExit = false;
            FacRef.StartGatewayAndProjectServerAsync();
            while (Factory.CanConnectToVeriStandGateway("") != true)
            {
                continue;
            }
            Console.WriteLine("Gateway started.");
        }

        static bool DeploySDF(string systemDefinitionFilePath)
        {
            Console.WriteLine("Deploying System Definition...");
            Factory FacRef = new Factory();
            var Workspace = FacRef.GetIWorkspace2("localhost");
            var options = new DeployOptions();
            options.DeploySystemDefinition = true;
            options.Timeout = 120000;

            // Deploy SDF
            var errors = Workspace.ConnectToSystem(systemDefinitionFilePath, options);
            if (errors.Code == 0)
                Console.WriteLine("Successfully Deployed. \n");
            else if (errors.Code == 4294659638)
                Console.WriteLine("Already Deployed");
            else
            {
                ErrChk(errors, "Deployment failed.");
                return false;
            }

            return true;
        }

        static void UndeploySDF()
        {
            Console.WriteLine("Undeploying System Definition...");
            Factory FacRef = new Factory();
            var Workspace = FacRef.GetIWorkspace2("localhost");
            var err = Workspace.DisconnectFromSystem("", true);
            ErrChk(err, "Undeploying System Definition");
        }

        static void DisplayChannelValues(string[] channelNames, double[] values, string title)
        {
            Console.WriteLine();
            Console.WriteLine($"{title}: ");
            Console.WriteLine("| Channel Name | Value |");
            Console.WriteLine("|--------------|-------|");

            for (int i = 0; i < channelNames.Length && i < values.Length; i++)
            {
                Console.WriteLine($"| {channelNames[i]} | {values[i]} |");
            }

        }

        static void GetAndPrintAliases(IWorkspace2 workspace)
        {
            Console.WriteLine();
            Error err = workspace.GetAliasList(out string[] aliasNames, out string[] linkedChannels);
            ErrChk(err, "GetAliasList");

            if (err.IsError) return;

            if (aliasNames.Length == 0) Console.WriteLine("Aliases list is empty");
            else
            {
                err = workspace.GetMultipleChannelValues(aliasNames, out double[] values);
                Console.WriteLine("Aliases Configured at Runtime: ");
                Console.WriteLine();
                Console.WriteLine("| Alias Name | Linked Channel Name | Value |");
                Console.WriteLine("| ---------- | ------------------- | ----- |");
                for (int i = 0; i < aliasNames.Length; i++)
                    Console.WriteLine($"| {aliasNames[i]} | {linkedChannels[i]} | {values[i]} |");
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        static void ErrChk(Error err, string title)
        {
            if (err.IsError)
            {
                Console.WriteLine($"{title} failed. Error Code: {(int)err.ErrorCode}");
                // Console.Write(err.Message);
            }
            else
            {
                Console.WriteLine($"{title} succeeded");
            }
        }

        #endregion

        static void Main(string[] args)
        {
            try
            {
                ApplyRuntimeConfigurationDemo();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
