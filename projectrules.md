# Sample Rule Documentation

```json
{
  "Rules": 
  [
    {
      "Element Classes": [
        "Autodesk.Revit.DB.FamilyInstance"
      ],
      "Custom Code": "InPlaceFamilyCheck",
      "User Message": "There are too many In-Place Families in the model."
    },
    {
      "Rule Name": "Insert Orientation = Host Insert",
      "Categories": [
        "Doors", "Windows"
      ],
      "Parameter Name": "Orientation",
      "From Host Instance": "Orientation",
      "User Message": "The Orientation of an insert must equal the Orientation of its host"
    },
    {
      "Rule Name": "Comments a b c",
      "Categories": [
        "Walls"
      ],
      "Parameter Name": "Comments",
      "User Message": "Comments must be a, b, or c",
      "List Options":
      [
        {"name": "a", "description": ""},
        {"name": "b", "description": ""},
        {"name": "c", "description": ""},
      ]
    },
    {
      "Rule Name": "Mark is Number",
      "User Message": "Mark must be a number",
      "Categories": [
        "<all>"
      ],
      "Parameter Name": "Mark",
      "Regex": "^[0-9]+$"
    },
    {
      "Rule Name": "Room Name Dup",
      "Categories": [
        "Rooms"
      ],
      "User Message": "Room Name cannot duplicate an existing value",
      "Parameter Name": "Name",
      "Prevent Duplicates": "True"
    }
  ]
}
```
