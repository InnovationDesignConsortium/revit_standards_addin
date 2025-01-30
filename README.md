# Revit Standards Addin

## Description
The Revit Standards Addin is a project that enables firms to control, deploy, and manage the data in their Revit files. It does this by focusing on the following key parts.
- A Rules File that defines a set of rules and validations that are enforced in a Revit model
- A UI Schema that defines an alternative Properties Panel in Revit
- A Github-based, central management approach to manage these rules across an organization

## Prerequisites
There are four components required to use this application.
1. A **Github repository** and the **Revit Standards Github App** installed on your repository
    
    OR

    A set of local files and an associated Environment Variable

1. A couple of custom **Environment Variables** on each workstation
1. The **Revit Standards Addin**

## Setup and Deployment
Below are instructions to set up and deploy each component across an organization.
1. Create an empty Github repository. It can be Public, but the application was designed to work with a Private repository. This repository is where you will store your rules and configuration files and any other referenced files. You can start with a set of sample files by cloning [this repo](https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/tree/main). Alternatively, you can create the folder structure and files from scratch using the samples as a guide. There are very few files required to get this up and running.
1. Install the [RevitStandardsGithubApp](https://github.com/apps/revitstandardsgithubapp/installations/new) and give it access to your repo. More on [installing Github apps](https://docs.github.com/en/apps/using-github-apps/installing-a-github-app-from-a-third-party#installing-a-github-app). The RevitStandardsGithubApp needs read access so it can read the rules and configuration files.
1. As an alternative to storing the config files on GitHub, you can store the configuration and files locally. To do this, set the environment variable `RevitStandardsAddinFilePath` with the full path to the folder where the files will be stored. If this is done, no other environment variables need to be set.
1. Each workstation using this application MUST have the following Environment Variables configured to direct the Revit addin to the Github repo from step 1. 

    `RevitStandardsAddinGitOwner`: owner of the repo where the rules and JSON are saved, such as InnovationDesignConsortium  
    `RevitStandardsAddinGitRepo`: name of the repo where the rules and JSON are stored, such as PrivateRepoTest which is the test repo at https://github.com/InnovationDesignConsortium/PrivateRepoTest

    This image shows required variables. The variable names must match, but you should provide the values from your own repo.

    ![image](https://github.com/user-attachments/assets/022232f8-361d-4ae6-95bb-3bffa6675d9e)

   Additional Environment Variables that MAY be used are:

   `RevitDataValidatorDebug`: when set to 1 you can put JSON and rule files in the same folder as the DLL and those will be used instead of the files on Github  
    `RevitStandardsAddinGitServerUrl`: the URL for a Github Enterprise server account, if it exists.  
    `RevitStandardsAddinGitPat`: the Personal Access Token that can be used instead of installing the Github App.

1. Install the [latest release](https://github.com/InnovationDesignConsortium/revit_standards_addin/releases) of the Revit addin on each user's workstation using the provided MSI. 

NOTE: The environment variables can be System or User variables. System variables are checked first, if they do not exist then User variables will be used.

### Software Updates
The Revit addin is designed to notify a user of updates and prompt to update itself when Revit is closed. 
```
// Can this be disabled? 
// Do we have a deployment strategy that might include an option to disable this?
// Can we provide some information about digitally signing?
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
At it's most basic level, the Rules File is a Markdown file with a JSON code block to define the rules. Markdown syntax can be used to describe each rule or to provide valuable context to rule authors. The Rules File should be located in a subfolder inside `Standards/RevitStandardsPanel` and at least one path in the Configuration File should point to this folder. This [Sample Rules File](https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/blob/main/Standards/RevitStandardsPanel/AnnotatedTest/rules.md) describes each rule type and demonstrates the file format.

The Revit Standards Addin provides a framework for two types of rules - Workset Rules and Parameter Rules.

### Workset Rules
Workset rules all follow the same, simple structure.

1. A list of Revit Categories or Element Classes
2. The name of a Workset
3. A list of Parameter names and Regular Expression (regex) expressions
4. An optional "When Run" property

If all the parameters match the corresponding regex expressions, then the elements of those categories will be put on the workset. The named Parameters and Workset MUST exist in the model in order for the rule to work. If one of these are missing, the rule will be skipped. Parameters MAY be either Instance or Type.

Categories are Revit Categories as displayed in the Revit user interface. Element Classes are Revit API classes. The class names need to include the full namespace of the class which can be found in the API CHM at https://github.com/ADN-DevTech/revit-api-chms or https://www.revitapidocs.com/

For example:
- Autodesk.Revit.DB.Architecture.Room
- Autodesk.Revit.DB.Structure.AreaLoad
- Autodesk.Revit.DB.Mechanical.Duct

The allowable values for "When Run" are `"Realtime"`, `"Open"`, `"Save"`, `"SyncToCentral"` and essentially define when the rule is evaluated. The default is Realtime if unspecified and you can specify more than one per rule as a list.

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
        ],
        "When Run": ["Save", "SyncToCentral"]
    }
]
```

Note that elements in different Revit Categories may use different parameters to indicate similar relationships. For example, Furniture is associated with a _Level_ while Walls have a _Base Constraint_. In this case two rules would be needed to enforce moving Furniture and Walls located on Level 1 to the same Workset.

Additional description may be available in the Sample Rules File.

### Parameter Rules
Parameter Rules are more varied and can be subdivided into nine sub-types which have various requirements. All of these rules have a Rule Name, a User Message, a Parameter Name, and a Categories or Element Classes list. Each sub-type requires slightly different, additional information about how to execute the desired behavior. The optional "When Run" property also applies here.

Unless otherwise noted, the parameter must be an Instance Parameter.

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
This type of rule restricts a Parameter to only values defined in a list. The list of allowed values can be enumerated in the rule file like in this example that restricts the Comments parameter on Walls to only the values "a", "b", or "c".

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
    "Key Values":
    [
        ["A", "Wall A", "Floor A", "Ceiling A"],
        ["B", "Wall B", "Floor B", "Ceiling B"],
        ["C", "Wall C", "Floor C", "Ceiling C"]
    ]
}
```

When properly configured, the Properties Panel can show the allowable values for key value rules as a dropdown menu.

![image](https://github.com/user-attachments/assets/f73ebb12-5ec7-4827-aa6e-2d4adf648ce4)

Key Value Rules can also be driven by a Global Parameter and an external CSV file. Using this variation, the rule would look like this:

```json
{
    "Rule Name": "Code Occupancy",
    "Categories": ["Rooms"],
    "Key Value Path": "BuildingCodeOccupancy.csv"
}
```

And the corresponding CSV should be formatted as below. The first column is the Global Parameter, the second is the Driving Parameter of the corresonding element, and the remaining columns are the Driven Parameters.

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

To include a comma in the value for a field, enclose the value in double quotes like
```csv
foo,"100,000",a1
```

#### Format Rules
Specify a format that uses other parameter values of the same element to define the parameter's value. For example, the Type Name of a Wall must be the concatenation of its Function, Structural Material, and Width with fixed text strings inbetween. Note that the other parameters must be surrounded by curly braces.

```json
{
    "Rule Name": "Set Wall Type Function",
    "Element Classes": ["Autodesk.Revit.DB.WallType"],
    "Parameter Name": "Type Name",
    "Format": "{Function} - {Structural Material} - {Width}",
    "User Message": "Type name does not match required format"
}
```

#### Requirement Rules
A conditional equation can be written to define a requirement for a parameter's value. For example, the Sill Height of a Window must be greater than its Width or the Sill Height of a Door must be equal to 0. Note that as in Format Rules, the referenced parameters must be surrounded by curly braces.

```json
{
    "Rule Name": "Window Sill Height",
    "Categories": ["Windows"],
    "Parameter Name": "Sill Height",
    "Requirement": "> {Width}",
    "User Message": "Sill height must greater than width"
},
{
    "Rule Name": "Door Sill Height",
    "Categories": ["Doors"],
    "Parameter Name": "Sill Height",
    "Requirement": "= 0",
    "User Message": "Sill height must be 0"
}
```

#### Regex Rules
Check that a parameter value matches a [regular expression](https://regexr.com/). For example, the Mark value must be a number. Note that you can apply a rule to a list of categories, but you can also apply it to all categories by specifying `<all>` in the categories list.

```json
{
    "Rule Name": "Mark is Number",
    "User Message": "Mark must be a number",
    "Categories": ["<all>"],
    "Parameter Name": "Mark",
    "Regex": "^[0-9]+$"
}
```

#### Formula Rules
Perform mathematical operations on parameter values of the specified element and write the results to another parameter. For example, multiply the Occupancy Count of a Room by its Area and write that value to the Occupancy Load parameter. Note that referenced parameters are surrounded by curly braces. Allowable operators include addition `+`, subtraction `-`, multiplication `*`, and division `/`, exponents using the `^2` format, and trigonometric functions such as `sin(x)`. Order of operations can be controlled using parenthesis. This feature uses [Flee library](https://github.com/mparlak/Flee/).

```json
{
    "Rule Name": "Room Occupancy",
    "Categories": ["Rooms"],
    "Parameter Name": "Occupant Count",
    "Formula": "{Occupancy Load} * {Area}"
}
```

#### From Host Instance Rules
Set the value a parameter in a hosted element to have the same value as a parameter in the element's host. For example, setting a Windows's orientation to match the orientation of the host Wall.

```json
{
    "Rule Name": "Insert Orientation = Host Insert",
    "Categories": ["Doors", "Windows"],
    "Parameter Name": "Orientation",
    "From Host Instance": "Orientation",
    "User Message": "The Orientation of an insert must equal the Orientation of its host"
}
```

#### Prevent Duplicates Rules
Makes sure that there are not two elements with the same value for the same parameter. For example, preventing duplicate Room Number values.

```json
{
    "Rule Name": "Room Number Dup",
    "Categories": ["Rooms"],
    "User Message": "Room Number cannot duplicate an existing value",
    "Parameter Name": "Number",
    "Prevent Duplicates": "True"
}
```

#### Custom Code Rules
This runs the code in a C# file in the same folder as the rule definition file. It can check the model and return an error (such as the "model can have a maximum of 5 in-place families") or it can modify the document (such as "set the SheetGroup parameter to the first two characters of the Sheet Number parameter"). There are several examples demonstrating the powerful capabilities of this type of rule in the Sample Rules Repository.

CS files must implement a public method named "Run" as follows:

```cs
public IEnumerable<ElementId> Run(Document doc, List<ElementId> ids)
{
}
```

If you push the "Run" button in the panel, then `ids` will be null

If the rule is run because of a modification to the Revit document, then `ids` will be the added & modified ids.

Create an entry in the `rules.md` file such as this, with the file name in the "Custom Code" field.

```json
{
    "Rule Name": "In Place Family Quantity",
    "Element Classes": ["Autodesk.Revit.DB.FamilyInstance"],
    "Custom Code": "InPlaceFamilyCheck",
    "User Message": "There are too many In-Place Families in the model."
}
```

## User Interface for Rule Interaction
Many rules and behaviors are intended to run silently in the background without any user interaction. Workset rules, for example, do not typically prompt for user interaction, the placement of furniture family on Level 1 will automatically be set to workset Level 1 Stuff if all the criteria are met. No prompts or notifications will appear. Similarly, Key Value Rules, From Host Rules, Calculation Rules, and some Custom Rules can perform their validations and actions without dialogs interrupting or notifying the user. 

In other cases, when a rule is triggered, it's because an incorrect value has been input and we need the user to correct it. For example, if a rule requires the Mark to be a number and the user has input a series of letters, the correct value cannot be automatically deduced and the user must be prompted to correct it. This will start with a standard Revit Warning explaining the rule that was triggered which can be expanded to show the elements involved.

![image](https://github.com/user-attachments/assets/240db269-0f88-44a2-b8ce-7b4f0f0a1b7d)

![image](https://github.com/user-attachments/assets/d9f43ba0-9d9c-4b58-b896-9089ebaf4365)

At the same time, an interactive Resolve Rule Errors dialog box will open up providing a mechanism for understanding and correcting the error as well as focusing on each element.

![image](https://github.com/user-attachments/assets/2e7133a6-5192-499a-85a9-b1718acb0620)

Additional warnings may be displayed if attempting to close the Resolve Rule Errors dialog without resolving all the errors.

![image](https://github.com/user-attachments/assets/3b9e8313-7e68-47c9-b7bf-56829e70b7fe)

Similar to the Properties Panel, the Resolve Rule Errors dialog box can show the list of allowable values to resolve certain errors.

![image](https://github.com/user-attachments/assets/02ccbaab-bcee-4c67-8c17-b320faa318fe)

Custom Rules can also provide code for custom dialog boxes.

![image](https://github.com/user-attachments/assets/987cb773-3b4f-4f78-b1e4-41e259bc818d)

### Logging
- The logging uses the [NLog](https://nlog-project.org/) framework
- There will be an NLog.config file in the Addin folder (such as `C:\ProgramData\Autodesk\Revit\Addins\2023\RevitDataValidator`).
- NLog is highly configurable - for example you can specify what severity of log messages are written to one or more different types of files.
- Message severities are:
    - `Trace`: Everything is fine, internal message given to report status
    - `Info`: A parameter value was changed because of a rule
        - `Setting Occupancy Load to 10097.54 to match formula {Occupancy Count} * {Area}`
        - `Rename type 'Interior - By Category - 8"' to 'Coreshaft - By Category - 8"' to match format '{Function} - {Structural Material} - {Width}'`
    - `Warn`: User has done something that violates a rule
        - `'Walls:Interior - By Category - 8":364636' parameter 'Comments' value 'x' is not a valid value! Valid values are [a, b, c]`
        - `'Walls:Interior - By Category - 8":364636' 'Mark' value '1x' does not match regex ^[0-9]+$ for rule 'Mark is Number'`
    - `Error`: Something has gone wrong. Probably indicates a software bug
- Revit changes the TEMP folder to be a subfolder of the TEMP folder that it creates. So if you want the log file to be written to the TEMP folder the config file needs to specify that the file should be one directory up from the TEMP folder
  
`fileName="${tempdir}/../RevitDataValidatorLog.log"`

## User Interface

### Properties Panel
While most of the rules do not require much of a user interface, those that do are complimented by the Properties Panel. The Properties Panel lists all Workset and Parameter Rules along with a "Run" button to force a rule to run on demand. Below the rules, at the bottom of the Panel, is a link to view the Rules File. This makes the ability to document each rule directly in the rules.md file quite valuable and easily accessible to each team member. 

![image](https://github.com/user-attachments/assets/9745cd47-8985-4375-964e-e493f6f1b706)

### Parameter Packs and Pack Sets

In addition to viewing the rules, the Properties Panel can be configured to show various subsets of parameters in a contextually aware manner based on what elements are selected. This can be useful for users needing to perform a specific task. For example, if someone was populating Rooms in a model, they might only be concerned with the Name, Number, Occupancy, Room Style, and Department. A Parameter Pack can be defined to only show those values in the Properties Panel when a Room is selected. A Parameter Pack is a collection of parameters applied to one or more Revit Categories. Multiple Packs can be created for a single Category, so in addition to the Room parameters above, we can create another group of important reference parameters to be shown below. These are called Pack Sets. The order within a Pack and the groupings of Pack Sets are customizable. In collaboration with a List Rule, the allowable values of a particular parameter, such as Room Style, can be displayed in a dropdown for easy selection. The Properties Panel is non-modal and dockable which means it can remain open (either docked to the side or floating) while you are working in the model. A Pack Set selection dropdown sits at the top of the Properties Panel. This provides a way for different Pack Sets to be chosen if there are multiple workflows set up for the same Revit Categories. 

This example parameterpacks.json file only has two Packs and one Set. The Pack Set includes both Parameter Packs.

```json
{
    "Parameter Packs": 
    [
        {
            "Name": "Rooms - Core Placement Parameters",
            "Category": "Rooms",
            "Parameters":
            [    
                "Name", "Number", "Occupancy", "Room Style", "Department"
            ],
            "Custom Tools":
            [
                "Place Furniture Instance In Room"
            ],
            "URL": "https://help.autodesk.com/view/RVT/2024/ENU/?guid=GUID-DD74A51D-A0B0-4461-A4BA-0F9CCC191CDB",
            "PDF": "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf"
        },
        {
            "Name": "Rooms - Important Reference Parameters",
            "Category": "Rooms",
            "Parameters":
            [    
                "Level", "Area", "Floor Finish", "Base Finish", "Wall Finish", "Ceiling Finish"
            ]
        }
    ],
    "Pack Sets":
    [
        {
            "Name": "Room Placement",
            "Category": "Rooms",
            "Parameter Packs":
            [
                "Rooms - Core Placement Parameters", "Rooms - Important Reference Parameters"
            ]
        }
    ] 
}
```

The Properties Panel can also display custom links to URLs and PDFs intersperced between the Packs. These can be useful for providing a link to external documentation about the workflow associated with a Parameter Pack.

![image](https://github.com/user-attachments/assets/93b0f9ab-870a-450f-99b0-4fe880ec49f5)

## Known Issues

### General
1. Handle when there are multiple parameters with the same name

### Properties Panel
1. Not implemented for all parameter types
    - Partially implemented for parameters that store an ElementId (currently implemented for Levels and Phases)
    - Not implemented for Properties that offer user a list of values (such as Location Line for Walls which has valid values Wall Centerline, Core Centerline, Finish Face: Interior, Finish Face: Exterior, Core Face: Interior, Core Face: Exterior) 
2. Initial width of the data grid is too small. Need to manually stretch the panel to get it to have the correct width
3. Support “enum” parameters like Wall Location Line, Revit has a fixed set of values (Wall Centerline, Core Centerline, Finish Face: Exterior, etc).

### Rules
1. 



