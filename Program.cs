// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;

namespace CreateVirtualMachineUsingCustomImageFromVHD
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;
        private static readonly string userName = Utilities.CreateUsername();
        private static readonly string password = Utilities.CreatePassword();
        private static AzureLocation region = AzureLocation.EastUS;

        /**
         * Azure Compute sample for managing virtual machines -
         *  - Create an un-managed virtual machine from PIR image with data disks
         *  - Deallocate the virtual machine
         *  - Generalize the virtual machine
         *  - Create a virtual machine custom image from OS and Data disk VHDs of virtual machine
         *  - Create a second virtual machine using the custom image
         *  - Create a second virtual machine using the custom image and configure the data disks
         *  - Deletes the custom image
         *  - Get SAS Uri to the virtual machine's managed disks.
         */
        public static async Task RunSample(ArmClient client)
        {
            var linuxVmName1 = Utilities.CreateRandomName("VM1");
            var linuxVmName2 = Utilities.CreateRandomName("VM2");
            var linuxVmName3 = Utilities.CreateRandomName("VM3");
            var customImageName = Utilities.CreateRandomName("img");
            var rgName = Utilities.CreateRandomName("rgCOMV");
            var storageName = Utilities.CreateRandomName("storage");
            var publicIpDnsLabel = Utilities.CreateRandomName("pip");
            var publicIpDnsLabel2 = Utilities.CreateRandomName("pip");
            var publicIpDnsLabel3 = Utilities.CreateRandomName("pip");
            var subnetName = Utilities.CreateRandomName("sub");
            var vnetName = Utilities.CreateRandomName("vnet");
            var nicName = Utilities.CreateRandomName("nic");
            var nicName2 = Utilities.CreateRandomName("nic");
            var nicName3 = Utilities.CreateRandomName("nic");
            var ipConfigName1 = Utilities.CreateRandomName("config");
            var ipConfigName2 = Utilities.CreateRandomName("config");

            var apacheInstallScript = "https://raw.githubusercontent.com/Azure/azure-libraries-for-net/master/Samples/Asset/install_apache.sh";
            var apacheInstallCommand = "bash install_apache.sh";
            var apacheInstallScriptUris = new List<string>();
            apacheInstallScriptUris.Add(apacheInstallScript);

            try
            {
                //============================================================
                // Create resource group
                //
                var subscription = await client.GetDefaultSubscriptionAsync();
                var resourceGroupData = new ResourceGroupData(AzureLocation.SouthCentralUS);
                var resourceGroup = (await subscription.GetResourceGroups()
                    .CreateOrUpdateAsync(WaitUntil.Completed, rgName, resourceGroupData)).Value;
                _resourceGroupId = resourceGroup.Id;

                //============================================================
                // Create storage account
                //
                var storageData = new StorageAccountCreateOrUpdateContent(
                    new StorageSku(StorageSkuName.StandardLrs), StorageKind.StorageV2, region);
                var storage = (await resourceGroup.GetStorageAccounts()
                    .CreateOrUpdateAsync(WaitUntil.Completed, storageName, storageData)).Value;

                //============================================================
                // Create network related resource
                //
                var ipAddressData = new PublicIPAddressData()
                {
                    Location = region,
                    PublicIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    DnsSettings = new PublicIPAddressDnsSettings()
                    {
                        DomainNameLabel = publicIpDnsLabel
                    }
                };
                var publicIpAddress = (await resourceGroup.GetPublicIPAddresses()
                    .CreateOrUpdateAsync(WaitUntil.Completed, publicIpDnsLabel, ipAddressData)).Value;

                var vnetData = new VirtualNetworkData()
                {
                    Location = region,
                    AddressPrefixes = { "10.0.0.0/16" },
                    Subnets = { new SubnetData() { Name = subnetName, AddressPrefix = "10.0.0.0/28" } }
                };
                var vnet = (await resourceGroup.GetVirtualNetworks()
                    .CreateOrUpdateAsync(WaitUntil.Completed, vnetName, vnetData)).Value;
                var subnet = (await vnet.GetSubnets().GetAsync(subnetName)).Value;

                var nicData = new NetworkInterfaceData()
                {
                    Location = region,
                    IPConfigurations = {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = ipConfigName1,
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            Primary = false,
                            Subnet = new SubnetData()
                            {
                                Id = subnet.Id
                            }
                        },
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = ipConfigName2,
                            PublicIPAddress = publicIpAddress.Data,
                            Primary = true,
                            Subnet = new SubnetData()
                            {
                                Id = subnet.Id
                            }
                        }
                    }
                };
                var nic = (await resourceGroup.GetNetworkInterfaces()
                    .CreateOrUpdateAsync(WaitUntil.Completed, nicName, nicData)).Value;

                //=============================================================
                // Create a Linux VM using a PIR image with un-managed OS and data disks and customize virtual
                // machine using custom script extension

                Utilities.Log("Creating a un-managed Linux VM");

                var vmData = new VirtualMachineData(region)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardDS1V2
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        ComputerName = linuxVmName1,
                        AdminUsername = userName,
                        AdminPassword = password
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nic.Id,
                                Primary = true,
                            }
                        }
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                        {
                            Name = linuxVmName1,
                            OSType = SupportedOperatingSystemType.Linux,
                            Caching = CachingType.None,
                            VhdUri = new Uri($"https://{storageName}.blob.core.windows.net/vhds/{linuxVmName1}.vhd")
                        },
                        DataDisks =
                        {
                            new VirtualMachineDataDisk(1, DiskCreateOptionType.Empty)
                            {
                                Name = "disk-1",
                                DiskSizeGB = 100,
                                VhdUri =  new Uri($"https://{storageName}.blob.core.windows.net/vhds/disk-1.vhd")
                            },
                            new VirtualMachineDataDisk(2, DiskCreateOptionType.Empty)
                            {
                                DiskSizeGB = 50,
                                Name = "disk-2",
                                VhdUri =  new Uri($"https://{storageName}.blob.core.windows.net/vhds/disk-2.vhd")
                            },
                            new VirtualMachineDataDisk(3, DiskCreateOptionType.Empty)
                            {
                                DiskSizeGB = 60,
                                Name = "disk-3",
                                VhdUri =  new Uri($"https://{storageName}.blob.core.windows.net/vhds/disk-3.vhd")
                            }
                        },
                        ImageReference = new ImageReference()
                        {
                            Publisher = "Canonical",
                            Offer = "UbuntuServer",
                            Sku = "16.04-LTS",
                            Version = "latest",
                        }
                    },
                };

                var linuxVM = (await resourceGroup.GetVirtualMachines()
                    .CreateOrUpdateAsync(WaitUntil.Completed, linuxVmName1, vmData)).Value;

                var settings = new Dictionary<string, object>() { { "fileUris", apacheInstallScriptUris }, { "commandToExecute", apacheInstallCommand } };
                var extensionData = new VirtualMachineExtensionData(region)
                {
                    Publisher = "Microsoft.OSTCExtensions",
                    ExtensionType = "CustomScriptForLinux",
                    TypeHandlerVersion = "1.4",
                    AutoUpgradeMinorVersion = true,
                    Settings = BinaryData.FromObjectAsJson(settings)
                };
                var extension = (await linuxVM.GetVirtualMachineExtensions()
                    .CreateOrUpdateAsync(WaitUntil.Completed, "CustomScriptForLinux", extensionData)).Value;

                Utilities.Log("Created a Linux VM with un-managed OS and data disks: " + linuxVM.Id);

                // De-provision the virtual machine
                publicIpAddress = await publicIpAddress.GetAsync();
                Utilities.DeprovisionAgentInLinuxVM(publicIpAddress.Data.IPAddress, 22, userName, password);

                //=============================================================
                // Deallocate the virtual machine
                Utilities.Log("Deallocate VM: " + linuxVM.Id);

                await linuxVM.DeallocateAsync(WaitUntil.Completed);

                Utilities.Log("De-allocated VM: " + linuxVM.Id);

                //=============================================================
                // Generalize the virtual machine
                Utilities.Log("Generalize VM: " + linuxVM.Id);

                await linuxVM.GeneralizeAsync();

                Utilities.Log("Generalized VM: " + linuxVM.Id);

                //=============================================================
                // Create a virtual machine custom image from OS and data disks of the virtual machine

                Utilities.Log("Creating virtual machine custom image from un-managed disk VHDs: " + linuxVM.Id);

                var imageData = new DiskImageData(region)
                {
                    HyperVGeneration = HyperVGeneration.V2,
                    StorageProfile = new ImageStorageProfile()
                    {
                        OSDisk = new ImageOSDisk(SupportedOperatingSystemType.Linux, OperatingSystemStateType.Generalized)
                        {
                            BlobUri = linuxVM.Data.StorageProfile.OSDisk.VhdUri
                        },
                        DataDisks =
                        {
                            new ImageDataDisk(linuxVM.Data.StorageProfile.DataDisks[0].Lun)
                            {
                                BlobUri = linuxVM.Data.StorageProfile.DataDisks[0].VhdUri
                            },
                            new ImageDataDisk(linuxVM.Data.StorageProfile.DataDisks[1].Lun)
                            {
                                BlobUri = linuxVM.Data.StorageProfile.DataDisks[1].VhdUri
                            },
                            new ImageDataDisk(linuxVM.Data.StorageProfile.DataDisks[2].Lun)
                            {
                                BlobUri = linuxVM.Data.StorageProfile.DataDisks[2].VhdUri,
                                Caching = CachingType.ReadOnly
                            }
                        }
                    }
                };
                var virtualMachineCustomImage = (await resourceGroup.GetDiskImages()
                    .CreateOrUpdateAsync(WaitUntil.Completed, customImageName, imageData)).Value;

                Utilities.Log("Created custom image");

                //=============================================================
                // Create a Linux VM using custom image

                Utilities.Log("Creating a Linux VM using custom image: " + virtualMachineCustomImage.Id);

                ipAddressData = new PublicIPAddressData()
                {
                    Location = region,
                    PublicIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    DnsSettings = new PublicIPAddressDnsSettings()
                    {
                        DomainNameLabel = publicIpDnsLabel2
                    }
                };
                var publicIpAddress2 = (await resourceGroup.GetPublicIPAddresses()
                    .CreateOrUpdateAsync(WaitUntil.Completed, publicIpDnsLabel2, ipAddressData)).Value;

                nicData = new NetworkInterfaceData()
                {
                    Location = region,
                    IPConfigurations = {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = ipConfigName1,
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            Primary = false,
                            Subnet = new SubnetData()
                            {
                                Id = subnet.Id
                            }
                        },
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = ipConfigName2,
                            PublicIPAddress = publicIpAddress2.Data,
                            Primary = true,
                            Subnet = new SubnetData()
                            {
                                Id = subnet.Id
                            }
                        }
                    }
                };
                var nic2 = (await resourceGroup.GetNetworkInterfaces()
                    .CreateOrUpdateAsync(WaitUntil.Completed, nicName2, nicData)).Value;

                vmData = new VirtualMachineData(region)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardDS1V2
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        ComputerName = linuxVmName2,
                        AdminUsername = userName,
                        AdminPassword = password
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nic2.Id,
                                Primary = true,
                            }
                        }
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        ImageReference = new ImageReference()
                        {
                            Id = virtualMachineCustomImage.Id
                        }
                    },
                };

                var linuxVM2 = (await resourceGroup.GetVirtualMachines()
                    .CreateOrUpdateAsync(WaitUntil.Completed, linuxVmName2, vmData)).Value;

                Utilities.Log("Created Linux VM" + linuxVM2.Id);

                //=============================================================
                // Create another Linux VM using custom image and configure the data disks from image and
                // add another data disk

                ipAddressData = new PublicIPAddressData()
                {
                    Location = region,
                    PublicIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                    DnsSettings = new PublicIPAddressDnsSettings()
                    {
                        DomainNameLabel = publicIpDnsLabel3
                    }
                };
                var publicIpAddress3 = (await resourceGroup.GetPublicIPAddresses()
                    .CreateOrUpdateAsync(WaitUntil.Completed, publicIpDnsLabel3, ipAddressData)).Value;

                nicData = new NetworkInterfaceData()
                {
                    Location = region,
                    IPConfigurations = {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = ipConfigName1,
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            Primary = false,
                            Subnet = new SubnetData()
                            {
                                Id = subnet.Id
                            }
                        },
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = ipConfigName2,
                            PublicIPAddress = publicIpAddress3.Data,
                            Primary = true,
                            Subnet = new SubnetData()
                            {
                                Id = subnet.Id
                            }
                        }
                    }
                };
                var nic3 = (await resourceGroup.GetNetworkInterfaces()
                    .CreateOrUpdateAsync(WaitUntil.Completed, nicName3, nicData)).Value;

                vmData = new VirtualMachineData(region)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardDS1V2
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        ComputerName = linuxVmName3,
                        AdminUsername = userName,
                        AdminPassword = password
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nic3.Id,
                                Primary = true,
                            }
                        }
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                        {
                            ManagedDisk = new VirtualMachineManagedDisk()
                        },
                        DataDisks =
                        {
                            new VirtualMachineDataDisk(1, DiskCreateOptionType.FromImage)
                            {
                                Caching = CachingType.ReadWrite,
                                ManagedDisk = new VirtualMachineManagedDisk()
                            },
                            new VirtualMachineDataDisk(2, DiskCreateOptionType.FromImage)
                            {
                                Caching = CachingType.ReadOnly,
                                ManagedDisk = new VirtualMachineManagedDisk()
                            },
                            new VirtualMachineDataDisk(3, DiskCreateOptionType.FromImage)
                            {
                                Caching = CachingType.ReadWrite,
                                ManagedDisk = new VirtualMachineManagedDisk()
                            },
                            new VirtualMachineDataDisk(4, DiskCreateOptionType.Empty)
                            {
                                DiskSizeGB = 50,
                                ManagedDisk = new VirtualMachineManagedDisk()
                            }
                        },
                        ImageReference = new ImageReference()
                        {
                            Id = virtualMachineCustomImage.Id
                        }
                    }
                };

                var linuxVM3 = (await resourceGroup.GetVirtualMachines()
                    .CreateOrUpdateAsync(WaitUntil.Completed, linuxVmName3, vmData)).Value;

                Utilities.Log("Created Linux VM" + linuxVM3.Id);

                // Getting the SAS URIs requires virtual machines to be de-allocated
                // [Access not permitted because'disk' is currently attached to running VM]
                //
                Utilities.Log("De-allocating the virtual machine - " + linuxVM3.Id);

                await linuxVM3.DeallocateAsync(WaitUntil.Completed);

                //=============================================================
                // Get the readonly SAS URI to the OS disks

                Utilities.Log("Getting OS and data disks SAS Uris");

                // OS Disk SAS Uri
                var osDisk = (await resourceGroup.GetManagedDisks().GetAsync(linuxVM3.Data.StorageProfile.OSDisk.Name)).Value;

                var osDiskSasUri = (await osDisk.GrantAccessAsync(WaitUntil.Completed, new GrantAccessData(AccessLevel.Read, 24 * 60))).Value;
                Utilities.Log("OS disk SAS Uri: " + osDiskSasUri.AccessSas);

                //=============================================================
                // Deleting the custom image
                Utilities.Log("Deleting custom Image: " + virtualMachineCustomImage.Id);

                await virtualMachineCustomImage.DeleteAsync(WaitUntil.Completed);

                Utilities.Log("Deleted custom image");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Console.WriteLine($"Deleting Resource Group: {_resourceGroupId}");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Console.WriteLine($"Deleted Resource Group: {_resourceGroupId}");
                    }
                }
                catch (Exception ex)
                {
                    Utilities.Log(ex);
                }

            }
        }
        
        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var credential = new DefaultAzureCredential();

                var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
                // you can also use `new ArmClient(credential)` here, and the default subscription will be the first subscription in your list of subscription
                var client = new ArmClient(credential, subscriptionId);

                await RunSample(client);
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}
