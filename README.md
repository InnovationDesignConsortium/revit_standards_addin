# Revit Standards Addin

## Description

The Revit Standard Addin is a project that enables firms to control, deploy, and manage the data in their Revit files. It does this by focusing on the following key parts.
- A Rules File that control what rules and validations are enforced
- A UI Schema that defines an alternative Properties Panel
- A Github-based, central management approach to apply this to individual projects

## Prerequisites

For the Application to function, you need four primary components.
1. A Public or Private Github repo to store your Rules File
2. The [Revit Standards Github App](https://github.com/apps/revitstandardsgithubapp/) installed on your repo to enable access to the content from Revit
3. Environment Variables that map the local Revit addin to the repo
4. The Revit Standards Addin

## Firm Setup and Deployment

The Revit addin is dependent on a few bits of infrastructure.
1. First, create an empty Github repo to store your rules. This can be public, but the application was designed for it to be private. You may clone [this repo](https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/tree/main) to get access to a set of sample Rules Files for testing.
2. Once you have the repo setup, install the [RevitStandardsGithubApp](https://github.com/apps/revitstandardsgithubapp/installations/new) and give it access to your repo. For more information on installing Github apps, refer to [this page](https://docs.github.com/en/apps/using-github-apps/installing-a-github-app-from-a-third-party#installing-a-github-app). The App needs read access so it can read the rules and configuration files.
3. Once you have this information, ensure that anyone using the addin has environment variables that direct the Revit addin to the correct repo.

This image shows required keys. The keys must match, but you should provide the values from your own repo.
![image](https://github.com/user-attachments/assets/6618e2a3-4b36-452a-bbe7-a6d1319e84b0)

4. Install the latest release of the Revit addin from https://github.com/InnovationDesignConsortium/revit_standards_addin/releases

### Software Updates

The Revit addin is designed to notify the user of updates and prompt to update itself when Revit is closed. 

// Can this be disabled? 
// Do we have a deployment strategy that might include an option to disable this?

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

Only one Rules File can be active for each Revit model. The Configuration File is evaluated from top to bottom when a model is opened or activated, so in the event that a file matches more than one criteria, the "first match wins."

We took this approach so a firm deploying the addin can limit the application of rules to only select projects. The Configuration File can be adjusted so that it applies a default set of rules to all projects. If a file doesn't match any of the Regex rules found in the Configuration File, none of the rules will apply. 

## The Rules File
At it's most basic level, the Rules File is a Markdown file with JSON code blocks intersperced to define each rule. Markdown syntax can be used inbetween the code blocks to describe each rule or to provide valuable context. Future development could reveal this context to the user. The Rules File should be located in a subfolder inside `Standards/RevitStandardsPanel` and at least one path in the Configuration File should point to this folder. This [sample Rules file](https://github.com/InnovationDesignConsortium/revit_standards_addin_rule_sample/blob/main/Standards/RevitStandardsPanel/AllOtherFiles/rules.md) describes each rule type and demonstrates the file format.

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







