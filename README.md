# Timelapse - Deprecated. See saulgreenberg/Timelapse for the latest version

This is an old version of the Timelapse software, pre version 2.3.0.0
This repository contains the source code for and releases of [Timelapse 2](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?n=Main.HomePage), created by Saul Greenberg of the University of Calgary and Greenberg Consulting Inc, and revised with the help of others.

Timelapse2 is an Image Analyser for Camera Traps, where it is used by scientists, researchers, resource managers, educators and students to visually analyze and encode data from thousands of images and videos. See the [Timelapse web site](http://saul.cpsc.ucalgary.ca/timelapse/pmwiki.php?) for packaged downloads, tutorial guides oriented towards end-users, samples to play with and other resources.

Timelapse is currently in use across the world within broadly varying institutions --- national and regional parks, ecological agencies, fishery departments, conservation societies, university groups, etc. --- for quite different needs (e.g., wildlife monitoring, fisheries usage, resource monitoring and management, and even social science studies). What they have in common is
* they collect large numbers of images from one to very many field cameras
* they are interested in examining images and encoding (tagging) data that is usually specific to their projects
* they have their own needs and ways for performing analytics on that data.
Timelapse 2 helps in the 2nd step, where users then export the entered data so that it can be analyzed in another package of their choosing (e.g., R, spreadsheets, etc.)

We also collaborated with Microsoft's AI for Earth team, especially Dan Morris, who created the Megadetector image recognition system. Megadetector is excellent at detecting, locating and broadly classifying detected entities (wildlife, people and vehicles) in images. If you submit your images to Megadetector, an image recognition file is produced.  Timelapse can import that file, and display detected items within bounding boxes. Queries can also be run against recognized entities.

### Contributing

Bug reports, feature requests, and feedback are most welcome. Let us know! We can't improve the system if we don't hear from you. If you wish to co-develop this project, see below. 

### History
Timelapse was originally designed for a fisheries biologist who ran many field cameras in Timelapse mode, hence its name. Over time, its interface and functionality has been extended to meet the needs of a broad variety of scientists, agencies, researchers and educators who use field cameras in many different ways. 

In 2016, another developer joined forces to overhaul Timelapse for improved code quality and flexibility. In late 2016, our coding effort diverged: see [Carnassial](https://github.com/CascadesCarnivoreProject/Carnassial) - although it appears that development has ceased on that project. Divergence happened mostly due to differing project requirements.  

This repository begins at Timelapse Version 2.2.0.0

## For Developers
If you wish to co-develop this project, contact saul.greenberg@ucalgary.ca to see if our project goals coincide.

### Development environment
Install [Visual Studio](https://www.visualstudio.com/vs/), and then include the options below:

* Common Tools -> GitHub Extension for Visual Studio

After installation clone the repository locally through Visual Studio's Team Explorer or by using a GIT interface such as SourceTree.

* clone the repo locally through Visual Studio's Team Explorer or GitHub's clone or download options
* install Visual StyleCop (Tools -> Extensions and Updates -> Online)

After installation clone the repository locally through Visual Studio's Team Explorer or by using a GIT interface such as SourceTree.

Development must be done against .NET 4.8, as we target Timelapse users who may not have newer versions available on their systems or the ability to update .NET (e.g., locked down institutional machines).

Also helpful are

* Atlassian's [SourceTree](https://www.atlassian.com/software/sourcetree), a recommended augmentation of the git support available in Visual Studio's Team Explorer.
*  [Nuget Package Manager](https://docs.nuget.org/ndocs/guides/install-nuget#nuget-package-manager-in-visual-studio) for accessing and including 3rd party packages used by Timelapse.
* The [Visual Studio Image Library](https://msdn.microsoft.com/en-us/library/ms246582.aspx) for icons.

### Dependencies
* Timelapse and the template editor require .NET 4.5 or newer.
* Timelapse and the template editor are tested on Windows 10 and - as far as we know - should run without issue on earlier versions of Windows. End users will need to [install .NET 4.5 or newer](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) if it's not already present. However, expect some performance degradation on really old machiines or Windows versions, as they are less able to handle rapid display of large images.
* Timelapse uses various packages (including dlls available via NuGet). See the License file for a list of packages and their particular license terms.s
* Screen size of 1600 x 900 or larger is preferred, although it is usable on smaller displays.

