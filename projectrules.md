# Sample Rule Documentation

```json
{
  "Rules": 
  [
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
