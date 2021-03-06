---
page_type: sample
languages:
- csharp
products:
- azure
extensions:
  services: Compute
  platforms: dotnet
---

# Getting started with creating a virtual machine using a VHD, either OS or data disk VHD #

 Azure Compute sample for managing virtual machines -
  - Create an un-managed virtual machine from PIR image with data disks
  - Deallocate the virtual machine
  - Generalize the virtual machine
  - Create a virtual machine custom image from OS and Data disk VHDs of virtual machine
  - Create a second virtual machine using the custom image
  - Create a second virtual machine using the custom image and configure the data disks
  - Deletes the custom image
  - Get SAS Uri to the virtual machine's managed disks.


## Running this Sample ##

To run this sample:

Set the environment variable `AZURE_AUTH_LOCATION` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-libraries-for-net/blob/master/AUTH.md).

    git clone https://github.com/Azure-Samples/managed-disk-dotnet-create-virtual-machine-using-custom-image-from-VHD.git

    cd managed-disk-dotnet-create-virtual-machine-using-custom-image-from-VHD

    dotnet build

    bin\Debug\net452\CreateVirtualMachineUsingCustomImageFromVHD.exe

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/tree/Fluent)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.