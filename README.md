# Revit Standards Addin

## Description

The Revit Standard Addin is a project that enables firms to control, deploy, and manage the data in their Revit files. It does this by focusing on the following key parts.
- A Rules File that control what rules and validations are enforced
- A UI Schema that defines an alternative Properties Panel
- A Github-based, central management approach to apply this to individual projects

## Prerequisites

For the Application to function, you need four primary components:
1. A Public or Private Github repo to store your Rules File
2. The [Revit Standards Github App](https://github.com/apps/revitstandardsgithubapp/) installed on your repo to enable access to the content from Revit
3. Environment Variables that map the local Revit addin to the repo
4. The Revit Standards Addin

## Firm Setup and Deployment

The Revit addin is dependent on a few bits of infrastructure:
1. First, create an empty Github repo to store your rules. This can be public, but the application was designed for it to be private. You may clone [this repo](https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/tree/main) to get access to a set of sample Rules Files for testing.
2. Once you have the repo setup, install the [RevitStandardsGithubApp](https://github.com/apps/revitstandardsgithubapp/installations/new) and give it access to your repo. For more information on installing Github apps, refer to [this page](https://docs.github.com/en/apps/using-github-apps/installing-a-github-app-from-a-third-party#installing-a-github-app). The App needs read access so it can read the rules and configuration files.
3. Once you have this information, ensure that anyone using the addin has environment variables that direct the Revit addin to the correct repo.

This image shows required keys. The keys must match, but you should provide the values from your own repo.
![image](https://github.com/user-attachments/assets/6618e2a3-4b36-452a-bbe7-a6d1319e84b0)

4. Install the latest release of the Revit addin from https://github.com/InnovationDesignConsortium/revit_standards_addin/releases

### Software Updates

The Revit addin is designed to notify the user of updates and prompt to update itself when Revit is closed. 
```
// Can this be disabled? 
// Do we have a deployment strategy that might include an option to disable this?
```

## Rule Administration
The application stores the Rules and Configuraton Files in a standard folder structure as seen in this image. 
![image](https://github.com/user-attachments/assets/3c511d64-053d-49ac-b8e9-db28eab9ccb9)

### The Configuration File

The `Config.json` file is the first file the Revit addin needs. It should always be found in the `/Standards/RevitStandardsPanel` folder. This file communicates which set of rules applies to each file opened in Revit. In the image above the `AllOtherFiles`, `House`, and `Library` files are distinct collections of rules. 

```json
{
  "StandardsConfig": [
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

Only one Rules File can be active for each Revit model. The Configuration File is evaluated from top to bottom when a model is opened or activated, so in the event that a file matches more than one pattern, the "first match wins."

We took this approach so a firm deploying the addin can limit the application of rules to only select projects. The Configuration File can be adjusted so that it applies a default set of rules to all projects. If a file doesn't match any of the Regex rules found in the Configuration File, none of the rules will apply. 

## The Rules File
At it's most basic level, the Rules File is a Markdown file with JSON code blocks intersperced to define each rule. Markdown syntax can be used inbetween the code blocks to describe each rule or to provide valuable context. Future development could reveal this context to the user. The Rules File should be located in a subfolder inside `Standards/RevitStandardsPanel` and at least one path in the Configuration File should point to this folder. This [Sample Rules File](https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/blob/main/Standards/RevitStandardsPanel/AllOtherFiles/rules.md) describes each rule type and demonstrates the file format.

The Revit Standards Addin provides a framework for two types of rules - Workset Rules and Parameter Rules.

### Workset Rules

**Workset rules** all follow the same, simple structure.

1. A list of Revit Categories
2. The name of a Workset
3. A list of Instance or Type Parameter names and values

If all the parameters match the corresponding value, then the elements of those categories will be put on the workset. The named Parameters and Workset MUST exist in the model in order for the rule to work. If one of these are missing, the rule will be skipped.

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
      ]
    }
  ]
```

Note that elements in different Revit Categories may use different parameters to indicate the same similar relationships. For example, Furniture is associated with a _Level_ while Walls have a _Base Constraint_. In this case two rules would be needed to enforce Furniture and Walls located on Level 1 with the same Workset.

Additional description may be available in the Sample Rules File.

### Parameter Rules

**Parameter Rules** are more varied and can be subdivided into nine sub-types which have various requirements. All of these rules have a Rule Name, a User Message, and a Parameter Name as well as a way to specify which elements to operate on (generally this is by specifying a Revit Category) and some additional information specific to the kind of rule. 

#### List Rules

This type of rule restricts a Parameter to only values defined in a list (i.e. Comments must be "a", "b", or "c"). The list of allowed values can either be enumerated in the rule file or read from an external CSV file.

Additionally, there is an option to specify a "Filter Parameter" where we want to have the value of one parameter (i.e. MyCat) will filter the values that are allowed for another parameter (SubCat).

#### Key Value Rules

Settong the value of one parameter changes the values of multiple other parameters on the same element. Similar to a Key Schedule in Revit, but available in more places.

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







