# Revit Standards Addin

## Description

The Revit Standard Addin is a project that looks to enable firms to control, deploy and manage the data in Revit files.
It does this by focusing on the key parts.
- A Rule Settings Files that Control what rules and validations are done to Files
- A UI Schema that defines an alternative Properties Panel
- A Github based Central Management Approach to apply this to individual projects.

## Prerequisites

For the Application to function you need four primary components;
1. Public or Private Github Repo to store your Rules
2. The "Revit Standards Github App" installed on your repo to enable access to the content from Revit
3. Environment Variables that map the local Revit addin to the Repo
4. The Revit Standards Addin

## Firm Setup and Deployment

The Revit application is dependent on a few bits of infrastructure.
1. First you must identify an empty github repo to store your rules.  This can be public, but the application was designed for it to be private. You can clone this repo to get access to sample rule files for you testing.
Sample Repo to Clone (https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/tree/main)
2. Once you have the repo setup you need to install the RevitStandardsGithubApp and give it access to your repo.
https://github.com/apps/revitstandardsgithubapp/installations/new   Here are the generic instructions from Github.  The App must have read access so it can read the rule and configuration files.
3. Once you have this information you need to ensure that any user accessing the application has environmental variabels that direct the Revit Addin to the correct repo.

This image shows required keys. The keys must match, but you need to get the values from your repo.
![image](https://github.com/user-attachments/assets/6618e2a3-4b36-452a-bbe7-a6d1319e84b0)

4. At this time you are ready to install the Revit Addin. https://github.com/InnovationDesignConsortium/revit_standards_addin/releases/tag/v0.0.0.11

## Rule Administration
The application stors the Rules and Configuraton files in a standard folder structure as see in this image. 
![image](https://github.com/user-attachments/assets/3c511d64-053d-49ac-b8e9-db28eab9ccb9)

The "Config.json" is the first file the Reivt Addin looks for.  It should always be found in the "/Standards/RevitStandardsPanel" folder. It communicates to Revit which set of rules apply to each file opened in Revit.   In the image above the "AllOtherFiles, House, and Library" are diffrent collections of rules. 

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

We took this approach so a firm deploying the addin can only apply it to select projects and when ready apply a default set of rules to all projects.   If a file doesn't match any of the Regex fules found in the config.json, none of the rules will apply. 

The config file is evaluated top to bottom and thus the "first match wins" if a file matches more then one criteria. 

## Understanding the Rules File
Markdown file in the specified directory... point people to the rule file that documents each rule type. Then we don't need the next couple of sections

### Workset Rules (document in the sample rule file)
Explain the example below...
This rule runs automatically if the workset applies
```json
"Categories": [
  "Furniture", "Entourage"
],
"Workset": "Level 1 Stuff",
"Parameters":
[
  {"Name": "Level", "Value": "Level 1"},
  {"Name": "Workset Rule Applies", "Value": "1"}
]
```
// video

### Parameter Rules (document in the sample rule file)
```json
// code snippet for each rule
```
// video for some rules

## User Interface for Rule Enforcement
Address the rule validations in batch. The multi-element rule editor.

## Test the Setup

## Test the Rules


## Parameter Packs and Pack Sets

## User Interface







