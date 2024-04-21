# Sample Rule Documentation

```json
{
  
  "Workset Rules":
  [
    {
      "Categories": ["Furniture", "Entourage"],
      "Workset": "Level 1 Stuff",
      "Parameters":
      [
        {"Name": "Level", "Value": "Level 1"},
        {"Name": "Auto Assign Workset", "Value": "1"}
      ]
    },
    {
      "Categories": ["Furniture", "Entourage"],
      "Workset": "Level 2 Stuff",
      "Parameters":
      [
        {"Name": "Level", "Value": "Level 2"},
        {"Name": "Auto Assign Workset", "Value": "1"}
      ]
    }
  ],
  "Parameter Rules": 
  [
    {
      "Categories": ["Rooms"],
      "Key Parameters": ["Room Style", "Wall Finish", "Floor Finish", "Ceiling Finish"],
      "Key Values": [
        ["A", "A1", "A2", "A3"],
        ["B", "B1", "B2", "B3"],
        ["C", "C1", "C2", "C3"],
        ]
    },
    {
      "Categories": ["Rooms"],
      "Parameter Name": "Occupancy Load",
      "Formula": "{Occupancy Count} * {Area}",
    },
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
      "Rule Name": "Room Number Dup",
      "Categories": [
        "Rooms"
      ],
      "User Message": "Room Number cannot duplicate an existing value",
      "Parameter Name": "Number",
      "Prevent Duplicates": "True"
    }
  ]
}
```
