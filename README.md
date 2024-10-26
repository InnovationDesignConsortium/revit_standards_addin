# Revit Standards Addin

## Description
The Revit Standard Addin is a project that enables firms to control, deploy, and manage the data in their Revit files. It does this by focusing on the following key parts.
- A Rules File that defines a set of rules and validations that are enforced in a Revit model
- A UI Schema that defines an alternative Properties Panel in Revit
- A Github-based, central management approach to manage these rules across an organization

## Prerequisites
There are four components required to use this application.
1. A **Github repository**
2. The **Revit Standards Github App** installed on your repository
3. A couple of custom **System Variables** on each workstation
4. The **Revit Standards Addin**

## Setup and Deployment
Below are instructions to set up and deploy each component across an organization.
1. Create an empty Github repository. It can be Public, but the application was designed to work with a Private repository. This repository is where you will store your rules and configuration files and any other referenced files. You can start with a set of sample files by cloning [this repo](https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/tree/main). Alternatively, you can create the folder structure and files from scratch using the samples as a guide. There are very few files required to get this up and running.
2. Install the [RevitStandardsGithubApp](https://github.com/apps/revitstandardsgithubapp/installations/new) and give it access to your repo. More on [installing Github apps](https://docs.github.com/en/apps/using-github-apps/installing-a-github-app-from-a-third-party#installing-a-github-app). The RevitStandardsGithubApp needs read access so it can read the rules and configuration files.
3. Each workstation using this application MUST have the following System Variables configured to direct the Revit addin to the Github repo from step 1. 

    `RevitStandardsAddinGitOwner`: owner of the repo where the rules and JSON are saved, such as InnovationDesignConsortium  
    `RevitStandardsAddinGitRepo`: name of the repo where the rules and JSON are stored, such as PrivateRepoTest which is the test repo at https://github.com/InnovationDesignConsortium/PrivateRepoTest

    This image shows required variables. The variable names must match, but you should provide the values from your own repo.

    ![image](https://github.com/user-attachments/assets/022232f8-361d-4ae6-95bb-3bffa6675d9e)

   Additional System Variables that MAY be used are:

   `RevitDataValidatorDebug`: when set to 1 you can put JSON and rule files in the same folder as the DLL and those will be used instead of the files on Github  
    `RevitStandardsAddinGitServerUrl`: the URL for a Github Enterprise server account, if it exists.  
    `RevitStandardsAddinGitPat`: the Personal Access Token that can be used instead of installing the Github App.

5. Install the [latest release](https://github.com/InnovationDesignConsortium/revit_standards_addin/releases) of the Revit addin on each user's workstation using the provided MSI. 

### Software Updates
The Revit addin is designed to notify a user of updates and prompt to update itself when Revit is closed. 
```
// Can this be disabled? 
// Do we have a deployment strategy that might include an option to disable this?
// Can we provide some inforation about digitally signing?
```

## Rule Administration
The core configuration and management of this application is via the Rules (`rules.md`) and Configuraton (`config.json`) files. These files are stored in a standard folder structure as shown below and are the minimum required files. Additional CSV or CS files that add custom functionality or supplement standard rules should be stored alongside the `rules.md` file.

![image](https://github.com/user-attachments/assets/a5455bb9-6ea5-474b-8f53-e6f6d7f05104)

### The Configuration File
The `config.json` file is the first file the Revit addin needs. It MUST be found in the `/Standards/RevitStandardsPanel` folder. In the image above, the `AllOtherFiles`, `House`, and `Library` files are distinct collections of rules. The Configuration File establishes a series of file path patterns, using regular expressions `RvtFullPathRegex`, that define which Rules File, `PathToStandardsFiles`, a given Revit model will use.

```json
{
    "StandardsConfig":
    [
        {
            "PathToStandardsFiles": "/Standards/RevitStandardsPanel/House",
            "RvtFullPathRegex": 
            [
                "House\\.rvt$",
                "\\\\House\\\\"
            ]
        },
        {
            "PathToStandardsFiles": "/Standards/RevitStandardsPanel/Library",
            "RvtFullPathRegex": 
            [
                "Library\\.rvt$"
            ]
        },
        {
            "PathToStandardsFiles": "/Standards/RevitStandardsPanel/AllOtherFiles",
            "RvtFullPathRegex": 
            [
                ".*"
            ]
        }
    ]
}
```

Only one Rules File is active for each Revit model. The Configuration File is evaluated from top to bottom when a model is opened or activated, so in the event that a file matches more than one pattern, the "first match wins."

We took this approach so organizations deploying the addin can limit the application of rules to only select projects. The Configuration File can be adjusted so that it applies a default set of rules to all projects or targeted to apply to a small subset of projects or even an individual project file. If a file doesn't match any of the Regex rules found in the Configuration File, none of the rules will apply. 

## The Rules File
At it's most basic level, the Rules File is a Markdown file with a JSON code block to define the rules. Markdown syntax can be used to describe each rule or to provide valuable context to rule authors. The Rules File should be located in a subfolder inside `Standards/RevitStandardsPanel` and at least one path in the Configuration File should point to this folder. This [Sample Rules File](https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/blob/main/Standards/RevitStandardsPanel/AllOtherFiles/rules.md) describes each rule type and demonstrates the file format.

The Revit Standards Addin provides a framework for two types of rules - Workset Rules and Parameter Rules.

### Workset Rules
Workset rules all follow the same, simple structure.

1. A list of Revit Categories
2. The name of a Workset
3. A list of Parameter names and values

If all the parameters match the corresponding value, then the elements of those categories will be put on the workset. The named Parameters and Workset MUST exist in the model in order for the rule to work. If one of these are missing, the rule will be skipped. Parameters MAY be either Instance or Type.

```json
"Workset Rules":
[
    {
        "Categories": ["Furniture", "Entourage"],
        "Workset": "Level 1 Stuff",
        "Parameters":
        [
            {"Name": "Level", "Value": "Level 1"},
            {"Name": "Workset Rule Applies", "Value": "1"}
            // Additional parameter names/values can be added. All criteria must be met in order for the rule to apply 
        ]
    }
]
```

Note that elements in different Revit Categories may use different parameters to indicate similar relationships. For example, Furniture is associated with a _Level_ while Walls have a _Base Constraint_. In this case two rules would be needed to enforce moving Furniture and Walls located on Level 1 to the same Workset.

Additional description may be available in the Sample Rules File.

### Parameter Rules
Parameter Rules are more varied and can be subdivided into nine sub-types which have various requirements. All of these rules have a Rule Name, a User Message, and a Parameter Name (unless otherwise noted, this must be an Instance Parameter), but they require slightly different, additional information to specify which element(s) to operate on (generally by specifying a Revit Category) and how to execute the desired behavior.

```json
"Parameter Rules":
[
    {
        "Rule Name": "This is the name of the rule", // this value will show up in the Revit Panel and in other dialog boxes
        "Categories": ["Walls"], // in some cases this can be substituted with "Element Classes": "Autodesk.Revit.DB.WallType"
        "Parameter Name": "Comments", // if this parameter does not exist, the rule will be skipped
        "User Message": "This is the message the user will see if the rule is violated"
        // Each kind of rule requires additional key/value pairs that are described below
    }
]
```

#### List Rules
This type of rule restricts a Parameter to only values defined in a list. The list of allowed values be enumerated in the rule file like in this example that restricts the Comments parameter on Walls to only the values "a", "b", or "c".

```json
{
    "Rule Name": "Comments a b c",
    "Categories": ["Walls"],
    "Parameter Name": "Comments",
    "User Message": "Comments must be a, b, or c",
    "Is Value Required": false, // this property can be set to `true` or `false`, if false, the parameter may be left blank
    "List Options":
    [
        {"name": "a", "description": ""},
        {"name": "b", "description": ""},
        {"name": "c", "description": ""}
    ]
}
```

For longer lists, the list of values can be contained in a separate CSV file and the rule structured like this:

```json
{
    "Rule Name": "Comments from csv",
    "Categories": ["Doors"],
    "Is Value Required": false,
    "Parameter Name": "MyCat",
    "List Source": "DoorCatValues.csv"
}
```

The CSV file for lists is simply one value per line.

```csv
a
b
c
```

Additionally, there is variation of a list rule that provides an option to specify a "Filter Parameter" where we want to have the value of one parameter (i.e. MyCat) filter the values that are allowed for another parameter (SubCat). This rule would be formatted like this:

```json
{
    "Categories": ["Doors"],
    "Parameter Name": "SubCat",
    "Filter Parameter": "MyCat",
    "Is Value Required": false,
    "List Options": 
    [
        {"name": "a.1", "Filter Value": "a"},
        {"name": "a.2", "Filter Value": "a"},
        {"name": "a.3", "Filter Value": "a"},
        {"name": "b.1", "Filter Value": "b"},
        {"name": "b.2", "Filter Value": "b"},
        {"name": "b.3", "Filter Value": "b"},
        {"name": "c.1", "Filter Value": "c"},
        {"name": "c.2", "Filter Value": "c"},
        {"name": "c.3", "Filter Value": "c"},
    ]
}
```

#### Key Value Rules
Setting the value of one parameter changes the values of multiple other parameters on the same element. Similar to a Key Schedule in Revit, but available in more places and enforced outside of the model.

```json
{
    "Rule Name": "Room Finish Keys",
    "Categories": ["Rooms"],
    "Parameter Name": "Room Style",
    "Driven Parameters": ["Wall Finish", "Floor Finish", "Ceiling Finish"],
    "Key Values": [
        ["A", "Wall A", "Floor A", "Ceiling A"],
        ["B", "Wall B", "Floor B", "Ceiling B"],
        ["C", "Wall C", "Floor C", "Ceiling C"]
    ]
}
```

Key Value Rules can also be driven by an external CSV and a Global Parameter. Using this variation, the rule would look like this:

```json
{
    "Rule Name": "Code Occupancy",
    "Categories": ["Rooms"],
    "Key Value Path": "BuildingCodeOccupancy.csv"
}
```

And the corresponding CSV would be formatted as shown below. The first column is a Global Parameter, the second is the Driving Parameter of the corresonding element, and the remaining columns are the Driven Parameters.

```csv
State_Code,Occupant,OccupantLoadFactor,Occupant_General,Occupant_Specific_Use
TX-BLD-21,Assembly_Excercise_with_Equip TX,50,Assembly TX,Exercise with Equipment TX
TX-BLD-21,Assembly _Waiting TX,5,Assembly TX,Waiting TX
TX-BLD-21,Business TX,50,Business TX,
TX-BLD-21,Storage TX,40,Storage TX,
TX-BLD-21,Assembly_Excercise_without_Equip TX,15,Assembly TX,Excercise_without_Equip TX
NYC-BLD-68,Assembly_Excercise_with_Equip NY,50,Assembly NY,Exercise with Equipment NY 
NYC-BLD-68,Assembly _Waiting NY,60,Assembly NY,Waiting NY
NYC-BLD-68,Business NY,510,Business NY,
NYC-BLD-68,Storage NY,410,Storage NY,
NYC-BLD-68,Assembly_Excercise_without_Equip NY,160,Assembly NY,Excercise_without_Equip NY
```

#### Requirement Rules
A conditional equation can be written to define a requirement for a parameter's value. For example, the Sill Height of a Window must be greater than the Width.

#### Format Rules
Specify a format that will use other parameter values of the same element to define the parameter's value. For example, the Type Name of a Wall must be the concatenation of its Function, Structural Material, and Width with a fixed text string.

#### Regex Rules
Check that a parameter value matches a [regular expression](https://regexr.com/). For example, the Mark value must be a number.

#### Formula Rules
Perform mathematical operations on parameter values of the specified element and write the results to another parameter. For example, multiply the Occupancy Count of a Room by its Area and write that value to the Occupancy Load parameter.

#### From Host Instance Rules
Set the value a parameter in a hosted element to have the same value as a parameter in the element's host. For example, setting a Windows's orientation to match the orientation of the host Wall.

#### Prevent Duplicates Rules
Makes sure that there are not two elements with the same value for the same parameter. For example, preventing duplicate Room Number values.

#### Custom Code Rules
This runs the code in a C# file in the same folder as the rule definition file. It can check the model and return an error (such as the "model can have a maximum of 5 in-place families") or it can modify the document (such as "set the SheetGroup parameter to the first two characters of the Sheet Number parameter")

## User Interface for Rule Enforcement
Address the rule validations in batch. The multi-element rule editor.

## Test the Setup

## Test the Rules

## Parameter Packs and Pack Sets

## User Interface







