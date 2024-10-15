# Revit Standards Addin

## Descripton

The Revit Standard Addin is a project that looks to enable firms to control, deploy and manage the data in Revit files.
It does this by focusing on the key parts.
- A Rule Settings Files that Control what rules and validations are done to Files
- A UI Schema that defines an alternative Properties Panel
- A Github based Central Management Approach to apply this to individual projects.

## Application Structure

For the Application to function you need four primary components;
1. Public or Private Github Repo to Store your Rules
2. The "Revit Standards Github App" installed on your repo to enable access to the content from Revit
3. Environment Varailes that Map the local Revit Plugin to the Repo
4. The Revit Addin

# Admin Setup

The Revit application is dependent on a few bits of infrastructure.
1. First you must Create or have Repo to store your rule files.  Fork This Repo for your own testing.  It can be private as the Addin

[Documentation] ([https://docs.google.com/document/d/1RsWmZouS6jPszB60BpDToNKCkMIO1Pc8LKXXLXn-4O0/edit?usp=sharing](https://docs.google.com/document/d/1C9JdVicjKdV8yUelszdzi-aJxLn5y9Hm/edit?usp=sharing&ouid=113873482648746687410&rtpof=true&sd=true))
